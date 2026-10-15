using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
public static class OperationsListsChecks
{
 static void Check(bool value,string message){if(!value)throw new Exception("Operations lists: "+message);}
 sealed class NoNetworkFactory:IHttpClientFactory{public HttpClient CreateClient(string name)=>throw new Exception("No real messages allowed in this test.");}
 public static async Task Run(){
  var temp=Path.Combine(Path.GetTempPath(),"inokskar-lists-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  try{
   var b=WebApplication.CreateBuilder();b.Environment.ContentRootPath=AppContext.BaseDirectory;b.Configuration["Storage:Path"]=temp;
   var settings=new MailSettingsStore(b.Environment,b.Configuration,DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(temp,"keys"))));
   using var mail=new MailQueue(b.Environment,b.Configuration,settings,new NoNetworkFactory(),NullLogger<MailQueue>.Instance);
   foreach(var name in new[]{"One","Two","Three"})mail.Enqueue("test@example.invalid",name,"secret-html","secret-text");
   var rows=await mail.PendingRows();Check(rows.Count==3,"mail listing");Check(!rows.ToJsonString().Contains("secret-"),"message body kept private");
   var ids=rows.Select(x=>x!["id"]!.ToString()).ToArray();Check(await mail.DeletePending(new[]{ids[0],ids[0]})==1,"single mail deletion and duplicate ids");Check(await mail.DeletePending(new[]{ids[1],new string('a',32)})==1,"selected mail deletion");Check((await mail.PendingRows()).Single()!["id"]!.ToString()==ids[2],"unselected mail retained");
   var bad=Path.Combine(temp,"mail-outbox",new string('b',32)+".json");File.WriteAllText(bad,"{broken");Check((await mail.PendingRows()).Count==2,"corrupt mail remains manageable");Check(await mail.DeletePending(new[]{new string('b',32)})==1,"corrupt mail deletion");
   var backupDir=Path.Combine(temp,"backups");Directory.CreateDirectory(backupDir);var names=new[]{"inokskar-auto-20261015-120001.zip","inokskar-auto-20261015-120002.zip","inokskar-auto-20261015-120003.zip"};foreach(var name in names)File.WriteAllText(Path.Combine(backupDir,name),"test backup");File.WriteAllText(Path.Combine(backupDir,"unrelated.txt"),"keep");File.WriteAllText(Path.Combine(backupDir,names[0]+".tmp"),"partial");
   var backups=new BackupFiles(temp);Check(backups.Rows().Count==3,"only completed automatic backups listed");Check(backups.Delete(new[]{names[0],names[0]})==1,"single backup deletion");Check(backups.Delete(new[]{names[1]})==1&&backups.Rows().Single()!["id"]!.ToString()==names[2],"selected backups and retained file");
   foreach(var invalid in new[]{"../outside.zip","/tmp/outside.zip","unrelated.txt"}){var rejected=false;try{backups.Delete(new[]{invalid});}catch(ArgumentException){rejected=true;}Check(rejected,"backup path restriction");}
   var mailRejected=false;try{await mail.DeletePending(new[]{"../../catalog-store"});}catch(ArgumentException){mailRejected=true;}Check(mailRejected,"mail path restriction");Check(File.Exists(Path.Combine(backupDir,"unrelated.txt")),"other data untouched");
   var reader=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Role,"Admin"),new Claim("inokskar:permission","system.view"),new Claim("inokskar:permission","backup.view")},"test"));
   Check(OperationsLists.Can(reader,false,"view")&&!OperationsLists.Can(reader,false,"edit")&&OperationsLists.Can(reader,true,"view")&&!OperationsLists.Can(reader,true,"edit"),"read-only access cannot delete");
   Check(!OperationsLists.Can(new ClaimsPrincipal(new ClaimsIdentity()),false,"view"),"anonymous access rejected");
   var catalogStore=new Store(b.Environment,b.Configuration);
   for(var i=0;i<3;i++){var state=catalogStore.Snapshot();catalogStore.Save(new JsonObject{["revision"]=state["revision"]!.DeepClone(),["data"]=state["data"]!.DeepClone(),["label"]="Test version "+i});}
   var versions=catalogStore.CatalogVersionRows();Check(versions.Count==3,"catalog history list");var current=catalogStore.Snapshot();
   Check(catalogStore.DeleteCatalogVersions(new[]{versions[0]!["id"]!.ToString()},"tester")==1,"single catalog history deletion");
   Check(catalogStore.DeleteCatalogVersions(new[]{versions[1]!["id"]!.ToString(),versions[2]!["id"]!.ToString()},"tester")==2,"bulk catalog history deletion");
   var after=catalogStore.Snapshot();Check(after["data"]!.ToJsonString()==current["data"]!.ToJsonString()&&after["revision"]!.ToString()==current["revision"]!.ToString(),"current catalog and revision protected");
   var warrantyIds=new List<string>();for(var i=0;i<3;i++){
    var saved=catalogStore.SaveWarranty(new JsonObject{["productMode"]="manual",["productName"]="Test device",["businessName"]="Test business",["serialNumber"]="OPS-00"+i,["deliveryDate"]=DateTime.Today.ToString("yyyy-MM-dd"),["warrantyMonths"]=12,["components"]=new JsonArray(new JsonObject{["name"]="Gövde",["covered"]=true,["months"]=12})},"tester");
    warrantyIds.Add(saved["record"]!["id"]!.ToString());
   }
   Check(catalogStore.DeleteWarranties(new[]{warrantyIds[0],warrantyIds[0]},"tester")==1,"single warranty deletion and duplicates");
   Check(catalogStore.DeleteWarranties(new[]{warrantyIds[1]},"tester")==1&&catalogStore.WarrantyList().Single()!["id"]!.ToString()==warrantyIds[2],"selected warranty deletion preserves other warranties");
   Check(catalogStore.Snapshot()["warrantyHistory"]!.AsArray().Count(x=>x!["action"]!.ToString()=="Garanti kaydı silindi")==2,"warranty deletion history");
   var reloaded=new Store(b.Environment,b.Configuration);Check(reloaded.CatalogVersionRows().Count==0&&reloaded.WarrantyList().Count==1,"version and warranty deletions persist");
   Check(OperationsLists.Can(reader,"catalog-versions","view")&&!OperationsLists.Can(reader,"catalog-versions","edit")&&!OperationsLists.Can(reader,"warranties","edit"),"catalog and warranty edit permissions enforced");
   Console.WriteLine("Catalog and warranty management checks: PASS (single/bulk deletion, current catalog/revision protection, unselected records retained, history, persistence, permissions; isolated data).");
   Console.WriteLine("Operations management checks: PASS (mail and backup listing, single/selected deletion, duplicates, corrupt queue, path restrictions, privacy, read-only permissions; isolated files, no messages).");
  }finally{Directory.Delete(temp,true);}
 }
}
