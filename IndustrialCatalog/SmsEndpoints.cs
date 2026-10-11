using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

public static class SmsEndpoints
{
    static bool CanMessage(HttpContext c,string action)=>(AccessControl.Role(c.User) is "Admin" or "SuperAdmin")&&AccessControl.Has(c.User,AccessPermissions.CustomerSupport,action);
    static IResult Html(string html,int status=200)=>Results.Content(html,"text/html; charset=utf-8",statusCode:status);
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/contact-capabilities",(SmsSettingsStore sms)=>Results.Json(new{phoneMembership=sms.Active}));
        app.MapGet("/admin/sms",(HttpContext c,SmsSettingsStore sms)=>AccessControl.Has(c.User,AccessPermissions.System,"view")?Html(SmsPages.Settings(sms.Active)):Results.Forbid()).RequireAuthorization();
        app.MapGet("/api/admin/sms/settings",(HttpContext c,SmsSettingsStore sms)=>AccessControl.Has(c.User,AccessPermissions.System,"view")?Results.Json(sms.PublicSettings()):Results.Forbid()).RequireAuthorization();
        app.MapPost("/api/admin/sms/settings",async(HttpContext c,SmsSettingsStore sms,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.System,"edit"))return Results.Forbid();var body=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("Sağlayıcı bilgileri gerekli.");if(body["action"]?.ToString() is string action)sms.Manage(action,body["id"]?.ToString()??"");else sms.Save(body);store.RecordAudit("Sağlayıcı ayarı güncellendi","sms","",AccessControl.DisplayName(c.User));return Results.Json(sms.PublicSettings());}).RequireAuthorization();
        app.MapPost("/api/admin/sms/inquiry-test",async(HttpContext c,SmsSettingsStore sms,SmsGateway gateway,Store store,IConfiguration config)=>{
            if(!AccessControl.Has(c.User,AccessPermissions.System,"edit"))return Results.Forbid();
            if(!sms.Active)return Results.BadRequest(new{error="Önce sağlayıcıyı yapılandırıp aktif edin."});
            var body=await c.Request.ReadFromJsonAsync<JsonObject>()??new JsonObject();
            var phone=SmsPhone.Normalize(body["phone"]?.ToString());
            var origin=SeoPages.Origin(config)??throw new ArgumentException("Genel site adresi tanımlanmalı.");
            await gateway.Send(phone,InquirySmsNotifier.Message(new JsonObject{["purpose"]="destek",["message"]="Talep bildirimi bağlantı testi. Bu mesaj gerçek bir müşteri talebi değildir."},origin+"/admin?tab=inquiries",true),c.RequestAborted);
            store.RecordAudit("Talep telefonu test bildirimi sağlayıcı tarafından kabul edildi","sms","","Sistem");
            return Results.Json(new{ok=true,message="Test mesajı sağlayıcı tarafından kabul edildi. Telefona ulaştığını ayrıca kontrol edin."});
        }).RequireAuthorization().RequireRateLimiting("sms-admin");
        app.MapGet("/telefon-kayit",(SmsSettingsStore sms)=>sms.Active?Html(SmsPages.Phone("register")):Results.NotFound());
        app.MapGet("/telefon-sifre",(HttpContext c,SmsSettingsStore sms)=>sms.Active?Html(SmsPages.Phone("recover",id:Guid.TryParseExact(c.Request.Query["challenge"],"N",out _)?c.Request.Query["challenge"].ToString():"")):Results.NotFound());
        app.MapPost("/telefon/kod",async(HttpContext c,SmsSettingsStore sms,SmsCodes codes,SmsGateway gateway,CustomerDirectory customers)=>{
            if(!sms.Active)return Results.NotFound();var f=await c.Request.ReadFormAsync();var purpose=f["purpose"].ToString();if(purpose is not ("register" or "recover"))return Results.BadRequest();
            try{var phone=SmsPhone.Normalize(f["phone"].ToString());var customer=purpose=="recover"?customers.ByPhone(phone):null;var pair=codes.Create(phone,purpose,customer);if(purpose=="register"&&!customers.PhoneExists(phone)||purpose=="recover"&&customer!=null){await gateway.Send(phone,"İNOKSKAR "+(purpose=="register"?"üyelik":"şifre yenileme")+" kodunuz: "+pair.Code+". 10 dakika geçerlidir. Bu kodu paylaşmayın.",c.RequestAborted);codes.Delivered(pair.Challenge.Id);}return Html(SmsPages.Phone(purpose,id:pair.Challenge.Id,notice:"Numara bu işlem için uygunsa doğrulama kodu gönderildi. Kod 10 dakika geçerlidir."));}
            catch(Exception e) when(e is ArgumentException or InvalidOperationException){return Html(SmsPages.Phone(purpose,e.Message),400);}
        }).RequireRateLimiting("sms-request");
        app.MapPost("/telefon/onay",async(HttpContext c,SmsSettingsStore sms,SmsCodes codes,CustomerDirectory customers,Store store)=>{
            if(!sms.Active)return Results.NotFound();var f=await c.Request.ReadFormAsync();var purpose=f["purpose"].ToString();var id=f["challenge"].ToString();if(purpose is not ("register" or "recover"))return Results.BadRequest();
            try{var pass=f["password"].ToString();if(pass.Length is <8 or >128||pass!=f["confirm"].ToString())throw new ArgumentException("Şifreler aynı ve 8-128 karakter olmalıdır.");if(purpose=="register"&&f["name"].ToString().Trim().Length is <2 or >100)throw new ArgumentException("Ad soyad 2-100 karakter olmalıdır.");CustomerAccountRecord? created=null;
                codes.Consume(id,f["code"].ToString(),purpose,x=>{if(purpose=="register")created=customers.CreateByPhone(f["name"].ToString(),x.Phone,pass,f["businessName"].ToString());else customers.ResetPasswordByPhone(x.CustomerId,x.Phone,x.Stamp,pass);});
                if(created!=null){store.RecordAudit("Telefonla müşteri üyeliği oluşturuldu",created.Id,"",created.Name);await c.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,CustomerDirectory.Principal(created),Membership.PersistentSession());return Results.Redirect("/hesabim/profil");}
                await c.SignOutAsync();store.RecordAudit("Telefon doğrulamasıyla şifre yenilendi","customer","","");return Results.Redirect("/telefon-giris?reset=1");
            }catch(ArgumentException e){return Html(SmsPages.Phone(purpose,e.Message,id),400);}
        }).RequireRateLimiting("sms-verify");
        app.MapGet("/telefon-giris",(SmsSettingsStore sms)=>sms.Active?Html(SmsPages.Login()):Results.NotFound());
        app.MapPost("/telefon-giris",async(HttpContext c,SmsSettingsStore sms,CustomerDirectory customers,Store store)=>{if(!sms.Active)return Results.NotFound();var f=await c.Request.ReadFormAsync();var user=customers.VerifyPhone(f["phone"].ToString(),f["password"].ToString());if(user==null)return Html(SmsPages.Login("Telefon veya şifre hatalı. Telefonunuzu henüz doğrulamadıysanız SMS ile şifre yenileyebilirsiniz."),401);await c.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,CustomerDirectory.Principal(user),Membership.PersistentSession());store.RecordAudit("Müşteri telefonla giriş yaptı",user.Id,"",user.Name);return Results.Redirect("/hesabim/profil");}).RequireRateLimiting("login");
        app.MapGet("/admin/sms/messages",(HttpContext c,SmsSettingsStore sms)=>!sms.Active?Results.NotFound():CanMessage(c,"view")?Html(SmsPages.Messages()):Results.Forbid()).RequireAuthorization();
        app.MapGet("/api/admin/sms/messages",(HttpContext c,SmsSettingsStore sms,CustomerDirectory customers,SmsCodes codes,SmsQueue queue)=>!sms.Active?Results.NotFound():CanMessage(c,"view")?Results.Json(new{customers=customers.List().Where(x=>x.Active&&SmsPhone.TryNormalize(x.Phone)!=null).Select(x=>new{x.Id,x.Name,x.Phone}),requests=codes.Requests(),jobs=queue.Status()}):Results.Forbid()).RequireAuthorization();
        app.MapPost("/api/admin/sms/messages",async(HttpContext c,SmsSettingsStore sms,CustomerDirectory customers,SmsQueue queue,Store store)=>{
            if(!sms.Active)return Results.NotFound();if(!CanMessage(c,"edit"))return Results.Forbid();var b=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("Mesaj bilgisi gerekli.");var id=b["customerId"]?.ToString()??"";var rows=customers.List().Where(x=>x.Active&&SmsPhone.TryNormalize(x.Phone)!=null&&(id=="all"||x.Id==id)).ToList();if(rows.Count==0)throw new ArgumentException("Geçerli alıcı seçin.");var job=queue.Enqueue(rows.Select(x=>x.Phone),b["message"]?.ToString()??"");store.RecordAudit("Müşteri mesajı gönderim kuyruğuna alındı",job,rows.Count+" alıcı",AccessControl.DisplayName(c.User));return Results.Json(new{ok=true,job,total=rows.Count});
        }).RequireAuthorization().RequireRateLimiting("sms-admin");
        app.MapPost("/api/admin/sms/assistance/{id}",async(string id,HttpContext c,SmsSettingsStore sms,CustomerDirectory customers,SmsCodes codes,SmsGateway gateway,Store store)=>{
            if(!sms.Active)return Results.NotFound();if(!CanMessage(c,"edit"))return Results.Forbid();var b=await c.Request.ReadFromJsonAsync<JsonObject>();if(b?["identityConfirmed"]?.GetValue<bool>()!=true)throw new ArgumentException("Müşterinin kimliğini doğrudan iletişimle doğrulayın.");var customer=customers.ById(id);if(customer==null||SmsPhone.TryNormalize(customer.Phone)==null||customers.ByPhone(customer.Phone)?.Id!=id)throw new ArgumentException("Tek bir hesaba bağlı geçerli telefon bulunamadı.");var pair=codes.Create(customer.Phone,"recover",customer);var origin=SeoPages.Origin(c.RequestServices.GetRequiredService<IConfiguration>())??$"{c.Request.Scheme}://{c.Request.Host}";await gateway.Send(customer.Phone,"İNOKSKAR tek kullanımlık şifre yenileme kodunuz: "+pair.Code+". 10 dakika geçerlidir. "+origin.TrimEnd('/')+"/telefon-sifre?challenge="+Uri.EscapeDataString(pair.Challenge.Id),c.RequestAborted);codes.Delivered(pair.Challenge.Id);store.RecordAudit("Müşteriye tek kullanımlık telefon desteği gönderildi",id,"",AccessControl.DisplayName(c.User));return Results.Json(new{ok=true,message="Tek kullanımlık kod sağlayıcı tarafından kabul edildi."});
        }).RequireAuthorization().RequireRateLimiting("sms-admin");
    }
}
