using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class MailQueue : BackgroundService
{
    readonly string outbox;
    readonly MailSettingsStore settings;
    readonly ILogger<MailQueue> logger;
    readonly object gate = new();
    string lastError = "";
    DateTimeOffset? lastSuccess;

    public MailQueue(IWebHostEnvironment env, IConfiguration config, MailSettingsStore settings, ILogger<MailQueue> logger)
    {
        this.settings = settings;
        this.logger = logger;
        var dataPath = Path.GetFullPath(config["Storage:Path"] ?? "App_Data", env.ContentRootPath);
        outbox = Path.Combine(dataPath, "mail-outbox");
        Directory.CreateDirectory(outbox);
    }

    public bool IsConfigured => settings.Snapshot().IsConfigured;

    public string AdminAddress
    {
        get
        {
            var value = settings.Snapshot().AdminAddress;
            return MailAddress.TryCreate(value, out var configured) ? configured.Address : "";
        }
    }

    public void Enqueue(string to, string subject, string html, string text)
    {
        if (!MailAddress.TryCreate(to, out var address)) return;
        var job = new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString("N"),
            ["to"] = address.Address,
            ["subject"] = subject.Trim(),
            ["html"] = html,
            ["text"] = text,
            ["createdUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["attempts"] = 0,
            ["lastError"] = ""
        };
        var file = Path.Combine(outbox, job["id"]!.ToString() + ".json");
        var temp = file + ".tmp";
        lock (gate)
        {
            File.WriteAllText(temp, job.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, file, true);
        }
    }

    public JsonObject Status()
    {
        lock (gate)
        {
            var current = settings.Snapshot();
            return new JsonObject
            {
                ["configured"] = current.IsConfigured,
                ["enabled"] = current.Enabled,
                ["provider"] = current.Provider,
                ["fromAddress"] = current.FromAddress,
                ["pending"] = Directory.Exists(outbox) ? Directory.EnumerateFiles(outbox, "*.json", SearchOption.TopDirectoryOnly).Count() : 0,
                ["lastSuccess"] = lastSuccess?.ToString("O") ?? "",
                ["lastError"] = lastError,
                ["adminAddress"] = AdminAddress
            };
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try { if (IsConfigured) await ProcessPending(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { lock (gate) lastError = ex.Message; logger.LogWarning(ex, "E-posta kuyruğu işlenemedi."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    async Task ProcessPending(CancellationToken token)
    {
        foreach (var file in Directory.EnumerateFiles(outbox, "*.json", SearchOption.TopDirectoryOnly).OrderBy(x => x).Take(20))
        {
            token.ThrowIfCancellationRequested();
            JsonObject job;
            try { job = JsonNode.Parse(await File.ReadAllTextAsync(file, token))?.AsObject() ?? throw new InvalidDataException("E-posta kuyruğu kaydı geçersiz."); }
            catch (Exception ex) { logger.LogWarning(ex, "Bozuk e-posta kuyruğu kaydı atlandı: {File}", Path.GetFileName(file)); continue; }

            var attempts = job["attempts"]?.GetValue<int>() ?? 0;
            if (attempts >= 20) continue;
            try
            {
                await Send(job, settings.Snapshot(), token);
                File.Delete(file);
                lock (gate) { lastSuccess = DateTimeOffset.UtcNow; lastError = ""; }
            }
            catch (Exception ex)
            {
                attempts++;
                job["attempts"] = attempts;
                job["lastError"] = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                await File.WriteAllTextAsync(file, job.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), token);
                lock (gate) lastError = ex.Message;
                logger.LogWarning(ex, "E-posta gönderilemedi; kuyrukta tutuluyor. Deneme: {Attempts}", attempts);
            }
        }
    }

    public async Task SendTestAsync(string to, CancellationToken token = default)
    {
        if (!MailAddress.TryCreate(to, out var address)) throw new ArgumentException("Geçerli bir test e-posta adresi girin.");
        var current = settings.Snapshot();
        if (!current.IsConfigured) throw new InvalidOperationException("E-posta sağlayıcısı henüz eksiksiz ve aktif yapılandırılmamış.");
        var job = new JsonObject
        {
            ["to"] = address.Address,
            ["subject"] = "İNOKSKAR e-posta bağlantı testi",
            ["html"] = "<div style='font-family:Arial,sans-serif;line-height:1.6;color:#102a51'><h2>Bağlantı başarılı</h2><p>Bu e-posta, İnokskar yönetim panelindeki sağlayıcı bağlantı testinden gönderildi.</p></div>",
            ["text"] = "İNOKSKAR e-posta bağlantı testi başarılı."
        };
        try
        {
            await Send(job, current, token);
            lock (gate) { lastSuccess = DateTimeOffset.UtcNow; lastError = ""; }
        }
        catch (Exception ex)
        {
            lock (gate) lastError = ex.Message;
            throw;
        }
    }

    async Task Send(JsonObject job, MailSettingsSnapshot current, CancellationToken token)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(current.FromAddress, current.FromName),
            Subject = job["subject"]?.ToString() ?? "İNOKSKAR",
            Body = job["html"]?.ToString() ?? "",
            IsBodyHtml = true,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };
        message.To.Add(job["to"]?.ToString() ?? throw new InvalidDataException("Alıcı e-posta bulunamadı."));
        using var client = new SmtpClient(current.Host, current.Port)
        {
            EnableSsl = current.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(current.Username) ? null : new NetworkCredential(current.Username, current.Password),
            Timeout = 15000
        };
        token.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
        token.ThrowIfCancellationRequested();
    }
}

public static class MailTemplates
{
    static string E(string? value) => System.Net.WebUtility.HtmlEncode(value ?? "");
    public static (string Subject, string Html, string Text) AdminRequest(JsonObject request, int count, string? adminUrl)
    {
        var service = string.Equals(request["type"]?.ToString(), "service", StringComparison.OrdinalIgnoreCase);
        var kind = service ? "servis" : "destek";
        var business = request["businessName"]?.ToString();
        var name = request["name"]?.ToString() ?? "Müşteri";
        var subject = $"{count} kişi servis veya destek talebinde bulundu";
        var link = !string.IsNullOrWhiteSpace(adminUrl) ? $"<p><a href='{E(adminUrl)}'>Yönetim panelinde talepleri görüntüleyin</a></p>" : "";
        var html = $"<div style='font-family:Arial,sans-serif;line-height:1.6;color:#102a51'><h2>Yeni {kind} talebi</h2><p><strong>{E(name)}</strong>{(string.IsNullOrWhiteSpace(business) ? "" : " · " + E(business))} yeni bir {kind} talebi oluşturdu.</p><p>Toplam kayıtlı servis/destek talebi: <strong>{count}</strong></p><p>{E(request["message"]?.ToString())}</p>{link}</div>";
        var text = $"Yeni {kind} talebi\n{name}\nToplam kayıt: {count}\n{request["message"]}";
        return (subject, html, text);
    }

    public static (string Subject, string Html, string Text) CustomerReceipt(JsonObject request)
    {
        var service = string.Equals(request["type"]?.ToString(), "service", StringComparison.OrdinalIgnoreCase);
        var kind = service ? "servis" : "destek";
        var id = request["requestCode"]?.ToString() ?? "";
        var name = request["name"]?.ToString() ?? "";
        var subject = service ? "Servis talebiniz alındı | İNOKSKAR" : "Destek talebiniz alındı | İNOKSKAR";
        var html = $"<div style='font-family:Arial,sans-serif;line-height:1.7;color:#102a51'><h2>Talebiniz başarıyla alındı</h2><p>Sayın {E(name)},</p><p>İNOKSKAR {kind} talebiniz kayıt altına alınmıştır. Ekibimiz talebinizi inceleyerek sizinle iletişime geçecektir.</p><p><strong>Talep numarası:</strong> {E(id)}</p><p>Bu e-posta talebinizin alındığını teyit eder; otomatik olarak oluşturulmuştur.</p><p>Saygılarımızla,<br><strong>İNOKSKAR Soğutma ve Endüstriyel Mutfak</strong></p></div>";
        var text = $"Sayın {name},\n\n{kind} talebiniz kayıt altına alınmıştır. Talep numarası: {id}. Ekibimiz sizinle iletişime geçecektir.\n\nİNOKSKAR";
        return (subject, html, text);
    }

    public static (string Subject, string Html, string Text) PasswordReset(string resetUrl)
    {
        var safeUrl = E(resetUrl);
        var subject = "Şifre yenileme bağlantınız | İNOKSKAR";
        var html = $"<div style='font-family:Arial,sans-serif;line-height:1.7;color:#102a51;max-width:620px'><h2>Şifre yenileme isteği</h2><p>İNOKSKAR hesabınız için bir şifre yenileme isteği aldık.</p><p><a href='{safeUrl}' style='display:inline-block;padding:12px 18px;border-radius:9px;background:#1455c0;color:#fff;text-decoration:none;font-weight:700'>Şifremi yenile</a></p><p>Bu bağlantı <strong>30 dakika</strong> boyunca ve yalnızca bir kez kullanılabilir.</p><p>Bu isteği siz yapmadıysanız e-postayı yok sayabilirsiniz; mevcut şifreniz değişmez.</p><p>Saygılarımızla,<br><strong>İNOKSKAR Soğutma ve Endüstriyel Mutfak</strong></p></div>";
        var text = $"İNOKSKAR şifre yenileme isteği. Bağlantı 30 dakika boyunca ve yalnızca bir kez geçerlidir:\n{resetUrl}\n\nBu isteği siz yapmadıysanız e-postayı yok sayabilirsiniz.";
        return (subject, html, text);
    }
}
