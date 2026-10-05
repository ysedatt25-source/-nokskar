using System.Text.Json;

public sealed class AdminAuthState
{
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string RecoveryCodeHash { get; set; } = "";
}

public static class AdminAuth
{
    static readonly object Gate = new();

    public static AdminAuthState Load(string dataPath, IConfiguration config)
    {
        lock (Gate)
        {
            var fallback = new AdminAuthState
            {
                Email = (config["Admin:Email"] ?? "").Trim(),
                PasswordHash = config["Admin:PasswordHash"] ?? "",
                RecoveryCodeHash = config["Admin:RecoveryCodeHash"] ?? ""
            };
            var file = Path.Combine(dataPath, "admin-auth.json");
            if (!File.Exists(file)) return fallback;
            try
            {
                var stored = JsonSerializer.Deserialize<AdminAuthState>(File.ReadAllText(file)) ?? new AdminAuthState();
                if (string.IsNullOrWhiteSpace(stored.Email)) stored.Email = fallback.Email;
                if (string.IsNullOrWhiteSpace(stored.PasswordHash)) stored.PasswordHash = fallback.PasswordHash;
                if (string.IsNullOrWhiteSpace(stored.RecoveryCodeHash)) stored.RecoveryCodeHash = fallback.RecoveryCodeHash;
                return stored;
            }
            catch
            {
                // Bozuk yerel kimlik dosyası uygulamanın açılmasını engellemez; appsettings değerlerine geri dönülür.
                return fallback;
            }
        }
    }

    public static bool Verify(AdminAuthState auth, string suppliedEmail, string password) =>
        !string.IsNullOrWhiteSpace(auth.Email)
        && !string.IsNullOrWhiteSpace(auth.PasswordHash)
        && string.Equals((suppliedEmail ?? "").Trim(), auth.Email, StringComparison.OrdinalIgnoreCase)
        && Passwords.Verify(password ?? "", auth.PasswordHash);

    public static bool HasRecoveryCode(AdminAuthState auth) => !string.IsNullOrWhiteSpace(auth.RecoveryCodeHash);

    public static void ChangePassword(string dataPath, IConfiguration config, string currentPassword, string newPassword)
    {
        lock (Gate)
        {
            var auth = Load(dataPath, config);
            if (!Passwords.Verify(currentPassword ?? "", auth.PasswordHash)) throw new ArgumentException("Mevcut şifre hatalı.");
            ValidateNewPassword(newPassword);
            if (Passwords.Verify(newPassword, auth.PasswordHash)) throw new ArgumentException("Yeni şifre mevcut şifreyle aynı olamaz.");
            auth.PasswordHash = Passwords.Hash(newPassword);
            Persist(dataPath, auth);
        }
    }

    public static void ResetWithRecoveryCode(string dataPath, IConfiguration config, string suppliedEmail, string recoveryCode, string newPassword)
    {
        lock (Gate)
        {
            var auth = Load(dataPath, config);
            if (!string.Equals((suppliedEmail ?? "").Trim(), auth.Email, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(auth.RecoveryCodeHash)
                || !Passwords.Verify(recoveryCode ?? "", auth.RecoveryCodeHash))
                throw new ArgumentException("E-posta veya kurtarma kodu hatalı.");
            ValidateNewPassword(newPassword);
            auth.PasswordHash = Passwords.Hash(newPassword);
            Persist(dataPath, auth);
        }
    }

    public static void ResetPasswordByVerifiedEmail(string dataPath, IConfiguration config, string email, string newPassword)
    {
        lock (Gate)
        {
            var auth = Load(dataPath, config);
            if (!string.Equals((email ?? "").Trim(), auth.Email, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Hesap bulunamadı.");
            ValidateNewPassword(newPassword);
            auth.PasswordHash = Passwords.Hash(newPassword);
            Persist(dataPath, auth);
        }
    }

    static void ValidateNewPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 128)
            throw new ArgumentException("Yeni şifre 8 ile 128 karakter arasında olmalıdır.");
    }

    static void Persist(string dataPath, AdminAuthState auth)
    {
        try
        {
            Directory.CreateDirectory(dataPath);
            var file = Path.Combine(dataPath, "admin-auth.json");
            var temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(auth, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, file, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException("Yeni şifre kaydedilemedi. App_Data klasörünün yazma iznini kontrol edin.");
        }
    }
}
