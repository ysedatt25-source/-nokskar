using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public sealed record CatalogBackupPackage(JsonObject Data, int SourceRevision, Dictionary<string, byte[]> Files, Dictionary<string, byte[]> TechnicalFiles, JsonArray? Warranties, JsonObject? Operations);

public static class CatalogBackup
{
    const long MaxArchiveBytes = 512L * 1024 * 1024;
    const long MaxManifestBytes = 10L * 1024 * 1024;
    const long MaxMediaBytes = 100L * 1024 * 1024;
    const int MaxEntries = 5000;
    const string FormatName = "inokskar-industrial-catalog-backup";
    static readonly Regex MediaName = new("^[a-f0-9]{32}\\.(png|jpg|webp|gif|mp4|pdf)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly Regex TechnicalName = new("^[a-f0-9]{32}\\.(png|jpg|webp|gif|pdf)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static byte[] Create(JsonObject state, string dataPath)
    {
        var data = state["data"]?.DeepClone().AsObject() ?? throw new InvalidOperationException("Katalog verisi bulunamadı.");
        CatalogRules.Validate(data);
        var revision = state["revision"]?.GetValue<int>() ?? 0;
        var warranties = state["warranties"] is JsonArray warrantyRows ? warrantyRows.DeepClone().AsArray() : new JsonArray();
        WarrantyRules.ValidateStoredCollection(warranties, data);
        var operations = new JsonObject
        {
            ["inquiries"] = state["inquiries"]?.DeepClone() ?? new JsonArray(),
            ["warrantyHistory"] = state["warrantyHistory"]?.DeepClone() ?? new JsonArray(),
            ["auditLog"] = state["auditLog"]?.DeepClone() ?? new JsonArray(),
            ["events"] = state["events"]?.DeepClone() ?? new JsonArray(),
            ["technicalProfiles"] = state["technicalProfiles"]?.DeepClone() ?? new JsonObject(),
            ["technicalHistory"] = state["technicalHistory"]?.DeepClone() ?? new JsonArray()
        };
        var uploadDirectory = Path.Combine(dataPath, "uploads");
        var media = ReferencedMedia(data).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var fileRows = new JsonArray();
        var technicalDirectory = Path.Combine(dataPath, "technical-files");
        var technicalMedia = ReferencedTechnicalMedia(operations).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var technicalFileRows = new JsonArray();

        foreach (var name in media)
        {
            var path = Path.Combine(uploadDirectory, name);
            if (!File.Exists(path)) throw new InvalidOperationException($"Yedek oluşturulamadı: kullanılan dosya bulunamadı ({name}).");
            var info = new FileInfo(path);
            if (info.Length is <= 0 or > MaxMediaBytes) throw new InvalidOperationException($"Yedek oluşturulamadı: dosya boyutu geçersiz ({name}).");
            fileRows.Add(new JsonObject
            {
                ["name"] = name,
                ["length"] = info.Length,
                ["sha256"] = HashFile(path)
            });
        }

        foreach (var name in technicalMedia)
        {
            var path = Path.Combine(technicalDirectory, name);
            if (!File.Exists(path)) throw new InvalidOperationException($"Yedek oluşturulamadı: teknik dosya bulunamadı ({name}).");
            var info = new FileInfo(path);
            if (info.Length is <= 0 or > MaxMediaBytes) throw new InvalidOperationException($"Yedek oluşturulamadı: teknik dosya boyutu geçersiz ({name}).");
            technicalFileRows.Add(new JsonObject { ["name"] = name, ["length"] = info.Length, ["sha256"] = HashFile(path) });
        }

        var manifest = new JsonObject
        {
            ["format"] = FormatName,
            ["version"] = 3,
            ["createdUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["sourceRevision"] = revision,
            ["data"] = data,
            ["warranties"] = warranties,
            ["operations"] = operations,
            ["files"] = fileRows,
            ["technicalFiles"] = technicalFileRows
        };

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("backup.json", CompressionLevel.Optimal);
            using (var writer = new StreamWriter(manifestEntry.Open(), new UTF8Encoding(false)))
                writer.Write(manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            foreach (var name in media)
            {
                var entry = archive.CreateEntry("files/" + name, CompressionLevel.Optimal);
                using var source = File.OpenRead(Path.Combine(uploadDirectory, name));
                using var target = entry.Open();
                source.CopyTo(target);
            }
            foreach (var name in technicalMedia)
            {
                var entry = archive.CreateEntry("technical-files/" + name, CompressionLevel.Optimal);
                using var source = File.OpenRead(Path.Combine(technicalDirectory, name));
                using var target = entry.Open();
                source.CopyTo(target);
            }
        }
        if (output.Length > MaxArchiveBytes) throw new InvalidOperationException("Yedek 512 MB sınırını aşıyor.");
        return output.ToArray();
    }

    public static CatalogBackupPackage Read(Stream input)
    {
        if (!input.CanRead) throw new ArgumentException("Yedek dosyası okunamıyor.");
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count is < 1 or > MaxEntries) throw new ArgumentException("Yedek ZIP içeriği geçersiz veya çok büyük.");

        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            if (string.IsNullOrWhiteSpace(name) || entry.Name.Length == 0 || name.Contains('\\') || name.StartsWith('/') || name.Contains("../", StringComparison.Ordinal) || name.Contains(':'))
                throw new ArgumentException("Yedek ZIP içinde geçersiz dosya yolu bulundu.");
            if (!string.Equals(name, "backup.json", StringComparison.OrdinalIgnoreCase) && !(name.StartsWith("files/", StringComparison.OrdinalIgnoreCase) && MediaName.IsMatch(name[6..])) && !(name.StartsWith("technical-files/", StringComparison.OrdinalIgnoreCase) && TechnicalName.IsMatch(name[16..])))
                throw new ArgumentException("Yedek ZIP yalnızca manifest, katalog medyası ve teknik servis dosyalarını içerebilir.");
            if (!entries.TryAdd(name, entry)) throw new ArgumentException("Yedek ZIP içinde yinelenen dosya adı bulundu.");
        }

        if (!entries.TryGetValue("backup.json", out var manifestEntry)) throw new ArgumentException("Yedek manifesti bulunamadı.");
        if (manifestEntry.Length is <= 0 or > MaxManifestBytes) throw new ArgumentException("Yedek manifesti boyutu geçersiz.");
        JsonObject manifest;
        using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8, true, 4096, false))
            manifest = JsonNode.Parse(reader.ReadToEnd())?.AsObject() ?? throw new ArgumentException("Yedek manifesti geçersiz.");

        var version = manifest["version"]?.GetValue<int>() ?? 0;
        if (manifest["format"]?.ToString() != FormatName || version is < 1 or > 3)
            throw new ArgumentException("Bu ZIP desteklenen İnokskar katalog yedeği değil.");
        var data = manifest["data"]?.DeepClone().AsObject() ?? throw new ArgumentException("Yedekte katalog verisi bulunamadı.");
        CatalogRules.Validate(data);
        var sourceRevision = manifest["sourceRevision"]?.GetValue<int>() ?? 0;
        var warranties = manifest["warranties"] is JsonArray warrantyRows ? warrantyRows.DeepClone().AsArray() : null;
        if (warranties != null) WarrantyRules.ValidateStoredCollection(warranties, data);
        var operations = version >= 2 && manifest["operations"] is JsonObject operationRows ? operationRows.DeepClone().AsObject() : null;
        var declared = manifest["files"]?.AsArray() ?? throw new ArgumentException("Yedek dosya listesi bulunamadı.");
        if (declared.Count > MaxEntries - 1) throw new ArgumentException("Yedekte çok fazla medya dosyası var.");

        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var rowNode in declared)
        {
            var row = rowNode?.AsObject() ?? throw new ArgumentException("Yedek dosya listesi geçersiz.");
            var name = row["name"]?.ToString() ?? "";
            if (!MediaName.IsMatch(name) || files.ContainsKey(name)) throw new ArgumentException("Yedek dosya adı geçersiz veya yinelenmiş.");
            var expectedLength = row["length"]?.GetValue<long>() ?? -1;
            var expectedHash = row["sha256"]?.ToString() ?? "";
            if (expectedLength is <= 0 or > MaxMediaBytes || !Regex.IsMatch(expectedHash, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant))
                throw new ArgumentException($"Yedek dosya bilgisi geçersiz ({name}).");
            if (!entries.TryGetValue("files/" + name, out var entry)) throw new ArgumentException($"Yedekte gerekli medya dosyası eksik ({name}).");
            if (entry.Length != expectedLength || entry.Length > MaxMediaBytes) throw new ArgumentException($"Yedek medya boyutu uyuşmuyor ({name}).");
            total += entry.Length;
            if (total > MaxArchiveBytes) throw new ArgumentException("Yedek açılmış içerik sınırını aşıyor.");
            using var source = entry.Open();
            using var ms = new MemoryStream((int)entry.Length);
            source.CopyTo(ms);
            var bytes = ms.ToArray();
            if (!string.Equals(Hash(bytes), expectedHash, StringComparison.Ordinal)) throw new ArgumentException($"Yedek medya doğrulaması başarısız ({name}).");
            var detected = Uploads.Detect(bytes) ?? throw new ArgumentException($"Yedekte desteklenmeyen medya bulundu ({name}).");
            if (!string.Equals(detected.Extension, Path.GetExtension(name), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"Yedek medya uzantısı içerikle uyuşmuyor ({name}).");
            files.Add(name, bytes);
        }

        var technicalFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var declaredTechnical = version >= 3 ? (manifest["technicalFiles"]?.AsArray() ?? new JsonArray()) : new JsonArray();
        foreach (var rowNode in declaredTechnical)
        {
            var row = rowNode?.AsObject() ?? throw new ArgumentException("Yedek teknik dosya listesi geçersiz.");
            var name = row["name"]?.ToString() ?? "";
            if (!TechnicalName.IsMatch(name) || technicalFiles.ContainsKey(name)) throw new ArgumentException("Yedek teknik dosya adı geçersiz veya yinelenmiş.");
            var expectedLength = row["length"]?.GetValue<long>() ?? -1; var expectedHash = row["sha256"]?.ToString() ?? "";
            if (expectedLength is <= 0 or > MaxMediaBytes || !Regex.IsMatch(expectedHash, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant)) throw new ArgumentException($"Yedek teknik dosya bilgisi geçersiz ({name}).");
            if (!entries.TryGetValue("technical-files/" + name, out var entry)) throw new ArgumentException($"Yedekte gerekli teknik dosya eksik ({name}).");
            if (entry.Length != expectedLength || entry.Length > MaxMediaBytes) throw new ArgumentException($"Yedek teknik dosya boyutu uyuşmuyor ({name}).");
            total += entry.Length; if (total > MaxArchiveBytes) throw new ArgumentException("Yedek açılmış içerik sınırını aşıyor.");
            using var source = entry.Open(); using var ms = new MemoryStream((int)entry.Length); source.CopyTo(ms); var bytes = ms.ToArray();
            if (!string.Equals(Hash(bytes), expectedHash, StringComparison.Ordinal)) throw new ArgumentException($"Yedek teknik dosya doğrulaması başarısız ({name}).");
            var detected = Uploads.Detect(bytes) ?? throw new ArgumentException($"Yedekte desteklenmeyen teknik dosya bulundu ({name}).");
            if (detected.Mime == "video/mp4" || !string.Equals(detected.Extension, Path.GetExtension(name), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"Yedek teknik dosya uzantısı içerikle uyuşmuyor ({name}).");
            technicalFiles.Add(name, bytes);
        }
        if (operations != null) foreach (var referenced in ReferencedTechnicalMedia(operations)) if (!technicalFiles.ContainsKey(referenced)) throw new ArgumentException($"Teknik cihaz yedeğinde kullanılan dosya eksik ({referenced}).");
        foreach (var archiveName in entries.Keys.Where(x => x.StartsWith("technical-files/", StringComparison.OrdinalIgnoreCase))) if (!technicalFiles.ContainsKey(archiveName[16..])) throw new ArgumentException("Yedek ZIP manifestinde tanımlanmamış teknik dosya var.");

        var declaredNames = files.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var referenced in ReferencedMedia(data))
            if (!declaredNames.Contains(referenced)) throw new ArgumentException($"Katalog yedeğinde kullanılan medya dosyası eksik ({referenced}).");
        foreach (var archiveName in entries.Keys.Where(x => x.StartsWith("files/", StringComparison.OrdinalIgnoreCase)))
            if (!declaredNames.Contains(archiveName[6..])) throw new ArgumentException("Yedek ZIP manifestinde tanımlanmamış medya dosyası var.");

        return new CatalogBackupPackage(data, sourceRevision, files, technicalFiles, warranties, operations);
    }

    public static JsonObject Restore(CatalogBackupPackage package, Store store, string dataPath, int expectedRevision)
    {
        store.EnsureRevision(expectedRevision);
        var uploadDirectory = Path.Combine(dataPath, "uploads");
        var technicalDirectory = Path.Combine(dataPath, "technical-files");
        Directory.CreateDirectory(uploadDirectory);
        Directory.CreateDirectory(technicalDirectory);

        foreach (var pair in package.Files)
        {
            var target = Path.Combine(uploadDirectory, pair.Key);
            if (File.Exists(target) && !string.Equals(HashFile(target), Hash(pair.Value), StringComparison.Ordinal))
                throw new ArgumentException($"Mevcut dosyanın içeriği yedekle farklı; üzerine yazılmadı ({pair.Key}).");
        }
        foreach (var pair in package.TechnicalFiles)
        {
            var target = Path.Combine(technicalDirectory, pair.Key);
            if (File.Exists(target) && !string.Equals(HashFile(target), Hash(pair.Value), StringComparison.Ordinal)) throw new ArgumentException($"Mevcut teknik dosyanın içeriği yedekle farklı; üzerine yazılmadı ({pair.Key}).");
        }

        var staging = Path.Combine(dataPath, "restore-staging-" + Guid.NewGuid().ToString("N"));
        var installed = new List<string>();
        Directory.CreateDirectory(staging);
        var stagingUploads = Path.Combine(staging, "uploads"); var stagingTechnical = Path.Combine(staging, "technical-files"); Directory.CreateDirectory(stagingUploads); Directory.CreateDirectory(stagingTechnical);
        try
        {
            foreach (var pair in package.Files) File.WriteAllBytes(Path.Combine(stagingUploads, pair.Key), pair.Value);
            foreach (var pair in package.TechnicalFiles) File.WriteAllBytes(Path.Combine(stagingTechnical, pair.Key), pair.Value);

            store.EnsureRevision(expectedRevision);
            foreach (var pair in package.Files)
            {
                var target = Path.Combine(uploadDirectory, pair.Key);
                if (File.Exists(target)) continue;
                try { File.Move(Path.Combine(stagingUploads, pair.Key), target, false); installed.Add(target); }
                catch (IOException) when (File.Exists(target) && string.Equals(HashFile(target), Hash(pair.Value), StringComparison.Ordinal)) { }
            }
            foreach (var pair in package.TechnicalFiles)
            {
                var target = Path.Combine(technicalDirectory, pair.Key); if (File.Exists(target)) continue;
                try { File.Move(Path.Combine(stagingTechnical, pair.Key), target, false); installed.Add(target); }
                catch (IOException) when (File.Exists(target) && string.Equals(HashFile(target), Hash(pair.Value), StringComparison.Ordinal)) { }
            }

            return store.RestoreBackup(package.Data, expectedRevision, "Tam ZIP yedeğinden geri yükleme", package.Warranties, package.Operations);
        }
        catch
        {
            foreach (var target in installed) try { if (File.Exists(target)) File.Delete(target); } catch { }
            throw;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    static IEnumerable<string> ReferencedMedia(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text) && text.StartsWith("/api/files/", StringComparison.Ordinal))
        {
            var name = text[11..];
            if (!MediaName.IsMatch(name)) throw new ArgumentException("Katalogda geçersiz yerel medya bağlantısı bulundu.");
            yield return name;
            yield break;
        }
        if (node is JsonObject obj)
            foreach (var child in obj.Select(x => x.Value)) foreach (var item in ReferencedMedia(child)) yield return item;
        else if (node is JsonArray array)
            foreach (var child in array) foreach (var item in ReferencedMedia(child)) yield return item;
    }

    static IEnumerable<string> ReferencedTechnicalMedia(JsonObject operations)
    {
        if (operations["technicalProfiles"] is not JsonObject profiles) yield break;
        foreach (var profileNode in profiles.Select(x => x.Value))
        {
            if (profileNode is not JsonObject profile || profile["attachments"] is not JsonArray attachments) continue;
            foreach (var item in attachments)
            {
                var name = item?["storedName"]?.ToString() ?? ""; if (string.IsNullOrWhiteSpace(name)) continue;
                if (!TechnicalName.IsMatch(name)) throw new ArgumentException("Teknik cihaz dosyasında geçersiz yerel dosya adı bulundu."); yield return name;
            }
        }
    }

    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
