using System.Text.Json.Nodes;
public static class ProductVideoChecks
{
    public static void Run()
    {
        var source=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"seed.json")))!.AsObject();
        source["settings"]!["address"]="";source["settings"]!["notificationSmsEnabled"]=false;
        CatalogRules.Validate(source);
        var product=source["products"]!.AsArray()[0]!.AsObject();
        var video="/api/files/"+new string('a',32)+".mp4";
        product["video"]=video;CatalogRules.Validate(source);
        if(CatalogRules.Public(source)["products"]!.AsArray()[0]!["video"]!.GetValue<string>()!=video)throw new Exception("Public catalog lost product video.");
        foreach(var invalid in new[]{"javascript:alert(1)","https://example.com/movie.mp4","/api/files/"+new string('a',32)+".jpg"})
        {
            product["video"]=invalid;var rejected=false;
            try{CatalogRules.Validate(source);}catch(ArgumentException){rejected=true;}
            if(!rejected)throw new Exception("Invalid product video was accepted.");
        }
        product.Remove("video");CatalogRules.Validate(source);
        if(product["video"]!.GetValue<string>()!="")throw new Exception("Legacy product default failed.");
        Console.WriteLine("Product video checks: PASS (legacy catalogs, public persistence, invalid media paths; no live records).");
    }
}
