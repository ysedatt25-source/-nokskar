using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public static class InquiryAttachments
{
    public const int MaxFiles = 5;
    public const long MaxFileSize = 20L * 1024 * 1024;
    public const long MaxRequestSize = 110L * 1024 * 1024;

    public static async Task<JsonArray> SaveAsync(IFormFileCollection files, string dataPath, CancellationToken cancellationToken)
    {
        if (files.Count > MaxFiles) throw new ArgumentException("En fazla 5 proje dosyası ekleyebilirsiniz.");
        var attachments = new JsonArray();
        var savedNames = new List<string>();
        var directory = Path.Combine(dataPath, "inquiry-files");
        try
        {
            foreach (var file in files)
            {
                if (file.Length is <= 0 or > MaxFileSize)
                    throw new ArgumentException("Her PDF veya görsel en fazla 20 MB olabilir.");
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                var bytes = stream.ToArray();
                var detected = Uploads.Detect(bytes) ?? throw new ArgumentException("PDF, JPG, PNG veya WebP dosyası seçin.");
                if (detected.Mime is not ("application/pdf" or "image/jpeg" or "image/png" or "image/webp"))
                    throw new ArgumentException("Yalnız PDF, JPG, PNG ve WebP dosyaları kabul edilir.");
                var storedName = Guid.NewGuid().ToString("N") + detected.Extension;
                Directory.CreateDirectory(directory);
                await File.WriteAllBytesAsync(Path.Combine(directory, storedName), bytes, cancellationToken);
                savedNames.Add(storedName);
                var originalName = Path.GetFileName(file.FileName).Trim();
                if (originalName.Length > 160) originalName = originalName[..160];
                attachments.Add(new JsonObject
                {
                    ["id"] = Guid.NewGuid().ToString("N"),
                    ["originalName"] = string.IsNullOrWhiteSpace(originalName) ? "Proje belgesi" : originalName,
                    ["storedName"] = storedName,
                    ["mime"] = detected.Mime,
                    ["size"] = bytes.LongLength,
                    ["uploadedUtc"] = DateTimeOffset.UtcNow.ToString("O")
                });
            }
            return attachments;
        }
        catch
        {
            foreach (var name in savedNames)
                try { File.Delete(Path.Combine(directory, name)); } catch { }
            throw;
        }
    }

    public static void Cleanup(JsonArray attachments, string dataPath)
    {
        foreach (var item in attachments)
        {
            var name = item?["storedName"]?.ToString() ?? "";
            if (!Regex.IsMatch(name, @"^[a-f0-9]{32}\.(png|jpg|webp|pdf)$")) continue;
            try { File.Delete(Path.Combine(dataPath, "inquiry-files", name)); } catch { }
        }
    }

    public static IResult Open(string inquiryId, string attachmentId, HttpContext context, Store store, string dataPath, bool download)
    {
        if (!AccessControl.Has(context.User, AccessPermissions.Inquiries, "view")) return Results.Forbid();
        var inquiry = store.InquiryById(inquiryId);
        if (inquiry == null) return Results.NotFound();
        var data = JsonNode.Parse(inquiry["data"]?.ToString() ?? "{}")?.AsObject();
        if (data?["type"]?.ToString() != "support") return Results.NotFound();
        var file = (data["attachments"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(x => x["id"]?.ToString() == attachmentId);
        if (file == null) return Results.NotFound();
        var name = file["storedName"]?.ToString() ?? "";
        if (!Regex.IsMatch(name, @"^[a-f0-9]{32}\.(png|jpg|webp|pdf)$")) return Results.NotFound();
        var path = Path.Combine(dataPath, "inquiry-files", name);
        if (!File.Exists(path)) return Results.NotFound();
        var mime = file["mime"]?.ToString() ?? "application/octet-stream";
        if (mime is not ("application/pdf" or "image/jpeg" or "image/png" or "image/webp")) return Results.NotFound();
        var filename = file["originalName"]?.ToString() ?? "proje-belgesi";
        // Inline preview or explicit attachment download, both strictly behind authenticated inquiry-view rights.
        return download
            ? Results.File(path, mime, filename, enableRangeProcessing: true)
            : Results.File(path, mime, enableRangeProcessing: true);
    }
}
