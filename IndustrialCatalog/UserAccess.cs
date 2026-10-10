using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public sealed class AccessUserRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "TechnicalService";
    public string PasswordHash { get; set; } = "";
    public bool Active { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public Dictionary<string, AccessModulePermission> Permissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string CreatedUtc { get; set; } = "";
    public string UpdatedUtc { get; set; } = "";
    public string LastLoginUtc { get; set; } = "";
}

public sealed class AccessModulePermission
{
    public bool View { get; set; }
    public bool Edit { get; set; }
    public bool Download { get; set; }
}

public static class AccessPermissions
{
    public const string Catalog = "catalog";
    public const string Prices = "prices";
    public const string Settings = "settings";
    public const string Warranties = "warranties";
    public const string Technical = "technical";
    public const string Service = "service";
    public const string Inquiries = "inquiries";
    public const string Backup = "backup";
    public const string System = "system";
    public const string Users = "users";
    public const string CustomerSupport = "customerSupport";
    public static readonly string[] Modules = { Catalog, Prices, Settings, Warranties, Technical, Service, Inquiries, Backup, System, Users, CustomerSupport };

    public static string Claim(string module, string action) => module + "." + action;
}

public sealed class UserDirectory
{
    readonly object gate = new();
    readonly string file;
    readonly IConfiguration config;
    static readonly Regex IdPattern = new("^[a-f0-9]{32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    public UserDirectory(IWebHostEnvironment env, IConfiguration configuration)
    {
        config = configuration;
        var dataPath = Path.GetFullPath(config["Storage:Path"] ?? "App_Data", env.ContentRootPath);
        Directory.CreateDirectory(dataPath);
        file = Path.Combine(dataPath, "access-users.json");
        if (!File.Exists(file)) Persist(new List<AccessUserRecord>());
        else
        {
            try { _ = LoadUnsafe(); }
            catch (InvalidDataException)
            {
                var corrupt = file + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                try { File.Move(file, corrupt, true); } catch { }
                Persist(new List<AccessUserRecord>());
            }
        }
    }

    public string SuperAdminEmail => (config["Admin:Email"] ?? "").Trim();

    List<AccessUserRecord> LoadUnsafe()
    {
        try
        {
            var rows = JsonSerializer.Deserialize<List<AccessUserRecord>>(File.ReadAllText(file), JsonOptions) ?? new List<AccessUserRecord>();
            var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (!IdPattern.IsMatch(row.Id) || string.IsNullOrWhiteSpace(row.Email) || !emails.Add(row.Email)) throw new InvalidDataException("Kullanıcı kayıtları geçersiz.");
                if (row.Role is not ("Admin" or "TechnicalService")) throw new InvalidDataException("Kullanıcı rolü geçersiz.");
                row.Permissions ??= new Dictionary<string, AccessModulePermission>(StringComparer.OrdinalIgnoreCase);
                if (row.Permissions.Keys.Any(k => !AccessPermissions.Modules.Contains(k, StringComparer.OrdinalIgnoreCase))) throw new InvalidDataException("Kullanıcı yetkileri geçersiz.");
            }
            return rows;
        }
        catch (JsonException e) { throw new InvalidDataException("Kullanıcı kayıt dosyası okunamadı.", e); }
    }

