using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public static class WarrantyRules
{
    static readonly Regex SafeText = new(@"^[^\u0000-\u001F\u007F]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly Regex CodeInput = new(@"^[A-Za-z0-9\s-]{8,32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly Regex StoredHash = new(@"^\d{6}:[A-Za-z0-9+/=]+:[A-Za-z0-9+/=]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string NormalizeSerial(string? value) => Regex.Replace((value ?? "").Trim(), @"\s+", " ").ToUpperInvariant();
    public static string NormalizeCode(string? value) => Regex.Replace((value ?? "").Trim().ToUpperInvariant(), @"[\s-]+", "");

    public static string GenerateCode()
    {
        Span<char> chars = stackalloc char[10];
        for (var i = 0; i < chars.Length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars[..5]) + "-" + new string(chars[5..]);
    }

    public static JsonObject Prepare(JsonObject input, JsonObject catalog, JsonObject? existing, out string issuedCode)
    {
        issuedCode = "";
        var mode = input["productMode"]?.ToString() ?? "";
        var requestedId = input["productId"]?.ToString() ?? "";
        var existingId = existing?["productId"]?.ToString() ?? "";
        var manual = mode == "manual" ||
            (string.IsNullOrWhiteSpace(mode) && existingId.StartsWith("manual-", StringComparison.Ordinal));
        string productId, productName, productCode;
        if (manual)
        {
            productId = existingId.StartsWith("manual-", StringComparison.Ordinal)
                ? existingId
                : "manual-" + Guid.NewGuid().ToString("N");
            productName = Clean(input["productName"]?.ToString(), 200, "Ürün adını girin.");
            productCode = CleanOptional(input["productCode"]?.ToString(), 100, "Ürün kodu");
            if (string.IsNullOrWhiteSpace(productCode)) productCode = "Belirtilmedi";
        }
        else
        {
            productId = Clean(requestedId, 100, "Katalogdan bir ürün seçin veya elle girişe geçin.");
            var product = catalog["products"]?.AsArray().FirstOrDefault(x => x?["id"]?.ToString() == productId)?.AsObject();
            if (product == null && (existing == null || !string.Equals(existingId, productId, StringComparison.Ordinal)))
                throw new ArgumentException("Seçilen ürün katalogda bulunamadı. Elle ürün girişini kullanabilirsiniz.");
            productName = product != null
                ? Clean(product["name"]?.ToString(), 200, "Katalog ürününün adı geçersiz.")
                : Clean(existing?["productName"]?.ToString(), 200, "Garanti kaydındaki ürün adı bulunamadı.");
            var defaultCode = (product?["code"]?.ToString() ?? existing?["productCode"]?.ToString() ?? "").Trim();
            productCode = CleanOptional(input["productCode"]?.ToString() ?? defaultCode, 100, "Ürün kodu");
            if (string.IsNullOrWhiteSpace(productCode)) productCode = string.IsNullOrWhiteSpace(defaultCode) ? productId : defaultCode;
        }
        var businessName = Clean(input["businessName"]?.ToString(), 200, "Teslim edileceği işletme adı gerekli.");
        var customerName = CleanOptional(input["customerName"]?.ToString() ?? existing?["customerName"]?.ToString(), 120, "Müşteri / yetkili adı");
        var invoiceNumber = CleanOptional(input["invoiceNumber"]?.ToString(), 100, "Fatura numarası");
        var deliveryDocumentNumber = CleanOptional(input["deliveryDocumentNumber"]?.ToString(), 100, "Teslim belgesi numarası");
        var warrantyNote = CleanOptional(input["warrantyNote"]?.ToString(), 1200, "Garanti notu");
        var serial = NormalizeSerial(input["serialNumber"]?.ToString());
        if (serial.Length is < 2 or > 100 || !SafeText.IsMatch(serial)) throw new ArgumentException("Seri numarası 2-100 karakter arasında olmalıdır.");

        if (!DateOnly.TryParseExact(input["deliveryDate"]?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var delivery))
            throw new ArgumentException("Geçerli teslim tarihi girin.");
        var months = input["warrantyMonths"]?.GetValue<int>() ?? 0;
        if (months is < 1 or > 120) throw new ArgumentException("Garanti süresi 1 ile 120 ay arasında olmalıdır.");
        var end = delivery.AddMonths(months);

        var components = new JsonArray();
        var componentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var coveredCount = 0;
        var incomingComponents = input["components"] as JsonArray ?? new JsonArray();
        if (incomingComponents.Count == 0) throw new ArgumentException("En az bir garanti kapsamı bileşeni ekleyin.");
        if (incomingComponents.Count > 50) throw new ArgumentException("Bir garanti kaydında en fazla 50 bileşen olabilir.");
        foreach (var node in incomingComponents)
        {
            var row = node?.AsObject() ?? throw new ArgumentException("Garanti kapsamı satırı geçersiz.");
            var name = Clean(row["name"]?.ToString(), 100, "Bileşen adı gerekli.");
            if (!componentNames.Add(name)) throw new ArgumentException("Aynı garanti bileşeni birden fazla kez eklenemez.");
            var covered = row["covered"]?.GetValue<bool>() ?? false;
            var componentMonths = covered ? (row["months"]?.GetValue<int>() ?? months) : 0;
            if (covered && componentMonths <= 0) componentMonths = months;
            if (componentMonths is < 0 or > 120) throw new ArgumentException("Bileşen garanti süresi 0 ile 120 ay arasında olmalıdır.");
            if (covered && componentMonths > months) throw new ArgumentException("Bileşen garanti süresi genel garanti süresini aşamaz.");
            if (covered) coveredCount++;
            var note = (row["note"]?.ToString() ?? "").Trim();
            if (note.Length > 500 || (note.Length > 0 && !SafeText.IsMatch(note))) throw new ArgumentException("Garanti kapsamı açıklaması en fazla 500 karakter olabilir.");
            components.Add(new JsonObject
            {
                ["name"] = name,
                ["covered"] = covered,
                ["months"] = componentMonths,
                ["note"] = note,
                ["endDate"] = covered ? delivery.AddMonths(componentMonths).ToString("yyyy-MM-dd") : ""
            });
        }
        if (coveredCount == 0) throw new ArgumentException("En az bir bileşen garanti kapsamında olmalıdır.");

        var verificationHash = existing?["verificationHash"]?.ToString() ?? "";
        var verificationHint = existing?["verificationHint"]?.ToString() ?? "";
        var storedVerificationCode = existing?["verificationCode"]?.ToString() ?? "";
        var requestedCode = input["verificationCode"]?.ToString() ?? "";
        if (!string.IsNullOrWhiteSpace(requestedCode))
        {
            if (!CodeInput.IsMatch(requestedCode)) throw new ArgumentException("Doğrulama kodu 8-32 karakter arasında, harf/rakam biçiminde olmalıdır.");
            var normalized = NormalizeCode(requestedCode);
            if (normalized.Length is < 8 or > 24) throw new ArgumentException("Doğrulama kodu 8-24 harf/rakam içermelidir.");
            verificationHash = Passwords.Hash(normalized);
            verificationHint = normalized[^4..];
            storedVerificationCode = requestedCode.Trim().ToUpperInvariant();
            issuedCode = storedVerificationCode;
        }
        else if (existing == null)
        {
            var generated = GenerateCode();
            var normalized = NormalizeCode(generated);
            verificationHash = Passwords.Hash(normalized);
            verificationHint = normalized[^4..];
            storedVerificationCode = generated;
            issuedCode = generated;
        }
        if (string.IsNullOrWhiteSpace(verificationHash) || !StoredHash.IsMatch(verificationHash)) throw new ArgumentException("Garanti doğrulama kodu oluşturulamadı.");

        var now = DateTimeOffset.UtcNow.ToString("O");
        return new JsonObject
        {
            ["id"] = existing?["id"]?.ToString() ?? Guid.NewGuid().ToString("N"),
            ["productId"] = productId,
            ["productMode"] = manual ? "manual" : "catalog",
            ["productName"] = productName,
            ["productCode"] = productCode,
            ["businessName"] = businessName,
            ["customerName"] = customerName,
            ["invoiceNumber"] = invoiceNumber,
            ["deliveryDocumentNumber"] = deliveryDocumentNumber,
            ["warrantyNote"] = warrantyNote,
            ["serialNumber"] = serial,
            ["verificationHash"] = verificationHash,
            ["verificationHint"] = verificationHint,
            ["verificationCode"] = storedVerificationCode,
            ["deliveryDate"] = delivery.ToString("yyyy-MM-dd"),
            ["warrantyMonths"] = months,
            ["warrantyEndDate"] = end.ToString("yyyy-MM-dd"),
            ["components"] = components,
            ["created"] = existing?["created"]?.ToString() ?? now,
            ["updated"] = now
        };
    }

    public static bool Verify(JsonObject record, string suppliedCode)
    {
        var normalized = NormalizeCode(suppliedCode);
        return normalized.Length >= 8 && Passwords.Verify(normalized, record["verificationHash"]?.ToString() ?? "");
    }

    public static JsonObject AdminView(JsonObject record)
    {
        var view = record.DeepClone().AsObject();
        view.Remove("verificationHash");
        return AddDerived(view);
    }

    public static JsonObject PublicView(JsonObject record)
    {
        var view = record.DeepClone().AsObject();
        view.Remove("verificationHash");
        view.Remove("verificationHint");
        view.Remove("verificationCode");
        view.Remove("businessName");
        view.Remove("customerName");
        view.Remove("invoiceNumber");
        view.Remove("deliveryDocumentNumber");
        view.Remove("warrantyNote");
        view.Remove("created");
        view.Remove("updated");
        return AddDerived(view);
    }

    public static void ValidateStoredCollection(JsonArray records, JsonObject catalog)
    {
        if (records.Count > 100000) throw new ArgumentException("Garanti kaydı sayısı güvenli sınırı aşıyor.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in records)
        {
            var r = node?.AsObject() ?? throw new ArgumentException("Garanti kaydı geçersiz.");
            var id = r["id"]?.ToString() ?? "";
            if (!Regex.IsMatch(id, "^[a-f0-9]{32}$", RegexOptions.CultureInvariant) || !ids.Add(id)) throw new ArgumentException("Garanti kayıt kimliği geçersiz veya tekrarlı.");
            var productId = Clean(r["productId"]?.ToString(), 100, "Garanti kaydındaki ürün kimliği geçersiz.");
            Clean(r["productName"]?.ToString(), 200, "Garanti kaydındaki ürün adı geçersiz.");
            Clean(r["productCode"]?.ToString(), 100, "Garanti kaydındaki ürün kodu geçersiz.");
            var businessName = (r["businessName"]?.ToString() ?? "").Trim();
            if (businessName.Length > 200 || (businessName.Length > 0 && !SafeText.IsMatch(businessName))) throw new ArgumentException("Garanti kaydındaki işletme adı geçersiz.");
            CleanOptional(r["customerName"]?.ToString(), 120, "Müşteri / yetkili adı");
            CleanOptional(r["invoiceNumber"]?.ToString(), 100, "Fatura numarası");
            CleanOptional(r["deliveryDocumentNumber"]?.ToString(), 100, "Teslim belgesi numarası");
            CleanOptional(r["warrantyNote"]?.ToString(), 1200, "Garanti notu");
            var serial = NormalizeSerial(r["serialNumber"]?.ToString());
            if (serial.Length is < 2 or > 100 || !serials.Add(serial)) throw new ArgumentException("Garanti seri numarası geçersiz veya tekrarlı.");
            if (!StoredHash.IsMatch(r["verificationHash"]?.ToString() ?? "")) throw new ArgumentException("Garanti doğrulama kaydı geçersiz.");
            var storedCode = (r["verificationCode"]?.ToString() ?? "").Trim();
            if (storedCode.Length > 0)
            {
                if (!CodeInput.IsMatch(storedCode)) throw new ArgumentException("Garanti doğrulama kodu geçersiz.");
                var normalizedStoredCode = NormalizeCode(storedCode);
                if (normalizedStoredCode.Length is < 8 or > 24 || !string.Equals(r["verificationHint"]?.ToString(), normalizedStoredCode[^4..], StringComparison.Ordinal)) throw new ArgumentException("Garanti doğrulama kodu kaydı tutarsız.");
            }
            if (!DateOnly.TryParseExact(r["deliveryDate"]?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var delivery)) throw new ArgumentException("Garanti teslim tarihi geçersiz.");
            var months = r["warrantyMonths"]?.GetValue<int>() ?? 0;
            if (months is < 1 or > 120) throw new ArgumentException("Garanti süresi geçersiz.");
            if (r["warrantyEndDate"]?.ToString() != delivery.AddMonths(months).ToString("yyyy-MM-dd")) throw new ArgumentException("Garanti bitiş tarihi tutarsız.");
            var components = r["components"] as JsonArray ?? throw new ArgumentException("Garanti kapsamı bulunamadı.");
            if (components.Count is < 1 or > 50) throw new ArgumentException("Garanti kapsamı geçersiz.");
            var storedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var storedCoveredCount = 0;
            foreach (var cnode in components)
            {
                var c = cnode?.AsObject() ?? throw new ArgumentException("Garanti bileşeni geçersiz.");
                var name = Clean(c["name"]?.ToString(), 100, "Garanti bileşeni adı gerekli.");
                if (!storedNames.Add(name)) throw new ArgumentException("Garanti bileşeni adı tekrarlı.");
                var covered = c["covered"]?.GetValue<bool>() ?? false;
                var cm = c["months"]?.GetValue<int>() ?? 0;
                if (cm < 0 || cm > months || (covered && cm == 0) || (!covered && cm != 0)) throw new ArgumentException("Garanti bileşeni süresi geçersiz.");
                var note = (c["note"]?.ToString() ?? "").Trim();
                if (note.Length > 500 || (note.Length > 0 && !SafeText.IsMatch(note))) throw new ArgumentException("Garanti bileşeni açıklaması geçersiz.");
                var expectedEnd = covered ? delivery.AddMonths(cm).ToString("yyyy-MM-dd") : "";
                if (c["endDate"]?.ToString() != expectedEnd) throw new ArgumentException("Garanti bileşeni bitiş tarihi tutarsız.");
                if (covered) storedCoveredCount++;
            }
            if (storedCoveredCount == 0) throw new ArgumentException("Garanti kaydında kapsam içi bileşen bulunamadı.");
        }
    }

    static JsonObject AddDerived(JsonObject view)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var end = DateOnly.ParseExact(view["warrantyEndDate"]!.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var days = end.DayNumber - today.DayNumber;
        view["daysRemaining"] = days;
        view["status"] = days >= 0 ? "active" : "expired";
        if (view["components"] is JsonArray components)
        {
            foreach (var node in components)
            {
                if (node is not JsonObject c) continue;
                var covered = c["covered"]?.GetValue<bool>() ?? false;
                if (!covered) { c["status"] = "excluded"; c["daysRemaining"] = 0; continue; }
                if (!DateOnly.TryParseExact(c["endDate"]?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var componentEnd)) continue;
                var componentDays = componentEnd.DayNumber - today.DayNumber;
                c["daysRemaining"] = componentDays;
                c["status"] = componentDays >= 0 ? "active" : "expired";
            }
        }
        return view;
    }

    static string CleanOptional(string? value, int max, string label)
    {
        var text = (value ?? "").Trim();
        if (text.Length > max || (text.Length > 0 && !SafeText.IsMatch(text))) throw new ArgumentException(label + " geçersiz.");
        return text;
    }

    static string Clean(string? value, int max, string error)
    {
        var text = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > max || !SafeText.IsMatch(text)) throw new ArgumentException(error);
        return text;
    }
}
