using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text.Json;

public sealed class CustomerAccountRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Phone { get; set; } = "";
    public string BusinessName { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string District { get; set; } = "";
    public string TaxOffice { get; set; } = "";
    public string TaxNumber { get; set; } = "";
    public bool Active { get; set; } = true;
    public string CreatedUtc { get; set; } = "";
    public string UpdatedUtc { get; set; } = "";
    public string LastLoginUtc { get; set; } = "";
}

public sealed class CustomerDirectory
{
    readonly object gate = new();
    readonly string file;
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    public CustomerDirectory(IWebHostEnvironment env, IConfiguration config)
    {
        var dataPath = Path.GetFullPath(config["Storage:Path"] ?? "App_Data", env.ContentRootPath);
        Directory.CreateDirectory(dataPath);
        file = Path.Combine(dataPath, "customers.json");
        if (!File.Exists(file)) Persist(new List<CustomerAccountRecord>());
        else { try { _ = LoadUnsafe(); } catch { try { File.Move(file, file + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), true); } catch { } Persist(new List<CustomerAccountRecord>()); } }
    }

    List<CustomerAccountRecord> LoadUnsafe() => JsonSerializer.Deserialize<List<CustomerAccountRecord>>(File.ReadAllText(file), JsonOptions) ?? new List<CustomerAccountRecord>();
    void Persist(List<CustomerAccountRecord> rows)
    {
        var temp = file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(rows, JsonOptions));
        File.Move(temp, file, true);
    }

    static string CleanName(string? value) { var v=(value??"").Trim(); if(v.Length is <2 or >100) throw new ArgumentException("Ad soyad 2-100 karakter arasında olmalıdır."); return v; }
    static string CleanEmail(string? value) { var v=(value??"").Trim(); if(!System.Net.Mail.MailAddress.TryCreate(v,out var parsed)) throw new ArgumentException("Geçerli bir e-posta adresi girin."); return parsed.Address; }
    static string Clean(string? value,int max,string label,bool required=false) { var v=(value??"").Trim(); if(required&&v.Length<2) throw new ArgumentException(label+" gereklidir."); if(v.Length>max) throw new ArgumentException(label+" çok uzun."); return v; }
    static void ValidatePassword(string password){ if(string.IsNullOrWhiteSpace(password)||password.Length is <8 or >128) throw new ArgumentException("Şifre 8-128 karakter arasında olmalıdır."); }
    static CustomerAccountRecord Clone(CustomerAccountRecord value)=>JsonSerializer.Deserialize<CustomerAccountRecord>(JsonSerializer.Serialize(value,JsonOptions),JsonOptions)!;

    public CustomerAccountRecord Create(string name,string email,string password,string phone="",string businessName="")
    {
        lock(gate)
        {
            var rows=LoadUnsafe(); var normalized=CleanEmail(email);
            if(rows.Any(x=>string.Equals(x.Email,normalized,StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Bu e-posta adresiyle daha önce müşteri hesabı oluşturulmuş. Giriş yapabilir veya şifrenizi yenileyebilirsiniz.");
            ValidatePassword(password); var now=DateTimeOffset.UtcNow.ToString("O");
            var row=new CustomerAccountRecord{Id=Guid.NewGuid().ToString("N"),Name=CleanName(name),Email=normalized,PasswordHash=Passwords.Hash(password),Phone=Clean(phone,40,"Telefon"),BusinessName=Clean(businessName,180,"Firma"),Active=true,CreatedUtc=now,UpdatedUtc=now};
            rows.Add(row); Persist(rows); return Clone(row);
        }
    }

    public CustomerAccountRecord? Verify(string email,string password)
    {
        lock(gate)
        {
            var rows=LoadUnsafe(); var user=rows.FirstOrDefault(x=>x.Active&&string.Equals(x.Email,(email??"").Trim(),StringComparison.OrdinalIgnoreCase));
            if(user==null||!Passwords.Verify(password??"",user.PasswordHash)) return null;
            user.LastLoginUtc=DateTimeOffset.UtcNow.ToString("O"); user.UpdatedUtc=user.UpdatedUtc;
            Persist(rows); return Clone(user);
        }
    }

    public CustomerAccountRecord? ById(string id){ lock(gate){ var row=LoadUnsafe().FirstOrDefault(x=>x.Id==id&&x.Active); return row==null?null:Clone(row); } }
    public CustomerAccountRecord? ByEmail(string email){ lock(gate){ var row=LoadUnsafe().FirstOrDefault(x=>x.Active&&string.Equals(x.Email,(email??"").Trim(),StringComparison.OrdinalIgnoreCase)); return row==null?null:Clone(row); } }
    public bool EmailExists(string email){ lock(gate) return LoadUnsafe().Any(x=>string.Equals(x.Email,(email??"").Trim(),StringComparison.OrdinalIgnoreCase)); }

    public CustomerAccountRecord UpdateProfile(string id, System.Text.Json.Nodes.JsonObject input)
    {
        lock(gate)
        {
            var rows=LoadUnsafe(); var index=rows.FindIndex(x=>x.Id==id&&x.Active); if(index<0) throw new ArgumentException("Müşteri hesabı bulunamadı.");
            var row=rows[index]; if(input.ContainsKey("name"))row.Name=CleanName(input["name"]?.ToString()??row.Name); if(input.ContainsKey("phone"))row.Phone=Clean(input["phone"]?.ToString(),40,"Telefon"); if(input.ContainsKey("businessName"))row.BusinessName=Clean(input["businessName"]?.ToString(),180,"Firma"); if(input.ContainsKey("address"))row.Address=Clean(input["address"]?.ToString(),1200,"Adres"); if(input.ContainsKey("city"))row.City=Clean(input["city"]?.ToString(),100,"İl"); if(input.ContainsKey("district"))row.District=Clean(input["district"]?.ToString(),100,"İlçe"); if(input.ContainsKey("taxOffice"))row.TaxOffice=Clean(input["taxOffice"]?.ToString(),120,"Vergi dairesi"); if(input.ContainsKey("taxNumber"))row.TaxNumber=Clean(input["taxNumber"]?.ToString(),40,"Vergi numarası"); row.UpdatedUtc=DateTimeOffset.UtcNow.ToString("O"); rows[index]=row; Persist(rows); return Clone(row);
        }
    }

    public void ChangePassword(string id,string currentPassword,string newPassword)
    {
        ValidatePassword(newPassword);
        lock(gate)
        {
            var rows=LoadUnsafe(); var index=rows.FindIndex(x=>x.Id==id&&x.Active); if(index<0) throw new ArgumentException("Müşteri hesabı bulunamadı.");
            if(!Passwords.Verify(currentPassword??"",rows[index].PasswordHash)) throw new ArgumentException("Mevcut şifre hatalı.");
            if(Passwords.Verify(newPassword,rows[index].PasswordHash)) throw new ArgumentException("Yeni şifre mevcut şifreyle aynı olamaz.");
            rows[index].PasswordHash=Passwords.Hash(newPassword); rows[index].UpdatedUtc=DateTimeOffset.UtcNow.ToString("O"); Persist(rows);
        }
    }

    public void ResetPasswordByEmail(string email,string newPassword)
    {
        ValidatePassword(newPassword);
        var value=(email??"").Trim();
        lock(gate)
        {
            var rows=LoadUnsafe(); var index=rows.FindIndex(x=>x.Active&&string.Equals(x.Email,value,StringComparison.OrdinalIgnoreCase));
            if(index<0) throw new ArgumentException("Müşteri hesabı bulunamadı.");
            rows[index].PasswordHash=Passwords.Hash(newPassword);
            rows[index].UpdatedUtc=DateTimeOffset.UtcNow.ToString("O");
            Persist(rows);
        }
    }

    public static ClaimsPrincipal Principal(CustomerAccountRecord user)=>new(new ClaimsIdentity(new[]{
        new Claim(ClaimTypes.Name,user.Email),new Claim(ClaimTypes.Email,user.Email),new Claim(ClaimTypes.Role,"Customer"),new Claim("inokskar:user-id",user.Id),new Claim("inokskar:display-name",user.Name)
    },CookieAuthenticationDefaults.AuthenticationScheme));
}
