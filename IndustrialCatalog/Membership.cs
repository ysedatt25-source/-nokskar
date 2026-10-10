using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

public static class Membership
{
    static string E(string? s)=>WebUtility.HtmlEncode(s??"");
    public static AuthenticationProperties PersistentSession()=>new(){IsPersistent=true,AllowRefresh=true,ExpiresUtc=DateTimeOffset.UtcNow.AddDays(90)};
    public static bool SupportRequested(JsonObject body)=>body["permissions"]?[AccessPermissions.CustomerSupport] is JsonObject p && (p["view"]?.GetValue<bool>()==true||p["edit"]?.GetValue<bool>()==true);
    static bool CanSupport(ClaimsPrincipal user,string action)=>(AccessControl.Role(user) is "Admin" or "SuperAdmin")&&AccessControl.Has(user,AccessPermissions.CustomerSupport,action);
    public static bool SendVerification(HttpContext c,CustomerAccountRecord customer,MailQueue mail,PasswordResetStore resets,IConfiguration config)
    {
        if(customer.EmailVerified||!mail.IsConfigured)return false;
        var token=resets.Create(customer.Email,"customer-verify",customer.SecurityStamp);
        var origin=SeoPages.Origin(config)??$"{c.Request.Scheme}://{c.Request.Host}";
        var url=origin.TrimEnd('/')+"/uyelik-dogrula?token="+Uri.EscapeDataString(token);
        mail.Enqueue(customer.Email,"İNOKSKAR hesabınızı doğrulayın","<p>İNOKSKAR üyeliğinize hoş geldiniz.</p><p>E-posta adresinizi doğrulamak için aşağıdaki bağlantıyı kullanın. Bağlantı 30 dakika geçerlidir.</p><p><a href='"+E(url)+"'>E-posta adresimi doğrula</a></p>","E-posta adresinizi doğrulayın (30 dakika geçerli): "+url);
        return true;
    }
    public static void Map(WebApplication app,string dataPath)
    {
        app.MapGet("/api/customer/avatar/{id}",(string id,HttpContext c,CustomerDirectory customers)=>{
            var own=AccessControl.Role(c.User)=="Customer"&&AccessControl.UserId(c.User)==id;
            if(!own&&!CanSupport(c.User,"view"))return Results.Forbid();
            var row=customers.ById(id);if(row==null||!System.Text.RegularExpressions.Regex.IsMatch(row.AvatarFile,"^[a-f0-9]{32}\\.(png|jpg|webp)$"))return Results.NotFound();
            var path=Path.Combine(dataPath,"customer-avatars",row.AvatarFile);if(!File.Exists(path))return Results.NotFound();
            c.Response.Headers.CacheControl="private,no-store";
            return Results.File(path,Path.GetExtension(path) switch{".png"=>"image/png",".webp"=>"image/webp",_=>"image/jpeg"});
        }).RequireAuthorization();
        app.MapPost("/api/customer/avatar",async(HttpContext c,CustomerDirectory customers)=>{
            if(AccessControl.Role(c.User)!="Customer")return Results.Forbid();
            var form=await c.Request.ReadFormAsync();var image=form.Files.GetFile("image");
            if(image==null||image.Length is <24 or >2097152)return Results.BadRequest(new{error="En fazla 2 MB boyutunda JPG, PNG veya WebP görsel seçin."});
            await using var memory=new MemoryStream();await image.CopyToAsync(memory,c.RequestAborted);var bytes=memory.ToArray();var detected=Uploads.Detect(bytes);
            if(detected==null||detected.Value.Mime is not ("image/png" or "image/jpeg" or "image/webp"))return Results.BadRequest(new{error="Görsel biçimi desteklenmiyor."});
            var folder=Path.Combine(dataPath,"customer-avatars");Directory.CreateDirectory(folder);var name=Guid.NewGuid().ToString("N")+detected.Value.Extension;
            await File.WriteAllBytesAsync(Path.Combine(folder,name),bytes,c.RequestAborted);
            try{customers.SetAvatar(AccessControl.UserId(c.User),name);}catch{File.Delete(Path.Combine(folder,name));throw;}
            return Results.Json(new{ok=true});
        }).RequireAuthorization().RequireRateLimiting("public-write");
        app.MapDelete("/api/customer/avatar",(HttpContext c,CustomerDirectory customers)=>{
            if(AccessControl.Role(c.User)!="Customer")return Results.Forbid();customers.SetAvatar(AccessControl.UserId(c.User),"");return Results.Json(new{ok=true});
        }).RequireAuthorization();
        app.MapPost("/api/customer/sessions/revoke",async(HttpContext c,CustomerDirectory customers)=>{
            if(AccessControl.Role(c.User)!="Customer")return Results.Forbid();customers.RevokeSessions(AccessControl.UserId(c.User));var row=customers.ById(AccessControl.UserId(c.User))!;
            await c.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,CustomerDirectory.Principal(row),PersistentSession());return Results.Json(new{ok=true});
        }).RequireAuthorization().RequireRateLimiting("recovery");
        app.MapPost("/api/customer/verification",(HttpContext c,CustomerDirectory customers,MailQueue mail,PasswordResetStore resets,IConfiguration config)=>{
            if(AccessControl.Role(c.User)!="Customer")return Results.Forbid();var customer=customers.ById(AccessControl.UserId(c.User))!;
            if(customer.EmailVerified)return Results.Json(new{ok=true,message="E-posta adresiniz zaten doğrulanmış."});
            if(!SendVerification(c,customer,mail,resets,config))return Results.Json(new{error="E-posta hizmeti henüz hazır değil. Doğrulama bağlantısı gönderilemedi."},statusCode:503);
            return Results.Json(new{ok=true,message="Doğrulama bağlantısı e-posta adresinize gönderildi."});
        }).RequireAuthorization().RequireRateLimiting("recovery");
        app.MapGet("/uyelik-dogrula",(HttpContext c,PasswordResetStore resets,CustomerDirectory customers)=>{
            var token=c.Request.Query["token"].ToString();var target=resets.Validate(token);var customer=target==null?null:customers.ByEmail(target.Email);
            if(target?.Kind!="customer-verify"||customer==null||target.Stamp!=customer.SecurityStamp)return Results.Content(Shell("E-posta doğrulama","<section class='member-card'><h1>Bağlantı artık geçerli değil</h1><p>Hesabınızdan yeni bir doğrulama bağlantısı isteyebilirsiniz.</p><a class='member-button' href='/hesabim'>Hesabıma dön</a></section>"),"text/html; charset=utf-8",statusCode:400);
            // GET does not consume the token: mail scanners must not verify accounts.
            return Results.Content(Shell("E-posta doğrulama","<section class='member-card'><h1>E-posta adresinizi doğrulayın</h1><p>"+E(customer.Email)+" adresini hesabınıza bağlamak için onaylayın.</p><form method='post' action='/uyelik-dogrula'><input type='hidden' name='token' value='"+E(token)+"'><button class='member-button'>E-posta adresimi doğrula</button></form></section>"),"text/html; charset=utf-8");
        }).RequireRateLimiting("recovery");
        app.MapPost("/uyelik-dogrula",async(HttpContext c,PasswordResetStore resets,CustomerDirectory customers,Store store)=>{
            var form=await c.Request.ReadFormAsync();var token=form["token"].ToString();var target=resets.Validate(token);var customer=target==null?null:customers.ByEmail(target.Email);
            if(target?.Kind!="customer-verify"||customer==null||target.Stamp!=customer.SecurityStamp||resets.ConsumeTarget(token)==null)return Results.BadRequest(new{error="Doğrulama bağlantısı geçersiz veya süresi dolmuş."});
            customers.VerifyEmail(customer.Id);store.RecordAudit("Müşteri e-postası doğrulandı",customer.Id,"",customer.Email);
            return Results.Content(Shell("E-posta doğrulandı","<section class='member-card'><h1>Üyeliğiniz doğrulandı</h1><p>E-posta adresiniz güvenle hesabınıza bağlandı.</p><a class='member-button' href='/hesabim'>Hesabıma dön</a></section>"),"text/html; charset=utf-8");
        }).RequireRateLimiting("recovery");
        app.MapGet("/admin/customers",(HttpContext c,CustomerDirectory customers,Store store)=>CanSupport(c.User,"view")?Results.Content(Admin(customers,CanSupport(c.User,"edit"),store,AccessControl.Has(c.User,AccessPermissions.Warranties,"view")),"text/html; charset=utf-8"):Results.Forbid()).RequireAuthorization();
        app.MapPost("/api/admin/customers/{id}/assistance",async(string id,HttpContext c,CustomerDirectory customers,PasswordResetStore resets,Store store)=>{
            if(!CanSupport(c.User,"edit"))return Results.Forbid();var body=await c.Request.ReadFromJsonAsync<JsonObject>();if(body?["identityConfirmed"]?.GetValue<bool>()!=true)return Results.BadRequest(new{error="Müşterinin kimliğini doğrudan iletişimle doğrulayın."});
            var customer=customers.ById(id);if(customer==null)return Results.NotFound();var token=resets.Create(customer.Email,"customer-assist",customer.SecurityStamp);
            store.RecordAudit("Tek kullanımlık müşteri giriş desteği oluşturuldu",id,"15 dakika geçerli; kimlik doğrudan iletişimle doğrulandı",AccessControl.DisplayName(c.User));
            return Results.Json(new{code=token,url="/reset-password?token="+Uri.EscapeDataString(token),expiresMinutes=15});
        }).RequireAuthorization().RequireRateLimiting("recovery");
        app.MapPost("/api/admin/customers/{id}/warranties",async(string id,HttpContext c,CustomerDirectory customers,Store store)=>{
            if(!CanSupport(c.User,"edit")||!AccessControl.Has(c.User,AccessPermissions.Warranties,"view"))return Results.Forbid();
            var body=await c.Request.ReadFromJsonAsync<JsonObject>();var warrantyId=body?["warrantyId"]?.ToString()??"";
            if(customers.ById(id)==null||store.WarrantyById(warrantyId)==null)return Results.BadRequest(new{error="Müşteri veya garanti kaydı bulunamadı."});
            customers.BindWarranty(id,warrantyId);store.RecordAudit("Garanti müşteri hesabına bağlandı",id,warrantyId,AccessControl.DisplayName(c.User));return Results.Json(new{ok=true});
        }).RequireAuthorization();
    }
    static List<JsonNode?> Requests(CustomerAccountRecord customer,Store store)=>(store.Snapshot()["inquiries"]?.AsArray()??new JsonArray()).Where(x=>{
        try{var data=JsonNode.Parse(x?["data"]?.ToString()??"{}");return data?["customerId"]?.ToString()==customer.Id||(customer.EmailVerified&&string.IsNullOrEmpty(data?["customerId"]?.ToString())&&string.Equals(data?["email"]?.ToString(),customer.Email,StringComparison.OrdinalIgnoreCase));}catch{return false;}
    }).OrderByDescending(x=>x?["created"]?.ToString()).ToList();
    static string Date(string? s)=>DateTimeOffset.TryParse(s,out var d)?d.ToString("dd.MM.yyyy"):"—";
    static string Status(string? s)=>s switch{"new"=>"Talep alındı","review"=>"İnceleniyor","scheduled"=>"Servis planlandı","parts"=>"Parça bekleniyor","completed" or "resolved"=>"Tamamlandı","answered"=>"Yanıtlandı","contacted"=>"İletişim kuruldu","callback"=>"Geri dönüş planlandı","cancelled"=>"İptal edildi",_=>"İşlemde"};
    public static string Profile(CustomerAccountRecord customer,Store store)
    {
        var mine=Requests(customer,store);var requests=new StringBuilder();var replyCount=0;
        foreach(var row in mine.Take(100)){
            JsonNode? d;try{d=JsonNode.Parse(row?["data"]?.ToString()??"{}");}catch{continue;}
            var service=d?["type"]?.ToString()=="service";var purpose=d?["purpose"]?.ToString();var type=service?"Teknik servis":purpose is "teklif" or "quote"?"Teklif":purpose is "proje" or "project"?"Proje & danışmanlık":"Genel destek";
            var reply=d?["publicReply"]?.ToString()??"";if(reply.Length>0)replyCount++;
            requests.Append("<article class='member-request'><div class='member-row'><span class='member-tag'>"+E(type)+"</span><span class='member-status'>"+E(Status(service?d?["serviceStatus"]?.ToString()??row?["status"]?.ToString():row?["status"]?.ToString()))+"</span></div><h3>"+E(row?["requestCode"]?.ToString())+"</h3><p>"+E(d?["message"]?.ToString())+"</p><small>"+E(Date(row?["created"]?.ToString()))+"</small>");
            if(reply.Length>0)requests.Append("<div class='member-reply'><strong>İNOKSKAR yanıtı</strong><p>"+E(reply)+"</p></div>");
            if(d?["replyHistory"] is JsonArray history&&history.Count>1){requests.Append("<details><summary>Önceki yanıtlar</summary>");foreach(var h in history.Take(history.Count-1).Reverse())requests.Append("<div class='member-reply'><small>"+E(Date(h?["created"]?.ToString()))+"</small><p>"+E(h?["text"]?.ToString())+"</p></div>");requests.Append("</details>");}
            requests.Append("</article>");
        }
        if(requests.Length==0)requests.Append("<div class='member-empty'><h3>İlk talebinizle başlayın</h3><p>Destek, teklif ve servis talepleriniz; ekibimizin yanıtlarıyla birlikte burada yer alır.</p><a href='/iletisim'>Destek ekibine ulaşın →</a></div>");
        var devices=new StringBuilder();var deviceCount=0;foreach(var id in customer.WarrantyIds??new()){
            var w=store.WarrantyById(id);if(w==null)continue;deviceCount++;
            devices.Append("<article class='member-request'><h3>"+E(w["productName"]?.ToString())+"</h3><p>Seri numarası: "+E(w["serialNumber"]?.ToString())+"</p><small>Garanti bitişi: "+E(w["warrantyEndDate"]?.ToString())+"</small></article>");
        }
        if(devices.Length==0)devices.Append("<div class='member-empty'><h3>Cihaz kayıtlarınız burada</h3><p>Hesabınıza doğrulanarak bağlanan cihaz ve garanti kayıtları bu bölümde görünür. Mevcut cihazınızı bağlamak için ekibimizle iletişime geçin.</p><a href='/garanti-sorgulama'>Garanti sorgulama →</a></div>");
        var initials=string.Join("",customer.Name.Split(' ',StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x=>x[..1].ToUpperInvariant()));
        var avatar=string.IsNullOrEmpty(customer.AvatarFile)?"<span>"+E(initials)+"</span>":"<img src='/api/customer/avatar/"+E(customer.Id)+"?v="+Uri.EscapeDataString(customer.UpdatedUtc)+"' alt='Profil görseliniz'>";
        var verified=customer.EmailVerified?"<span class='member-status'>E-posta doğrulandı</span>":"<div class='member-notification'><strong>E-posta adresinizi doğrulayın</strong><p>Hesabınızı güvenceye alın ve aynı e-posta adresiyle oluşturduğunuz önceki talepleri görün.</p><button class='member-secondary' data-action='verification'>Doğrulama bağlantısı gönder</button></div>";
        var form=new StringBuilder();foreach(var field in new[]{("name","Adınız ve soyadınız",customer.Name),("phone","Telefon numaranız",customer.Phone),("businessName","Firma adı",customer.BusinessName),("city","İl",customer.City),("district","İlçe",customer.District),("taxOffice","Vergi dairesi",customer.TaxOffice),("taxNumber","Vergi numarası",customer.TaxNumber)})form.Append("<label>"+E(field.Item2)+"<input name='"+field.Item1+"' value='"+E(field.Item3)+"' maxlength='"+(field.Item1=="businessName"?180:field.Item1 is "phone" or "taxNumber"?40:field.Item1=="taxOffice"?120:100)+"' "+(field.Item1=="name"?"required minlength='2' autocomplete='name'":field.Item1=="phone"?"inputmode='tel' autocomplete='tel'":"")+"></label>");
        var body="<section class='member-welcome'><div class='member-avatar'>"+avatar+"</div><div><p class='member-eyebrow'>İNOKSKAR ÜYELİK MERKEZİ</p><h1>Merhaba, "+E(customer.Name)+"</h1><p>İletişiminiz, talepleriniz ve hizmetleriniz tek yerde.</p><small>Üye no: IN-"+E(customer.Id[..Math.Min(8,customer.Id.Length)].ToUpperInvariant())+" · Üyelik tarihi: "+Date(customer.CreatedUtc)+"</small></div></section>"+verified+
            "<div class='member-metrics'><a href='#requests'><strong>"+mine.Count+"</strong><span>Taleplerim</span></a><a href='#requests'><strong>"+replyCount+"</strong><span>Yanıtlanan talepler</span></a><a href='#devices'><strong>"+deviceCount+"</strong><span>Cihaz ve garantilerim</span></a></div><nav class='member-tabs' aria-label='Üyelik bölümleri'><button data-member-tab='overview' aria-pressed='true'>Genel bakış</button><button data-member-tab='profile' aria-pressed='false'>Profilim</button><button data-member-tab='requests' aria-pressed='false'>Taleplerim</button><button data-member-tab='devices' aria-pressed='false'>Cihazlarım</button><button data-member-tab='security' aria-pressed='false'>Güvenlik</button></nav><section class='member-card member-overview' data-member-panel='overview'><h2>Size nasıl yardımcı olabiliriz?</h2><p>Ekibimizle doğrudan iletişime geçin; süreci hesabınızdan takip edin.</p><div class='member-quick'><a href='/iletisim'>Genel destek <span>→</span></a><a href='/iletisim?amac=teklif'>Teklif iste <span>→</span></a><a href='/iletisim?amac=servis'>Teknik servis <span>→</span></a><a href='/iletisim?amac=proje'>Proje danışmanlığı <span>→</span></a></div></section><div class='member-columns'><div><section class='member-card' data-member-panel='profile'><p class='member-eyebrow'>HESABINIZ</p><h2>Profil bilgilerim</h2><form id='member-profile'><div class='member-grid'><label>Profil türü<select name='profileKind'><option value='individual' "+(customer.ProfileKind!="company"?"selected":"")+">Bireysel</option><option value='company' "+(customer.ProfileKind=="company"?"selected":"")+">Firma</option></select></label><label>E-posta adresiniz<input value='"+E(customer.Email)+"' readonly></label>"+form+"</div><label>Adres<textarea name='address' maxlength='1200' autocomplete='street-address'>"+E(customer.Address)+"</textarea></label><button class='member-button'>Değişiklikleri kaydet</button></form><div class='member-divider'><h3>Profil fotoğrafı / firma logosu</h3><p>JPG, PNG veya WebP · En fazla 2 MB</p><form id='member-avatar'><label>Görsel seç<input name='image' type='file' accept='image/png,image/jpeg,image/webp' required></label><div class='member-actions'><button class='member-secondary'>Görseli yükle</button><button type='button' class='member-text' data-action='avatar-remove'>Görseli kaldır</button></div></form></div></section><section class='member-card' data-member-panel='security'><h2>Hesap güvenliği</h2><p>Bu cihazda oturumunuz korunur. Ortak cihaz kullanıyorsanız işlem sonunda çıkış yapın.</p><div class='member-actions'><a class='member-secondary' href='/account/security'>Şifremi değiştir</a><button class='member-secondary' data-action='sessions/revoke'>Diğer cihazlardan çıkış yap</button></div></section></div><div><section class='member-card' id='requests' data-member-panel='requests'><div class='member-section-head'><div><p class='member-eyebrow'>EKİBİMİZLE İLETİŞİMİNİZ</p><h2>Taleplerim</h2></div><a class='member-secondary' href='/iletisim'>Yeni talep</a></div><div class='member-list'>"+requests+"</div></section><section class='member-card' id='devices' data-member-panel='devices'><h2>Cihaz ve garantilerim</h2><div class='member-list'>"+devices+"</div></section></div></div><p id='member-feedback' role='status' aria-live='polite' hidden></p>";
        return Shell("Üyelik merkezim",body,true);
    }
    static string Admin(CustomerDirectory customers,bool edit,Store store,bool warranties)
    {
        var options=new StringBuilder("<option value=''>Garanti kaydı seçin</option>");
        if(warranties)foreach(var w in store.WarrantyList())options.Append("<option value='"+E(w?["id"]?.ToString())+"'>"+E(w?["serialNumber"]?.ToString())+" · "+E(w?["businessName"]?.ToString())+"</option>");
        var body=new StringBuilder("<section class='member-card'><p class='member-eyebrow'>MÜŞTERİ İLİŞKİLERİ</p><h1>Üyeler & giriş desteği</h1><p>Müşteri hesapları ve doğrulanmış cihaz bağlantıları. Giriş desteği kodu yalnızca oluşturulduğunda gösterilir; 15 dakika geçerlidir ve bir kez kullanılabilir.</p><label>Müşteri ara<input id='member-search' type='search' placeholder='Ad, firma veya e-posta'></label></section><div class='member-admin-list'>");
        foreach(var row in customers.List()){
            body.Append("<article class='member-card member-customer' data-customer='"+E(row.Id)+"'><h2>"+E(row.Name)+"</h2><p>"+E(row.BusinessName)+"</p><p>"+E(row.Email)+" · "+E(row.Phone)+"</p><small>Üyelik: "+Date(row.CreatedUtc)+" · "+(row.EmailVerified?"E-posta doğrulandı":"E-posta doğrulaması bekleniyor")+"</small>");
            if(edit&&row.Active)body.Append("<div class='member-divider'><label class='member-check'><input type='checkbox' name='identity'> Müşterinin kimliğini doğrudan iletişimle doğruladım</label><button class='member-secondary' data-assistance>Tek kullanımlık giriş desteği oluştur</button><div class='member-code' hidden></div></div>"+(warranties?"<form class='member-bind'><label>Doğrulanmış garanti kaydı<select name='warrantyId' required>"+options+"</select></label><button class='member-secondary'>Garanti kaydını hesaba bağla</button></form>":"")+"");
            body.Append("</article>");
        }
        if(customers.List().Count==0)body.Append("<section class='member-card member-empty'><h2>Henüz müşteri üyeliği yok</h2><p>Müşteriler hesap oluşturduğunda burada görünür.</p></section>");
        body.Append("</div><p id='member-feedback' role='status' aria-live='polite' hidden></p>");
        return Shell("Müşteri üyelikleri",body.ToString(),false,true);
    }
    public static string Shell(string title,string body,bool customer=false,bool admin=false)=>"<!doctype html><html lang='tr'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1,viewport-fit=cover'><meta name='robots' content='noindex,nofollow'><title>"+E(title)+" | İNOKSKAR</title><link rel='stylesheet' href='/membership.css?v=1'><script defer src='/membership.js?v=1'></script></head><body class='membership-page'><header class='site-header private-brand-header member-header'><a class='private-brand-link' href='/' aria-label='İNOKSKAR ana sayfa'><img class='private-brand-image' src='/inokskar-header-brand.png' alt='İNOKSKAR'></a><button class='member-menu-toggle' aria-label='Menüyü aç' aria-expanded='false' aria-controls='member-menu'><svg viewBox='0 0 24 24' aria-hidden='true'><path d='M4 6h16M4 12h16M4 18h16'/></svg></button><nav id='member-menu'><a href='"+(admin?"/admin":"/")+"'>"+(admin?"Yönetim paneli":"Ana sayfa")+"</a>"+(customer?"<form method='post' action='/logout'><button>Çıkış yap</button></form>":"")+"</nav></header><main class='member-wrap'>"+body+"</main>"+(customer?"<nav class='member-bottom' aria-label='Müşteri gezinme'><a href='/'>Ana sayfa</a><a href='/urunler'>Ürünler</a><a href='/kategoriler'>Kategoriler</a><a href='/iletisim'>Destek</a><a href='/hesabim' aria-current='page'>Hesabım</a></nav>":"")+"</body></html>";
}
