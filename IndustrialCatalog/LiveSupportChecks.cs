using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;

public static class LiveSupportChecks
{
    public static void Run()
    {
        var path=Path.Combine(Path.GetTempPath(),"inokskar-support-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);
        try
        {
            var env=new TestEnvironment{ContentRootPath=path};var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Storage:Path",path}}).Build();
            var readOnly=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Role,"Admin"),new Claim("inokskar:permission","customerSupport.view")},"test"));
            Require(LiveSupportStore.Staff(readOnly)&&!LiveSupportStore.Respond(readOnly),"Read-only support cannot respond.");
            var technician=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Role,"TechnicalService"),new Claim("inokskar:permission","customerSupport.view"),new Claim("inokskar:permission","customerSupport.edit")},"test"));
            Require(!LiveSupportStore.Staff(technician),"Technical staff cannot impersonate support admins.");
            Require(SupportTranslation.PreservesIdentifiers("BNK001 25 € 120 cm","BNK001 costs 25 € and measures 120 cm"),"Identifiers are retained.");
            Require(!SupportTranslation.PreservesIdentifiers("BNK001 25 €","BNK002 costs 250 €"),"Changed product codes/prices reject translation.");
            Require(!SupportTranslation.PreservesIdentifiers("25 €","250 €"),"A numeric prefix is not the same price.");
            var handler=new TranslationHandler();var translator=new SupportTranslation(env,config,DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(path,"keys"))),new TestFactory(handler));
            Require(!translator.Ready,"No provider key must not pretend translation is ready.");translator.Configure("qa-translation-key-for-isolated-test",false);
            var good=translator.Translate("merha bn BNK001 25 €","tr","en",CancellationToken.None).GetAwaiter().GetResult();Require(good=="Hello, BNK001 costs 25 €.","Structured translation result is decoded.");
            handler.Translation="BNK002 costs 250 €.";Require(translator.Translate("BNK001 25 €","tr","en",CancellationToken.None).GetAwaiter().GetResult()==null,"Invalid translated identifiers fall back to original.");
            handler.Status=HttpStatusCode.TooManyRequests;Require(translator.Translate("test","tr","en",CancellationToken.None).GetAwaiter().GetResult()==null,"Provider failures preserve original messages.");
            var persisted=File.ReadAllText(Path.Combine(path,"live-support-translation.key"));Require(!persisted.Contains("qa-translation-key"),"Provider key is encrypted at rest.");
            var store=new LiveSupportStore(env,config);var actor="guest:"+new string('a',64);var id=JsonNode.Parse(JsonSerializer.Serialize(store.Open(actor,"Test","tr")))!["id"]!.ToString();
            var m=store.Add(id,actor,false,"Test","Merhaba BNK001","tr","test-1",allowTranslation:false);Require(!store.TranslationInput(id,m.Id,actor,false,"en").Allowed,"Translation opt-out must be enforced on the server.");
            Require(store.Add(id,actor,false,"Test","Merhaba BNK001","tr","test-1").Id==m.Id,"Retries are idempotent.");
            var blocked=false;try{store.DeleteClosed(id,"staff:founder");}catch(ArgumentException){blocked=true;}Require(blocked,"Active conversations cannot be deleted.");
            var attachment=new string('b',32)+".pdf";File.WriteAllText(Path.Combine(path,"live-support",attachment),"%PDF-1.4 test");store.Add(id,actor,false,"Test","","tr","attachment-test",attachment,"test.pdf","application/pdf");
            store.Close(id,"staff:founder");store.DeleteClosed(id,"staff:founder");Require(!File.Exists(Path.Combine(path,"live-support",attachment)),"Deletion removes attachments.");
            var reload=new LiveSupportStore(env,config);var after=JsonNode.Parse(JsonSerializer.Serialize(reload.State("staff:founder",true,id,"tr",0)))!;Require(after["threadGone"]!.GetValue<bool>()&&after["threads"]!.AsArray().Count==0,"Deletion persists and stale selections recover.");
            Console.WriteLine("Live support checks: PASS (isolated store, role checks, translation contract/opt-out, encrypted key; no real provider calls).");
        }
        finally{Directory.Delete(path,true);}
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    sealed class TestFactory(TranslationHandler handler):IHttpClientFactory{public HttpClient CreateClient(string name)=>new(handler,false);}
    sealed class TranslationHandler:HttpMessageHandler
    {
        public string Translation="Hello, BNK001 costs 25 €.";public HttpStatusCode Status=HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Require(request.RequestUri?.ToString()=="https://api.openai.com/v1/chat/completions","Provider endpoint is fixed.");
            var result=JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=JsonSerializer.Serialize(new{translation=Translation})}}}});
            return Task.FromResult(new HttpResponseMessage(Status){Content=new StringContent(result)});
        }
    }
    sealed class TestEnvironment:IWebHostEnvironment
    {
        public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Test";public string ContentRootPath{get;set;}="";public string WebRootPath{get;set;}="";
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
    }
}
