using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public sealed class InquirySmsNotifier(Store store,SmsSettingsStore settings,SmsQueue queue,IConfiguration config,ILogger<InquirySmsNotifier> log)
{
    public static string Short(string? text,int length){var clean=Regex.Replace(text??"",@"\s+"," ").Trim();var parts=StringInfo.ParseCombiningCharacters(clean);return parts.Length<=length?clean:clean[..parts[length]]+"…";}
    public static string Message(JsonObject data,string link,bool test=false){
        var topic=data["type"]?.ToString()=="service"?"Teknik servis":data["purpose"]?.ToString() switch{"teklif"=>"Teklif","proje"=>"Proje ve danışmanlık",_=>"Destek"};
        var description=Short(data["message"]?.ToString(),100);
        return "İNOKSKAR · "+(test?"Test bildirimi":"Yeni talep")+"\nKonu: "+topic+"\nAçıklama: "+description+"\nKontrol: "+link;
    }
    public void Notify(JsonObject row){
        var id=row["id"]?.ToString()??"";
        try{
            if(!settings.Active)return;
            var site=store.Snapshot()["data"]?["settings"];
            if(site?["notificationSmsEnabled"]?.GetValue<bool>()==false)return;
            var origin=SeoPages.Origin(config)??throw new ArgumentException("Genel site adresi tanımlanmalı.");
            var phone=SmsPhone.Normalize(site?["notificationPhone"]?.ToString()??"05426156525");
            var data=JsonNode.Parse(row["data"]?.ToString()??"{}")!.AsObject();
            var job=queue.Enqueue(new[]{phone},Message(data,origin+"/admin/inquiries/"+Uri.EscapeDataString(id)),"inquiry:"+id);
            store.RecordAudit("Telefon bildirimi kuyruğa alındı",id,job,"Sistem");
        }catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException or IOException){
            // A saved request remains successful if its notification cannot be queued.
            log.LogWarning("Talep telefon bildirimi kuyruğa alınamadı: {InquiryId}",id);
            try{store.RecordAudit("Telefon bildirimi kuyruğa alınamadı",id,"Sağlayıcı, bildirim numarası ve site adresini kontrol edin.","Sistem");}catch(IOException){log.LogWarning("Bildirim hata kaydı yazılamadı: {InquiryId}",id);}
        }
    }
}
