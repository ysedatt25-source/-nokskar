using System.Text.Json.Nodes;
public static class AuditLogChecks
{
 public static void Run(){
  var temp=Path.Combine(Path.GetTempPath(),"inokskar-audit-"+Guid.NewGuid().ToString("N"));
  try{
   var b=WebApplication.CreateBuilder();b.Environment.ContentRootPath=AppContext.BaseDirectory;b.Configuration["Storage:Path"]=temp;
   var store=new Store(b.Environment,b.Configuration);store.RecordAudit("First","one");store.RecordAudit("Second","two");store.RecordAudit("Third","three");
   var rows=store.AuditLog();var first=rows[2]!["id"]!.ToString();var second=rows[1]!["id"]!.ToString();var keep=rows[0]!["id"]!.ToString();var data=store.Snapshot()["data"]!.ToJsonString();
   if(store.DeleteAuditRecords(new[]{first,first},"tester")!=1)throw new Exception("Single/duplicate audit deletion failed.");
   if(store.DeleteAuditRecords(new[]{second,new string('a',32)},"tester")!=1)throw new Exception("Selected audit deletion failed.");
   var restored=new Store(b.Environment,b.Configuration);var remaining=restored.AuditLog();
   if(remaining.Any(x=>new[]{first,second}.Contains(x!["id"]!.ToString()))||!remaining.Any(x=>x!["id"]!.ToString()==keep))throw new Exception("Audit persistence/isolation failed.");
   if(restored.Snapshot()["data"]!.ToJsonString()!=data)throw new Exception("Audit delete changed catalog data.");
   foreach(var ids in new[]{Array.Empty<string>(),new[]{"invalid"},Enumerable.Range(0,501).Select(i=>i.ToString("x32")).ToArray()}){
    var rejected=false;try{store.DeleteAuditRecords(ids,"tester");}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Invalid audit deletion accepted.");
   }
   if(!remaining.Any(x=>x!["action"]!.ToString()=="İşlem günlükleri silindi"))throw new Exception("Deletion accountability missing.");
   Console.WriteLine("Audit deletion checks: PASS (single, bulk, duplicates, persistence, isolation, invalid selection, deletion history; isolated data).");
  }finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
 }
}
