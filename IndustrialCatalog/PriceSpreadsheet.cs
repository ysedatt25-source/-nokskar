using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

public sealed record PriceImportRow(string Code,string Name,decimal CurrentEuro,decimal NewEuro,decimal CurrentVat,decimal NewVat,decimal NewTl,string Error);

public static class PriceSpreadsheet
{
    const long MaxWorkbookBytes = 5L * 1024 * 1024;
    const int MaxRows = 10000;
    static readonly string[] CodeHeaders = ["urunkodu","ürünkodu","kod","productcode","sku"];
    static readonly string[] EuroHeaders = ["eurofiyat","eurofiyatı","euro","eur","priceeur"];
    static readonly string[] VatHeaders = ["kdvorani","kdvoranı","kdv","vatrate","vat"];

    public static byte[] CreateTemplate(JsonObject data)
    {
        var products = data["products"]!.AsArray();
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            Write(zip,"[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""");
            Write(zip,"_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Write(zip,"xl/workbook.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Fiyatlar" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Write(zip,"xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""");

            XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sheetData=new XElement(ns+"sheetData");
            var header=new[]{"UrunKodu","UrunAdi","EuroFiyat","KdvOrani"};
            sheetData.Add(Row(ns,1,header.Select((v,i)=>TextCell(ns,CellRef(i+1,1),v))));
            var rowIndex=2;
            foreach(var p in products)
            {
                var code=(p!["code"]?.ToString()??"").Trim();if(code=="")code=p["id"]!.ToString();
                var name=p["name"]!.ToString();
                var euro=p["euro"]?.GetValue<decimal>()??0;
                var vat=p["vatRate"]?.GetValue<decimal>()??20m;
                sheetData.Add(Row(ns,rowIndex,new[]{
                    TextCell(ns,CellRef(1,rowIndex),code),
                    TextCell(ns,CellRef(2,rowIndex),name),
                    NumberCell(ns,CellRef(3,rowIndex),euro),
                    NumberCell(ns,CellRef(4,rowIndex),vat)
                }));
                rowIndex++;
            }
            var worksheet=new XDocument(new XDeclaration("1.0","UTF-8","yes"),new XElement(ns+"worksheet",new XElement(ns+"cols",
                new XElement(ns+"col",new XAttribute("min",1),new XAttribute("max",1),new XAttribute("width",22),new XAttribute("customWidth",1)),
                new XElement(ns+"col",new XAttribute("min",2),new XAttribute("max",2),new XAttribute("width",42),new XAttribute("customWidth",1)),
                new XElement(ns+"col",new XAttribute("min",3),new XAttribute("max",4),new XAttribute("width",16),new XAttribute("customWidth",1))),sheetData));
            Write(zip,"xl/worksheets/sheet1.xml",worksheet.ToString(SaveOptions.DisableFormatting));
        }
        return output.ToArray();
    }

    public static IReadOnlyList<PriceImportRow> Preview(Stream stream,string fileName,JsonObject data)
    {
        using var ms=new MemoryStream();stream.CopyTo(ms);if(ms.Length is <=0 or >MaxWorkbookBytes)throw new ArgumentException("Fiyat dosyası boş veya 5 MB sınırını aşıyor.");
        var rows=fileName.EndsWith(".csv",StringComparison.OrdinalIgnoreCase)
            ? ReadCsv(Encoding.UTF8.GetString(ms.ToArray()))
            : fileName.EndsWith(".xlsx",StringComparison.OrdinalIgnoreCase)
                ? ReadXlsx(ms.ToArray())
                : throw new ArgumentException("Fiyat aktarımı için .xlsx veya .csv dosyası seçin.");
        return Match(rows,data);
    }

    public static JsonObject Apply(JsonObject data,IReadOnlyList<PriceImportRow> preview)
    {
        var errors=preview.Where(x=>!string.IsNullOrEmpty(x.Error)).ToArray();
        if(errors.Length>0)throw new ArgumentException($"Fiyat dosyasında {errors.Length} hatalı satır var. Önizlemedeki hataları düzeltmeden içe aktarılamaz.");
        var byCode=preview.ToDictionary(x=>x.Code,StringComparer.OrdinalIgnoreCase);
        foreach(var p in data["products"]!.AsArray())
        {
            var code=(p!["code"]?.ToString()??"").Trim();if(code=="")code=p["id"]!.ToString();
            if(!byCode.TryGetValue(code,out var row))continue;
            p["euro"]=row.NewEuro;p["vatRate"]=row.NewVat;
        }
        return data;
    }

    static IReadOnlyList<PriceImportRow> Match(List<string[]> rows,JsonObject data)
    {
        if(rows.Count<2)throw new ArgumentException("Fiyat dosyasında başlık ve en az bir ürün satırı olmalı.");
        if(rows.Count>MaxRows+1)throw new ArgumentException($"Fiyat dosyası en fazla {MaxRows} ürün satırı içerebilir.");
        var headers=rows[0].Select(NormalizeHeader).ToArray();
        int Find(string[] names){for(var i=0;i<headers.Length;i++)if(names.Contains(headers[i],StringComparer.Ordinal))return i;return -1;}
        var codeCol=Find(CodeHeaders);var euroCol=Find(EuroHeaders);var vatCol=Find(VatHeaders);
        if(codeCol<0||euroCol<0)throw new ArgumentException("Fiyat dosyasında UrunKodu ve EuroFiyat sütunları zorunludur. KdvOrani isteğe bağlıdır.");
        var products=data["products"]!.AsArray();
        var productMap=products.ToDictionary(p=>{var c=(p!["code"]?.ToString()??"").Trim();return c==""?p["id"]!.ToString():c;},StringComparer.OrdinalIgnoreCase);
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var result=new List<PriceImportRow>();
        foreach(var cells in rows.Skip(1))
        {
            string Get(int i)=>i>=0&&i<cells.Length?cells[i].Trim():"";
            var code=Get(codeCol);if(string.IsNullOrWhiteSpace(code)&&cells.All(string.IsNullOrWhiteSpace))continue;
            var error="";JsonNode? p=null;
            if(string.IsNullOrWhiteSpace(code))error="Ürün kodu boş.";
            else if(!seen.Add(code))error="Aynı ürün kodu dosyada birden fazla kez bulunuyor.";
            else if(!productMap.TryGetValue(code,out p))error="Bu ürün kodu katalogda bulunamadı.";
            var currentEuro=p?["euro"]?.GetValue<decimal>()??0;var currentVat=p?["vatRate"]?.GetValue<decimal>()??20m;
            var euro=currentEuro;var vat=currentVat;
            if(error==""&&!TryDecimal(Get(euroCol),out euro))error="Euro fiyatı geçersiz.";
            if(error==""&&(euro<0||euro>100000000))error="Euro fiyatı 0 ile 100.000.000 arasında olmalı.";
            var rawVat=Get(vatCol);
            if(error==""&&vatCol>=0&&!string.IsNullOrWhiteSpace(rawVat)&&!TryDecimal(rawVat,out vat))error="KDV oranı geçersiz.";
            if(error==""&&(vat<0||vat>100))error="KDV oranı 0 ile 100 arasında olmalı.";
            var rate=data["settings"]!["rate"]!.GetValue<decimal>();var currentTl=p?["tl"]?.GetValue<decimal>()??0;
            if(error==""&&euro>0&&rate<=0)error="Fiyat hesaplamak için geçerli EUR/TL kuru gerekli.";
            var newTl=euro>0&&rate>0?CatalogRules.Price(euro*rate,currentTl):0;
            result.Add(new PriceImportRow(code,p?["name"]?.ToString()??"",currentEuro,euro,currentVat,vat,newTl,error));
        }
        if(result.Count==0)throw new ArgumentException("Fiyat dosyasında işlenecek ürün satırı bulunamadı.");
        return result;
    }

    static List<string[]> ReadCsv(string text)
    {
        text=text.TrimStart('\uFEFF');var first=text.Split(new[]{"\r\n","\n"},StringSplitOptions.None).FirstOrDefault()??"";
        var sep=first.Count(c=>c==';')>=first.Count(c=>c=='\t')&&first.Count(c=>c==';')>=first.Count(c=>c==',')?';':first.Count(c=>c=='\t')>=first.Count(c=>c==',')?'\t':',';
        var rows=new List<string[]>();var row=new List<string>();var cell=new StringBuilder();var quoted=false;
        for(var i=0;i<text.Length;i++)
        {
            var ch=text[i];
            if(quoted){if(ch=='"'&&i+1<text.Length&&text[i+1]=='"'){cell.Append('"');i++;}else if(ch=='"')quoted=false;else cell.Append(ch);continue;}
            if(ch=='"'){quoted=true;continue;}if(ch==sep){row.Add(cell.ToString());cell.Clear();continue;}
            if(ch=='\r'||ch=='\n'){if(ch=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;row.Add(cell.ToString());cell.Clear();rows.Add(row.ToArray());row.Clear();continue;}cell.Append(ch);
        }
        if(cell.Length>0||row.Count>0){row.Add(cell.ToString());rows.Add(row.ToArray());}
        return rows.Where(r=>r.Any(x=>!string.IsNullOrWhiteSpace(x))).ToList();
    }

    static List<string[]> ReadXlsx(byte[] bytes)
    {
        using var ms=new MemoryStream(bytes);using var zip=new ZipArchive(ms,ZipArchiveMode.Read);
        if(zip.Entries.Count>200)throw new ArgumentException("Excel dosyasında beklenmeyen sayıda bölüm var.");
        var strings=new List<string>();var shared=zip.GetEntry("xl/sharedStrings.xml");
        if(shared!=null){if(shared.Length>MaxWorkbookBytes)throw new ArgumentException("Excel metin tablosu çok büyük.");using var s=shared.Open();var doc=XDocument.Load(s,LoadOptions.None);XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";strings=doc.Descendants(ns+"si").Select(si=>string.Concat(si.Descendants(ns+"t").Select(x=>x.Value))).ToList();}
        var sheet=zip.GetEntry("xl/worksheets/sheet1.xml")??zip.Entries.FirstOrDefault(e=>e.FullName.StartsWith("xl/worksheets/sheet",StringComparison.OrdinalIgnoreCase)&&e.FullName.EndsWith(".xml",StringComparison.OrdinalIgnoreCase))??throw new ArgumentException("Excel çalışma sayfası bulunamadı.");
        if(sheet.Length>MaxWorkbookBytes)throw new ArgumentException("Excel çalışma sayfası çok büyük.");using var stream=sheet.Open();var xdoc=XDocument.Load(stream,LoadOptions.None);XNamespace main="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows=new List<string[]>();
        foreach(var row in xdoc.Descendants(main+"row"))
        {
            var cells=new SortedDictionary<int,string>();
            foreach(var c in row.Elements(main+"c"))
            {
                var reference=(string?)c.Attribute("r")??"A1";var col=ColumnIndex(reference);var type=(string?)c.Attribute("t")??"";var value=c.Element(main+"v")?.Value??"";
                if(type=="s"&&int.TryParse(value,out var si)&&si>=0&&si<strings.Count)value=strings[si];else if(type=="inlineStr")value=string.Concat(c.Descendants(main+"t").Select(x=>x.Value));
                cells[col]=value;
            }
            if(cells.Count==0)continue;var max=cells.Keys.Max();var arr=new string[max+1];foreach(var kv in cells)arr[kv.Key]=kv.Value;rows.Add(arr);
        }
        return rows;
    }

    static bool TryDecimal(string raw,out decimal value)
    {
        raw=(raw??"").Trim().Replace(" ","");
        var comma=raw.LastIndexOf(',');var dot=raw.LastIndexOf('.');
        if(comma>=0&&dot<0)return decimal.TryParse(raw,NumberStyles.Number,CultureInfo.GetCultureInfo("tr-TR"),out value);
        if(dot>=0&&comma<0)return decimal.TryParse(raw,NumberStyles.Number,CultureInfo.InvariantCulture,out value);
        if(comma>=0&&dot>=0)
            return decimal.TryParse(raw,NumberStyles.Number,comma>dot?CultureInfo.GetCultureInfo("tr-TR"):CultureInfo.InvariantCulture,out value);
        return decimal.TryParse(raw,NumberStyles.Number,CultureInfo.InvariantCulture,out value);
    }
    static string NormalizeHeader(string value)=>new string(value.Trim().Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).Select(char.ToLowerInvariant).Where(char.IsLetterOrDigit).ToArray());
    static int ColumnIndex(string reference){var n=0;foreach(var ch in reference){if(!char.IsLetter(ch))break;n=n*26+(char.ToUpperInvariant(ch)-'A'+1);}return Math.Max(0,n-1);}
    static string CellRef(int column,int row){var name="";while(column>0){column--;name=(char)('A'+column%26)+name;column/=26;}return name+row;}
    static XElement Row(XNamespace ns,int index,IEnumerable<XElement> cells)=>new(ns+"row",new XAttribute("r",index),cells);
    static XElement TextCell(XNamespace ns,string reference,string value)=>new(ns+"c",new XAttribute("r",reference),new XAttribute("t","inlineStr"),new XElement(ns+"is",new XElement(ns+"t",value)));
    static XElement NumberCell(XNamespace ns,string reference,decimal value)=>new(ns+"c",new XAttribute("r",reference),new XElement(ns+"v",value.ToString(CultureInfo.InvariantCulture)));
    static void Write(ZipArchive zip,string path,string content){var e=zip.CreateEntry(path,CompressionLevel.Fastest);using var writer=new StreamWriter(e.Open(),new UTF8Encoding(false));writer.Write(content);}
}
