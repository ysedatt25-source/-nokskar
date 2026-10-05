using System.Text.Json.Nodes;

public static class SystemStatus
{
    public static JsonObject Backup(string dataPath)
    {
        try
        {
            var directory = Path.Combine(dataPath, "backups");
            if (!Directory.Exists(directory)) return new JsonObject { ["count"] = 0, ["latest"] = "", ["latestBytes"] = 0L };
            var files = Directory.EnumerateFiles(directory, "inokskar-auto-*.zip", SearchOption.TopDirectoryOnly)
                .Select(x => new FileInfo(x)).OrderByDescending(x => x.LastWriteTimeUtc).ToArray();
            var latest = files.FirstOrDefault();
            return new JsonObject
            {
                ["count"] = files.Length,
                ["latest"] = latest == null ? "" : new DateTimeOffset(latest.LastWriteTimeUtc, TimeSpan.Zero).ToString("O"),
                ["latestBytes"] = latest?.Length ?? 0L
            };
        }
        catch
        {
            return new JsonObject { ["count"] = 0, ["latest"] = "", ["latestBytes"] = 0L };
        }
    }
}
