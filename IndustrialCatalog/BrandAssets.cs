using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

// All historic brand URLs resolve the current site setting before static files can serve an old logo.
public static class BrandAssets
{
 static readonly HashSet<string> Aliases=new(StringComparer.OrdinalIgnoreCase){"/brand-logo","/inokskar-logo.svg","/inokskar-logo.png","/inokskar-header-brand.png","/loading-brand.svg"};
 public static (string file,string mime) Local(JsonObject? settings,string dataPath,string webRoot){
  var source=settings?["headerImage"]?.ToString()??"";var fallback=Path.Combine(webRoot,"inokskar-logo.svg");
  if(string.IsNullOrWhiteSpace(source)||Aliases.Contains(source))return (fallback,"image/svg+xml");
  if(Regex.IsMatch(source,@"^/api/files/[a-f0-9]{32}\.(png|jpg|webp|gif)$")){
   var path=Path.Combine(dataPath,"uploads",Path.GetFileName(source));if(File.Exists(path)&&new FileInfo(path).LinkTarget==null)return(path,Mime(path));
  }else if(Regex.IsMatch(source,@"^/[A-Za-z0-9/_-]+\.(png|jpg|webp|gif|svg)$")&&!source.Contains("..")){
   var path=Path.GetFullPath(Path.Combine(webRoot,source.TrimStart('/')));if(path.StartsWith(Path.GetFullPath(webRoot)+Path.DirectorySeparatorChar,StringComparison.Ordinal)&&File.Exists(path)&&new FileInfo(path).LinkTarget==null)return(path,Mime(path));
  }
  return(fallback,"image/svg+xml");
 }
 static string Mime(string path)=>Path.GetExtension(path).ToLowerInvariant() switch{".png"=>"image/png",".jpg"=>"image/jpeg",".webp"=>"image/webp",".gif"=>"image/gif",_=>"image/svg+xml"};
 public static void Use(WebApplication app,string dataPath,string webRoot){app.Use(async(ctx,next)=>{
  if(ctx.Request.Path=="/api/brand"&&HttpMethods.IsGet(ctx.Request.Method)){var value=ctx.RequestServices.GetRequiredService<Store>().Snapshot()["data"]?["settings"]?["headerImage"]?.ToString()??"";ctx.Response.Headers.CacheControl="no-store";await ctx.Response.WriteAsJsonAsync(new{url="/brand-logo?v="+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))[..12]});return;}
  if(!(HttpMethods.IsGet(ctx.Request.Method)||HttpMethods.IsHead(ctx.Request.Method))||!Aliases.Contains(ctx.Request.Path.Value??"")){await next();return;}
  var settings=ctx.RequestServices.GetRequiredService<Store>().Snapshot()["data"]?["settings"] as JsonObject;
  ctx.Response.Headers.CacheControl="no-store";ctx.Response.Headers["X-Content-Type-Options"]="nosniff";
  var source=settings?["headerImage"]?.ToString()??"";
  if(Uri.TryCreate(source,UriKind.Absolute,out var remote)&&remote.Scheme is "http" or "https"){ctx.Response.Redirect(source,false);return;}
  var asset=Local(settings,dataPath,webRoot);if(!File.Exists(asset.file)){ctx.Response.StatusCode=404;return;}
  ctx.Response.ContentType=asset.mime;ctx.Response.ContentLength=new FileInfo(asset.file).Length;
  if(!HttpMethods.IsHead(ctx.Request.Method))await ctx.Response.SendFileAsync(asset.file,ctx.RequestAborted);
 });}
}
