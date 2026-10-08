using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Net.Mail;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

try
{
if(args.Contains("--hash-password"))
{
    Console.Write("Yeni yönetici parolası (en az 8 karakter): ");var password="";
    while(true){var key=Console.ReadKey(true);if(key.Key==ConsoleKey.Enter)break;if(key.Key==ConsoleKey.Backspace){if(password.Length>0)password=password[..^1];}else password+=key.KeyChar;}
    Console.WriteLine();if(password.Length<8)throw new ArgumentException("Parola en az 8 karakter olmalı.");Console.WriteLine(Passwords.Hash(password));return;
}
var builder=WebApplication.CreateBuilder(args);
if(builder.Environment.IsDevelopment())builder.WebHost.UseUrls("http://127.0.0.1:5093");
builder.Services.AddSingleton<Store>();builder.Services.AddHttpClient();builder.Services.AddHostedService<RateUpdater>();builder.Services.AddHostedService<BackupScheduler>();
builder.Services.AddAntiforgery(o=>{o.HeaderName="X-INOKSKAR-CSRF";o.FormFieldName="__inokskar_csrf";o.Cookie.Name="Inokskar.Antiforgery";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;});
builder.Services.AddSingleton<MailSettingsStore>();builder.Services.AddSingleton<MailQueue>();builder.Services.AddSingleton<IHostedService>(sp=>sp.GetRequiredService<MailQueue>());builder.Services.AddSingleton<OperationalLog>();builder.Services.AddSingleton<AdminLoginGuard>();builder.Services.AddSingleton<UserDirectory>();builder.Services.AddSingleton<CustomerDirectory>();builder.Services.AddSingleton<PasswordResetStore>();
var dataPath=Path.GetFullPath(builder.Configuration["Storage:Path"]??"App_Data",builder.Environment.ContentRootPath);
Directory.CreateDirectory(dataPath);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataPath,"keys")));
builder.Services.Configure<FormOptions>(o=>o.MultipartBodyLengthLimit=512L*1024*1024);
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=512L*1024*1024);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{
    o.LoginPath="/login";o.Cookie.Name="IndustrialCatalog.Admin";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;o.ExpireTimeSpan=TimeSpan.FromHours(4);o.SlidingExpiration=true;
    o.Events.OnRedirectToLogin=c=>{if(c.Request.Path.StartsWithSegments("/api"))c.Response.StatusCode=401;else c.Response.Redirect(c.RedirectUri);return Task.CompletedTask;};
    o.Events.OnValidatePrincipal=async c=>{var role=c.Principal?.FindFirstValue(ClaimTypes.Role)??"";if(role=="SuperAdmin")return;var uid=c.Principal?.FindFirstValue("inokskar:user-id")??"";if(role=="Customer"){var customers=c.HttpContext.RequestServices.GetRequiredService<CustomerDirectory>();var customer=customers.ById(uid);if(customer==null||!customer.Active){c.RejectPrincipal();await c.HttpContext.SignOutAsync();return;}c.ReplacePrincipal(CustomerDirectory.Principal(customer));c.ShouldRenew=true;return;}var directory=c.HttpContext.RequestServices.GetRequiredService<UserDirectory>();var current=directory.ById(uid);if(current==null||!current.Active){c.RejectPrincipal();await c.HttpContext.SignOutAsync();return;}c.ReplacePrincipal(UserDirectory.Principal(current));c.ShouldRenew=true;};
});builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o=>{
    o.RejectionStatusCode=429;
    o.AddPolicy("login",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(5)}));
    o.AddPolicy("recovery",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=5,Window=TimeSpan.FromMinutes(15)}));
    o.AddPolicy("public-write",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=30,Window=TimeSpan.FromMinutes(1)}));
    o.AddPolicy("warranty-query",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=12,Window=TimeSpan.FromMinutes(5)}));
});
var app=builder.Build();
if(!app.Environment.IsDevelopment()){app.UseExceptionHandler("/error");app.UseHsts();if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT")))app.UseHttpsRedirection();}
app.Use(async(ctx,next)=>{try{await next();}catch(Exception ex){app.Services.GetRequiredService<OperationalLog>().Error(ex,ctx);throw;}});
app.Use(async(ctx,next)=>{
    ctx.Response.Headers["X-Content-Type-Options"]="nosniff";ctx.Response.Headers["X-Frame-Options"]="SAMEORIGIN";ctx.Response.Headers["Referrer-Policy"]="strict-origin-when-cross-origin";ctx.Response.Headers["Permissions-Policy"]="camera=(), microphone=(), geolocation=(), payment=(), usb=()";
    ctx.Response.Headers["Cross-Origin-Opener-Policy"]="same-origin";ctx.Response.Headers["Cross-Origin-Resource-Policy"]="same-site";
    var csp="default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'self'; form-action 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; img-src 'self' data: blob: https:; media-src 'self' data: blob: https:; font-src 'self' data: https://fonts.gstatic.com; connect-src 'self'";
    if(!app.Environment.IsDevelopment())csp+="; upgrade-insecure-requests";ctx.Response.Headers["Content-Security-Policy"]=csp;
    if(ctx.Request.Path.StartsWithSegments("/api")||ctx.Request.Path.StartsWithSegments("/admin")||ctx.Request.Path=="/login"||(ctx.Request.Path=="/forgot-password"||ctx.Request.Path=="/reset-password"))ctx.Response.Headers.CacheControl="no-store";
    if(HttpMethods.IsPost(ctx.Request.Method)||HttpMethods.IsPut(ctx.Request.Method)||HttpMethods.IsPatch(ctx.Request.Method)||HttpMethods.IsDelete(ctx.Request.Method)){
        var origin=ctx.Request.Headers.Origin.ToString();var expected=$"{ctx.Request.Scheme}://{ctx.Request.Host}";
        if(origin!=expected){ctx.Response.StatusCode=403;await ctx.Response.WriteAsJsonAsync(new{error="Geçersiz istek kaynağı."});return;}
    }
    try{await next();}catch(Exception e) when(e is ArgumentException or InvalidOperationException or InvalidDataException or System.Text.Json.JsonException or System.Xml.XmlException or FormatException or OverflowException or NullReferenceException){ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new{error=e is ArgumentException?e.Message:"İçerik veya dosya biçimi geçersiz."});}
});
app.UseStaticFiles(new StaticFileOptions{OnPrepareResponse=ctx=>{
    var versioned=ctx.Context.Request.Query.ContainsKey("v")||ctx.Context.Request.Path.StartsWithSegments("/assets");
    ctx.Context.Response.Headers.CacheControl=versioned?"public,max-age=604800,immutable":"no-cache";
}});
app.UseAuthentication();
app.Use(async(ctx,next)=>{
    var unsafeMethod=HttpMethods.IsPost(ctx.Request.Method)||HttpMethods.IsPut(ctx.Request.Method)||HttpMethods.IsPatch(ctx.Request.Method)||HttpMethods.IsDelete(ctx.Request.Method);
    var publicWrite=ctx.Request.Path=="/api/inquiry"||ctx.Request.Path=="/api/service-request"||ctx.Request.Path=="/api/inquiry-status"||ctx.Request.Path=="/api/event";var protectedPath=ctx.Request.Path.StartsWithSegments("/api")||ctx.Request.Path.StartsWithSegments("/admin")||ctx.Request.Path.StartsWithSegments("/hesabim")||ctx.Request.Path=="/logout";
    if(unsafeMethod&&protectedPath&&!publicWrite&&ctx.User.Identity?.IsAuthenticated==true)
    {
        try{await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx);}
        catch(AntiforgeryValidationException){ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new{error="Güvenlik doğrulaması başarısız. Sayfayı yenileyip tekrar deneyin."});return;}
    }
    await next();
});
app.Use(async(ctx,next)=>{if(ctx.User.Identity?.IsAuthenticated==true&&ctx.User.FindFirst("inokskar:must-change-password")?.Value=="1"){var path=ctx.Request.Path;var allowed=path=="/account/security"||path=="/api/account/password"||path=="/logout"||path=="/api/security/csrf";if(!allowed){if(path.StartsWithSegments("/api")){ctx.Response.StatusCode=403;await ctx.Response.WriteAsJsonAsync(new{error="Devam etmek için önce geçici şifrenizi değiştirin."});}else ctx.Response.Redirect("/account/security");return;}}await next();});
app.UseAuthorization();app.UseRateLimiter();
app.MapGet("/error",()=>Results.Problem("İşlem tamamlanamadı. Lütfen tekrar deneyin."));
app.MapGet("/healthz",()=>Results.Json(new{status="ok",checkedAt=DateTimeOffset.UtcNow}));
app.MapGet("/build-info",()=>{
    var commit=Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA")??"";
    var release=string.IsNullOrWhiteSpace(commit)?"development":commit[..Math.Min(8,commit.Length)];
    return Results.Json(new{release,commit,deployment=Environment.GetEnvironmentVariable("RAILWAY_DEPLOYMENT_ID")??"",environment=Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT_NAME")??Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")??""});
});
app.MapGet("/api/security/csrf",(HttpContext c,IAntiforgery antiforgery)=>{var tokens=antiforgery.GetAndStoreTokens(c);return Results.Json(new{token=tokens.RequestToken??""});}).RequireAuthorization();
app.MapGet("/robots.txt",(IConfiguration config)=>Results.Text("User-agent: *\nDisallow: /admin\nDisallow: /login\nDisallow: /forgot-password\nDisallow: /reset-password\nDisallow: /api/\n"+(SeoPages.Origin(config) is string origin?"Sitemap: "+origin+"/sitemap.xml\n":""),"text/plain"));
app.MapGet("/sitemap.xml",(Store store,IConfiguration config)=>SeoPages.Origin(config) is string origin
    ?Results.Content(SeoPages.Sitemap(CatalogRules.Public(store.Snapshot()["data"]!.AsObject()),origin),"application/xml; charset=utf-8")
    :Results.Problem("Site:PublicBaseUrl ayarını gerçek site adresinizle yapılandırın.",statusCode:503));
