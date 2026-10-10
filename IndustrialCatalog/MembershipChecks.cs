using System.Security.Claims;
using System.Text.Json.Nodes;

// Isolated checks run during the image build; never use the live data directory.
public static class MembershipChecks
{
    static void Check(bool value,string name){if(!value)throw new InvalidOperationException("Membership check failed: "+name);}
    public static void Run()
    {
        var temp=Path.Combine(Path.GetTempPath(),"inokskar-membership-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try{
            var builder=WebApplication.CreateBuilder();builder.Configuration["Storage:Path"]=temp;
            var customers=new CustomerDirectory(builder.Environment,builder.Configuration);
            var resets=new PasswordResetStore(builder.Environment,builder.Configuration);
            var first=customers.Create("Test Üye","member@example.invalid","Test-password-123");
            var second=customers.Create("Diğer Üye","other@example.invalid","Other-password-123");
            Check(first.PasswordHash!="Test-password-123"&&Passwords.Verify("Test-password-123",first.PasswordHash),"hashed passwords");
            Check(customers.Verify(first.Email,"wrong")==null,"incorrect password rejected");
            Check(!first.EmailVerified,"new accounts require email verification");
            customers.VerifyEmail(first.Id);Check(customers.ById(first.Id)!.EmailVerified,"email verification persists");
            Check(Membership.PersistentSession().IsPersistent&&Membership.PersistentSession().ExpiresUtc>DateTimeOffset.UtcNow.AddDays(89),"persistent customer session");
            var stamp=first.SecurityStamp;customers.ChangePassword(first.Id,"Test-password-123","New-password-123");
            Check(customers.ById(first.Id)!.SecurityStamp!=stamp,"password change revokes existing tickets");
            Check(customers.Verify(first.Email,"Test-password-123")==null,"old password rejected");
            first=customers.ById(first.Id)!;stamp=first.SecurityStamp;customers.RevokeSessions(first.Id);Check(customers.ById(first.Id)!.SecurityStamp!=stamp,"session revocation");
            var token=resets.Create(first.Email,"customer-assist",customers.ById(first.Id)!.SecurityStamp);
            Check(resets.Validate(token)?.Kind=="customer-assist","assistance token target");
            var serialized=File.ReadAllText(Path.Combine(temp,"password-reset-tokens.json"));Check(!serialized.Contains(token),"only token hash persisted");
            Check(resets.ConsumeTarget(token)!=null&&resets.ConsumeTarget(token)==null,"one use token");
            var expired=resets.Create(first.Email,"customer-assist");var tokenFile=Path.Combine(temp,"password-reset-tokens.json");var tokens=JsonNode.Parse(File.ReadAllText(tokenFile))!.AsArray();tokens[0]!["expiresUtc"]=DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");File.WriteAllText(tokenFile,tokens.ToJsonString());Check(resets.Validate(expired)==null&&resets.ConsumeTarget(expired)==null,"expired code rejected");
            token=resets.Create(first.Email,"customer-assist");var replacement=resets.Create(first.Email,"customer-assist");Check(resets.Validate(token)==null&&resets.Validate(replacement)!=null,"new assistance invalidates previous code");
            customers.BindWarranty(first.Id,"test-warranty");var blocked=false;try{customers.BindWarranty(second.Id,"test-warranty");}catch(ArgumentException){blocked=true;}Check(blocked,"device cannot belong to multiple customer accounts");
            customers.UpdateProfile(first.Id,new JsonObject{["profileKind"]="company",["businessName"]="Test Firma"});Check(customers.ById(first.Id)!.ProfileKind=="company","company profile persists");
            Check(Membership.SupportRequested(new JsonObject{["permissions"]=new JsonObject{["customerSupport"]=new JsonObject{["edit"]=true}}}),"support grant detection");
            Check(CustomerDirectory.Principal(customers.ById(first.Id)!).FindFirstValue("inokskar:security-stamp")==customers.ById(first.Id)!.SecurityStamp,"session stamp claim");
            var store=new Store(builder.Environment,builder.Configuration);
            var owned=store.AddInquiry(new JsonObject{["customerId"]=first.Id,["email"]=first.Email,["message"]="Cihaz bakım planım hakkında bilgi almak istiyorum.",["type"]="support",["publicReply"]="Ekibimiz bakım planınız için sizinle iletişime geçecek.",["internalNote"]="PRIVATE-INTERNAL-SENTINEL"});
            store.AddInquiry(new JsonObject{["customerId"]=second.Id,["email"]=first.Email,["message"]="OTHER-CUSTOMER-SENTINEL"});
            store.AddInquiry(new JsonObject{["email"]=first.Email,["message"]="LEGACY-EMAIL-SENTINEL"});
            var member=customers.ById(first.Id)!;member.EmailVerified=false;
            var html=Membership.Profile(member,store);
            Check(!html.Contains("PRIVATE-INTERNAL-SENTINEL")&&!html.Contains("OTHER-CUSTOMER-SENTINEL")&&!html.Contains("LEGACY-EMAIL-SENTINEL"),"request isolation and private notes");
            member.EmailVerified=true;Check(Membership.Profile(member,store).Contains("LEGACY-EMAIL-SENTINEL"),"verified legacy email requests");
            member.Name="<script>alert(1)</script>";Check(!Membership.Profile(member,store).Contains("<script>alert(1)</script>"),"profile HTML encoding");
            Console.WriteLine("Membership security checks: PASS (isolated temporary data)");
        }finally{Directory.Delete(temp,true);}
    }
}
