using System.Diagnostics;
using System.Text;

public static class StartupRecovery
{
    public static void Run(Exception error, string[] args)
    {
        var now = DateTimeOffset.Now;
        var directory = ResolveLogDirectory();
        string logPath = "";
        string htmlPath = "";
        try
        {
            Directory.CreateDirectory(directory);
            logPath = Path.Combine(directory, "startup-fatal.log");
            var report = BuildText(error, now, args);
            File.AppendAllText(logPath, report + Environment.NewLine + new string('-', 90) + Environment.NewLine, Encoding.UTF8);
            htmlPath = Path.Combine(directory, "startup-failure.html");
            File.WriteAllText(htmlPath, BuildHtml(error, now, logPath), Encoding.UTF8);
        }
        catch
        {
            // Son çare: mevcut çalışma klasörünü kullan.
            try
            {
                directory = AppContext.BaseDirectory;
                logPath = Path.Combine(directory, "startup-fatal.log");
                File.AppendAllText(logPath, BuildText(error, now, args), Encoding.UTF8);
                htmlPath = Path.Combine(directory, "startup-failure.html");
                File.WriteAllText(htmlPath, BuildHtml(error, now, logPath), Encoding.UTF8);
            }
            catch { }
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine();
        Console.WriteLine("İNOKSKAR başlangıç güvenlik/kurtarma modu devreye girdi.");
        Console.ResetColor();
        Console.WriteLine("Uygulama genel .NET çıkış hatasıyla kapatılmadı.");
        Console.WriteLine("Ayrıntılı başlangıç kaydı: " + (string.IsNullOrWhiteSpace(logPath) ? "oluşturulamadı" : logPath));
        Console.WriteLine("Hata: " + error.GetType().Name + " - " + error.Message);

        if (!string.IsNullOrWhiteSpace(htmlPath) && File.Exists(htmlPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = htmlPath, UseShellExecute = true });
            }
            catch { }
        }

        // Visual Studio'nun 0xe0434352 genel kapanış penceresine düşmemesi için
        // süreç bilinçli olarak canlı tutulur. Kullanıcı Visual Studio'dan Durdur ile kapatır.
        Thread.Sleep(Timeout.Infinite);
    }

    static string ResolveLogDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("INOKSKAR_STARTUP_LOG_DIR");
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
        if (OperatingSystem.IsWindows())
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(local)) return Path.Combine(local, "Inokskar", "Logs");
        }
        return Path.Combine(AppContext.BaseDirectory, "startup-logs");
    }

    static string BuildText(Exception error, DateTimeOffset now, string[] args)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{now:O}] INOKSKAR başlangıç hatası");
        sb.AppendLine("BaseDirectory: " + AppContext.BaseDirectory);
        sb.AppendLine("CurrentDirectory: " + Environment.CurrentDirectory);
        sb.AppendLine("OS: " + Environment.OSVersion);
        sb.AppendLine("Framework: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        sb.AppendLine("Args: " + string.Join(" ", args ?? Array.Empty<string>()));
        sb.AppendLine();
        sb.AppendLine(error.ToString());
        return sb.ToString();
    }

    static string BuildHtml(Exception error, DateTimeOffset now, string logPath)
    {
        static string E(string? value) => System.Net.WebUtility.HtmlEncode(value ?? "");
        return """
<!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>İNOKSKAR Başlangıç Kurtarma</title>
<style>body{font:16px system-ui;background:#f5f7fb;color:#102a51;margin:0;padding:36px}.card{max-width:900px;margin:auto;background:#fff;border:1px solid #dbe4ef;border-radius:16px;padding:28px;box-shadow:0 18px 50px #102a5112}h1{margin-top:0}.ok{background:#eaf6ef;color:#17663f;padding:12px 14px;border-radius:10px}.err{background:#fff1ef;color:#8e2b20;padding:14px;border-radius:10px}pre{white-space:pre-wrap;word-break:break-word;background:#0f1f33;color:#e9f1fb;padding:16px;border-radius:10px;max-height:420px;overflow:auto}.muted{color:#60748d}</style></head><body><main class="card"><h1>İNOKSKAR başlangıç kurtarma modu</h1><p class="ok">Uygulama genel .NET çökmesiyle kapatılmadı. Hata güvenli biçimde yakalandı ve kaydedildi.</p><p class="err"><strong>Hata:</strong> __ERROR__</p><p><strong>Zaman:</strong> __TIME__</p><p><strong>Log:</strong> <code>__LOG__</code></p><p class="muted">Bu dosyayı veya log kaydını paylaşarak başlangıç sorununu doğrudan teşhis edebilirsiniz. Visual Studio'da Durdur düğmesiyle kurtarma sürecini kapatabilirsiniz.</p><details><summary>Teknik ayrıntı</summary><pre>__DETAIL__</pre></details></main></body></html>
""".Replace("__ERROR__", E(error.GetType().Name + " - " + error.Message))
   .Replace("__TIME__", E(now.ToString("O")))
   .Replace("__LOG__", E(logPath))
   .Replace("__DETAIL__", E(error.ToString()));
    }
}
