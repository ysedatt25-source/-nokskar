using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.DataProtection;

public sealed record MailProviderPreset(string Id, string Name, string Host, int Port, bool EnableSsl, string UsernameHint = "", string HelpText = "");

public sealed record MailSettingsSnapshot(
    string Provider,
    bool Enabled,
    string Host,
    int Port,
    bool EnableSsl,
    string Username,
    string Password,
    string FromAddress,
    string FromName,
    string AdminAddress,
    bool PasswordConfigured)
{
    public bool RequiresCredentials => !string.Equals(Provider, "smtp", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(Username);
    public bool IsConfigured => Enabled
        && !string.IsNullOrWhiteSpace(Host)
        && Port is > 0 and <= 65535
        && MailAddress.TryCreate(FromAddress, out _)
        && (!RequiresCredentials || (!string.IsNullOrWhiteSpace(Username) && PasswordConfigured));
}

public sealed class MailSettingsStore
{
    readonly string file;
    readonly IConfiguration config;
    readonly IDataProtector protector;
    readonly object gate = new();

    public static IReadOnlyList<MailProviderPreset> Providers { get; } = new[]
    {
        new MailProviderPreset("gmail", "Gmail", "smtp.gmail.com", 587, true, "ornek@gmail.com"),
        new MailProviderPreset("microsoft365", "Microsoft 365", "smtp.office365.com", 587, true, "ornek@firma.com"),
        new MailProviderPreset("outlook", "Outlook.com / Hotmail", "smtp-mail.outlook.com", 587, true, "ornek@outlook.com"),
        new MailProviderPreset("yandex", "Yandex Mail", "smtp.yandex.com", 587, true, "ornek@yandex.com"),
        new MailProviderPreset("zoho", "Zoho Mail", "smtp.zoho.com", 587, true, "ornek@firma.com"),
        new MailProviderPreset("brevo", "Brevo", "smtp-relay.brevo.com", 587, true, "Brevo SMTP kullanıcı adı"),
        new MailProviderPreset("mailjet", "Mailjet", "in-v3.mailjet.com", 587, true, "Mailjet API Key", "Mailjet seçildiğinde gönderim HTTPS API üzerinden yapılır. Kullanıcı adı alanına API Key, parola alanına Secret Key girin; SMTP port engellerinden etkilenmez."),
        new MailProviderPreset("sendgrid", "SendGrid", "smtp.sendgrid.net", 587, true, "apikey"),
        new MailProviderPreset("amazon-ses", "Amazon SES", "email-smtp.eu-central-1.amazonaws.com", 587, true, "SES SMTP kullanıcı adı"),
        new MailProviderPreset("mailgun", "Mailgun", "smtp.mailgun.org", 587, true, "postmaster@alanadiniz.com"),
        new MailProviderPreset("smtp", "Genel / Özel SMTP", "", 587, true, "SMTP kullanıcı adı")
    };

    public MailSettingsStore(IWebHostEnvironment env, IConfiguration config, IDataProtectionProvider dataProtection)
    {
        this.config = config;
        var dataPath = Path.GetFullPath(config["Storage:Path"] ?? "App_Data", env.ContentRootPath);
        Directory.CreateDirectory(dataPath);
        file = Path.Combine(dataPath, "mail-settings.json");
        protector = dataProtection.CreateProtector("INOKSKAR.MailSettings.v1");
    }

    public MailSettingsSnapshot Snapshot()
    {
        lock (gate)
        {
            var stored = LoadStored();
            var provider = Text(stored, "provider", config["Mail:Provider"] ?? GuessProvider(config["Mail:Host"]));
            var enabled = Bool(stored, "enabled", BoolConfig("Mail:Enabled", false));
            var host = Text(stored, "host", config["Mail:Host"] ?? "");
            var port = Int(stored, "port", int.TryParse(config["Mail:Port"], out var configuredPort) ? configuredPort : 587);
            var enableSsl = Bool(stored, "enableSsl", BoolConfig("Mail:EnableSsl", true));
            var username = Text(stored, "username", config["Mail:Username"] ?? "");
            var fromAddress = Text(stored, "fromAddress", config["Mail:FromAddress"] ?? "");
            var fromName = Text(stored, "fromName", config["Mail:FromName"] ?? "İNOKSKAR");
            var adminAddress = Text(stored, "adminAddress", config["Mail:AdminAddress"] ?? config["Admin:Email"] ?? "");

            var password = Environment.GetEnvironmentVariable("INOKSKAR_MAIL_PASSWORD") ?? "";
            if (string.IsNullOrWhiteSpace(password))
            {
                var protectedPassword = stored?["protectedPassword"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(protectedPassword))
                {
                    try { password = protector.Unprotect(protectedPassword); }
                    catch { password = ""; }
                }
            }
            if (string.IsNullOrWhiteSpace(password)) password = config["Mail:Password"] ?? "";

            return new MailSettingsSnapshot(provider, enabled, host, port, enableSsl, username, password, fromAddress, fromName, adminAddress, !string.IsNullOrWhiteSpace(password));
        }
    }

    public MailSettingsSnapshot Save(JsonObject input)
    {
        lock (gate)
        {
            var currentStored = LoadStored() ?? new JsonObject();
            var provider = (input["provider"]?.ToString() ?? "smtp").Trim().ToLowerInvariant();
            if (!Providers.Any(x => x.Id == provider)) throw new ArgumentException("E-posta sağlayıcısı geçersiz.");
            var host = Clean(input["host"], 240, "SMTP sunucusu");
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("SMTP sunucusu gereklidir.");
            if (!int.TryParse(input["port"]?.ToString(), out var port) || port is < 1 or > 65535) throw new ArgumentException("SMTP portu geçersiz.");
            var username = Clean(input["username"], 320, "SMTP kullanıcı adı", allowEmpty: true);
            var fromAddress = CleanEmail(input["fromAddress"], "Gönderen e-posta");
            var adminAddress = CleanEmail(input["adminAddress"], "Yönetici e-posta");
            var fromName = Clean(input["fromName"], 180, "Gönderen adı");
            var enabled = input["enabled"]?.GetValue<bool>() ?? false;
            var enableSsl = input["enableSsl"]?.GetValue<bool>() ?? true;
            var newPassword = input["password"]?.ToString() ?? "";
            if (newPassword.Length > 512) throw new ArgumentException("SMTP parolası çok uzun.");

            var next = new JsonObject
            {
                ["provider"] = provider,
                ["enabled"] = enabled,
                ["host"] = host,
                ["port"] = port,
                ["enableSsl"] = enableSsl,
                ["username"] = username,
                ["fromAddress"] = fromAddress,
                ["fromName"] = fromName,
                ["adminAddress"] = adminAddress,
                ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O")
            };

            if (!string.IsNullOrWhiteSpace(newPassword)) next["protectedPassword"] = protector.Protect(newPassword);
            else if (!string.IsNullOrWhiteSpace(currentStored["protectedPassword"]?.ToString())) next["protectedPassword"] = currentStored["protectedPassword"]!.ToString();

            var hasPassword = !string.IsNullOrWhiteSpace(newPassword)
                || !string.IsNullOrWhiteSpace(next["protectedPassword"]?.ToString())
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INOKSKAR_MAIL_PASSWORD"))
                || !string.IsNullOrWhiteSpace(config["Mail:Password"]);
            var requiresCredentials = provider != "smtp" || !string.IsNullOrWhiteSpace(username);
            if (enabled && requiresCredentials && string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Seçilen sağlayıcı için SMTP kullanıcı adı veya API Key gereklidir.");
            if (enabled && requiresCredentials && !hasPassword)
                throw new ArgumentException("Seçilen sağlayıcı için SMTP parolası veya Secret Key gereklidir.");

            var temp = file + ".tmp";
            File.WriteAllText(temp, next.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, file, true);
            return SnapshotUnlocked(next);
        }
    }

    public JsonObject PublicSettings()
    {
        var s = Snapshot();
        return new JsonObject
        {
            ["provider"] = s.Provider,
            ["enabled"] = s.Enabled,
            ["host"] = s.Host,
            ["port"] = s.Port,
            ["enableSsl"] = s.EnableSsl,
            ["username"] = s.Username,
            ["fromAddress"] = s.FromAddress,
            ["fromName"] = s.FromName,
            ["adminAddress"] = s.AdminAddress,
            ["passwordConfigured"] = s.PasswordConfigured,
            ["configured"] = s.IsConfigured
        };
    }

    public static JsonArray ProviderJson() => new(Providers.Select(p => (JsonNode)new JsonObject
    {
        ["id"] = p.Id,
        ["name"] = p.Name,
        ["host"] = p.Host,
        ["port"] = p.Port,
        ["enableSsl"] = p.EnableSsl,
        ["usernameHint"] = p.UsernameHint,
        ["helpText"] = p.HelpText
    }).ToArray());

    MailSettingsSnapshot SnapshotUnlocked(JsonObject stored)
    {
        var provider = Text(stored, "provider", "smtp");
        var enabled = Bool(stored, "enabled", false);
        var host = Text(stored, "host", "");
        var port = Int(stored, "port", 587);
        var enableSsl = Bool(stored, "enableSsl", true);
        var username = Text(stored, "username", "");
        var fromAddress = Text(stored, "fromAddress", "");
        var fromName = Text(stored, "fromName", "İNOKSKAR");
        var adminAddress = Text(stored, "adminAddress", config["Admin:Email"] ?? "");
        var password = Environment.GetEnvironmentVariable("INOKSKAR_MAIL_PASSWORD") ?? "";
        if (string.IsNullOrWhiteSpace(password))
        {
            var protectedPassword = stored["protectedPassword"]?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(protectedPassword))
            {
                try { password = protector.Unprotect(protectedPassword); } catch { password = ""; }
            }
        }
        if (string.IsNullOrWhiteSpace(password)) password = config["Mail:Password"] ?? "";
        return new MailSettingsSnapshot(provider, enabled, host, port, enableSsl, username, password, fromAddress, fromName, adminAddress, !string.IsNullOrWhiteSpace(password));
    }

    JsonObject? LoadStored()
    {
        if (!File.Exists(file)) return null;
        try { return JsonNode.Parse(File.ReadAllText(file))?.AsObject(); }
        catch { return null; }
    }

    bool BoolConfig(string key, bool fallback) => bool.TryParse(config[key], out var value) ? value : fallback;
    static string Text(JsonObject? obj, string key, string fallback) => (obj?[key]?.ToString() ?? fallback).Trim();
    static bool Bool(JsonObject? obj, string key, bool fallback) => obj?[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
    static int Int(JsonObject? obj, string key, int fallback) => int.TryParse(obj?[key]?.ToString(), out var n) ? n : fallback;
    static string Clean(JsonNode? node, int max, string label, bool allowEmpty = false)
    {
        var value = (node?.ToString() ?? "").Trim();
        if (!allowEmpty && string.IsNullOrWhiteSpace(value)) throw new ArgumentException(label + " gereklidir.");
        if (value.Length > max) throw new ArgumentException(label + " çok uzun.");
        return value;
    }
    static string CleanEmail(JsonNode? node, string label)
    {
        var value = (node?.ToString() ?? "").Trim();
        if (!MailAddress.TryCreate(value, out var address)) throw new ArgumentException(label + " geçerli bir e-posta adresi olmalıdır.");
        return address.Address;
    }
    static string GuessProvider(string? host)
    {
        var value = (host ?? "").ToLowerInvariant();
        if (value.Contains("gmail")) return "gmail";
        if (value.Contains("office365")) return "microsoft365";
        if (value.Contains("outlook")) return "outlook";
        if (value.Contains("yandex")) return "yandex";
        if (value.Contains("zoho")) return "zoho";
        if (value.Contains("brevo")) return "brevo";
        if (value.Contains("mailjet")) return "mailjet";
        if (value.Contains("sendgrid")) return "sendgrid";
        if (value.Contains("amazonaws")) return "amazon-ses";
        if (value.Contains("mailgun")) return "mailgun";
        return "smtp";
    }
}
