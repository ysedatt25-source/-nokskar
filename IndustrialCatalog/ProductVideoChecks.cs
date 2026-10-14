using System.Text.Json.Nodes;
public static class ProductVideoChecks
{
    public static void Run()
    {
        var source=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"seed.json")))!.AsObject();
        source["settings"]!["address"]="";source["settings"]!["notificationSmsEnabled"]=false;
        CatalogRules.Validate(source);
        if(source["settings"]!["heroImages"]!.AsArray().Count!=0)throw new Exception("Legacy hero gallery default failed.");
        var gallery=new JsonArray(Enumerable.Range(1,10).Select(i=>(JsonNode?)JsonValue.Create("/hero-"+i+".jpg")).ToArray());
        source["settings"]!["heroImages"]=gallery;CatalogRules.Validate(source);
        if(CatalogRules.Public(source)["settings"]!["heroImages"]!.AsArray().Count!=10)throw new Exception("Public catalog lost hero gallery.");
        foreach(var invalid in new[]{new JsonArray(Enumerable.Range(1,11).Select(i=>(JsonNode?)JsonValue.Create("/hero-"+i+".jpg")).ToArray()),new JsonArray("javascript:alert(1)"),new JsonArray("/movie.mp4")}){
            source["settings"]!["heroImages"]=invalid;var rejected=false;try{CatalogRules.Validate(source);}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Invalid hero gallery was accepted.");
        }
        source["settings"]!["heroImages"]=new JsonArray();
        Console.WriteLine("Hero gallery checks: PASS (legacy catalogs, ten-image limit, public persistence, invalid paths)." );
        var product=source["products"]!.AsArray()[0]!.AsObject();
        if(product["priceUnit"]!.GetValue<string>()!="unit")throw new Exception("Legacy price unit default failed.");
        foreach(var unit in new[]{"unit","metre","m2"}){
            product["priceUnit"]=unit;CatalogRules.Validate(source);
            if(CatalogRules.Public(source)["products"]![0]!["priceUnit"]!.GetValue<string>()!=unit)throw new Exception("Public catalog lost price unit.");
        }
        product["priceUnit"]="invalid";var invalidUnitRejected=false;try{CatalogRules.Validate(source);}catch(ArgumentException){invalidUnitRejected=true;}
        if(!invalidUnitRejected)throw new Exception("Invalid price unit was accepted.");
        product.Remove("priceUnit");CatalogRules.Validate(source);
        Console.WriteLine("Product price unit checks: PASS (legacy default, three units, public persistence, invalid unit).");
        if(product["picturedLength"]!.GetValue<string>()!="")throw new Exception("Legacy pictured size default failed.");
        foreach(var size in new[]{"2","2,5","3.75"}){
            product["picturedLength"]=size;CatalogRules.Validate(source);
            if(CatalogRules.Public(source)["products"]![0]!["picturedLength"]!.GetValue<string>()!=size)throw new Exception("Pictured size lost in public catalog.");
        }
        foreach(var size in new[]{"0","-2","abc","2 metres","<script>"}){
            product["picturedLength"]=size;var rejectedSize=false;try{CatalogRules.Validate(source);}catch(ArgumentException){rejectedSize=true;}
            if(!rejectedSize)throw new Exception("Invalid pictured size accepted.");
        }
        product["picturedLength"]="";CatalogRules.Validate(source);
        Console.WriteLine("Pictured product size checks: PASS (optional, decimals, public persistence, invalid input).");
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
