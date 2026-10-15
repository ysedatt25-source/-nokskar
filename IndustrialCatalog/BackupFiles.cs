using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
public sealed class BackupFiles(string dataPath)
{
 readonly object gate=new();
 readonly string directory=Path.Combine(dataPath,"backups");
 static bool Valid(string id)=>Regex.IsMatch(id,@"^inokskar-auto-\d{8}-\d{6}\.zip$");
 public JsonArray Rows(){lock(gate){
  var rows=new JsonArray();if(!Directory.Exists(directory))return rows;
  foreach(var file in Directory.EnumerateFiles(directory,"inokskar-auto-*.zip").Select(x=>new FileInfo(x)).OrderByDescending(x=>x.LastWriteTimeUtc)){
   if(!Valid(file.Name)||file.LinkTarget!=null||!file.Exists)continue;
   rows.Add(new JsonObject{["id"]=file.Name,["name"]=file.Name,["createdUtc"]=file.LastWriteTimeUtc.ToString("O"),["bytes"]=file.Length});
  }return rows;
 }}
 public int Delete(IEnumerable<string> ids){var selected=ids.Distinct(StringComparer.Ordinal).ToArray();if(selected.Length<1||selected.Length>1000||selected.Any(x=>!Valid(x)))throw new ArgumentException("Geçerli otomatik yedekleri seçin (en fazla 1000).");
  lock(gate){var count=0;foreach(var id in selected){var file=Path.Combine(directory,id);if(!File.Exists(file)||new FileInfo(file).LinkTarget!=null)continue;File.Delete(file);count++;}return count;}
 }
}
