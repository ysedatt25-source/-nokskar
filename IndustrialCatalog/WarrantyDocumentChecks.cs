using System.Text.Json.Nodes;

public static class WarrantyDocumentChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("Warranty document check failed: " + message); }
    public static void Run()
    {
        var input = new JsonObject { ["productMode"]="manual", ["productName"]="Endüstriyel Pişirme Ünitesi", ["businessName"]="Örnek İşletme", ["serialNumber"]="DEMO-001", ["manufacturingDate"]="2026-09-15", ["deliveryDate"]="2026-10-11", ["warrantyMonths"]=24, ["components"]=new JsonArray(new JsonObject { ["name"]="Gövde", ["covered"]=true, ["months"]=24 }) };
        var catalog = new JsonObject { ["products"]=new JsonArray() };
        var record = WarrantyRules.Prepare(input,catalog,null,out _);
        Check(record["manufacturingDate"]?.ToString()=="2026-09-15", "manufacturing date preserved");
        input.Remove("manufacturingDate");
        var updated=WarrantyRules.Prepare(input,catalog,record,out _);
        Check(updated["manufacturingDate"]?.ToString()=="2026-09-15", "older clients do not erase manufacturing date");
        input["manufacturingDate"]="2026-11-01";
        var rejected=false;try{WarrantyRules.Prepare(input,catalog,record,out _);}catch(ArgumentException){rejected=true;}
        Check(rejected,"manufacturing after delivery rejected");
        input["manufacturingDate"]="2026-02-30";
        rejected=false;try{WarrantyRules.Prepare(input,catalog,record,out _);}catch(ArgumentException){rejected=true;}
        Check(rejected,"invalid calendar date rejected");
        input["manufacturingDate"]="";
        var old=WarrantyRules.Prepare(input,catalog,null,out _);
        var oldHtml=WarrantyPages.Certificate(old);
        Check(!oldHtml.Contains("data-print-kind=\"thermal\" disabled")&&oldHtml.Contains("İmalat tarihi kayıtlı değil"),"missing manufacturing date is disclosed without inventing a date");
        record["productName"]="<script>unsafe()</script> @@CODE@@";
        var html=WarrantyPages.Certificate(record);
        Check(!html.Contains("<script>unsafe()")&&html.Contains("&lt;script&gt;"),"record fields HTML encoded");
        Check(html.Contains("@@CODE@@"),"field data is not expanded as another template token");
        Check(html.Contains("id=\"certificate-pdf-panel\" hidden")&&html.Contains("id=\"certificate-thermal-panel\" hidden"),"print panels initially closed");
        Check(html.Contains("id=\"certificate-thermal-wide-panel\" hidden"),"second thermal panel initially closed");
        Check(html.IndexOf("id=\"warranty-document\"",StringComparison.Ordinal)>html.IndexOf("id=\"certificate-pdf-panel\" hidden",StringComparison.Ordinal),"certificate belongs to the closed PDF panel");
        Check(!html.Contains("onclick=\"window.print()\""),"printing uses an isolated document");
        Check(html.Contains("data-serial=\"DEMO-001\"")&&html.Contains("data-warranty-qr"),"QR has the record serial and its own preview");
        Check(html.IndexOf("/warranty-print-vendor.js",StringComparison.Ordinal)<html.IndexOf("/warranty-document.js",StringComparison.Ordinal),"PDF and QR tools load before print controls");
        var technical=new JsonObject{["mode"]="both",["voltage"]="230",["frequency"]="50",["current"]="4.2",["totalPower"]="1800",["compressor"]="Danfoss SC12",["compressorPower"]="320",["gas"]="R290",["gasAmount"]="120",["heaterPower"]="1500",["motorPower"]="25",["ceMark"]=true,["ceNumber"]="CE-2026-001",["ceDocument"]="/api/files/"+new string('a',32)+".pdf"};
        input["technicalLabel"]=technical;
        var equipped=WarrantyRules.Prepare(input,catalog,null,out _);
        Check(equipped["technicalLabel"]?["current"]?.ToString()=="4,2","decimal units normalized");
        var equippedHtml=WarrantyPages.Certificate(equipped);
        Check(System.Net.WebUtility.HtmlDecode(equippedHtml).Contains("Gaz: R290 · 120 g")&&System.Net.WebUtility.HtmlDecode(equippedHtml).Contains("Isıtıcı: 1500 W"),"cooling and heating reach label");
        Check(equippedHtml.Contains("/ce-mark.svg")&&equippedHtml.Contains("CE belge: CE-2026-001"),"optional CE mark and number reach label");
        Check(!equippedHtml.Contains("@@TECHNICAL@@")&&!equippedHtml.Contains("@@LABEL_CLASS@@"),"label tokens resolved");
        input.Remove("technicalLabel");var preserved=WarrantyRules.Prepare(input,catalog,equipped,out _);
        Check(preserved["technicalLabel"]?["gasAmount"]?.ToString()=="120","older clients preserve technical snapshot");
        technical["mode"]="heating";Check(!TechnicalLabel.Lines(technical).Any(x=>x.StartsWith("Gaz:",StringComparison.Ordinal)),"unselected cooling does not print");
        foreach(var invalid in new[]{"javascript:alert(1)","https://example.com/private.pdf","/api/files/"+new string('a',32)+".mp4"}){technical["ceDocument"]=invalid;var bad=false;try{TechnicalLabel.Normalize(technical);}catch(ArgumentException){bad=true;}Check(bad,"invalid CE link rejected");}
        technical["ceDocument"]="";technical["current"]="-1";var invalidPower=false;try{TechnicalLabel.Normalize(technical);}catch(ArgumentException){invalidPower=true;}Check(invalidPower,"negative technical current rejected");
        var catalogProduct=new JsonObject{["id"]="cooler",["name"]="Soğutucu",["code"]="S-01",["technicalLabel"]=equipped["technicalLabel"]!.DeepClone()};
        catalog["products"]!.AsArray().Add(catalogProduct);input["productMode"]="catalog";input["productId"]="cooler";
        var inherited=WarrantyRules.Prepare(input,catalog,null,out _);
        Check(inherited["technicalLabel"]?["gasAmount"]?.ToString()=="120","product technical defaults inherited");
        catalogProduct["technicalLabel"]!["gasAmount"]="150";
        var fixedSnapshot=WarrantyRules.Prepare(input,catalog,inherited,out _);
        Check(fixedSnapshot["technicalLabel"]?["gasAmount"]?.ToString()=="120","catalog changes do not alter existing device snapshot");
        var legacyLabel=TechnicalLabel.Normalize(null);Check(legacyLabel["mode"]!.ToString()=="none"&&!legacyLabel["ceMark"]!.GetValue<bool>(),"old products do not acquire technical values or CE mark");
        Check(html.Contains("data-booklet-page=\"1\"")&&html.Contains("data-booklet-page=\"2\"")&&html.Contains("data-booklet-page=\"3\"")&&html.Contains("data-booklet-page=\"4\""),"four booklet leaves rendered");
        Check(html.Contains("data-booklet-mode=\"cooling both\"")&&html.Contains("data-booklet-mode=\"heating both\""),"separate heating and cooling text profiles");
        Check(html.Contains("kapsamı, bileşen tablosuyla daraltılamaz")||html.Contains("kapsam, bileşen tablosuyla daraltılamaz"),"mandatory consumer coverage not waived");
        Console.WriteLine("Warranty document and manufacturing date checks: PASS (no live records)");
    }
}
