using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public sealed record PasswordResetTarget(string Email, string Kind, string Stamp);

public sealed class PasswordResetStore
{
    sealed class Entry
    {
        public string TokenHash { get; set; } = "";
        public string Email { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Stamp { get; set; } = "";
        public string ExpiresUtc { get; set; } = "";
        public string CreatedUtc { get; set; } = "";
    }

    readonly object gate = new();
    readonly string file;
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    public PasswordResetStore(IWebHostEnvironment env, IConfiguration config)
    {
        var dataPath = Path.GetFullPath(config["Storage:Path"] ?? "App_Data", env.ContentRootPath);
        Directory.CreateDirectory(dataPath);
        file = Path.Combine(dataPath, "password-reset-tokens.json");
        if (!File.Exists(file)) Persist(new List<Entry>());
    }

    public string Create(string email, string kind, string stamp="")
    {
        lock (gate)
        {
            var normalized = (email ?? "").Trim();
            if (!System.Net.Mail.MailAddress.TryCreate(normalized, out var parsed)) throw new ArgumentException("Geçerli bir e-posta adresi girin.");
            if (kind is not ("superadmin" or "staff" or "customer" or "customer-assist" or "customer-verify")) throw new ArgumentException("Şifre yenileme hedefi geçersiz.");

            var rows = Load();
            var now = DateTimeOffset.UtcNow;
            rows.RemoveAll(x => !DateTimeOffset.TryParse(x.ExpiresUtc, out var expires) || expires <= now || string.Equals(x.Email, parsed.Address, StringComparison.OrdinalIgnoreCase));
            if (rows.Count > 200) rows = rows.OrderByDescending(x => x.CreatedUtc).Take(150).ToList();

            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            rows.Add(new Entry
            {
                TokenHash = Hash(token),
                Email = parsed.Address,
                Kind = kind,
                Stamp = stamp,
                CreatedUtc = now.ToString("O"),
                ExpiresUtc = now.AddMinutes(kind=="customer-assist"?15:30).ToString("O")
            });
            Persist(rows);
            return token;
        }
    }

    public PasswordResetTarget? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            var hash = Hash(token);
            var rows = Load();
            var row = rows.FirstOrDefault(x => string.Equals(x.TokenHash, hash, StringComparison.Ordinal)
                && DateTimeOffset.TryParse(x.ExpiresUtc, out var expires) && expires > now);
            return row == null ? null : new PasswordResetTarget(row.Email, row.Kind, row.Stamp);
        }
    }

    public bool Consume(string token) => ConsumeTarget(token) != null;

    public PasswordResetTarget? ConsumeTarget(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            var hash = Hash(token);
            var rows = Load();
            var index = rows.FindIndex(x => string.Equals(x.TokenHash, hash, StringComparison.Ordinal)
                && DateTimeOffset.TryParse(x.ExpiresUtc, out var expires) && expires > now);
            if (index < 0) return null;
            var row = rows[index];
            rows.RemoveAt(index);
            Persist(rows);
            return new PasswordResetTarget(row.Email, row.Kind, row.Stamp);
        }
    }

    List<Entry> Load()
    {
        try { return JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(file), JsonOptions) ?? new List<Entry>(); }
        catch { return new List<Entry>(); }
    }

    void Persist(List<Entry> rows)
    {
        var temp = file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(rows, JsonOptions));
        File.Move(temp, file, true);
    }

    static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
