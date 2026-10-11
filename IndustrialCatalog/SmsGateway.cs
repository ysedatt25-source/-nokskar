using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

public sealed class SmsGateway
{
    readonly SmsSettingsStore settings;
    readonly HttpClient http;
    public SmsGateway(SmsSettingsStore settings){this.settings=settings;http=new HttpClient(new SocketsHttpHandler{AllowAutoRedirect=false,UseProxy=false,ConnectCallback=ConnectPublic}){Timeout=TimeSpan.FromSeconds(20)};}
    internal SmsGateway(SmsSettingsStore settings,HttpMessageHandler handler){this.settings=settings;http=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(20)};}
    public static Uri ValidateEndpoint(string value){if(!Uri.TryCreate(value,UriKind.Absolute,out var u)||u.Scheme!="https"||u.Port!=443||u.UserInfo.Length>0||u.Query.Length>0||u.Fragment.Length>0||u.HostNameType==UriHostNameType.Unknown||u.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase)||u.Host.EndsWith(".local",StringComparison.OrdinalIgnoreCase)||IPAddress.TryParse(u.Host,out var ip)&&!IsPublic(ip))throw new ArgumentException("API adresi, dışarıya açık ve 443 portunda çalışan bir HTTPS adresi olmalıdır.");return u;}
    internal static bool IsPublic(IPAddress ip){if(ip.IsIPv4MappedToIPv6)ip=ip.MapToIPv4();if(IPAddress.IsLoopback(ip))return false;var b=ip.GetAddressBytes();if(b.Length==4)return b[0]!=0&&b[0]!=10&&b[0]!=127&&b[0]<224&&!(b[0]==169&&b[1]==254)&&!(b[0]==172&&b[1]>=16&&b[1]<=31)&&!(b[0]==192&&(b[1]==168||b[1]==0))&&!(b[0]==100&&b[1]>=64&&b[1]<=127)&&!(b[0]==198&&(b[1]==18||b[1]==19));return b.Length==16&&(b[0]&0xe0)==0x20;}
    static async ValueTask<Stream> ConnectPublic(SocketsHttpConnectionContext c,CancellationToken ct){var ips=await Dns.GetHostAddressesAsync(c.DnsEndPoint.Host,ct);if(ips.Length==0||ips.Any(x=>!IsPublic(x)))throw new HttpRequestException("API sunucusu adresi uygun değil.");var socket=new Socket(ips[0].AddressFamily,SocketType.Stream,ProtocolType.Tcp);try{await socket.ConnectAsync(new IPEndPoint(ips[0],c.DnsEndPoint.Port),ct);return new NetworkStream(socket,true);}catch{socket.Dispose();throw;}}
    internal static JsonNode Expand(JsonNode node,IReadOnlyDictionary<string,string> values){if(node is JsonObject o){var r=new JsonObject();foreach(var p in o)r[p.Key]=p.Value==null?null:Expand(p.Value,values);return r;}if(node is JsonArray a)return new JsonArray(a.Select(x=>x==null?null:Expand(x,values)).ToArray());if(node is JsonValue v&&v.TryGetValue<string>(out var text)){text=System.Text.RegularExpressions.Regex.Replace(text,@"\{(phone|message|sender|username|secret)\}",m=>values.TryGetValue(m.Groups[1].Value,out var value)?value:m.Value);return JsonValue.Create(text)!;}return node.DeepClone();}
    public async Task Send(string phone,string message,CancellationToken ct=default)
    {
        var p=settings.ActiveProfile()??throw new InvalidOperationException("Gönderim hizmeti kapalı.");var secret=settings.Secret(p);phone=SmsPhone.Normalize(phone);if(message.Length is <1 or >1000)throw new ArgumentException("Mesaj 1-1000 karakter olmalıdır.");
        var endpoint=p.Endpoint.Replace("{username}",Uri.EscapeDataString(p.Username),StringComparison.Ordinal);using var req=new HttpRequestMessage(HttpMethod.Post,ValidateEndpoint(endpoint));
        if(p.Auth=="basic")req.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(p.Username+":"+secret)));
        else if(p.Auth=="bearer")req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",secret);
        else if(p.Auth=="header")req.Headers.Add(p.HeaderName,secret);
        JsonNode body;
        switch(p.Adapter){
            case "netgsm":body=new JsonObject{["msgheader"]=p.Sender,["encoding"]="TR",["iysfilter"]="0",["messages"]=new JsonArray(new JsonObject{["msg"]=message,["no"]=phone[2..]})};break;
            case "iletimerkezi":body=new JsonObject{["request"]=new JsonObject{["authentication"]=new JsonObject{["key"]=p.Username,["hash"]=secret},["order"]=new JsonObject{["sender"]=p.Sender,["iys"]="0",["message"]=new JsonObject{["text"]=message,["receipents"]=new JsonObject{["number"]=new JsonArray(JsonValue.Create(phone))}}}}};break;
            case "verimor":body=new JsonObject{["username"]=p.Username,["password"]=secret,["source_addr"]=p.Sender,["messages"]=new JsonArray(new JsonObject{["msg"]=message,["dest"]=phone})};break;
            case "twilio":req.Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"To","+"+phone},{"From",p.Sender},{"Body",message}});body=new JsonObject();break;
            default:body=Expand(JsonNode.Parse(p.Template)??throw new ArgumentException("İstek şablonu eksik."),new Dictionary<string,string>{{"phone",phone},{"message",message},{"sender",p.Sender},{"username",p.Username},{"secret",secret}});break;
        }
        req.Content??=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json");
        try{using var res=await http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);if(!res.IsSuccessStatusCode)throw new InvalidOperationException("Sağlayıcı gönderimi kabul etmedi. Bağlantı bilgilerini ve bakiyeyi kontrol edin.");
            await using var stream=await res.Content.ReadAsStreamAsync(ct);using var memory=new MemoryStream();var buffer=new byte[4096];int count;while((count=await stream.ReadAsync(buffer,ct))>0){if(memory.Length+count>65536)throw new InvalidOperationException("Sağlayıcı yanıtı doğrulanamadı.");memory.Write(buffer,0,count);}var text=Encoding.UTF8.GetString(memory.ToArray()).Trim();
            if(p.Adapter=="verimor"){if(!long.TryParse(text,out var id)||id<=0)throw new InvalidOperationException("Sağlayıcı gönderimi doğrulamadı.");}
            else{var json=JsonNode.Parse(text);JsonNode? v=json;foreach(var key in p.SuccessPath.Split('.',StringSplitOptions.RemoveEmptyEntries))v=v is JsonObject jo?jo[key]:null;if(v==null||(p.SuccessValue=="*"?string.IsNullOrWhiteSpace(v.ToString()):v.ToString()!=p.SuccessValue))throw new InvalidOperationException("Sağlayıcı gönderimi doğrulamadı.");}
        }catch(OperationCanceledException) when(!ct.IsCancellationRequested){throw new InvalidOperationException("Gönderim zaman aşımına uğradı. Sonucu sağlayıcı panelinden kontrol edin.");}catch(HttpRequestException){throw new InvalidOperationException("Sağlayıcı bağlantısı kurulamadı.");}catch(System.Text.Json.JsonException){throw new InvalidOperationException("Sağlayıcı yanıtı doğrulanamadı.");}
    }
}
public static class SmsPhone
{
    public static string? TryNormalize(string? phone){var raw=(phone??"").Trim();if(raw.Length>40)return null;if(raw.Any(c=>!char.IsAsciiDigit(c)&&c!='+'&&c!=' '&&c!='-'&&c!='('&&c!=')'))return null;var v=new string(raw.Where(char.IsAsciiDigit).ToArray());if(v.StartsWith("0090",StringComparison.Ordinal))v=v[2..];if(v.Length==11&&v[0]=='0')v="9"+v;if(v.Length==10)v="90"+v;return v.Length==12&&v.StartsWith("905",StringComparison.Ordinal)?v:null;}
    public static string Normalize(string? phone)=>TryNormalize(phone)??throw new ArgumentException("Geçerli bir cep telefonu girin. Örnek: 542 123 45 67.");
}