app.MapGet("/login",(HttpContext ctx)=>ctx.User.Identity?.IsAuthenticated==true?Results.Redirect("/hesabim"):Results.Content(LoginPage(notice:ctx.Request.Query.ContainsKey("reset")?"Şifreniz güncellendi. Yeni şifrenizle giriş yapabilirsiniz.":""),"text/html; charset=utf-8"));
app.MapGet("/hesabim",(HttpContext c)=>Results.Redirect(RoleLanding(c.User))).RequireAuthorization();
app.MapPost("/login",async(HttpContext ctx,IConfiguration config,AdminLoginGuard guard,Store store,UserDirectory directory,CustomerDirectory customers)=>{
    if(guard.IsLocked(out var remaining))return Results.Content(LoginPage($"Çok fazla başarısız giriş denemesi. Yaklaşık {Math.Max(1,(int)Math.Ceiling(remaining.TotalMinutes))} dakika sonra tekrar deneyin."),"text/html; charset=utf-8",statusCode:429);
    var form=await ctx.Request.ReadFormAsync();var email=form["email"].ToString();var password=form["password"].ToString();var auth=AdminAuth.Load(dataPath,config);ClaimsPrincipal? principal=null;
    if(AdminAuth.Verify(auth,email,password))principal=UserDirectory.SuperAdminPrincipal(auth.Email);
    else{var user=directory.Verify(email,password);if(user!=null)principal=UserDirectory.Principal(user);else{var customer=customers.Verify(email,password);if(customer!=null)principal=CustomerDirectory.Principal(customer);}}
    if(principal==null){guard.RegisterFailure();store.RecordAudit("Başarısız kullanıcı girişi","login",ctx.Connection.RemoteIpAddress?.ToString()??"",email);return Results.Content(LoginPage("E-posta veya parola hatalı."),"text/html; charset=utf-8",statusCode:401);}
    guard.RegisterSuccess();store.RecordAudit("Kullanıcı girişi","login",ctx.Connection.RemoteIpAddress?.ToString()??"",principal.Identity?.Name??email);await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,principal);return Results.Redirect(RoleLanding(principal));
}).RequireRateLimiting("login");
app.MapGet("/kayit",(HttpContext c)=>c.User.Identity?.IsAuthenticated==true?Results.Redirect("/hesabim"):Results.Content(CustomerPages.Register(),"text/html; charset=utf-8"));
app.MapPost("/kayit",async(HttpContext c,CustomerDirectory customers,UserDirectory directory,Store store)=>{var f=await c.Request.ReadFormAsync();var name=f["name"].ToString();var email=f["email"].ToString();var pass=f["password"].ToString();var confirm=f["confirm"].ToString();if(pass!=confirm)return Results.Content(CustomerPages.Register("Şifre ile tekrarı aynı olmalıdır.",name,email),"text/html; charset=utf-8",statusCode:400);try{if(directory.EmailExists(email))throw new ArgumentException("Bu e-posta adresi sistemde kayıtlı. Yeni hesap oluşturmak yerine giriş yapın.");var customer=customers.Create(name,email,pass,f["phone"].ToString(),f["businessName"].ToString());store.RecordAudit("Müşteri hesabı oluşturuldu",customer.Id,customer.Email,customer.Name);await c.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,CustomerDirectory.Principal(customer));return Results.Redirect("/hesabim/profil");}catch(ArgumentException e){return Results.Content(CustomerPages.Register(e.Message,name,email),"text/html; charset=utf-8",statusCode:400);}}).RequireRateLimiting("login");
app.MapGet("/hesabim/profil",(HttpContext c,CustomerDirectory customers,Store store)=>{if(AccessControl.Role(c.User)!="Customer")return Results.Redirect(RoleLanding(c.User));var customer=customers.ById(AccessControl.UserId(c.User));return customer==null?Results.Redirect("/login"):Results.Content(CustomerPages.Profile(customer,store),"text/html; charset=utf-8");}).RequireAuthorization();
app.MapGet("/api/customer/profile",(HttpContext c,CustomerDirectory customers)=>{if(AccessControl.Role(c.User)!="Customer")return Results.NoContent();var row=customers.ById(AccessControl.UserId(c.User));return row==null?Results.NoContent():Results.Json(new{id=row.Id,name=row.Name,email=row.Email,phone=row.Phone,businessName=row.BusinessName,address=row.Address,city=row.City,district=row.District,taxOffice=row.TaxOffice,taxNumber=row.TaxNumber});});
app.MapPost("/api/customer/profile",async(HttpContext c,CustomerDirectory customers)=>{if(AccessControl.Role(c.User)!="Customer")return Results.Forbid();var body=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("Profil bilgileri gerekli.");var row=customers.UpdateProfile(AccessControl.UserId(c.User),body);return Results.Json(new{ok=true,name=row.Name,phone=row.Phone,businessName=row.BusinessName,address=row.Address,city=row.City,district=row.District,taxOffice=row.TaxOffice,taxNumber=row.TaxNumber});}).RequireAuthorization();
app.MapGet("/forgot-password",()=>Results.Content(ForgotPasswordPage(),"text/html; charset=utf-8"));
app.MapPost("/forgot-password",async(HttpContext ctx,IConfiguration config,MailQueue mail,PasswordResetStore resets,UserDirectory directory,CustomerDirectory customers,Store store)=>{
    var form=await ctx.Request.ReadFormAsync();var email=form["email"].ToString().Trim();
    if(!MailAddress.TryCreate(email,out var parsed))return Results.Content(ForgotPasswordPage("Geçerli bir e-posta adresi girin."),"text/html; charset=utf-8",statusCode:400);
    if(!mail.IsConfigured)return Results.Content(ForgotPasswordPage("E-posta hizmeti şu anda hazır değil. Lütfen daha sonra tekrar deneyin."),"text/html; charset=utf-8",statusCode:503);
    var normalized=parsed.Address;string? kind=null;
    var auth=AdminAuth.Load(dataPath,config);
    if(string.Equals(normalized,auth.Email,StringComparison.OrdinalIgnoreCase))kind="superadmin";
    else if(directory.ByEmail(normalized)!=null)kind="staff";
    else if(customers.ByEmail(normalized)!=null)kind="customer";
    if(kind!=null)
    {
        var token=resets.Create(normalized,kind);
        var origin=SeoPages.Origin(config)??$"{ctx.Request.Scheme}://{ctx.Request.Host}";
        var resetUrl=origin.TrimEnd('/')+"/reset-password?token="+Uri.EscapeDataString(token);
        var message=MailTemplates.PasswordReset(resetUrl);
        mail.Enqueue(normalized,message.Subject,message.Html,message.Text);
        store.RecordAudit("Şifre yenileme bağlantısı oluşturuldu","password-reset","",normalized);
    }
    return Results.Content(ForgotPasswordPage(notice:"E-posta adresi kayıtlıysa şifre yenileme bağlantısı gönderim kuyruğuna alındı. Bağlantı 30 dakika geçerlidir."),"text/html; charset=utf-8");
}).RequireRateLimiting("recovery");
app.MapGet("/reset-password",(HttpContext ctx,PasswordResetStore resets)=>{
    var token=ctx.Request.Query["token"].ToString();
    var valid=resets.Validate(token)!=null;
    return Results.Content(ResetPasswordPage(token,valid?"":"Bu şifre yenileme bağlantısı geçersiz veya süresi dolmuş.",valid),"text/html; charset=utf-8");
});
app.MapPost("/reset-password",async(HttpContext ctx,IConfiguration config,PasswordResetStore resets,UserDirectory directory,CustomerDirectory customers,Store store)=>{
    var form=await ctx.Request.ReadFormAsync();var token=form["token"].ToString();var next=form["password"].ToString();var confirm=form["confirm"].ToString();
    var target=resets.Validate(token);
    if(target==null)return Results.Content(ResetPasswordPage(token,"Bu şifre yenileme bağlantısı geçersiz veya süresi dolmuş.",false),"text/html; charset=utf-8",statusCode:400);
    if(next!=confirm)return Results.Content(ResetPasswordPage(token,"Yeni şifre ile tekrarı aynı olmalıdır.",true),"text/html; charset=utf-8",statusCode:400);
    if(string.IsNullOrWhiteSpace(next)||next.Length is <8 or >128)return Results.Content(ResetPasswordPage(token,"Yeni şifre 8 ile 128 karakter arasında olmalıdır.",true),"text/html; charset=utf-8",statusCode:400);
    try
    {
        target=resets.ConsumeTarget(token);
        if(target==null)throw new ArgumentException("Şifre yenileme bağlantısı artık geçerli değil.");
        if(target.Kind=="superadmin")AdminAuth.ResetPasswordByVerifiedEmail(dataPath,config,target.Email,next);
        else if(target.Kind=="staff")directory.ResetPasswordByEmail(target.Email,next);
        else if(target.Kind=="customer")customers.ResetPasswordByEmail(target.Email,next);
        else throw new ArgumentException("Şifre yenileme hedefi geçersiz.");
        store.RecordAudit("Şifre e-posta bağlantısıyla yenilendi","password-reset","",target.Email);
        return Results.Redirect("/login?reset=1");
    }
    catch(ArgumentException e){return Results.Content(ResetPasswordPage(token,e.Message,true),"text/html; charset=utf-8",statusCode:400);}
}).RequireRateLimiting("recovery");
app.MapPost("/logout",async(HttpContext c)=>{await c.SignOutAsync();return Results.Redirect("/");}).RequireAuthorization();
app.MapGet("/api/me",(HttpContext c)=>Results.Json(new{email=c.User.Identity?.Name??"",name=AccessControl.DisplayName(c.User),role=AccessControl.Role(c.User),superAdmin=AccessControl.IsSuperAdmin(c.User),mustChangePassword=c.User.FindFirstValue("inokskar:must-change-password")=="1",permissions=c.User.FindAll("inokskar:permission").Select(x=>x.Value).ToArray()})).RequireAuthorization();
app.MapGet("/api/admin/security",(HttpContext c,IConfiguration config,UserDirectory directory)=>{var uid=AccessControl.UserId(c.User);if(AccessControl.IsSuperAdmin(c.User)){var auth=AdminAuth.Load(dataPath,config);return Results.Json(new{email=auth.Email,role="SuperAdmin"});}var user=directory.ById(uid);return user==null?Results.NotFound():Results.Json(new{email=user.Email,role=user.Role});}).RequireAuthorization();
app.MapPost("/api/admin/password",async(HttpContext c,IConfiguration config,UserDirectory directory)=>{
    try
    {
        var body=await c.Request.ReadFromJsonAsync<JsonObject>();
        if(body==null)return Results.BadRequest(new{error="Şifre bilgilerini eksiksiz doldurun."});
        var current=body["currentPassword"]?.ToString()??"";
        var next=body["newPassword"]?.ToString()??"";
        var confirm=body["confirmPassword"]?.ToString()??"";
        if(string.IsNullOrEmpty(current))return Results.BadRequest(new{error="Mevcut şifrenizi girin."});
        if(next!=confirm)return Results.BadRequest(new{error="Yeni şifre ile tekrarı aynı olmalıdır."});
        if(string.IsNullOrEmpty(next)||next.Length<8||next.Length>128)
            return Results.BadRequest(new{error="Yeni şifre 8 ile 128 karakter arasında olmalıdır."});
        if(AccessControl.IsSuperAdmin(c.User))
            AdminAuth.ChangePassword(dataPath,config,current,next);
        else
            directory.ChangeOwnPassword(AccessControl.UserId(c.User),current,next);
        await c.SignOutAsync();
        return Results.Json(new{ok=true,reauthenticate=true,message="Şifreniz başarıyla değiştirildi."});
    }
    catch(ArgumentException error)
    {
        return Results.BadRequest(new{error=error.Message});
    }
    catch(System.Text.Json.JsonException)
    {
        return Results.BadRequest(new{error="Şifre bilgileri okunamadı. Sayfayı yenileyip tekrar deneyin."});
    }
}).RequireAuthorization().RequireRateLimiting("login");
app.MapPost("/api/account/password",async(HttpContext c,IConfiguration config,UserDirectory directory,CustomerDirectory customers)=>{var body=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("Şifre bilgileri gerekli.");var current=body["currentPassword"]?.ToString()??"";var next=body["newPassword"]?.ToString()??"";var confirm=body["confirmPassword"]?.ToString()??"";if(next!=confirm)throw new ArgumentException("Yeni şifre ile tekrarı aynı olmalıdır.");if(AccessControl.IsSuperAdmin(c.User))AdminAuth.ChangePassword(dataPath,config,current,next);else if(AccessControl.Role(c.User)=="Customer")customers.ChangePassword(AccessControl.UserId(c.User),current,next);else directory.ChangeOwnPassword(AccessControl.UserId(c.User),current,next);await c.SignOutAsync();return Results.Json(new{ok=true});}).RequireAuthorization().RequireRateLimiting("login");
app.MapGet("/account/security",(HttpContext c)=>Results.Content(AccessPages.AccountSecurity(c.User.Identity?.Name??"",AccessControl.Role(c.User)),"text/html; charset=utf-8")).RequireAuthorization();
app.MapGet("/admin/security",(HttpContext c)=>Results.Redirect("/account/security")).RequireAuthorization();
app.MapPost("/admin/security/password",(HttpContext c)=>Results.Redirect("/account/security")).RequireAuthorization();
app.MapGet("/garanti-sorgulama",(Store store)=>Results.Content(WarrantyPages.Public(CatalogRules.Public(store.Snapshot()["data"]!.AsObject())),"text/html; charset=utf-8"));
// Retired legacy service form: old bookmarks go to the unified technical-service form.
app.MapGet("/servis-talebi",()=>Results.Redirect("/iletisim?amac=servis",permanent:true));
app.MapGet("/talep-sorgula",()=>Results.Content(InquiryStatusPages.Public(),"text/html; charset=utf-8"));
app.MapGet("/gizlilik",(Store store)=>Results.Content(LegalPages.Render("gizlilik",CatalogRules.Public(store.Snapshot()["data"]!.AsObject())),"text/html; charset=utf-8"));
app.MapGet("/kvkk",(Store store)=>Results.Content(LegalPages.Render("kvkk",CatalogRules.Public(store.Snapshot()["data"]!.AsObject())),"text/html; charset=utf-8"));
app.MapGet("/cerez",(Store store)=>Results.Content(LegalPages.Render("cerez",CatalogRules.Public(store.Snapshot()["data"]!.AsObject())),"text/html; charset=utf-8"));
app.MapGet("/admin/users",(HttpContext c)=>AccessControl.Has(c.User,AccessPermissions.Users,"view")?Results.Content(AccessPages.Users(),"text/html; charset=utf-8"):Results.Forbid()).RequireAuthorization();
app.MapGet("/teknik",(HttpContext c)=>AccessControl.Has(c.User,AccessPermissions.Technical,"view")?Results.Content(TechnicalPages.Dashboard(),"text/html; charset=utf-8"):Results.Forbid()).RequireAuthorization();
app.MapGet("/teknik/cihaz/{id}",(string id,HttpContext c,Store store)=>!AccessControl.Has(c.User,AccessPermissions.Technical,"view")?Results.Forbid():store.WarrantyById(id)==null?Results.NotFound():Results.Content(TechnicalPages.Device(id),"text/html; charset=utf-8")).RequireAuthorization();
app.MapGet("/admin/warranties",(HttpContext c)=>AccessControl.Has(c.User,AccessPermissions.Warranties,"edit")?Results.Content(WarrantyPages.Admin(),"text/html; charset=utf-8"):Results.Forbid()).RequireAuthorization();
app.MapGet("/admin/warranties/list",(HttpContext c)=>AccessControl.Has(c.User,AccessPermissions.Warranties,"view")?Results.Content(WarrantyPages.AdminList(),"text/html; charset=utf-8"):Results.Forbid()).RequireAuthorization();
app.MapGet("/admin/warranties/{id}/certificate",(string id,HttpContext c,Store store)=>!(AccessControl.Role(c.User) is "Admin" or "SuperAdmin")||!AccessControl.Has(c.User,AccessPermissions.Warranties,"view")?Results.Forbid():store.WarrantyList().FirstOrDefault(x=>x?["id"]?.ToString()==id) is JsonObject warranty?Results.Content(WarrantyPages.Certificate(warranty),"text/html; charset=utf-8"):Results.NotFound()).RequireAuthorization();
app.MapGet("/admin/system",(HttpContext c)=>AccessControl.Has(c.User,AccessPermissions.System,"view")?Results.Content(OperationsPages.Admin(),"text/html; charset=utf-8"):Results.Forbid()).RequireAuthorization();
app.MapGet("/admin/mail",(HttpContext c)=>AccessControl.Has(c.User,AccessPermissions.System,"view")?Results.Content(MailSettingsPages.Admin(),"text/html; charset=utf-8"):Results.Forbid()).RequireAuthorization();
app.MapGet("/admin/header-brand",(HttpContext c)=>AccessControl.Has(c.User,AccessPermissions.Settings,"edit")?Results.Content(BrandingPages.Admin(),"text/html; charset=utf-8"):Results.Forbid()).RequireAuthorization();
app.MapGet("/admin/inquiries/{id}",(string id,HttpContext c,Store store)=>{var inquiry=store.InquiryById(id);if(inquiry==null)return Results.Content(InquiryPages.NotFound(),"text/html; charset=utf-8",statusCode:404);var parsed=JsonNode.Parse(inquiry["data"]?.ToString()??"{}")?.AsObject();var service=string.Equals(parsed?["type"]?.ToString(),"service",StringComparison.OrdinalIgnoreCase);if(service&&!AccessControl.Has(c.User,AccessPermissions.Service,"view"))return Results.Forbid();if(!service&&!AccessControl.Has(c.User,AccessPermissions.Inquiries,"view"))return Results.Forbid();inquiry=store.MarkInquiryRead(id,AccessControl.DisplayName(c.User))??inquiry;return Results.Content(InquiryPages.AdminDetail(inquiry,store.AuditForTarget(id),store.InquiryTemplates()),"text/html; charset=utf-8");}).RequireAuthorization();
app.MapPost("/admin/inquiries/{id}/delete",(string id,HttpContext c,Store store)=>{var inquiry=store.InquiryById(id);if(inquiry==null)return Results.NotFound();var parsed=JsonNode.Parse(inquiry["data"]?.ToString()??"{}")?.AsObject();var service=string.Equals(parsed?["type"]?.ToString(),"service",StringComparison.OrdinalIgnoreCase);if(service&&!AccessControl.Has(c.User,AccessPermissions.Service,"edit"))return Results.Forbid();if(!service&&!AccessControl.Has(c.User,AccessPermissions.Inquiries,"edit"))return Results.Forbid();store.RemoveInquiry(id,AccessControl.DisplayName(c.User),service?"service":"support");return Results.Redirect("/admin?tab=inquiries");}).RequireAuthorization();
app.MapPost("/admin/inquiries/{id}/service",async(string id,HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Service,"edit"))return Results.Forbid();var form=await c.Request.ReadFormAsync();var input=new JsonObject
    {
        ["status"]=form["status"].ToString(),["appointmentDate"]=form["appointmentDate"].ToString(),["technician"]=form["technician"].ToString(),
        ["internalNote"]=form["internalNote"].ToString(),["parts"]=form["parts"].ToString(),["resolution"]=form["resolution"].ToString()
    };
    if(store.UpdateInquiryWorkflow(id,input,AccessControl.DisplayName(c.User))==null)return Results.NotFound();
    return Results.Redirect("/admin/inquiries/"+Uri.EscapeDataString(id));
}).RequireAuthorization();
app.MapPost("/admin/inquiries/{id}/workflow",async(string id,HttpContext c,Store store)=>{
    var inquiry=store.InquiryById(id);if(inquiry==null)return Results.NotFound();
    var parsed=JsonNode.Parse(inquiry["data"]?.ToString()??"{}")?.AsObject();
    var service=string.Equals(parsed?["type"]?.ToString(),"service",StringComparison.OrdinalIgnoreCase);
    if(service&&!AccessControl.Has(c.User,AccessPermissions.Service,"edit"))return Results.Forbid();
    if(!service&&!AccessControl.Has(c.User,AccessPermissions.Inquiries,"edit"))return Results.Forbid();
    var form=await c.Request.ReadFormAsync();
    var input=new JsonObject{["status"]=form["status"].ToString(),["callbackAt"]=form["callbackAt"].ToString(),
        ["publicReply"]=form["publicReply"].ToString(),["internalNote"]=form["internalNote"].ToString()};
    if(store.UpdateInquiryCustomerWorkflow(id,input,AccessControl.DisplayName(c.User))==null)return Results.NotFound();
    return Results.Redirect("/admin/inquiries/"+Uri.EscapeDataString(id));
}).RequireAuthorization();
app.MapPost("/admin/inquiries/{id}/templates",async(string id,HttpContext c,Store store)=>{
    var inquiry=store.InquiryById(id);if(inquiry==null)return Results.NotFound();
    var parsed=JsonNode.Parse(inquiry["data"]?.ToString()??"{}")?.AsObject();
    var service=string.Equals(parsed?["type"]?.ToString(),"service",StringComparison.OrdinalIgnoreCase);
    if(service&&!AccessControl.Has(c.User,AccessPermissions.Service,"edit"))return Results.Forbid();
    if(!service&&!AccessControl.Has(c.User,AccessPermissions.Inquiries,"edit"))return Results.Forbid();
    var form=await c.Request.ReadFormAsync();
    store.SaveInquiryTemplate(form["action"].ToString(),form["id"].ToString(),form["title"].ToString(),
        form["status"].ToString(),form["body"].ToString(),AccessControl.DisplayName(c.User));
    return Results.Redirect("/admin/inquiries/"+Uri.EscapeDataString(id));
}).RequireAuthorization();
app.MapGet("/admin",(HttpContext c,IWebHostEnvironment e)=>(AccessControl.Role(c.User) is "Admin" or "SuperAdmin")&&CatalogPermissionGuard.CanOpenAdmin(c.User)?Results.File(Path.Combine(e.WebRootPath,"index.html"),"text/html"):Results.Forbid()).RequireAuthorization();
app.MapGet("/api/admin/users",(HttpContext c,UserDirectory directory)=>AccessControl.Has(c.User,AccessPermissions.Users,"view")?Results.Json(new{users=directory.PublicList()}):Results.Forbid()).RequireAuthorization();
app.MapPost("/api/admin/users",async(HttpContext c,UserDirectory directory,CustomerDirectory customers,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Users,"edit"))return Results.Forbid();var body=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("Kullanıcı bilgileri gerekli.");var email=body["email"]?.ToString()??"";if(customers.EmailExists(email))return Results.BadRequest(new{error="Bu e-posta bir müşteri hesabında kullanılıyor."});try{var user=directory.Create(body);store.RecordAudit("Kullanıcı oluşturuldu",user.Id,user.Role,AccessControl.DisplayName(c.User));return Results.Json(new{user=UserDirectory.PublicJson(user)});}catch(ArgumentException ex){return Results.BadRequest(new{error=ex.Message});}}).RequireAuthorization();
app.MapPut("/api/admin/users/{id}",async(string id,HttpContext c,UserDirectory directory,CustomerDirectory customers,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Users,"edit"))return Results.Forbid();if(string.Equals(id,AccessControl.UserId(c.User),StringComparison.OrdinalIgnoreCase))return Results.BadRequest(new{error="Kendi hesabınızın rolünü veya yetkilerini bu bölümden değiştiremezsiniz."});var body=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("Kullanıcı bilgileri gerekli.");var email=body["email"]?.ToString()??"";if(customers.EmailExists(email))return Results.BadRequest(new{error="Bu e-posta bir müşteri hesabında kullanılıyor."});try{var user=directory.Update(id,body);store.RecordAudit("Kullanıcı yetkileri güncellendi",id,user.Email,AccessControl.DisplayName(c.User));return Results.Json(new{user=UserDirectory.PublicJson(user)});}catch(ArgumentException ex){return Results.BadRequest(new{error=ex.Message});}}).RequireAuthorization();
app.MapPost("/api/admin/users/{id}/password",async(string id,HttpContext c,UserDirectory directory,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Users,"edit"))return Results.Forbid();var body=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("Şifre gerekli.");try{directory.ResetPassword(id,body["password"]?.ToString()??"",body["mustChangePassword"]?.GetValue<bool>()??true);store.RecordAudit("Kullanıcı şifresi sıfırlandı",id,"",AccessControl.DisplayName(c.User));return Results.Json(new{ok=true});}catch(ArgumentException ex){return Results.BadRequest(new{error=ex.Message});}}).RequireAuthorization();
app.MapDelete("/api/admin/users/{id}",(string id,HttpContext c,UserDirectory directory,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Users,"edit"))return Results.Forbid();if(string.Equals(id,AccessControl.UserId(c.User),StringComparison.OrdinalIgnoreCase))return Results.BadRequest(new{error="Kendi hesabınızı silemezsiniz."});if(!directory.Delete(id))return Results.NotFound(new{error="Kullanıcı bulunamadı."});store.RecordAudit("Kullanıcı silindi",id,"",AccessControl.DisplayName(c.User));return Results.Json(new{ok=true});}).RequireAuthorization();
app.MapGet("/api/warranty-products",(HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Warranties,"view"))return Results.Forbid();var products=store.Snapshot()["data"]!["products"]!.AsArray().Select(x=>new{id=x!["id"]?.ToString()??"",code=x["code"]?.ToString()??"",name=x["name"]?.ToString()??""});return Results.Json(new{products});}).RequireAuthorization();
app.MapGet("/api/technical/warranties",(HttpContext c,Store store)=>AccessControl.Has(c.User,AccessPermissions.Technical,"view")?Results.Json(new{records=store.TechnicalWarrantyList()}):Results.Forbid()).RequireAuthorization();
app.MapGet("/api/technical/device/{id}",(string id,HttpContext c,Store store)=>!AccessControl.Has(c.User,AccessPermissions.Technical,"view")?Results.Forbid():store.TechnicalDevice(id) is JsonObject device?Results.Json(device):Results.NotFound(new{error="Cihaz bulunamadı."})).RequireAuthorization();
app.MapPost("/api/technical/device/{id}",async(string id,HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Technical,"edit"))return Results.Forbid();var body=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("Teknik cihaz dosyası gerekli.");var profile=store.SaveTechnicalProfile(id,body,AccessControl.DisplayName(c.User));return Results.Json(new{profile});}).RequireAuthorization();
app.MapPost("/api/technical/device/{id}/services",async(string id,HttpContext c,Store store)=>{
    if(!AccessControl.Has(c.User,AccessPermissions.Technical,"edit")&&!AccessControl.Has(c.User,AccessPermissions.Service,"edit"))return Results.Forbid();
    var device=store.TechnicalDevice(id);if(device?["warranty"] is not JsonObject warranty)return Results.NotFound(new{error="Cihaz bulunamadı."});
    var body=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("Servis kaydı gerekli.");
    var message=(body["message"]?.ToString()??"").Trim();if(message.Length is <2 or >5000)throw new ArgumentException("Servis açıklaması 2-5000 karakter olmalıdır.");
    var status=(body["status"]?.ToString()??"new").Trim();var allowed=new[]{"new","review","scheduled","parts","completed","cancelled"};if(!allowed.Contains(status,StringComparer.Ordinal))throw new ArgumentException("Servis durumu geçersiz.");
    Func<JsonNode?,int,string,string> cleanText=(node,max,label)=>{var value=(node?.ToString()??"").Trim();if(value.Length>max)throw new ArgumentException(label+" çok uzun.");return value;};
    var clean=new JsonObject{
      ["type"]="service",
      ["name"]=cleanText(body["name"],100,"Yetkili adı"),
      ["email"]=cleanText(body["email"],160,"E-posta"),
      ["phone"]=cleanText(body["phone"],40,"Telefon"),
      ["contact"]=cleanText(body["email"],160,"E-posta"),
      ["businessName"]=warranty["businessName"]?.ToString()??"",
      ["address"]=cleanText(body["address"],1200,"Adres"),
      ["productName"]=warranty["productName"]?.ToString()??"",
      ["productCode"]=warranty["productCode"]?.ToString()??"",
      ["serialNumber"]=warranty["serialNumber"]?.ToString()??"",
      ["message"]=message,
      ["serviceStatus"]=status,
      ["appointmentDate"]=cleanText(body["appointmentDate"],30,"Servis tarihi"),
      ["technician"]=cleanText(body["technician"],120,"Teknisyen"),
      ["internalNote"]=cleanText(body["internalNote"],3000,"İç not"),
      ["parts"]=cleanText(body["parts"],2000,"Değişen parçalar"),
      ["resolution"]=cleanText(body["resolution"],3000,"Servis sonucu")
    };
    if(string.IsNullOrWhiteSpace(clean["name"]?.ToString()))clean["name"]=warranty["businessName"]?.ToString()??"Teknik servis";
    var row=store.AddInquiry(clean,AccessControl.DisplayName(c.User));
    return Results.Json(new{ok=true,record=row,requestCode=row["requestCode"]?.ToString()??""});
}).RequireAuthorization();
app.MapPost("/api/technical/device/{id}/attachments",async(string id,HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Technical,"edit"))return Results.Forbid();var form=await c.Request.ReadFormAsync();var file=form.Files.GetFile("file")??throw new ArgumentException("Resim veya PDF seçin.");if(file.Length is <=0 or >26214400)throw new ArgumentException("Teknik dosya en fazla 25 MB olabilir.");using var ms=new MemoryStream();await file.CopyToAsync(ms);var bytes=ms.ToArray();var detected=Uploads.Detect(bytes)??throw new ArgumentException("Desteklenen teknik dosyalar: PNG, JPEG, WebP, GIF veya PDF.");if(detected.Mime is not ("image/png" or "image/jpeg" or "image/webp" or "image/gif" or "application/pdf"))throw new ArgumentException("Teknik alana yalnız resim veya PDF yüklenebilir.");var directory=Path.Combine(dataPath,"technical-files");Directory.CreateDirectory(directory);var storedName=Guid.NewGuid().ToString("N")+detected.Extension;await File.WriteAllBytesAsync(Path.Combine(directory,storedName),bytes);var title=(form["title"].ToString()??"").Trim();if(title.Length is <1 or >140){File.Delete(Path.Combine(directory,storedName));throw new ArgumentException("Dosya başlığı 1-140 karakter olmalıdır.");}var description=(form["description"].ToString()??"").Trim();if(description.Length>500){File.Delete(Path.Combine(directory,storedName));throw new ArgumentException("Dosya açıklaması çok uzun.");}var meta=new JsonObject{["id"]=Guid.NewGuid().ToString("N"),["title"]=title,["description"]=description,["originalName"]=Path.GetFileName(file.FileName),["storedName"]=storedName,["mime"]=detected.Mime,["size"]=bytes.LongLength,["uploadedAt"]=DateTimeOffset.UtcNow.ToString("O"),["uploadedBy"]=AccessControl.DisplayName(c.User)};try{var saved=store.AddTechnicalAttachment(id,meta,AccessControl.DisplayName(c.User));return Results.Json(new{attachment=saved});}catch{try{File.Delete(Path.Combine(directory,storedName));}catch{}throw;}}).RequireAuthorization();
app.MapGet("/api/technical/device/{id}/attachments/{attachmentId}/view",(string id,string attachmentId,HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Technical,"view"))return Results.Forbid();var a=store.TechnicalAttachment(id,attachmentId);if(a==null)return Results.NotFound();var name=a["storedName"]?.ToString()??"";if(!System.Text.RegularExpressions.Regex.IsMatch(name,@"^[a-f0-9]{32}\.(png|jpg|webp|gif|pdf)$"))return Results.NotFound();var path=Path.Combine(dataPath,"technical-files",name);if(!File.Exists(path))return Results.NotFound();return Results.File(path,a["mime"]?.ToString()??"application/octet-stream",enableRangeProcessing:true);}).RequireAuthorization();
app.MapGet("/api/technical/device/{id}/attachments/{attachmentId}/download",(string id,string attachmentId,HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Technical,"download"))return Results.Forbid();var a=store.TechnicalAttachment(id,attachmentId);if(a==null)return Results.NotFound();var name=a["storedName"]?.ToString()??"";if(!System.Text.RegularExpressions.Regex.IsMatch(name,@"^[a-f0-9]{32}\.(png|jpg|webp|gif|pdf)$"))return Results.NotFound();var path=Path.Combine(dataPath,"technical-files",name);if(!File.Exists(path))return Results.NotFound();return Results.File(path,a["mime"]?.ToString()??"application/octet-stream",a["originalName"]?.ToString()??name);}).RequireAuthorization();
app.MapDelete("/api/technical/device/{id}/attachments/{attachmentId}",(string id,string attachmentId,HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Technical,"edit"))return Results.Forbid();var removed=store.RemoveTechnicalAttachment(id,attachmentId,AccessControl.DisplayName(c.User));if(removed==null)return Results.NotFound(new{error="Dosya bulunamadı."});var name=removed["storedName"]?.ToString()??"";if(System.Text.RegularExpressions.Regex.IsMatch(name,@"^[a-f0-9]{32}\.(png|jpg|webp|gif|pdf)$")){var path=Path.Combine(dataPath,"technical-files",name);if(File.Exists(path))File.Delete(path);}return Results.Json(new{ok=true});}).RequireAuthorization();
app.MapGet("/api/catalog",(HttpContext c,Store store)=>{
    var state=store.Snapshot();if(!c.Request.Query.ContainsKey("admin")){var publicData=CatalogRules.Public(state["data"]!.AsObject());publicData["posts"]=new JsonArray();publicData["settings"]!["logo"]="";return Results.Json(new{data=publicData});}
    if(c.User.Identity?.IsAuthenticated!=true)return Results.Json(new{error="Oturum açın."},statusCode:401);
    if(!CatalogPermissionGuard.CanOpenAdmin(c.User))return Results.Forbid();
    var history=AccessControl.Has(c.User,AccessPermissions.Backup,"view")?new JsonArray(state["history"]!.AsArray().Select(h=>{var x=h!.DeepClone().AsObject();x.Remove("data");return (JsonNode)x;}).ToArray()):new JsonArray();
    var events=state["events"]!.AsArray().GroupBy(x=>new{kind=x!["kind"]!.ToString(),product=x["product"]!.ToString()}).Select(g=>new{g.Key.kind,g.Key.product,total=g.Count()});
    // Compatibility shim for the packaged prebuilt admin bundle. The active R9 source and stored catalog
    // no longer contain logo/blog fields; a fresh ClientApp build makes this shim unnecessary.
    var adminData=state["data"]!.DeepClone().AsObject();adminData["settings"]!["logo"]="";adminData["posts"]=new JsonArray();
    var inquiryRows=new JsonArray();foreach(var node in state["inquiries"]!.AsArray().Take(100)){if(node is not JsonObject row)continue;var parsed=JsonNode.Parse(row["data"]?.ToString()??"{}")?.AsObject();var service=string.Equals(parsed?["type"]?.ToString(),"service",StringComparison.OrdinalIgnoreCase);if(service&&!AccessControl.Has(c.User,AccessPermissions.Service,"view"))continue;if(!service&&!AccessControl.Has(c.User,AccessPermissions.Inquiries,"view"))continue;inquiryRows.Add(row.DeepClone());}
    return Results.Json(new{data=adminData,revision=state["revision"],history,events,inquiries=inquiryRows});
});
app.MapGet("/api/inquiries/unread-count",(HttpContext c,Store store)=>{if(c.User.Identity?.IsAuthenticated!=true)return Results.Json(new{error="Oturum açın."},statusCode:401);var support=AccessControl.Has(c.User,AccessPermissions.Inquiries,"view");var service=AccessControl.Has(c.User,AccessPermissions.Service,"view");if(!support&&!service)return Results.Forbid();return Results.Json(new{count=store.UnreadInquiryCount(support,service)});}).RequireAuthorization(); app.MapPost("/api/catalog",async(HttpContext c,Store store)=>{var body=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("İçerik gerekli.");var after=body["data"]?.AsObject()??throw new ArgumentException("Katalog verisi gerekli.");var before=store.Snapshot()["data"]!.AsObject();if(!CatalogPermissionGuard.CanSave(c.User,before,after))return Results.Forbid();store.RecordAudit("Katalog güncellemesi yetkilendirildi","catalog","",AccessControl.DisplayName(c.User));return Results.Json(store.Save(body));}).RequireAuthorization();
app.MapGet("/api/prices/template",(HttpContext c,Store store)=>AccessControl.Has(c.User,AccessPermissions.Prices,"view")?Results.File(PriceSpreadsheet.CreateTemplate(store.Snapshot()["data"]!.AsObject()),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","inokskar-fiyat-sablonu.xlsx"):Results.Forbid()).RequireAuthorization();
app.MapPost("/api/prices/preview",async(HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Prices,"view"))return Results.Forbid();
    var form=await c.Request.ReadFormAsync();var file=form.Files.GetFile("file")??throw new ArgumentException("Excel veya CSV fiyat dosyasını seçin.");
    if(file.Length is <=0 or >5L*1024*1024)throw new ArgumentException("Fiyat dosyası en fazla 5 MB olabilir.");
    using var stream=file.OpenReadStream();var rows=PriceSpreadsheet.Preview(stream,file.FileName,store.Snapshot()["data"]!.AsObject());return Results.Json(new{rows});
}).RequireAuthorization();
app.MapPost("/api/prices/import",async(HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Prices,"edit"))return Results.Forbid();
    var form=await c.Request.ReadFormAsync();var file=form.Files.GetFile("file")??throw new ArgumentException("Excel veya CSV fiyat dosyasını seçin.");
    if(file.Length is <=0 or >5L*1024*1024)throw new ArgumentException("Fiyat dosyası en fazla 5 MB olabilir.");
    if(!int.TryParse(form["revision"].ToString(),out var revision))throw new ArgumentException("Geçerli katalog sürümü gönderilmedi.");
    var state=store.Snapshot();using var stream=file.OpenReadStream();var rows=PriceSpreadsheet.Preview(stream,file.FileName,state["data"]!.AsObject());
    var data=PriceSpreadsheet.Apply(state["data"]!.DeepClone().AsObject(),rows);var saved=store.Save(new JsonObject{["revision"]=revision,["data"]=data,["label"]="Excel/CSV toplu fiyat güncellemesi"});
    return Results.Json(new{updated=rows.Count,revision=saved["revision"],data=saved["data"]});
}).RequireAuthorization();
app.MapGet("/api/backup",(HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Backup,"view"))return Results.Forbid();var bytes=CatalogBackup.Create(store.Snapshot(),dataPath);store.RecordAudit("Tam ZIP yedeği oluşturuldu","backup","",AccessControl.DisplayName(c.User));return Results.File(bytes,"application/zip",$"inokskar-tam-yedek-{DateTime.Now:yyyy-MM-dd-HHmm}.zip");}).RequireAuthorization();
app.MapPost("/api/rate/refresh",async(HttpContext c,Store store,IHttpClientFactory clients,CancellationToken cancellationToken)=>{if(!AccessControl.Has(c.User,AccessPermissions.Prices,"edit"))return Results.Forbid();
    var snapshot=await OfficialExchangeRate.FetchEuroBuyingAsync(clients,cancellationToken);
    store.UpdateExchangeRate(snapshot,DateTimeOffset.UtcNow);
    var state=store.Snapshot();var settings=state["data"]!["settings"]!;
    return Results.Json(new{rate=settings["rate"],lastRate=settings["lastRate"],rateSource=settings["rateSource"],rateDate=settings["rateDate"],revision=state["revision"]});
}).RequireAuthorization();
app.MapGet("/api/admin/system-health",(HttpContext c,Store store,MailQueue mail,OperationalLog logs,AdminLoginGuard loginGuard)=>{if(!AccessControl.Has(c.User,AccessPermissions.System,"view"))return Results.Forbid();
    var state=store.Snapshot();var settings=state["data"]!["settings"]!;var last=settings["lastRate"]?.ToString()??"";
    var stale=!DateTimeOffset.TryParse(last,out var checkedAt)||DateTimeOffset.UtcNow-checkedAt>TimeSpan.FromMinutes(30);
    return Results.Json(new{revision=state["revision"],warranties=state["warranties"]!.AsArray().Count,inquiries=state["inquiries"]!.AsArray().Count,rate=settings["rate"],lastRate=last,rateSource=settings["rateSource"],rateDate=settings["rateDate"],rateStale=stale,storageProvider="JSON / App_Data",backup=SystemStatus.Backup(dataPath),mail=mail.Status(),loginSecurity=loginGuard.Status(),errors=logs.RecentErrors()});
}).RequireAuthorization();
app.MapGet("/api/admin/audit",(HttpContext c,Store store)=>AccessControl.Has(c.User,AccessPermissions.System,"view")?Results.Json(new{rows=store.AuditLog(500)}):Results.Forbid()).RequireAuthorization();
app.MapGet("/api/admin/mail/settings",(HttpContext c,MailSettingsStore settings)=>AccessControl.Has(c.User,AccessPermissions.System,"view")?Results.Json(new{settings=settings.PublicSettings(),providers=MailSettingsStore.ProviderJson()}):Results.Forbid()).RequireAuthorization();
app.MapPost("/api/admin/mail/settings",async(HttpContext c,MailSettingsStore settings,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.System,"edit"))return Results.Forbid();var body=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("E-posta sağlayıcı ayarları gerekli.");var saved=settings.Save(body);store.RecordAudit("E-posta sağlayıcı ayarı güncellendi","mail",saved.Provider,AccessControl.DisplayName(c.User));return Results.Json(new{ok=true,settings=settings.PublicSettings()});}).RequireAuthorization();
app.MapPost("/api/admin/mail/test",async(HttpContext c,MailQueue mail,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.System,"edit"))return Results.Forbid();var body=await c.Request.ReadFromJsonAsync<JsonObject>()??new JsonObject();var to=(body["to"]?.ToString()??mail.AdminAddress).Trim();if(string.IsNullOrWhiteSpace(to))return Results.BadRequest(new{error="Test alıcısı veya yönetici e-posta adresi gerekli."});try{await mail.SendTestAsync(to,c.RequestAborted);store.RecordAudit("SMTP bağlantı testi başarılı","mail",to,AccessControl.DisplayName(c.User));return Results.Json(new{ok=true,sent=true});}catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or SmtpException){store.RecordAudit("SMTP bağlantı testi başarısız","mail",ex.Message.Length>220?ex.Message[..220]:ex.Message,AccessControl.DisplayName(c.User));return Results.BadRequest(new{error="SMTP bağlantı testi başarısız: "+ex.Message});}}).RequireAuthorization();
app.MapPost("/api/backup/restore",async(HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Backup,"edit"))return Results.Forbid();
    var form=await c.Request.ReadFormAsync();var file=form.Files.GetFile("file")??throw new ArgumentException("ZIP yedek dosyasını seçin.");
    if(file.Length is <=0 or >512L*1024*1024)throw new ArgumentException("Yedek dosyası en fazla 512 MB olabilir.");
    if(!int.TryParse(form["revision"].ToString(),out var revision))throw new ArgumentException("Geçerli katalog sürümü gönderilmedi.");
    using var stream=file.OpenReadStream();var package=CatalogBackup.Read(stream);return Results.Json(CatalogBackup.Restore(package,store,dataPath,revision));
}).RequireAuthorization();
app.MapPost("/api/upload",async(HttpContext c,IWebHostEnvironment e)=>{
    var form=await c.Request.ReadFormAsync();var file=form.Files.GetFile("file")??throw new ArgumentException("Dosya seçin.");
    var kind=c.Request.Query["kind"].ToString();if(string.IsNullOrEmpty(kind))kind="image";if(kind=="hero"&&!AccessControl.Has(c.User,AccessPermissions.Settings,"edit"))return Results.Forbid();if(kind is "image" or "pdf"&&!AccessControl.Has(c.User,AccessPermissions.Catalog,"edit"))return Results.Forbid();
    if(kind is not ("image" or "pdf" or "hero"))throw new ArgumentException("Geçersiz yükleme alanı.");
    var maxBytes=kind=="hero"?100L*1024*1024:20L*1024*1024;
    if(file.Length<=0||file.Length>maxBytes)throw new ArgumentException(kind=="hero"?"Ana sayfa medyası en fazla 100 MB olabilir.":"Dosya en fazla 20 MB olabilir.");
    using var buffer=new MemoryStream();await file.CopyToAsync(buffer);var bytes=buffer.ToArray();var type=Uploads.Detect(bytes);
    if(type==null)throw new ArgumentException("Desteklenen dosya türleri: PNG, JPEG, WebP, GIF, MP4 veya PDF.");
    var mime=type.Value.Mime;
    var valid=kind switch{
        "pdf"=>mime=="application/pdf",
        "hero"=>mime is "image/png" or "image/jpeg" or "image/webp" or "image/gif" or "video/mp4",
        _=>mime is "image/png" or "image/jpeg" or "image/webp"
    };
    if(!valid)throw new ArgumentException(kind=="pdf"?"PDF alanına yalnızca PDF yükleyin.":"Bu alana PNG, JPEG, WebP, GIF veya MP4 yükleyin.");
    var directory=Path.Combine(dataPath,"uploads");Directory.CreateDirectory(directory);var name=Guid.NewGuid().ToString("N")+type.Value.Extension;await File.WriteAllBytesAsync(Path.Combine(directory,name),bytes);return Results.Json(new{url="/api/files/"+name});
}).RequireAuthorization();
app.MapGet("/api/files/{id}",(string id)=>{
    if(!System.Text.RegularExpressions.Regex.IsMatch(id,@"^[a-f0-9]{32}\.(png|jpg|webp|gif|mp4|pdf)$"))return Results.NotFound();var file=Path.Combine(dataPath,"uploads",id);if(!File.Exists(file))return Results.NotFound();var mime=Path.GetExtension(file).ToLowerInvariant() switch{".png"=>"image/png",".jpg"=>"image/jpeg",".webp"=>"image/webp",".gif"=>"image/gif",".mp4"=>"video/mp4",_=>"application/pdf"};return Results.File(file,mime,enableRangeProcessing:true);
});
app.MapGet("/api/warranties",(HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Warranties,"view"))return Results.Forbid();var records=store.WarrantyList();if(AccessControl.Role(c.User)=="TechnicalService")foreach(var node in records){if(node is not JsonObject row)continue;row.Remove("verificationCode");row.Remove("verificationHint");}return Results.Json(new{records});}).RequireAuthorization();
app.MapPost("/api/warranties",async(HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Warranties,"edit"))return Results.Forbid();var body=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("Garanti kaydı gerekli.");var saved=store.SaveWarranty(body,AccessControl.DisplayName(c.User));if(AccessControl.Role(c.User)=="TechnicalService"){saved.Remove("verificationCode");if(saved["record"] is JsonObject record){record.Remove("verificationCode");record.Remove("verificationHint");}}return Results.Json(saved);}).RequireAuthorization();
app.MapDelete("/api/warranties/{id}",(string id,HttpContext c,Store store)=>{if(!AccessControl.Has(c.User,AccessPermissions.Warranties,"edit"))return Results.Forbid();if(!store.DeleteWarranty(id,AccessControl.DisplayName(c.User)))return Results.NotFound(new{error="Garanti kaydı bulunamadı."});return Results.Json(new{ok=true});}).RequireAuthorization();
app.MapPost("/api/warranty/query",async(HttpContext c,Store store)=>{
    var body=await c.Request.ReadFromJsonAsync<JsonObject>();
    var serial=body?["serialNumber"]?.ToString()??"";var code=body?["verificationCode"]?.ToString()??"";
    var warranty=store.QueryWarranty(serial,code);
    return warranty==null?Results.Json(new{found=false,message="Girilen bilgilerle eşleşen garanti kaydı bulunamadı. Seri numarası ve garanti doğrulama kodunu kontrol edin."}):Results.Json(new{found=true,warranty});
}).RequireRateLimiting("warranty-query");
app.MapDelete("/api/inquiries/{id}",(string id,HttpContext c,Store store)=>{var inquiry=store.InquiryById(id);if(inquiry==null)return Results.NotFound(new{error="Müşteri talebi bulunamadı."});var parsed=JsonNode.Parse(inquiry["data"]?.ToString()??"{}")?.AsObject();var service=string.Equals(parsed?["type"]?.ToString(),"service",StringComparison.OrdinalIgnoreCase);if(service&&!AccessControl.Has(c.User,AccessPermissions.Service,"edit"))return Results.Forbid();if(!service&&!AccessControl.Has(c.User,AccessPermissions.Inquiries,"edit"))return Results.Forbid();if(!store.RemoveInquiry(id,AccessControl.DisplayName(c.User),service?"service":"support"))return Results.NotFound(new{error="Müşteri talebi bulunamadı."});return Results.Json(new{ok=true});}).RequireAuthorization();
app.MapPost("/api/inquiry-status",async(HttpContext c,Store store)=>{
    var body=await c.Request.ReadFromJsonAsync<JsonObject>()??new JsonObject();
    var phone=(body["phone"]?.ToString()??"").Trim();
    var normalized=InquiryStatusPages.NormalizePhone(phone);
    if(normalized.Length!=10)return Results.BadRequest(new{error="Geçerli bir Türkiye telefon numarası girin. +90, 0 veya ülke kodu olmadan yazabilirsiniz."});
    var rows=InquiryStatusPages.Lookup(store,phone);
    return Results.Json(new{ok=true,count=rows.Count,inquiries=rows});
}).RequireRateLimiting("warranty-query");
app.MapPost("/api/inquiry",async(HttpContext c,Store store,MailQueue mail,IConfiguration config,CustomerDirectory customers)=>{
    var b=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("Form gerekli.");if(!string.IsNullOrEmpty(b["website"]?.ToString()))return Results.Json(new{ok=true});
    var name=(b["name"]?.ToString()??"").Trim();var email=(b["email"]?.ToString()??b["contact"]?.ToString()??"").Trim();var phone=(b["phone"]?.ToString()??"").Trim();var message=(b["message"]?.ToString()??"").Trim();
    if(name.Length is < 2 or > 100||message.Length is < 2 or > 5000)return Results.BadRequest(new{error="Ad ve talep açıklaması eksik veya geçersiz."});if(!System.Net.Mail.MailAddress.TryCreate(email,out var parsedEmail)||!System.Text.RegularExpressions.Regex.IsMatch(email,@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$"))return Results.BadRequest(new{error="E-posta adresi eksik veya hatalı. Kişisel veya iş e-posta adresi kullanabilirsiniz (ör. ornek@gmail.com)."});var phoneDigits=System.Text.RegularExpressions.Regex.Replace(phone,@"\D","");if(!System.Text.RegularExpressions.Regex.IsMatch(phone,@"^\+?[\d\s().-]+$")||phoneDigits.Length is <10 or >15)return Results.BadRequest(new{error="Telefon numarası eksik veya hatalı. 10-15 rakam girin; yalnızca +, boşluk, parantez, nokta ve tire kullanılabilir."});
    // Preserve the selected support purpose and contact channel with the inquiry record.
    var purpose=(b["purpose"]?.ToString()??"genel").Trim().ToLowerInvariant();
    if(purpose is not ("genel" or "teklif" or "proje"))purpose="genel";
    var preference=(b["contactPreference"]?.ToString()??"phone").Trim().ToLowerInvariant();
    if(preference is not ("phone" or "email" or "system"))preference="phone";
    foreach(var (key,max) in new[]{("topic",60),("productName",180),("productCode",100),("quantity",10),
        ("projectType",80),("projectCity",120),("projectSize",120),("projectTimeline",40)})
        if((b[key]?.ToString()??"").Length>max)return Results.BadRequest(new{error="Talep alanlarından biri izin verilen uzunluğu aşıyor."});
    var clean=new JsonObject{["type"]="support",["name"]=name,["email"]=parsedEmail.Address,
        ["phone"]=phone,["contact"]=parsedEmail.Address,["message"]=message,["purpose"]=purpose,
        ["contactPreference"]=preference,["topic"]=(b["topic"]?.ToString()??"").Trim(),
        ["productName"]=(b["productName"]?.ToString()??"").Trim(),
        ["productCode"]=(b["productCode"]?.ToString()??"").Trim(),
        ["quantity"]=(b["quantity"]?.ToString()??"").Trim(),
        ["projectType"]=(b["projectType"]?.ToString()??"").Trim(),
        ["projectCity"]=(b["projectCity"]?.ToString()??"").Trim(),
        ["projectSize"]=(b["projectSize"]?.ToString()??"").Trim(),
        ["projectTimeline"]=(b["projectTimeline"]?.ToString()??"").Trim()};if(AccessControl.Role(c.User)=="Customer"){var customerId=AccessControl.UserId(c.User);clean["customerId"]=customerId;if(b["saveProfile"]?.ToString()=="true"||b["saveProfile"]?.ToString()=="on")customers.UpdateProfile(customerId,new JsonObject{["name"]=name,["phone"]=phone});}
    var row=store.AddInquiry(clean);var requestCode=row["requestCode"]?.ToString()??"";clean["requestCode"]=requestCode;var count=store.InquiryCount();
    var baseUrl=SeoPages.Origin(config);var adminUrl=baseUrl==null?null:baseUrl+"/admin?tab=inquiries";var admin=MailTemplates.AdminRequest(clean,count,adminUrl);var notificationRecipient=(store.Snapshot()["data"]?["settings"]?["notificationEmail"]?.ToString()??"").Trim();
    if(string.IsNullOrWhiteSpace(notificationRecipient))notificationRecipient=mail.AdminAddress;
    if(mail.IsConfigured&&!string.IsNullOrWhiteSpace(notificationRecipient))
        mail.Enqueue(notificationRecipient,admin.Subject,admin.Html,admin.Text);var customer=MailTemplates.CustomerReceipt(clean);mail.Enqueue(parsedEmail.Address,customer.Subject,customer.Html,customer.Text);
    return Results.Json(new{ok=true,requestCode});
}).RequireRateLimiting("public-write");
app.MapPost("/api/service-request",async(HttpContext c,Store store,MailQueue mail,IConfiguration config,CustomerDirectory customers)=>{
    var b=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("Servis talebi formu gerekli.");if(!string.IsNullOrEmpty(b["website"]?.ToString()))return Results.Json(new{ok=true});
    var name=(b["name"]?.ToString()??"").Trim();var email=(b["email"]?.ToString()??"").Trim();var phone=(b["phone"]?.ToString()??"").Trim();var business=(b["businessName"]?.ToString()??"").Trim();var address=(b["address"]?.ToString()??"").Trim();var message=(b["message"]?.ToString()??"").Trim();
    if(name.Length is < 2 or > 100||business.Length is < 2 or > 180||address.Length is < 5 or > 1200||message.Length is < 2 or > 5000)return Results.BadRequest(new{error="Servis talebi alanlarını kontrol edin. Ad, işletme, adres ve açıklama zorunludur."});if(!System.Net.Mail.MailAddress.TryCreate(email,out var parsedEmail)||!System.Text.RegularExpressions.Regex.IsMatch(email,@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$"))return Results.BadRequest(new{error="E-posta adresi eksik veya hatalı. Kişisel veya iş e-posta adresi kullanabilirsiniz (ör. ornek@gmail.com)."});var servicePhoneDigits=System.Text.RegularExpressions.Regex.Replace(phone,@"\D","");if(!System.Text.RegularExpressions.Regex.IsMatch(phone,@"^\+?[\d\s().-]+$")||servicePhoneDigits.Length is <10 or >15)return Results.BadRequest(new{error="Telefon numarası eksik veya hatalı. 10-15 rakam girin; yalnızca +, boşluk, parantez, nokta ve tire kullanılabilir."});
    foreach(var (key,max) in new[]{("productName",180),("productCode",100),("serialNumber",100)})if((b[key]?.ToString()??"").Length>max)throw new ArgumentException("Servis talebi ürün bilgilerini kontrol edin.");
    var serviceTopic=(b["serviceTopic"]?.ToString()??"ariza").Trim().ToLowerInvariant();
    if(serviceTopic is not ("ariza" or "bakim" or "yedek-parca" or "kurulum" or "diger"))serviceTopic="diger";
    var servicePreference=(b["contactPreference"]?.ToString()??"phone").Trim().ToLowerInvariant();
    if(servicePreference is not ("phone" or "email" or "system"))servicePreference="phone";
    var clean=new JsonObject{["type"]="service",["name"]=name,["email"]=parsedEmail.Address,
        ["phone"]=phone,["contact"]=parsedEmail.Address,["purpose"]="servis",
        ["contactPreference"]=servicePreference,["serviceTopic"]=serviceTopic,
        ["businessName"]=business,["address"]=address,
        ["productName"]=(b["productName"]?.ToString()??"").Trim(),
        ["productCode"]=(b["productCode"]?.ToString()??"").Trim(),
        ["serialNumber"]=(b["serialNumber"]?.ToString()??"").Trim(),
        ["message"]=message,["serviceStatus"]="new",["appointmentDate"]="",
        ["technician"]="",["internalNote"]="",["parts"]="",["resolution"]=""};if(AccessControl.Role(c.User)=="Customer"){var customerId=AccessControl.UserId(c.User);clean["customerId"]=customerId;if(b["saveProfile"]?.ToString()=="true"||b["saveProfile"]?.ToString()=="on")customers.UpdateProfile(customerId,new JsonObject{["name"]=name,["phone"]=phone,["businessName"]=business,["address"]=address});}
    var row=store.AddInquiry(clean);var requestCode=row["requestCode"]?.ToString()??"";clean["requestCode"]=requestCode;var count=store.InquiryCount();
    var baseUrl=SeoPages.Origin(config);var adminUrl=baseUrl==null?null:baseUrl+"/admin?tab=inquiries";var admin=MailTemplates.AdminRequest(clean,count,adminUrl);var notificationRecipient=(store.Snapshot()["data"]?["settings"]?["notificationEmail"]?.ToString()??"").Trim();
    if(string.IsNullOrWhiteSpace(notificationRecipient))notificationRecipient=mail.AdminAddress;
    if(mail.IsConfigured&&!string.IsNullOrWhiteSpace(notificationRecipient))
        mail.Enqueue(notificationRecipient,admin.Subject,admin.Html,admin.Text);var customer=MailTemplates.CustomerReceipt(clean);mail.Enqueue(parsedEmail.Address,customer.Subject,customer.Html,customer.Text);
    return Results.Json(new{ok=true,requestCode});
}).RequireRateLimiting("public-write");
app.MapPost("/api/event",async(HttpContext c,Store store)=>{
    var b=await c.Request.ReadFromJsonAsync<JsonObject>()??throw new ArgumentException("İşlem gerekli.");var kind=b["kind"]?.ToString();var product=b["product"]?.ToString();if(kind is not ("view" or "whatsapp")||string.IsNullOrEmpty(product)||product.Length>100)throw new ArgumentException("Geçersiz işlem.");store.Append("events",new JsonObject{["kind"]=kind,["product"]=product,["created"]=DateTimeOffset.UtcNow.ToString("O")});return Results.Json(new{ok=true});
}).RequireRateLimiting("public-write");
app.MapFallback((HttpContext c,Store store,IWebHostEnvironment env,IConfiguration config)=>{
    var requestPath=c.Request.Path.Value??"/";var path=requestPath.Length>1?requestPath.TrimEnd('/'):requestPath;if(path=="")path="/";
    if(path=="/blog"||path.StartsWith("/blog/"))return Results.Redirect("/urunler",false);
    var data=CatalogRules.Public(store.Snapshot()["data"]!.AsObject());var valid=new[]{"/","/urunler","/iletisim","/hakkimizda","/referanslar"}.Contains(path,StringComparer.OrdinalIgnoreCase);
    foreach(var (prefix,key) in new[]{("/urun/","products"),("/kategori/","categories")})
    {
        if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))continue;
        var encoded=path[prefix.Length..].Split('/',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"";
        var id=Uri.UnescapeDataString(encoded);
        valid=data[key]!.AsArray().Any(x=>string.Equals(x!["id"]!.ToString(),id,StringComparison.OrdinalIgnoreCase));
    }
    return valid?Results.Content(SeoPages.Render(File.ReadAllText(Path.Combine(env.WebRootPath,"index.html")),data,path,SeoPages.Origin(config)),"text/html; charset=utf-8"):Results.Content("<!doctype html><html lang='tr'><meta charset='utf-8'><h1>Sayfa bulunamadı</h1><a href='/'>Ana sayfaya dön</a></html>","text/html; charset=utf-8",statusCode:404);
});
app.Run();
}
catch(Exception fatal)
{
    StartupRecovery.Run(fatal, args);
}
static string RoleLanding(ClaimsPrincipal user)
{
    if(AccessControl.Role(user)=="Customer")return "/hesabim/profil";
    if(AccessControl.IsSuperAdmin(user))return "/admin";
    if(AccessControl.Role(user)=="TechnicalService")return "/teknik";
    if(AccessControl.Has(user,AccessPermissions.Catalog,"view"))return "/admin";
    if(AccessControl.Has(user,AccessPermissions.Warranties,"view"))return "/admin/warranties/list";
    if(AccessControl.Has(user,AccessPermissions.Technical,"view"))return "/teknik";
    return "/account/security";
}
static string LoginPage(string error="",string notice="")=>"""
<!doctype html><html lang="tr"><head><link rel='stylesheet' href='/r129-feedback.css?v=1'><script defer src='/r129-feedback.js?v=1'></script><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"><meta name="color-scheme" content="light"><title>İnokskar Hesap Merkezi | Giriş</title><style>
:root{--navy:#071a31;--navy2:#0b2749;--gold:#d5a348;--gold2:#f0c66b;--ink:#102640;--muted:#61758e;--line:#dce5f0;--surface:#f5f8fc;--danger:#a33333;--ok:#1f6b4a}*{box-sizing:border-box}html,body{margin:0;min-height:100%;font-family:Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif;color:var(--ink);background:#edf3f9}body{min-height:100vh;min-height:100dvh;display:grid;place-items:center;padding:clamp(12px,2.5vw,34px);background:radial-gradient(circle at 18% 10%,#dce9f8 0,transparent 28%),linear-gradient(135deg,#edf4fb,#f8fafc 48%,#e7eef7)}a{color:inherit;text-decoration:none}.shell{width:min(1020px,100%);min-height:min(590px,calc(100dvh - 40px));display:grid;grid-template-columns:minmax(300px,.72fr) minmax(430px,1fr);overflow:hidden;border:1px solid rgba(10,35,66,.12);border-radius:24px;background:#fff;box-shadow:0 26px 70px rgba(8,30,55,.16)}.visual{position:relative;min-height:590px;padding:28px;display:flex;flex-direction:column;justify-content:space-between;isolation:isolate;background:linear-gradient(155deg,#071a31 0%,#0d3157 58%,#102f50 100%)}.visual:before{content:"";position:absolute;inset:0;z-index:-1;background:radial-gradient(circle at 18% 18%,rgba(85,145,210,.18),transparent 36%),linear-gradient(180deg,rgba(255,255,255,.02),rgba(0,0,0,.08))}.visual:after{content:"";position:absolute;left:28px;right:28px;bottom:112px;height:1px;z-index:-1;background:linear-gradient(90deg,rgba(213,163,72,.7),transparent)}.brand{display:inline-flex;align-items:center;width:max-content}.brand img{height:54px;max-width:235px;object-fit:contain;filter:drop-shadow(0 7px 18px rgba(0,0,0,.18))}.visual-copy{max-width:420px;padding:10px 0 2px}.kicker{font-size:.76rem;font-weight:850;letter-spacing:.17em;color:var(--gold2);text-transform:uppercase}.visual h1{margin:11px 0 12px;color:#fff;font-size:clamp(1.8rem,3.4vw,2.75rem);line-height:1.04;letter-spacing:-.035em;max-width:12ch}.visual p{margin:0;max-width:390px;color:#dce8f5;font-size:.88rem;line-height:1.55}.role-row{display:flex;flex-wrap:wrap;gap:6px;margin-top:17px}.role-row span{display:inline-flex;align-items:center;min-height:31px;padding:0 10px;border:1px solid rgba(255,255,255,.15);border-radius:999px;background:rgba(255,255,255,.06);color:#f4f8ff;font-size:.71rem;font-weight:750}.panel{display:flex;align-items:center;padding:clamp(30px,4vw,48px);background:linear-gradient(180deg,#fff,#fbfcfe)}.panel-inner{width:100%;max-width:430px;margin:auto}.panel-top{display:flex;align-items:center;justify-content:space-between;gap:16px;margin-bottom:34px}.back{font-size:.88rem;font-weight:750;color:#405c79}.secure{display:inline-flex;align-items:center;gap:7px;color:#56708e;font-size:.78rem}.secure:before{content:"";width:8px;height:8px;border-radius:50%;background:#37a06d;box-shadow:0 0 0 4px #e8f5ee}.eyebrow{margin:0 0 9px;color:#b57e20;font-size:.74rem;font-weight:850;letter-spacing:.14em;text-transform:uppercase}.panel h2{margin:0;color:#0e2742;font-size:clamp(1.9rem,3vw,2.55rem);letter-spacing:-.035em;line-height:1.08}.lead{margin:12px 0 26px;color:var(--muted);font-size:.94rem;line-height:1.65}.notice,.error{margin:0 0 18px;padding:12px 14px;border-radius:12px;font-size:.88rem;line-height:1.5}.notice:empty,.error:empty{display:none}.notice{color:var(--ok);background:#ebf7f0;border:1px solid #cfe9da}.error{color:var(--danger);background:#fff1f1;border:1px solid #edcdcd}form{display:grid;gap:16px}label{display:grid;gap:7px;color:#253f5d;font-size:.88rem;font-weight:760}.field{position:relative}.field input{width:100%;min-height:54px;padding:0 15px;border:1px solid #cfdae8;border-radius:13px;background:#fff;color:#122944;font:inherit;font-size:1rem;box-shadow:inset 0 1px 2px rgba(11,35,65,.025);transition:border-color .18s,box-shadow .18s,background .18s}.field input:focus{outline:none;border-color:#4f7fb6;box-shadow:0 0 0 4px rgba(43,101,166,.11);background:#fff}.password input{padding-right:62px}.toggle{position:absolute;right:7px;top:7px;min-width:46px;height:40px;border:0;border-radius:9px;background:#eef3f9;color:#385978;font-weight:800;font-size:.72rem}.toggle:hover{background:#e3edf8}.submit{position:relative;overflow:hidden;width:100%;min-height:54px;margin-top:3px;border:1px solid rgba(237,201,112,.7);border-radius:13px;background:linear-gradient(180deg,var(--gold2),var(--gold));color:#fff;font:inherit;font-weight:850;font-size:.98rem;box-shadow:0 13px 26px rgba(197,142,43,.24),inset 0 1px 0 rgba(255,255,255,.35)}.submit:after{content:"";position:absolute;inset:-150% auto -150% -38%;width:34%;transform:rotate(20deg) translateX(-220%);background:linear-gradient(90deg,transparent,rgba(255,255,255,.18),rgba(255,255,255,.95),rgba(255,255,255,.18),transparent);transition:transform .8s ease}.submit:hover:after{transform:rotate(20deg) translateX(520%)}.form-links{display:flex;align-items:center;justify-content:space-between;gap:14px;margin-top:18px;font-size:.88rem}.form-links a{color:#174c86;font-weight:760}.helper{margin-top:26px;padding-top:21px;border-top:1px solid #e2e8f0;color:#6a7d92;font-size:.8rem;line-height:1.58}.mobile-brand{display:none}@media(max-width:900px){body{padding:0;background:#fff}.shell{width:100%;min-height:100dvh;border:0;border-radius:0;grid-template-columns:1fr;box-shadow:none}.visual{display:none}.panel{min-height:100dvh;padding:28px 22px 34px}.panel-inner{max-width:560px}.mobile-brand{display:flex;justify-content:center;margin-bottom:22px}.mobile-brand img{height:50px;max-width:215px}.panel-top{margin-bottom:20px}}@media(max-width:560px){.panel{align-items:flex-start;min-height:100dvh;padding:22px 18px calc(28px + env(safe-area-inset-bottom))}.panel-top{gap:10px;margin-bottom:16px}.secure{font-size:.72rem}.panel h2{font-size:1.8rem}.lead{font-size:.86rem;line-height:1.52;margin-bottom:18px}.field input,.submit{min-height:54px}.form-links{align-items:flex-start;flex-direction:column;gap:10px}.mobile-brand{margin-bottom:17px}.mobile-brand img{height:47px}}@media(prefers-reduced-motion:reduce){*{scroll-behavior:auto}.submit:after{transition:none}}
</style><link rel="stylesheet" href="/r126-auth.css"><link rel="stylesheet" href="/r13-interface.css?v=r22-clean"><link rel="stylesheet" href="/r13-pages.css?v=r22-2"><link rel="stylesheet" href="/r22-public-shell.css?v=r22-4"><link rel="stylesheet" href="/r22-layout-fixes.css?v=r22-30"><script src="/r13-pages.js?v=r22-2" defer></script></head><body class="r126-auth login-page"><main class="shell"><section class="visual" aria-label="İnokskar hesap merkezi"><a class="brand" href="/"><img src="/inokskar-header-brand.png" alt="İnokskar Soğutma ve Endüstriyel Mutfak"></a><div class="visual-copy"><span class="kicker">TEK HESAP MERKEZİ</span><h1>Güvenli giriş, doğru çalışma alanı.</h1><p>Müşteri, yönetim ve teknik servis kullanıcıları aynı güvenli giriş ekranını kullanır. Girişten sonra hesabınızın rol ve yetkilerine uygun çalışma alanına yönlendirilirsiniz.</p><div class="role-row"><span>Müşteri</span><span>Kurucu Süper Admin</span><span>Yönetim</span><span>Teknik Servis</span></div></div></section><section class="panel"><div class="panel-inner"><a class="mobile-brand" href="/"><img src="/inokskar-header-brand.png" alt="İnokskar"></a><div class="panel-top"><a class="back" href="/">Siteye dön</a><span class="secure">Güvenli oturum</span></div><p class="eyebrow">İNOKSKAR HESAP MERKEZİ</p><h2>Hesabınıza giriş yapın</h2><p class="lead">E-posta adresiniz ve şifrenizle devam edin. Sistem sizi yetkinize göre doğru panele yönlendirir.</p><p class="notice">NOTICE</p><p class="error" role="alert">ERROR</p><form method="post" action="/login"><label>E-posta adresi<div class="field"><input name="email" type="email" required autocomplete="username" inputmode="email" placeholder="ornek@firma.com"></div></label><label>Şifre<div class="field password"><input id="login-password" name="password" type="password" required autocomplete="current-password" placeholder="Şifrenizi girin"><button class="toggle" type="button" data-password-toggle="login-password" aria-label="Şifreyi göster">Göster</button></div></label><button class="submit" type="submit">Giriş yap</button></form><div class="form-links"><a href="/forgot-password">Şifremi unuttum</a><a href="/kayit">Müşteri kaydı oluştur</a></div><p class="helper">Oturumlar güvenli cookie ile korunur. Yetkiniz olmayan yönetim veya teknik servis bölümlerine erişim verilmez.</p></div></section></main><script>document.querySelectorAll('[data-password-toggle]').forEach(function(b){b.addEventListener('click',function(){var i=document.getElementById(b.getAttribute('data-password-toggle'));if(!i)return;var show=i.type==='password';i.type=show?'text':'password';b.textContent=show?'Gizle':'Göster';b.setAttribute('aria-label',show?'Şifreyi gizle':'Şifreyi göster');});});</script></body></html>
""".Replace("ERROR",System.Net.WebUtility.HtmlEncode(error)).Replace("NOTICE",System.Net.WebUtility.HtmlEncode(notice));
static string ForgotPasswordPage(string error="",string notice="")=>"""
<!doctype html><html lang="tr"><head><link rel='stylesheet' href='/r129-feedback.css?v=1'><script defer src='/r129-feedback.js?v=1'></script><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"><title>İnokskar | Şifre Yenileme</title><style>
:root{--gold:#d5a348;--gold2:#f0c66b;--ink:#102640;--muted:#61758e}*{box-sizing:border-box}body{margin:0;min-height:100vh;min-height:100dvh;display:grid;place-items:center;padding:24px;background:radial-gradient(circle at 16% 12%,#dbe9f8,transparent 30%),linear-gradient(135deg,#edf4fb,#f9fbfd);font-family:Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif;color:var(--ink)}a{color:#174c86;text-decoration:none;font-weight:750}.card{width:min(540px,100%);padding:clamp(26px,5vw,46px);border:1px solid rgba(10,35,66,.12);border-radius:26px;background:#fff;box-shadow:0 28px 70px rgba(8,30,55,.16)}.logo{display:flex;justify-content:center;margin-bottom:26px}.logo img{height:54px;max-width:235px}.eyebrow{color:#8b5d0d;font-size:.74rem;font-weight:850;letter-spacing:.14em;text-transform:uppercase}.card h1{margin:8px 0 12px;font-size:clamp(2rem,5vw,2.65rem);line-height:1.05;letter-spacing:-.04em}.muted{color:var(--muted);font-size:.91rem;line-height:1.62}.notice,.error{padding:12px 14px;margin:18px 0;border-radius:12px;font-size:.86rem;line-height:1.5}.notice:empty,.error:empty{display:none}.notice{color:#1f6b4a;background:#ebf7f0;border:1px solid #cfe9da}.error{color:#a33333;background:#fff1f1;border:1px solid #edcdcd}form{display:grid;gap:15px;margin-top:20px}label{display:grid;gap:7px;font-size:.87rem;font-weight:760;color:#29435f}input{width:100%;min-height:54px;padding:0 14px;border:1px solid #cfdae8;border-radius:13px;background:#fff;font:inherit;font-size:1rem}input:focus{outline:none;border-color:#4f7fb6;box-shadow:0 0 0 4px rgba(43,101,166,.11)}button{position:relative;overflow:hidden;min-height:54px;margin-top:4px;border:1px solid rgba(237,201,112,.7);border-radius:13px;background:linear-gradient(180deg,var(--gold2),var(--gold));color:#fff;font:inherit;font-weight:850;box-shadow:0 13px 26px rgba(197,142,43,.22)}button:after{content:"";position:absolute;inset:-150% auto -150% -38%;width:34%;transform:rotate(20deg) translateX(-220%);background:linear-gradient(90deg,transparent,rgba(255,255,255,.18),rgba(255,255,255,.95),rgba(255,255,255,.18),transparent);transition:transform .8s ease}button:hover:after{transform:rotate(20deg) translateX(520%)}.bottom{display:flex;justify-content:space-between;gap:14px;flex-wrap:wrap;margin-top:20px;padding-top:18px;border-top:1px solid #e1e8f0;font-size:.88rem}@media(max-width:560px){body{padding:0;align-items:start;background:#fff}.card{min-height:100dvh;border:0;border-radius:0;box-shadow:none;padding:28px 18px calc(28px + env(safe-area-inset-bottom))}.logo img{height:46px}.card h1{font-size:2rem}input,button{min-height:56px}.bottom{flex-direction:column}}
</style><link rel="stylesheet" href="/r13-interface.css?v=r22-clean"><link rel="stylesheet" href="/r13-pages.css?v=r22-2"><link rel="stylesheet" href="/r22-public-shell.css?v=r22-4"><script src="/r13-pages.js?v=r22-2" defer></script></head><body class="r126-auth recovery-page"><main class="card"><a class="logo" href="/"><img src="/inokskar-header-brand.png" alt="İnokskar"></a><span class="eyebrow">HESAP GÜVENLİĞİ</span><h1>Şifrenizi yenileyin</h1><p class="muted">Hesabınızda kullandığınız e-posta adresini girin. Adres kayıtlıysa 30 dakika geçerli, tek kullanımlık şifre yenileme bağlantısı gönderilir.</p><p class="notice">NOTICE</p><p class="error" role="alert">ERROR</p><form method="post" action="/forgot-password"><label>E-posta adresi<input name="email" type="email" required autocomplete="username" inputmode="email" placeholder="ornek@firma.com"></label><button type="submit">Yenileme bağlantısı gönder</button></form><div class="bottom"><a href="/login">Giriş ekranına dön</a><a href="/">Siteye dön</a></div></main></body></html>
""".Replace("ERROR",System.Net.WebUtility.HtmlEncode(error)).Replace("NOTICE",System.Net.WebUtility.HtmlEncode(notice));

static string ResetPasswordPage(string token,string error="",bool valid=true)
{
    var form=valid?"""<form method="post" action="/reset-password"><input type="hidden" name="token" value="TOKEN"><label>Yeni şifre<input name="password" type="password" minlength="8" maxlength="128" required autocomplete="new-password"></label><label>Yeni şifre tekrar<input name="confirm" type="password" minlength="8" maxlength="128" required autocomplete="new-password"></label><button type="submit">Yeni şifreyi kaydet</button></form>""":"";
    var html="""
<!doctype html><html lang="tr"><head><link rel='stylesheet' href='/r129-feedback.css?v=1'><script defer src='/r129-feedback.js?v=1'></script><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"><title>İnokskar | Yeni Şifre</title><style>
:root{--gold:#d5a348;--gold2:#f0c66b;--ink:#102640;--muted:#61758e}*{box-sizing:border-box}body{margin:0;min-height:100vh;min-height:100dvh;display:grid;place-items:center;padding:24px;background:radial-gradient(circle at 16% 12%,#dbe9f8,transparent 30%),linear-gradient(135deg,#edf4fb,#f9fbfd);font-family:Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif;color:var(--ink)}a{color:#174c86;text-decoration:none;font-weight:750}.card{width:min(540px,100%);padding:clamp(26px,5vw,46px);border:1px solid rgba(10,35,66,.12);border-radius:26px;background:#fff;box-shadow:0 28px 70px rgba(8,30,55,.16)}.logo{display:flex;justify-content:center;margin-bottom:26px}.logo img{height:54px;max-width:235px}.eyebrow{color:#8b5d0d;font-size:.74rem;font-weight:850;letter-spacing:.14em;text-transform:uppercase}.card h1{margin:8px 0 12px;font-size:clamp(2rem,5vw,2.65rem);line-height:1.05;letter-spacing:-.04em}.muted{color:var(--muted);font-size:.91rem;line-height:1.62}.error{color:#a33333;background:#fff1f1;border:1px solid #edcdcd;padding:12px 14px;margin:18px 0;border-radius:12px;font-size:.86rem;line-height:1.5}.error:empty{display:none}form{display:grid;gap:15px;margin-top:20px}label{display:grid;gap:7px;font-size:.87rem;font-weight:760;color:#29435f}input{width:100%;min-height:54px;padding:0 14px;border:1px solid #cfdae8;border-radius:13px;background:#fff;font:inherit;font-size:1rem}button{position:relative;overflow:hidden;min-height:54px;margin-top:4px;border:1px solid rgba(237,201,112,.7);border-radius:13px;background:linear-gradient(180deg,var(--gold2),var(--gold));color:#fff;font:inherit;font-weight:850;box-shadow:0 13px 26px rgba(197,142,43,.22)}.bottom{display:flex;justify-content:space-between;gap:14px;flex-wrap:wrap;margin-top:20px;padding-top:18px;border-top:1px solid #e1e8f0;font-size:.88rem}@media(max-width:560px){body{padding:0;align-items:start;background:#fff}.card{min-height:100dvh;border:0;border-radius:0;box-shadow:none;padding:28px 18px calc(28px + env(safe-area-inset-bottom))}.logo img{height:46px}.card h1{font-size:2rem}input,button{min-height:56px}.bottom{flex-direction:column}}
</style><link rel="stylesheet" href="/r13-interface.css?v=r22-clean"><link rel="stylesheet" href="/r13-pages.css?v=r22-2"><link rel="stylesheet" href="/r22-public-shell.css?v=r22-4"><script src="/r13-pages.js?v=r22-2" defer></script></head><body class="r126-auth recovery-page"><main class="card"><a class="logo" href="/"><img src="/inokskar-header-brand.png" alt="İnokskar"></a><span class="eyebrow">HESAP GÜVENLİĞİ</span><h1>Yeni şifrenizi belirleyin</h1><p class="muted">Bağlantı yalnızca bir kez kullanılabilir. Yeni şifreniz 8 ile 128 karakter arasında olmalıdır.</p><p class="error" role="alert">ERROR</p>FORM<div class="bottom"><a href="/forgot-password">Yeni bağlantı iste</a><a href="/login">Giriş ekranına dön</a></div></main></body></html>
""";
    return html.Replace("TOKEN",System.Net.WebUtility.HtmlEncode(token)).Replace("ERROR",System.Net.WebUtility.HtmlEncode(error)).Replace("FORM",form.Replace("TOKEN",System.Net.WebUtility.HtmlEncode(token)));
}

public static class Passwords
{
    public static string Hash(string value){var salt=RandomNumberGenerator.GetBytes(16);var hash=Rfc2898DeriveBytes.Pbkdf2(value,salt,210000,HashAlgorithmName.SHA256,32);return "210000:"+Convert.ToBase64String(salt)+":"+Convert.ToBase64String(hash);}
    public static bool Verify(string value,string stored){try{var parts=stored.Split(':');var iterations=int.Parse(parts[0]);if(iterations is <100000 or >1000000)return false;var actual=Rfc2898DeriveBytes.Pbkdf2(value,Convert.FromBase64String(parts[1]),iterations,HashAlgorithmName.SHA256,32);return CryptographicOperations.FixedTimeEquals(actual,Convert.FromBase64String(parts[2]));}catch{return false;}}
}
public static class Uploads
{
    public static (string Extension,string Mime)? Detect(byte[] b){if(b.Length>=8&&b.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))return(".png","image/png");if(b.Length>=3&&b[0]==255&&b[1]==216&&b[2]==255)return(".jpg","image/jpeg");if(b.Length>=12&&System.Text.Encoding.ASCII.GetString(b,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(b,8,4)=="WEBP")return(".webp","image/webp");if(b.Length>=6&&(System.Text.Encoding.ASCII.GetString(b,0,6)=="GIF87a"||System.Text.Encoding.ASCII.GetString(b,0,6)=="GIF89a"))return(".gif","image/gif");if(b.Length>=12&&System.Text.Encoding.ASCII.GetString(b,4,4)=="ftyp")return(".mp4","video/mp4");if(b.Length>=5&&System.Text.Encoding.ASCII.GetString(b,0,5)=="%PDF-")return(".pdf","application/pdf");return null;}
}
