using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
public static class CatalogRules
{
    public const decimal PriceChangeThreshold = 500m;
    public static decimal Price(decimal raw, decimal previous=0) => raw<=0?0:previous<=0?decimal.Round(raw,0,MidpointRounding.AwayFromZero):Math.Abs(raw-previous)>=PriceChangeThreshold?decimal.Round(raw,0,MidpointRounding.AwayFromZero):previous;
    static string Text(JsonNode? n,string key) => n?[key]?.GetValue<string>() ?? throw new ArgumentException(key+" metni gerekli.");
    static bool Safe(string v) => v=="" || (v.StartsWith('/')&&!v.StartsWith("//")&&!v.Contains('\\')) || Uri.TryCreate(v,UriKind.Absolute,out var uri)&&uri.Scheme is "https" or "http";
    public static void Validate(JsonObject d)
    {
        var settings=d["settings"]?.AsObject() ?? throw new ArgumentException("Ayarlar gerekli.");
        settings["headerImage"] ??= "/inokskar-header-brand.png";
        foreach(var key in new[]{"name","tagline","heroTitle","heroText","hero","headerImage","phone","whatsapp","email","address","hours","social","waMessage","lastRate","rateSource","rateDate","about"}) Text(settings,key);
        var siteName=Text(settings,"name").Trim();if(siteName.Length<2||!Regex.IsMatch(siteName,@"[\\p{L}\\p{N}]"))siteName="İNOKSKAR";settings["name"]=siteName;
        foreach(var key in new[]{"hero","headerImage","social"})if(!Safe(Text(settings,key)))throw new ArgumentException("Geçersiz bağlantı.");
        var rate=settings["rate"]!.GetValue<decimal>();if(rate<0)throw new ArgumentException("Kur negatif olamaz.");
        foreach(var key in new[]{"waEnabled","autoRate"})settings[key]!.GetValue<bool>();
        var wa=Text(settings,"whatsapp");if(wa!=""&&(!Regex.IsMatch(wa,@"^\+?[\d\s()-]+$")||Regex.Replace(wa,@"\D","").Length is <8 or >15))throw new ArgumentException("WhatsApp numarası geçersiz.");
        var email=Text(settings,"email");if(email!=""&&!System.Net.Mail.MailAddress.TryCreate(email,out _))throw new ArgumentException("E-posta geçersiz.");
        foreach(var m in settings["menu"]!.AsArray())if(string.IsNullOrWhiteSpace(Text(m,"name"))||!Safe(Text(m,"url")))throw new ArgumentException("Menü bağlantısı geçersiz.");
        d.Remove("posts");var cats=d["categories"]!.AsArray();var products=d["products"]!.AsArray();
        foreach(var list in new[]{cats,products})
        {var ids=new HashSet<string>();foreach(var x in list){var id=Text(x,"id");if(!Regex.IsMatch(id,@"^[a-zA-Z0-9_-]+$")||!ids.Add(id))throw new ArgumentException("Kayıt kimliği geçersiz veya tekrarlı.");x!["visible"]!.GetValue<bool>();}}
        var map=cats.ToDictionary(c=>Text(c,"id"));
        foreach(var c in cats)
        {if(string.IsNullOrWhiteSpace(Text(c,"name")))throw new ArgumentException("Kategori adı gerekli.");Text(c,"description");var cSeoTitle=c!["seoTitle"]?.ToString()??"";var cSeoDescription=c["seoDescription"]?.ToString()??"";c["seoTitle"]=cSeoTitle;c["seoDescription"]=cSeoDescription;if(cSeoTitle.Length>70||cSeoDescription.Length>180)throw new ArgumentException("Kategori SEO alanları çok uzun.");if(!Safe(Text(c,"image")))throw new ArgumentException("Kategori görseli geçersiz.");c!["menu"]!.GetValue<bool>();var seen=new HashSet<string>{Text(c,"id")};var parent=Text(c,"parent");while(parent!=""){if(!seen.Add(parent)||!map.ContainsKey(parent))throw new ArgumentException("Kategori ağacı geçersiz.");parent=Text(map[parent],"parent");}}
        var codes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var p in products)
        {
            if(string.IsNullOrWhiteSpace(Text(p,"name"))||!map.ContainsKey(Text(p,"category")))throw new ArgumentException("Ürün adı veya kategorisi geçersiz.");
            var code=p!["code"]?.GetValue<string>()?.Trim()??"";p["code"]=code;if(code.Length>100||!codes.Add(code==""?Text(p,"id"):code))throw new ArgumentException("Ürün kodu geçersiz veya başka üründe kullanılıyor.");
            var vat=p["vatRate"]?.GetValue<decimal>()??20m;p["vatRate"]=vat;if(p["euro"]!.GetValue<decimal>()<0||p["tl"]!.GetValue<decimal>()<0||p["tl"]!.GetValue<decimal>()!=decimal.Truncate(p["tl"]!.GetValue<decimal>())||vat<0||vat>100)throw new ArgumentException("Fiyat veya KDV oranı geçersiz.");
            if(!new[]{"Stokta","Sipariş üzerine","Bilgi alınız"}.Contains(Text(p,"status")))throw new ArgumentException("Ürün durumu geçersiz.");
            Text(p,"description");Text(p,"after");var pSeoTitle=p["seoTitle"]?.ToString()??"";var pSeoDescription=p["seoDescription"]?.ToString()??"";p["seoTitle"]=pSeoTitle;p["seoDescription"]=pSeoDescription;if(pSeoTitle.Length>70||pSeoDescription.Length>180)throw new ArgumentException("Ürün SEO alanları çok uzun.");p["demo"]!.GetValue<bool>();
            foreach(var spec in p["specs"]!.AsArray()){Text(spec,"name");Text(spec,"value");}
            foreach(var image in p["images"]!.AsArray())if(!Safe(image!.GetValue<string>()))throw new ArgumentException("Ürün görseli geçersiz.");
            if(!Safe(Text(p,"pdf")))throw new ArgumentException("PDF bağlantısı geçersiz.");
        }
    }
    public static JsonObject Public(JsonObject source)
    {
        var d=source.DeepClone().AsObject();var publicSettings=d["settings"]!.AsObject();var publicName=(publicSettings["name"]?.ToString()??"").Trim();if(publicName.Length<2||!Regex.IsMatch(publicName,@"[\\p{L}\\p{N}]"))publicSettings["name"]="İNOKSKAR";var cats=d["categories"]!.AsArray();var map=cats.ToDictionary(c=>c!["id"]!.ToString());
        bool Visible(JsonNode c){var seen=new HashSet<string>();while(true){if(c["visible"]?.GetValue<bool>()!=true||!seen.Add(c["id"]!.ToString()))return false;var parent=c["parent"]!.ToString();if(parent=="")return true;if(!map.TryGetValue(parent,out var next)||next==null)return false;c=next;}}
        var allowed=cats.Where(c=>c!=null&&Visible(c)).Select(c=>c!["id"]!.ToString()).ToHashSet();
        d["categories"]=new JsonArray(cats.Where(c=>allowed.Contains(c!["id"]!.ToString())).Select(c=>c!.DeepClone()).ToArray());
        d["products"]=new JsonArray(d["products"]!.AsArray().Where(p=>p!["visible"]!.GetValue<bool>()&&allowed.Contains(p["category"]!.ToString())).Select(p=>{var x=p!.DeepClone().AsObject();x.Remove("euro");return (JsonNode)x;}).ToArray());
        d.Remove("posts");d["settings"]!.AsObject().Remove("logo");d["settings"]!["rate"]=0;d["settings"]!["autoRate"]=false;d["settings"]!["lastRate"]="";d["settings"]!["rateSource"]="";d["settings"]!["rateDate"]="";return d;
    }
}
