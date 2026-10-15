using System.Text.Json;
using System.Text.Json.Nodes;
public static class ProductDescriptionChecks
{
 public static async Task Run(){
  var root=Path.Combine(Path.GetTempPath(),"inokskar-description-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"uploads"));
  try{
   foreach(var input in new[]{"https://example.com/a.jpg","/uploads/../secret.jpg","/uploads/"+new string('a',32)+".mp4","/uploads/a.jpg"}){
    try{await ProductDescriptions.ImageData(root,input,CancellationToken.None);throw new Exception("Unsafe image accepted");}catch(ArgumentException){}
   }
   var name=new string('a',32)+".jpg";await File.WriteAllBytesAsync(Path.Combine(root,"uploads",name),new byte[]{255,216,255,217});
   var image=await ProductDescriptions.ImageData(root,"/uploads/"+name,CancellationToken.None);Require(image.StartsWith("data:image/jpeg;base64,"),"Local bytes encoded");
   Directory.CreateDirectory(Path.Combine(root,"media-cache-v1"));await File.WriteAllBytesAsync(MediaOptimizer.DerivedPath(root,name,"detail"),new byte[]{1,2,3});
   Require((await ProductDescriptions.ImageData(root,"/uploads/"+name,CancellationToken.None)).StartsWith("data:image/webp;base64,"),"Optimized detail preferred");
   try{ProductDescriptions.ReadDescription(JsonNode.Parse("{\"suitable\":false,\"description\":\"generic fallback\"}")!);throw new Exception("Unclear photos accepted");}catch(ArgumentException){}
   try{ProductDescriptions.ReadDescription(new JsonObject{["suitable"]=true,["description"]="<script>"+new string('x',100)});throw new Exception("HTML accepted");}catch(JsonException){}
   var text=new string('x',100);Require(ProductDescriptions.ReadDescription(new JsonObject{["suitable"]=true,["description"]=text})==text,"Draft preserved");
   Console.WriteLine("Product description checks: PASS (local-only images, traversal rejection, detail variant, unclear evidence refusal, plain-text output; no provider calls).");
  }finally{Directory.Delete(root,true);}
 }
 static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
