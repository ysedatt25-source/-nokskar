using System.Text;
using System.Text.Json.Nodes;

public sealed class OperationalLog
{
    readonly string logDirectory;
    public OperationalLog(IWebHostEnvironment env, IConfiguration config)
    {
        var dataPath = Path.GetFullPath(config["Storage:Path"] ?? "App_Data", env.ContentRootPath);
        logDirectory = Path.Combine(dataPath, "logs");
        Directory.CreateDirectory(logDirectory);
    }
    public void Error(Exception ex, HttpContext? context = null)
    {
        var line = $"[{DateTimeOffset.UtcNow:O}] {context?.Request.Method} {context?.Request.Path} | {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{Environment.NewLine}";
        try { File.AppendAllText(Path.Combine(logDirectory, $"errors-{DateTime.UtcNow:yyyy-MM-dd}.log"), line, Encoding.UTF8); } catch { }
    }
    public JsonArray RecentErrors(int max = 30)
    {
        var rows = new JsonArray();
        try
        {
            foreach (var file in Directory.EnumerateFiles(logDirectory, "errors-*.log").OrderByDescending(x => x).Take(7))
            {
                var lines = File.ReadLines(file).Where(x => x.StartsWith("[", StringComparison.Ordinal)).TakeLast(max).Reverse();
                foreach (var line in lines) { rows.Add(line.Length > 800 ? line[..800] : line); if (rows.Count >= max) return rows; }
            }
        }
        catch { }
        return rows;
    }
}
