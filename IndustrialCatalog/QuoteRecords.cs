using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Globalization;

public sealed class QuoteConflictException:Exception{public QuoteConflictException():base("Teklif başka bir oturumda değişti. Listeyi yenileyip tekrar açın."){} }
public static class QuoteRules
{
 public static string Text(JsonObject b,string key,int max,bool required=false){var s=b[key]?.ToString().Trim()??"";if(s.Length>max||required&&s.Length==0)throw new ArgumentException(key+": bilgi eksik veya çok uzun.");return s;}
 public static decimal Number(JsonObject b,string key,decimal max,decimal fallback=0){if(b[key]==null)return fallback;if(b[key] is not JsonValue v||!v.TryGetValue<decimal>(out var n)||n<0||n>max)throw new ArgumentException(key+": geçerli bir sayı girin.");return n;}
 public static bool UploadedImage(string s)=>Regex.IsMatch(s,@"^/api/files/[a-f0-9]{32}\.(jpg|png|webp)$");
 public static decimal Round(decimal n)=>decimal.Round(n,2,MidpointRounding.AwayFromZero);
 public static bool SafeImage(string s)=>s==""||!s.Contains("..")&&(Regex.IsMatch(s,@"^/[A-Za-z0-9/_?.=&-]+$")&&!s.StartsWith("//")||Uri.TryCreate(s,UriKind.Absolute,out var u)&&u.Scheme is "https" or "http");
 public static JsonObject Normalize(JsonObject input,JsonObject catalog,JsonObject? prior=null){
  var q=new JsonObject();foreach(var key in new[]{"businessName","customerName","email","phone","address","taxInfo","payment","delivery","shipping","installation","notes","inquiryId"})q[key]=Text(input,key,key is "notes"?2000:key is "address"?600:300);
  if(q["businessName"]!.ToString()==""&&q["customerName"]!.ToString()=="")throw new ArgumentException("Müşteri veya firma adı gerekli.");
  foreach(var key in new[]{"date","validUntil"}){var s=Text(input,key,10,true);if(!DateOnly.TryParseExact(s,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw new ArgumentException("Teklif tarihlerini kontrol edin.");q[key]=s;}
  if(string.CompareOrdinal(q["validUntil"]!.ToString(),q["date"]!.ToString())<0)throw new ArgumentException("Geçerlilik tarihi teklif tarihinden önce olamaz.");
  var status=Text(input,"status",20);if(!new[]{"draft","sent","approved","declined"}.Contains(status))throw new ArgumentException("Teklif durumu geçersiz.");q["status"]=status;
  q["includeImages"]=input["includeImages"] is JsonValue bv&&bv.TryGetValue<bool>(out var photos)?photos:true;
  foreach(var key in new[]{"shipping","installation"}){
   var flag=key+"Included";var legacy=q[key]!.ToString().Trim().ToLowerInvariant();
   q[flag]=input[flag] is JsonValue iv&&iv.TryGetValue<bool>(out var included)?included:legacy is "dahil" or "dâhil";
  }
  if(input["items"] is not JsonArray items||items.Count is <1 or >100)throw new ArgumentException("Teklife 1–100 ürün ekleyin.");
  var normalized=new JsonArray();decimal list=0,discount=0,subtotal=0,tax=0;var ids=new HashSet<string>();
  foreach(var row in items){if(row is not JsonObject b)throw new ArgumentException("Ürün satırı geçersiz.");var id=Text(b,"id",32,true);if(!Regex.IsMatch(id,"^[a-f0-9]{32}$")||!ids.Add(id))throw new ArgumentException("Ürün satırı kimliği geçersiz.");
   var productId=Text(b,"productId",100);var p=(catalog["products"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(x=>x["id"]?.ToString()==productId);
   var before=(prior?["items"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(x=>x["id"]?.ToString()==id);
   var gallery=(p?["images"] as JsonArray??before?["gallery"] as JsonArray??new JsonArray()).Select(x=>x?.ToString()??"").Where(SafeImage).Where(x=>x!="").Distinct().Take(50).ToArray();
   var image=Text(b,"image",2000);if(image!=""&&(!SafeImage(image)||!gallery.Contains(image)&&before?["image"]?.ToString()!=image&&!UploadedImage(image)))throw new ArgumentException("Teklif fotoğrafını galeriden seçin veya cihazınızdan yükleyin.");
   gallery=gallery.Concat((before?["gallery"] as JsonArray??new JsonArray()).Select(x=>x?.ToString()??"").Where(UploadedImage)).Concat(UploadedImage(image)?new[]{image}:Array.Empty<string>()).Distinct().Take(100).ToArray();
   var item=new JsonObject{["id"]=id,["productId"]=productId,["name"]=Text(b,"name",200,true),["code"]=Text(b,"code",100),["description"]=Text(b,"description",500),["image"]=image,["gallery"]=new JsonArray(gallery.Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray())};
   var diameter=Text(b,"diameter",12);if(diameter!=""&&(!Regex.IsMatch(diameter,@"^\d+(?:[.,]\d+)?$")||!decimal.TryParse(diameter.Replace(',','.'),System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out var diameterValue)||diameterValue<=0))throw new ArgumentException("Çap (Ø) için santimetre cinsinden sıfırdan büyük bir sayı girin.");item["diameter"]=diameter;
   var unit=Text(b,"unit",10);if(!new[]{"adet","metre","m²","Çap (Ø)"}.Contains(unit))throw new ArgumentException("Birim geçersiz.");item["unit"]=unit;
   var quantity=Number(b,"quantity",100000);if(quantity<=0)throw new ArgumentException("Miktar sıfırdan büyük olmalı.");quantity=decimal.Round(quantity,3,MidpointRounding.AwayFromZero);if(quantity<=0)throw new ArgumentException("Miktar en az 0,001 olmalı.");
   var price=Round(Number(b,"unitPrice",100000000));var off=Number(b,"discount",100);var vat=Number(b,"vatRate",100,20);
   var raw=Round(quantity*price);var cut=Round(raw*off/100);var net=raw-cut;var taxValue=Round(net*vat/100);
   item["quantity"]=quantity;item["unitPrice"]=price;item["discount"]=off;item["vatRate"]=vat;item["net"]=net;item["tax"]=taxValue;item["total"]=net+taxValue;normalized.Add(item);list+=raw;discount+=cut;subtotal+=net;tax+=taxValue;
  }
  q["items"]=normalized;q["listTotal"]=list;q["discountTotal"]=discount;q["subtotal"]=subtotal;q["taxTotal"]=tax;q["total"]=subtotal+tax;return q;
 }
 public static void ValidateStored(JsonArray rows){if(rows.Count>1000)throw new ArgumentException("Teklif yedeği çok büyük.");var ids=new HashSet<string>();foreach(var row in rows.OfType<JsonObject>()){var id=Text(row,"id",32,true);if(!Regex.IsMatch(id,"^[a-f0-9]{32}$")||!ids.Add(id))throw new ArgumentException("Teklif yedeği geçersiz.");_ = Normalize(row,new JsonObject(),row);}if(rows.Any(x=>x is not JsonObject))throw new ArgumentException("Teklif yedeği geçersiz.");}
}
public sealed partial class Store
{
 public JsonArray QuoteList(){lock(gate)return new JsonArray((state["quotes"] as JsonArray??new JsonArray()).Reverse().Select(x=>x!.DeepClone()).ToArray());}
 public JsonObject? QuoteById(string id){lock(gate)return (state["quotes"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(x=>x["id"]?.ToString()==id)?.DeepClone().AsObject();}
 public JsonObject SaveQuote(JsonObject input,string actor){lock(gate){
  var id=input["id"]?.ToString()??"";var previous=(state["quotes"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(x=>x["id"]?.ToString()==id);
  if(id!=""&&previous==null)throw new ArgumentException("Teklif bulunamadı. Yeni teklif için kopyalama seçeneğini kullanın.");
  var version=previous?["version"]?.GetValue<int>()??0;if(previous!=null&&(input["version"]?.GetValue<int>()??-1)!=version)throw new QuoteConflictException();
  var quote=QuoteRules.Normalize(input,state["data"]!.AsObject(),previous);var next=state.DeepClone().AsObject();next["quotes"]??=new JsonArray();var rows=next["quotes"]!.AsArray();
  if(previous==null){if(rows.Count>=1000)throw new ArgumentException("En fazla 1000 teklif saklanabilir.");id=Guid.NewGuid().ToString("N");var seq=(next["quoteSequence"]?.GetValue<int>()??0)+1;next["quoteSequence"]=seq;quote["number"]="TKL-"+DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy")+"-"+seq.ToString("00000");quote["created"]=DateTimeOffset.UtcNow.ToString("O");}
  else{quote["number"]=previous["number"]!.DeepClone();quote["created"]=previous["created"]!.DeepClone();}
  quote["id"]=id;quote["version"]=version+1;quote["updated"]=DateTimeOffset.UtcNow.ToString("O");quote["updatedBy"]=actor;
  var index=rows.ToList().FindIndex(x=>x?["id"]?.ToString()==id);if(index<0)rows.Add(quote);else rows[index]=quote;
  AddAudit(next,previous==null?"Fiyat teklifi oluşturuldu":"Fiyat teklifi güncellendi",id,quote["number"]!.ToString(),actor);Persist(next);return quote.DeepClone().AsObject();
 }}
}