    void Persist(List<AccessUserRecord> rows)
    {
        var temp = file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(rows, JsonOptions));
        File.Move(temp, file, true);
    }

    public AccessUserRecord? Verify(string email, string password)
    {
        lock (gate)
        {
            var user = LoadUnsafe().FirstOrDefault(x => x.Active && string.Equals(x.Email, (email ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
            if (user == null || !Passwords.Verify(password ?? "", user.PasswordHash)) return null;
            user.LastLoginUtc = DateTimeOffset.UtcNow.ToString("O");
            var rows = LoadUnsafe();
            var index = rows.FindIndex(x => x.Id == user.Id);
            if (index >= 0) { rows[index].LastLoginUtc = user.LastLoginUtc; Persist(rows); }
            return Clone(user);
        }
    }

    public AccessUserRecord? ById(string id)
    {
        lock (gate) return CloneOrNull(LoadUnsafe().FirstOrDefault(x => x.Id == id));
    }

    public AccessUserRecord? ByEmail(string email)
    {
        var value = (email ?? "").Trim();
        lock (gate) return CloneOrNull(LoadUnsafe().FirstOrDefault(x => x.Active && string.Equals(x.Email, value, StringComparison.OrdinalIgnoreCase)));
    }

    public bool IsActive(string id)
    {
        lock (gate) return LoadUnsafe().Any(x => x.Id == id && x.Active);
    }

    public bool EmailExists(string email)
    {
        var value=(email??"").Trim();
        lock(gate) return string.Equals(value,SuperAdminEmail,StringComparison.OrdinalIgnoreCase)||LoadUnsafe().Any(x=>string.Equals(x.Email,value,StringComparison.OrdinalIgnoreCase));
    }

    public JsonArray PublicList()
    {
        lock (gate)
        {
            var rows = new JsonArray();
            rows.Add(new JsonObject
            {
                ["id"] = "superadmin", ["name"] = "Kurucu Süper Admin", ["email"] = SuperAdminEmail,
                ["role"] = "SuperAdmin", ["active"] = true, ["locked"] = true, ["mustChangePassword"] = false,
                ["createdUtc"] = "", ["updatedUtc"] = "", ["lastLoginUtc"] = "", ["permissions"] = FullPermissionsJson()
            });
            foreach (var user in LoadUnsafe().OrderBy(x => x.Role).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)) rows.Add(PublicJson(user));
            return rows;
        }
    }

    public AccessUserRecord Create(JsonObject input)
    {
        lock (gate)
        {
            var rows = LoadUnsafe();
            var email = CleanEmail(input["email"]?.ToString());
            if (string.Equals(email, SuperAdminEmail, StringComparison.OrdinalIgnoreCase) || rows.Any(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Bu e-posta zaten kullanılıyor.");
            var password = input["password"]?.ToString() ?? "";
            ValidatePassword(password);
            var now = DateTimeOffset.UtcNow.ToString("O");
            var user = new AccessUserRecord
            {
                Id = Guid.NewGuid().ToString("N"), Name = CleanName(input["name"]?.ToString()), Email = email,
                Role = CleanRole(input["role"]?.ToString()), PasswordHash = Passwords.Hash(password), Active = input["active"]?.GetValue<bool>() ?? true,
                MustChangePassword = input["mustChangePassword"]?.GetValue<bool>() ?? true, Permissions = ParsePermissions(input["permissions"] as JsonObject),
                CreatedUtc = now, UpdatedUtc = now
            };
            rows.Add(user); Persist(rows); return Clone(user);
        }
    }

    public AccessUserRecord Update(string id, JsonObject input)
    {
        lock (gate)
        {
            var rows = LoadUnsafe(); var index = rows.FindIndex(x => x.Id == id); if (index < 0) throw new ArgumentException("Kullanıcı bulunamadı.");
            var current = rows[index]; var email = CleanEmail(input["email"]?.ToString());
            if (string.Equals(email, SuperAdminEmail, StringComparison.OrdinalIgnoreCase) || rows.Where((x, i) => i != index).Any(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Bu e-posta zaten kullanılıyor.");
            current.Name = CleanName(input["name"]?.ToString()); current.Email = email; current.Role = CleanRole(input["role"]?.ToString());
            current.Active = input["active"]?.GetValue<bool>() ?? current.Active; current.Permissions = ParsePermissions(input["permissions"] as JsonObject); current.UpdatedUtc = DateTimeOffset.UtcNow.ToString("O");
            rows[index] = current; Persist(rows); return Clone(current);
        }
    }

    public void ResetPassword(string id, string password, bool mustChange = true)
    {
        ValidatePassword(password);
        lock (gate)
        {
            var rows = LoadUnsafe(); var index = rows.FindIndex(x => x.Id == id); if (index < 0) throw new ArgumentException("Kullanıcı bulunamadı.");
            rows[index].PasswordHash = Passwords.Hash(password); rows[index].MustChangePassword = mustChange; rows[index].UpdatedUtc = DateTimeOffset.UtcNow.ToString("O"); Persist(rows);
        }
    }

    public void ChangeOwnPassword(string id, string currentPassword, string newPassword)
    {
        ValidatePassword(newPassword);
        lock (gate)
        {
            var rows = LoadUnsafe(); var index = rows.FindIndex(x => x.Id == id && x.Active); if (index < 0) throw new ArgumentException("Kullanıcı bulunamadı.");
            if (!Passwords.Verify(currentPassword ?? "", rows[index].PasswordHash)) throw new ArgumentException("Mevcut şifre hatalı.");
            if (Passwords.Verify(newPassword, rows[index].PasswordHash)) throw new ArgumentException("Yeni şifre mevcut şifreyle aynı olamaz.");
            rows[index].PasswordHash = Passwords.Hash(newPassword); rows[index].MustChangePassword = false; rows[index].UpdatedUtc = DateTimeOffset.UtcNow.ToString("O"); Persist(rows);
        }
    }

    public void ResetPasswordByEmail(string email, string newPassword)
    {
        ValidatePassword(newPassword);
        var value = (email ?? "").Trim();
        lock (gate)
        {
            var rows = LoadUnsafe(); var index = rows.FindIndex(x => x.Active && string.Equals(x.Email, value, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new ArgumentException("Kullanıcı bulunamadı.");
            rows[index].PasswordHash = Passwords.Hash(newPassword);
            rows[index].MustChangePassword = false;
            rows[index].UpdatedUtc = DateTimeOffset.UtcNow.ToString("O");
            Persist(rows);
        }
    }

    public bool Delete(string id)
    {
        lock (gate) { var rows = LoadUnsafe(); var index = rows.FindIndex(x => x.Id == id); if (index < 0) return false; rows.RemoveAt(index); Persist(rows); return true; }
    }

    public static ClaimsPrincipal Principal(AccessUserRecord user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Email), new(ClaimTypes.Email, user.Email), new(ClaimTypes.Role, user.Role),
            new("inokskar:user-id", user.Id), new("inokskar:display-name", user.Name), new("inokskar:must-change-password", user.MustChangePassword ? "1" : "0")
        };
        foreach (var pair in user.Permissions)
        {
            if (pair.Value.View) claims.Add(new Claim("inokskar:permission", AccessPermissions.Claim(pair.Key, "view")));
            if (pair.Value.Edit) claims.Add(new Claim("inokskar:permission", AccessPermissions.Claim(pair.Key, "edit")));
            if (pair.Value.Download) claims.Add(new Claim("inokskar:permission", AccessPermissions.Claim(pair.Key, "download")));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static ClaimsPrincipal SuperAdminPrincipal(string email) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Name,email), new Claim(ClaimTypes.Email,email), new Claim(ClaimTypes.Role,"SuperAdmin"), new Claim("inokskar:user-id","superadmin"), new Claim("inokskar:display-name","Kurucu Süper Admin"), new Claim("inokskar:permission","*")
    }, CookieAuthenticationDefaults.AuthenticationScheme));

    public static JsonObject PublicJson(AccessUserRecord user)
    {
        var permissions = new JsonObject();
        foreach (var module in AccessPermissions.Modules)
        {
            user.Permissions.TryGetValue(module, out var p); p ??= new AccessModulePermission();
            permissions[module] = new JsonObject { ["view"] = p.View, ["edit"] = p.Edit, ["download"] = p.Download };
        }
        return new JsonObject
        {
            ["id"] = user.Id, ["name"] = user.Name, ["email"] = user.Email, ["role"] = user.Role, ["active"] = user.Active,
            ["mustChangePassword"] = user.MustChangePassword, ["createdUtc"] = user.CreatedUtc, ["updatedUtc"] = user.UpdatedUtc, ["lastLoginUtc"] = user.LastLoginUtc,
            ["permissions"] = permissions
        };
    }

    static JsonObject FullPermissionsJson()
    {
        var o = new JsonObject(); foreach (var module in AccessPermissions.Modules) o[module] = new JsonObject { ["view"] = true, ["edit"] = true, ["download"] = true }; return o;
    }

    static Dictionary<string, AccessModulePermission> ParsePermissions(JsonObject? input)
    {
        var result = new Dictionary<string, AccessModulePermission>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in AccessPermissions.Modules)
        {
            var row = input?[module] as JsonObject;
            var view = row?["view"]?.GetValue<bool>() ?? false;
            var edit = row?["edit"]?.GetValue<bool>() ?? false;
            var download = module == AccessPermissions.Technical && (row?["download"]?.GetValue<bool>() ?? false);
            if (edit) view = true; if (download) view = true;
            result[module] = new AccessModulePermission { View = view, Edit = edit, Download = download };
        }
        return result;
    }

    static string CleanName(string? value)
    {
        var v = (value ?? "").Trim(); if (v.Length is < 2 or > 100) throw new ArgumentException("Ad soyad 2-100 karakter arasında olmalıdır."); return v;
    }
    static string CleanEmail(string? value)
    {
        var v = (value ?? "").Trim(); if (!System.Net.Mail.MailAddress.TryCreate(v, out var parsed)) throw new ArgumentException("Geçerli bir e-posta adresi girin."); return parsed.Address;
    }
    static string CleanRole(string? value) => value switch { "Admin" => "Admin", "TechnicalService" => "TechnicalService", _ => throw new ArgumentException("Rol Admin veya Teknik Servis olmalıdır.") };
    static void ValidatePassword(string password) { if (string.IsNullOrWhiteSpace(password) || password.Length is < 8 or > 128) throw new ArgumentException("Şifre 8-128 karakter arasında olmalıdır."); }
    static AccessUserRecord Clone(AccessUserRecord source) => JsonSerializer.Deserialize<AccessUserRecord>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions)!;
    static AccessUserRecord? CloneOrNull(AccessUserRecord? source) => source == null ? null : Clone(source);
}

public static class AccessControl
{
    public static bool IsSuperAdmin(ClaimsPrincipal user) => user.IsInRole("SuperAdmin") || user.HasClaim("inokskar:permission", "*");
    public static bool Has(ClaimsPrincipal user, string module, string action) => IsSuperAdmin(user) || user.HasClaim("inokskar:permission", AccessPermissions.Claim(module, action));
    public static string UserId(ClaimsPrincipal user) => user.FindFirst("inokskar:user-id")?.Value ?? "";
    public static string DisplayName(ClaimsPrincipal user) => user.FindFirst("inokskar:display-name")?.Value ?? user.Identity?.Name ?? "Kullanıcı";
    public static string Role(ClaimsPrincipal user) => user.FindFirst(ClaimTypes.Role)?.Value ?? "";
    public static IResult Require(HttpContext context, string module, string action) => Has(context.User, module, action) ? Results.Ok() : Results.Forbid();
}

public static class CatalogPermissionGuard
{
    public static bool CanOpenAdmin(ClaimsPrincipal user)
    {
        if (AccessControl.IsSuperAdmin(user)) return true;
        if (!string.Equals(AccessControl.Role(user), "Admin", StringComparison.Ordinal)) return false;
        return new[]
        {
            AccessPermissions.Catalog, AccessPermissions.Prices, AccessPermissions.Settings, AccessPermissions.Inquiries, AccessPermissions.Service, AccessPermissions.Backup, AccessPermissions.System, AccessPermissions.Users, AccessPermissions.Warranties, AccessPermissions.Technical
        }.Any(m => AccessControl.Has(user, m, "view"));
    }

    public static bool CanSave(ClaimsPrincipal user, JsonObject before, JsonObject after)
    {
        if (AccessControl.IsSuperAdmin(user)) return true;
        if (!JsonNode.DeepEquals(before["settings"], after["settings"]) && !AccessControl.Has(user, AccessPermissions.Settings, "edit")) return false;
        if (!JsonNode.DeepEquals(before["categories"], after["categories"]) && !AccessControl.Has(user, AccessPermissions.Catalog, "edit")) return false;
        var oldProducts = before["products"] as JsonArray ?? new JsonArray();
        var newProducts = after["products"] as JsonArray ?? new JsonArray();
        if (!JsonNode.DeepEquals(ProductProjection(oldProducts, includePrices: false), ProductProjection(newProducts, includePrices: false)) && !AccessControl.Has(user, AccessPermissions.Catalog, "edit")) return false;
        if (!JsonNode.DeepEquals(ProductProjection(oldProducts, includePrices: true), ProductProjection(newProducts, includePrices: true)) && !AccessControl.Has(user, AccessPermissions.Prices, "edit")) return false;
        return true;
    }

    static JsonArray ProductProjection(JsonArray products, bool includePrices)
    {
        var rows = new JsonArray();
        foreach (var node in products)
        {
            if (node is not JsonObject p) continue;
            if (includePrices) rows.Add(new JsonObject { ["id"] = p["id"]?.ToString() ?? "", ["euro"] = p["euro"]?.DeepClone() ?? 0m, ["tl"] = p["tl"]?.DeepClone() ?? 0m, ["vatRate"] = p["vatRate"]?.DeepClone() ?? 20m });
            else
            {
                var clone = p.DeepClone().AsObject(); clone.Remove("euro"); clone.Remove("tl"); clone.Remove("vatRate"); rows.Add(clone);
            }
        }
        return rows;
    }
}
