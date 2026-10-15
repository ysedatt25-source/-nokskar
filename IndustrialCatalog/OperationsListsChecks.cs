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
   Console.WriteLine("Operations management checks: PASS (mail and backup listing, single/selected deletion, duplicates, corrupt queue, path restrictions, privacy, read-only permissions; isolated files, no messages).");
  }finally{Directory.Delete(temp,true);}
 }
}
