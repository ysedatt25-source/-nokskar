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
        Console.WriteLine("Warranty document and manufacturing date checks: PASS (no live records)");
    }
}
