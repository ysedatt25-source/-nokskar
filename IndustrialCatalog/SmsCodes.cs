using System.Security.Cryptography;
using System.Text.Json;

public sealed class SmsChallenge
{
    public string Id{get;set;}="";public string Phone{get;set;}="";public string Purpose{get;set;}="";public string CodeHash{get;set;}="";public string CustomerId{get;set;}="";public string Stamp{get;set;}="";public DateTimeOffset Created{get;set;}public DateTimeOffset Expires{get;set;}public int Attempts{get;set;}public bool Delivered{get;set;}public bool Used{get;set;}
}
public sealed class SmsCodes
{
    readonly object gate=new();readonly string file;
    public SmsCodes(IWebHostEnvironment env,IConfiguration config){var path=Path.GetFullPath(config["Storage:Path"]??"App_Data",env.ContentRootPath);Directory.CreateDirectory(path);file=Path.Combine(path,"sms-codes.json");}
    List<SmsChallenge> Load()=>File.Exists(file)?JsonSerializer.Deserialize<List<SmsChallenge>>(File.ReadAllText(file))??new():new();
    void Persist(List<SmsChallenge> rows){File.WriteAllText(file+".tmp",JsonSerializer.Serialize(rows));File.Move(file+".tmp",file,true);}
    public (SmsChallenge Challenge,string Code) Create(string phone,string purpose,CustomerAccountRecord? customer=null){phone=SmsPhone.Normalize(phone);if(purpose is not ("register" or "recover"))throw new ArgumentException("Doğrulama işlemi geçersiz.");lock(gate){var now=DateTimeOffset.UtcNow;var rows=Load().Where(x=>x.Created>now.AddHours(-24)).ToList();var related=rows.Where(x=>x.Phone==phone).ToList();if(related.Any(x=>x.Created>now.AddSeconds(-60))||related.Count(x=>x.Created>now.AddHours(-1))>=5)throw new ArgumentException("Yeni kod için bir süre bekleyin.");foreach(var old in rows.Where(x=>x.Phone==phone&&x.Purpose==purpose))old.Used=true;var code=RandomNumberGenerator.GetInt32(0,1000000).ToString("D6");var c=new SmsChallenge{Id=Guid.NewGuid().ToString("N"),Phone=phone,Purpose=purpose,CodeHash=Passwords.Hash(code),CustomerId=customer?.Id??"",Stamp=customer?.SecurityStamp??"",Created=now,Expires=now.AddMinutes(10)};rows.Add(c);Persist(rows);return(c,code);}}
    public object Requests(){lock(gate)return Load().Where(x=>x.Purpose=="recover"&&x.CustomerId.Length>0&&!x.Used&&x.Expires>DateTimeOffset.UtcNow).OrderByDescending(x=>x.Created).Take(100).Select(x=>new{x.CustomerId,x.Created,x.Expires,x.Delivered}).ToArray();}
    public void Delivered(string id){lock(gate){var rows=Load();var c=rows.FirstOrDefault(x=>x.Id==id);if(c!=null){c.Delivered=true;Persist(rows);}}}
    public void Consume(string id,string code,string purpose,Action<SmsChallenge> complete){lock(gate){var rows=Load();var c=rows.FirstOrDefault(x=>x.Id==id);if(c==null||c.Purpose!=purpose||c.Used||!c.Delivered||c.Expires<=DateTimeOffset.UtcNow||c.Attempts>=5)throw new ArgumentException("Kod hatalı, kullanılmış veya süresi dolmuş. Yeni kod isteyin.");c.Attempts++;Persist(rows);if(code.Length!=6||!code.All(char.IsAsciiDigit)||!Passwords.Verify(code,c.CodeHash))throw new ArgumentException("Doğrulama kodu hatalı.");c.Used=true;Persist(rows);complete(c);}}
}
