using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

public sealed class SmsJob
{
    public string Id{get;set;}="";public string DedupeKey{get;set;}="";public string ProviderId{get;set;}="";public string Payload{get;set;}="";public int Total{get;set;}public int Sent{get;set;}public int Failed{get;set;}public int Cursor{get;set;}public string State{get;set;}="pending";public DateTimeOffset Created{get;set;}=DateTimeOffset.UtcNow;
}
public sealed class SmsQueue:BackgroundService
{
    readonly object gate=new();readonly string file;readonly SmsSettingsStore settings;readonly SmsGateway gateway;readonly IDataProtector protector;
    sealed record Payload(string[] Phones,string Message);
    public SmsQueue(IWebHostEnvironment env,IConfiguration config,SmsSettingsStore settings,SmsGateway gateway,IDataProtectionProvider data){this.settings=settings;this.gateway=gateway;protector=data.CreateProtector("INOKSKAR.SmsQueue.v1");var path=Path.GetFullPath(config["Storage:Path"]??"App_Data",env.ContentRootPath);Directory.CreateDirectory(path);file=Path.Combine(path,"sms-jobs.json");}
    List<SmsJob> Load()=>File.Exists(file)?JsonSerializer.Deserialize<List<SmsJob>>(File.ReadAllText(file))??new():new();
    void Persist(List<SmsJob> rows){File.WriteAllText(file+".tmp",JsonSerializer.Serialize(rows));File.Move(file+".tmp",file,true);}
    public string Enqueue(IEnumerable<string> phones,string message,string dedupeKey=""){var p=settings.ActiveProfile()??throw new ArgumentException("Gönderim hizmeti kapalı.");var recipients=phones.Select(SmsPhone.Normalize).Distinct().ToArray();if(recipients.Length is <1 or >500)throw new ArgumentException("1-500 geçerli alıcı seçin.");message=message.Trim();if(message.Length is <1 or >1000)throw new ArgumentException("Mesaj 1-1000 karakter olmalıdır.");lock(gate){var rows=Load();if(dedupeKey.Length>0&&rows.FirstOrDefault(x=>x.DedupeKey==dedupeKey) is SmsJob prior)return prior.Id;if(rows.Count(x=>x.State=="pending")>=10)throw new ArgumentException("Gönderim kuyruğu dolu. Bir süre sonra tekrar deneyin.");var j=new SmsJob{DedupeKey=dedupeKey,Id=Guid.NewGuid().ToString("N"),ProviderId=p.Id,Total=recipients.Length,Payload=protector.Protect(JsonSerializer.Serialize(new Payload(recipients,message)))};rows.Add(j);Persist(rows.TakeLast(100).ToList());return j.Id;}}
    public object Status(){lock(gate)return Load().TakeLast(10).Reverse().Select(x=>new{x.Id,x.Total,x.Sent,x.Failed,x.State,x.Created}).ToArray();}
    protected override async Task ExecuteAsync(CancellationToken ct){while(!ct.IsCancellationRequested){try{await Process(ct);}catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}catch{ /* No payloads or secrets in operational logs. */ }try{await Task.Delay(2000,ct);}catch(OperationCanceledException){break;}}}
    async Task Process(CancellationToken ct){SmsJob? job;Payload? payload;int index;lock(gate){var rows=Load();var reservedRows=rows.Where(x=>x.State=="pending"&&x.Cursor>=x.Total).ToArray();foreach(var reserved in reservedRows){reserved.State="completed-with-errors";reserved.Payload="";}if(reservedRows.Length>0)Persist(rows);job=rows.FirstOrDefault(x=>x.State=="pending"&&x.Cursor<x.Total);if(job==null)return;var active=settings.ActiveProfile();if(active==null)return;if(active.Id!=job.ProviderId){job.State="cancelled";job.Payload="";Persist(rows);return;}payload=JsonSerializer.Deserialize<Payload>(protector.Unprotect(job.Payload));if(payload==null){job.State="failed";Persist(rows);return;}index=job.Cursor++;job.Failed++;Persist(rows);}
        // Reserve before sending; after a crash an uncertain message is never automatically sent twice.
        var accepted=false;try{await gateway.Send(payload.Phones[index],payload.Message,ct);accepted=true;}catch(InvalidOperationException){}lock(gate){var rows=Load();var current=rows.First(x=>x.Id==job.Id);if(accepted){current.Sent++;current.Failed--;}if(current.Cursor>=current.Total){current.State=current.Failed==0?"completed":"completed-with-errors";current.Payload="";}Persist(rows);}}
}
