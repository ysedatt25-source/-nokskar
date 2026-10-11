using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;

public sealed record SmsPreset(string Id,string Name,string Adapter,string Endpoint,string Auth,string SuccessPath,string SuccessValue,string UsernameLabel,string SecretLabel);
public sealed class SmsProfile
{
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public string Adapter {get;set;}="";
    public string Endpoint {get;set;}="";
    public string Auth {get;set;}="";
    public string Username {get;set;}="";
    public string ProtectedSecret {get;set;}="";
    public string Sender {get;set;}="";
    public string Template {get;set;}="";
    public string SuccessPath {get;set;}="";
    public string SuccessValue {get;set;}="";
    public string HeaderName {get;set;}="";
}
public sealed class SmsSettingsStore
{
    readonly object gate=new(); readonly string file; readonly IDataProtector protector;
    static readonly JsonSerializerOptions Options=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true,WriteIndented=true};
    sealed class State{public State(){} public bool Enabled{get;set;} public string Selected{get;set;}=""; public List<SmsProfile> Profiles{get;set;}=new();}
    public static readonly SmsPreset[] Presets={
        new("netgsm","Netgsm","netgsm","https://api.netgsm.com.tr/sms/rest/v2/send","basic","code","00","Kullanıcı kodu","API şifresi"),
        new("iletimerkezi","İleti Merkezi","iletimerkezi","https://api.iletimerkezi.com/v1/send-sms/json","body","response.status.code","200","API anahtarı","API hash"),
        new("verimor","Verimor","verimor","https://sms.verimor.com.tr/v2/send.json","body","","","Kullanıcı adı","API şifresi"),
        new("twilio","Twilio","twilio","https://api.twilio.com/2010-04-01/Accounts/{username}/Messages.json","basic","sid","*","Account SID","Auth Token")};
    public SmsSettingsStore(IWebHostEnvironment env,IConfiguration config,IDataProtectionProvider data){var path=Path.GetFullPath(config["Storage:Path"]??"App_Data",env.ContentRootPath);Directory.CreateDirectory(path);file=Path.Combine(path,"sms-settings.json");protector=data.CreateProtector("INOKSKAR.SmsSettings.v1");}
    State Load()=>File.Exists(file)?JsonSerializer.Deserialize<State>(File.ReadAllText(file),Options)??new():new();
    void SaveState(State s){File.WriteAllText(file+".tmp",JsonSerializer.Serialize(s,Options));File.Move(file+".tmp",file,true);}
    public SmsProfile? ActiveProfile(){lock(gate){var s=Load();var p=s.Profiles.FirstOrDefault(x=>x.Id==s.Selected);return s.Enabled&&p!=null&&Configured(p)?JsonSerializer.Deserialize<SmsProfile>(JsonSerializer.Serialize(p,Options),Options):null;}}
    public bool Active=>ActiveProfile()!=null;
    public string Secret(SmsProfile p){try{return protector.Unprotect(p.ProtectedSecret);}catch{return "";}}
    bool Configured(SmsProfile p)=>p.Endpoint.Length>0&&p.Adapter.Length>0&&p.Sender.Length>0&&Secret(p).Length>0&&(p.Auth is "bearer" or "header"||p.Username.Length>0)&&(p.Adapter=="verimor"||p.SuccessPath.Length>0&&p.SuccessValue.Length>0)&&(p.Adapter!="generic"||p.Template.Length>0);
    public object PublicSettings(){lock(gate){var s=Load();return new{enabled=s.Enabled,active=s.Enabled&&s.Profiles.Any(x=>x.Id==s.Selected&&Configured(x)),selected=s.Selected,presets=Presets,profiles=s.Profiles.Select(p=>new{p.Id,p.Name,p.Adapter,p.Endpoint,p.Auth,p.Username,p.Sender,p.Template,p.SuccessPath,p.SuccessValue,p.HeaderName,secretConfigured=Secret(p).Length>0}).ToArray()};}}
    static string Clean(JsonObject b,string key,int max=500){var v=(b[key]?.ToString()??"").Trim();if(v.Length>max||v.Any(c=>c=='\r'||c=='\n')&&key!="template")throw new ArgumentException("Alan değeri geçersiz: "+key);return v;}
    public void Save(JsonObject b)
    {
        lock(gate){var s=Load();var enabled=b["enabled"]?.GetValue<bool>()==true;
            if(b["disableOnly"]?.GetValue<bool>()==true){s.Enabled=false;SaveState(s);return;}
            var id=Clean(b,"id",80);var preset=Presets.FirstOrDefault(x=>x.Id==id);var existing=s.Profiles.FirstOrDefault(x=>x.Id==id);
            if(id=="other")id="custom-"+Guid.NewGuid().ToString("N");else if(preset==null&&existing==null)throw new ArgumentException("Sağlayıcı seçimi geçersiz.");
            var p=new SmsProfile{Id=id,Name=Clean(b,"name",100),Adapter=preset?.Adapter??"generic",Endpoint=Clean(b,"endpoint"),Auth=Clean(b,"auth",20),Username=Clean(b,"username",320),Sender=Clean(b,"sender",50),Template=Clean(b,"template",10000),SuccessPath=Clean(b,"successPath",200),SuccessValue=Clean(b,"successValue",200),HeaderName=Clean(b,"headerName",80)};
            if(p.Name.Length<2)throw new ArgumentException("Sağlayıcı adını girin.");
            if(p.Auth is not ("basic" or "bearer" or "header" or "body"))throw new ArgumentException("Kimlik doğrulama türünü seçin.");
            if(p.Auth=="header"&&!System.Text.RegularExpressions.Regex.IsMatch(p.HeaderName,"^[A-Za-z][A-Za-z0-9-]{0,79}$"))throw new ArgumentException("API başlık adını girin.");
            if(p.Endpoint.Length>0)SmsGateway.ValidateEndpoint(p.Endpoint.Replace("{username}","ACexample",StringComparison.Ordinal));
            if(p.Adapter=="generic"&&p.Template.Length>0){var json=JsonNode.Parse(p.Template);if(json is not JsonObject)throw new ArgumentException("İstek şablonu JSON nesnesi olmalıdır.");if(!p.Template.Contains("{phone}",StringComparison.Ordinal)||!p.Template.Contains("{message}",StringComparison.Ordinal))throw new ArgumentException("Şablon {phone} ve {message} alanlarını içermelidir.");}
            var secret=Clean(b,"secret",1024);p.ProtectedSecret=secret.Length>0?protector.Protect(secret):existing?.ProtectedSecret??"";
            if(enabled&&(!Configured(p)||p.Adapter=="generic"&&(p.Template.Length==0||p.SuccessPath.Length==0||p.SuccessValue.Length==0)))throw new ArgumentException("Aktifleştirmek için bağlantı, gönderici, kimlik bilgileri ve yanıt doğrulamasını tamamlayın.");
            s.Profiles.RemoveAll(x=>x.Id==id);s.Profiles.Add(p);s.Selected=id;s.Enabled=enabled;SaveState(s);
        }
    }
}
