using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Net;

public static class TechnicalLabel
{
    public static readonly (string Key,string Label,string Unit,string Group)[] Fields={
        ("voltage","Gerilim","V","electric"),("frequency","Frekans","Hz","electric"),("current","Akım","A","electric"),("totalPower","Toplam güç","W","electric"),
        ("compressor","Kompresör modeli","","cooling"),("compressorPower","Kompresör gücü","W","cooling"),("gas","Gaz türü","","cooling"),("gasAmount","Gaz miktarı","g","cooling"),
        ("heaterPower","Isıtıcı gücü","W","heating"),("motor","Motor modeli","","motor"),("motorPower","Motor gücü","W","motor"),("ceNumber","CE belge numarası","","ce")};
    public static JsonObject Normalize(JsonNode? node)
    {
        if(node!=null&&node is not JsonObject)throw new ArgumentException("Etiket teknik bilgileri geçersiz.");
        var input=node as JsonObject??new JsonObject();var result=new JsonObject();
        var mode=input["mode"]?.ToString()??"none";
        if(!new[]{"none","cooling","heating","both"}.Contains(mode))throw new ArgumentException("Cihaz türü geçersiz.");
        result["mode"]=mode;
        foreach(var field in Fields)
        {
            var value=(input[field.Key]?.ToString()??"").Trim();
            if(value.Length>48||Regex.IsMatch(value,@"[\x00-\x1f\x7f]"))throw new ArgumentException(field.Label+" en fazla 48 karakter olmalı.");
            if(field.Unit!=""&&value!="")
            {
                if(!decimal.TryParse(value.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var number)||number<0.001m||number>1000000)throw new ArgumentException(field.Label+" için sıfırdan büyük bir sayı girin.");
                value=number.ToString("0.###",CultureInfo.InvariantCulture).Replace('.',',');
            }
            result[field.Key]=value;
        }
        var document=input["ceDocument"]?.ToString()??"";
        if(document!=""&&!Regex.IsMatch(document,@"^/api/files/[a-f0-9]{32}\.pdf$"))throw new ArgumentException("CE belgesi siteye yüklenmiş PDF olmalı.");
        result["ceDocument"]=document;
        result["ceMark"]=input["ceMark"]?.GetValue<bool>()??false;
        return result;
    }
    public static List<string> Lines(JsonNode? node)
    {
        var t=Normalize(node);var mode=t["mode"]!.ToString();var lines=new List<string>();
        string V(string k)=>t[k]?.ToString()??"";
        string Unit(string k,string unit)=>V(k)==""?"":V(k)+" "+unit;
        void Add(string label,params string[] parts){var text=string.Join(" · ",parts.Where(x=>x!=""));if(text!="")lines.Add(label+text);}
        if(mode!="none")
        {
            Add("",Unit("voltage","V"),Unit("frequency","Hz"),Unit("current","A"));Add("Güç: ",Unit("totalPower","W"));
            if(mode is "cooling" or "both"){Add("Komp.: ",V("compressor"),Unit("compressorPower","W"));Add("Gaz: ",V("gas"),Unit("gasAmount","g"));}
            if(mode is "heating" or "both")Add("Isıtıcı: ",Unit("heaterPower","W"));
            Add("Motor: ",V("motor"),Unit("motorPower","W"));
        }
        Add("CE belge: ",V("ceNumber"));
        return lines;
    }
    public static string Html(JsonNode? node)=>string.Join("",Lines(node).Select(x=>"<div>"+WebUtility.HtmlEncode(x)+"</div>"));
    public static string Form()
    {
        var inputs=string.Join("",Fields.Select(f=>$"<label data-technical-group=\"{f.Group}\">{f.Label}{(f.Unit!=""?" ("+f.Unit+")":"")}<input name=\"tl_{f.Key}\" maxlength=\"48\" {(f.Unit!=""?"inputmode=\"decimal\"":"")} placeholder=\"İsteğe bağlı\"></label>"));
        return "<details class=\"warranty-optional technical-label-editor\"><summary>Etiket teknik bilgileri · isteğe bağlı</summary><p>Üründen aktarılır; bu cihaz için değiştirilebilir. Boş alanlar etikette görünmez.</p><label>Cihaz türü<select name=\"tl_mode\"><option value=\"none\">Teknik bilgi yok</option><option value=\"cooling\">Soğutucu</option><option value=\"heating\">Isıtıcı</option><option value=\"both\">Soğutucu + Isıtıcı</option></select></label><div class=\"warranty-two-col\">"+inputs+"</div><label><input type=\"checkbox\" name=\"tl_ceMark\"> CE işaretini etikette göster</label><label>CE belgesi (PDF)<input type=\"file\" id=\"label-ce-file\" accept=\"application/pdf\"></label><input type=\"hidden\" name=\"tl_ceDocument\"><div id=\"label-ce-status\" role=\"status\"></div><button type=\"button\" class=\"small-btn\" id=\"label-ce-remove\">Belgeyi kaldır</button><p>Eklenen CE belgesi QR ile doğrulanan garanti sayfasında müşteriye gösterilir.</p><div id=\"label-live-preview\" class=\"label-live-preview\" aria-live=\"polite\"></div><button type=\"button\" class=\"small-btn\" id=\"label-preview-open\">80 / 100 mm etiketi önizle</button><button type=\"button\" class=\"small-btn\" id=\"label-use-default\">Üründeki bilgileri yeniden aktar</button></details>";
    }
}
