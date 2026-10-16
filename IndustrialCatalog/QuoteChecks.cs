using System.Text.Json.Nodes;
using System.Security.Claims;
public static class QuoteChecks
{
 static void Check(bool value,string reason){if(!value)throw new Exception("Quote checks: "+reason);}
 static void Reject(Action action){try{action();throw new Exception("Invalid quote accepted");}catch(ArgumentException){}}
 public static void Run(){var root=Path.Combine(Path.GetTempPath(),"inokskar-quotes-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
  var b=WebApplication.CreateBuilder();b.Environment.ContentRootPath=AppContext.BaseDirectory;b.Configuration["Storage:Path"]=root;
  var store=new Store(b.Environment,b.Configuration);var catalog=store.Snapshot()["data"]!.DeepClone().AsObject();var product=catalog["products"]![0]!.AsObject();var first="/api/files/"+new string('a',32)+".jpg";var second="/api/files/"+new string('b',32)+".png";product["images"]=new JsonArray(first,second);product["tl"]=100m;product["euro"]=0;
  store.Save(new JsonObject{["revision"]=0,["data"]=JsonNode.Parse(catalog.ToJsonString()),["label"]="Isolated quote test"});
  var input=new JsonObject{["date"]="2026-10-16",["validUntil"]="2026-10-31",["status"]="draft",["includeImages"]=true,["businessName"]="Test müşteri",["items"]=new JsonArray(new JsonObject{["id"]=new string('c',32),["productId"]=product["id"]!.ToString(),["name"]="Test ürün",["code"]="T1",["image"]=first,["unit"]="metre",["quantity"]=2.5m,["unitPrice"]=100m,["discount"]=10m,["vatRate"]=20m})};
  var q=store.SaveQuote(input,"Isolated QA");Check(q["subtotal"]!.GetValue<decimal>()==225m&&q["taxTotal"]!.GetValue<decimal>()==45m&&q["total"]!.GetValue<decimal>()==270m,"discount/VAT/quantity totals");Check(q["items"]![0]!["image"]!.ToString()==first,"first catalog image retained");
  q["items"]![0]!["image"]=second;q["includeImages"]=false;var updated=store.SaveQuote(q,"Isolated QA");Check(updated["version"]!.GetValue<int>()==2&&updated["items"]![0]!["image"]!.ToString()==second&&!updated["includeImages"]!.GetValue<bool>(),"gallery replacement and no-photo layout");
  try{store.SaveQuote(q,"Isolated QA");throw new Exception("Concurrent overwrite accepted");}catch(QuoteConflictException){}
  var bad=updated.DeepClone().AsObject();bad["items"]![0]!["image"]="https://example.invalid/unrelated.jpg";Reject(()=>store.SaveQuote(bad,"QA"));bad=updated.DeepClone().AsObject();bad["items"]![0]!["quantity"]=0;Reject(()=>store.SaveQuote(bad,"QA"));
  var state=store.Snapshot();var changed=state["data"]!.DeepClone().AsObject();changed["products"]![0]!["tl"]=999m;store.Save(new JsonObject{["revision"]=state["revision"]!.DeepClone(),["data"]=JsonNode.Parse(changed.ToJsonString()),["label"]="Price changed"});Check(store.QuoteById(q["id"]!.ToString())!["total"]!.GetValue<decimal>()==270m,"catalog change cannot alter saved quote prices");
  var reload=new Store(b.Environment,b.Configuration);Check(reload.QuoteList().Count==1,"quote persists across restart");
  Directory.CreateDirectory(Path.Combine(root,"uploads"));File.WriteAllBytes(Path.Combine(root,"uploads",Path.GetFileName(first)),new byte[]{255,216,255,217});File.WriteAllBytes(Path.Combine(root,"uploads",Path.GetFileName(second)),new byte[]{1,2,3});
  var backup=CatalogBackup.Read(new MemoryStream(CatalogBackup.Create(store.Snapshot(),root)));Check(backup.Operations?["quotes"]?.AsArray().Count==1,"quote included in ZIP backup");
  var web=Path.Combine(root,"webroot");Directory.CreateDirectory(web);File.WriteAllText(Path.Combine(web,"inokskar-logo.svg"),"<svg xmlns='http://www.w3.org/2000/svg'/>");var settings=new JsonObject{["headerImage"]=first};Check(BrandAssets.Local(settings,root,web).file.EndsWith(Path.GetFileName(first)),"configured JPG shared by all brand aliases");settings["headerImage"]=second;Check(BrandAssets.Local(settings,root,web).mime=="image/png","updated brand asset and MIME immediately selected");settings["headerImage"]="/../secret.png";Check(BrandAssets.Local(settings,root,web).mime=="image/svg+xml","invalid brand path uses safe default");
  Check(!QuotePages.Can(new ClaimsPrincipal(new ClaimsIdentity()),"view"),"anonymous quote access denied");
  var pricesOnly=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("inokskar:permission","prices.view")},"test"));Check(!QuotePages.Can(pricesOnly,"view"),"price permission alone cannot expose customer quotes");
  var sales=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("inokskar:permission","prices.view"),new Claim("inokskar:permission","inquiries.view")},"test"));Check(QuotePages.Can(sales,"view")&&!QuotePages.Can(sales,"edit"),"sales read-only access separated from editing");
  QuoteRules.ValidateStored(store.QuoteList());Console.WriteLine("Quote and brand checks: PASS (first/gallery image, photo modes, decimal totals, saved price snapshot, persistence, concurrency, backup, permissions, central logo replacement; no live data).");
 }finally{Directory.Delete(root,true);}}
}
