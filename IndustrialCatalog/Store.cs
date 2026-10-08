using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed class Store
{
    readonly object gate = new();
    readonly string file;
    JsonObject state;
    public Store(IWebHostEnvironment env, IConfiguration config)
    {
        var directory = Path.GetFullPath(config["Storage:Path"] ?? "App_Data", env.ContentRootPath);
        Directory.CreateDirectory(directory);
        file = Path.Combine(directory, "catalog-store.json");
        state = File.Exists(file) ? JsonNode.Parse(File.ReadAllText(file))!.AsObject() : new JsonObject { ["revision"] = 0, ["data"] = JsonNode.Parse(File.ReadAllText(Path.Combine(env.ContentRootPath, "seed.json"))), ["history"] = new JsonArray(), ["events"] = new JsonArray(), ["inquiries"] = new JsonArray(), ["warranties"] = new JsonArray(), ["warrantyHistory"] = new JsonArray(), ["auditLog"] = new JsonArray() };
        NormalizeState(state);
        ApplyStoredPricePolicy(state["data"]!.AsObject());
        Persist(state);
    }
    static void ApplyStoredPricePolicy(JsonObject data)
    {
        var rate = data["settings"]?["rate"]?.GetValue<decimal>() ?? 0m;
        if (rate <= 0 || data["products"] is not JsonArray products) return;
        foreach (var node in products)
        {
            if (node is not JsonObject product) continue;
            var euro = decimal.Round(product["euro"]?.GetValue<decimal>() ?? 0m, 2, MidpointRounding.AwayFromZero);
            var published = product["tl"]?.GetValue<decimal>() ?? 0m;
            product["euro"] = euro;
            product["tl"] = CatalogRules.Price(euro * rate, published);
        }
    }
    static void NormalizeState(JsonObject root)
    {
        root["history"] ??= new JsonArray();
        root["events"] ??= new JsonArray();
        root["inquiries"] ??= new JsonArray();
        root["inquiryTemplates"] ??= new JsonArray(
            new JsonObject { ["id"] = "received", ["title"] = "Talebiniz alındı", ["status"] = "review", ["body"] = "Talebiniz alınmıştır. İlgili ekibimiz tarafından incelenmektedir. En kısa sürede bilgilendirme yapılacaktır." },
            new JsonObject { ["id"] = "reviewing", ["title"] = "Talep inceleniyor", ["status"] = "review", ["body"] = "Talebiniz incelenmeye devam ediyor. Gelişmeler hakkında sizi bilgilendireceğiz." },
            new JsonObject { ["id"] = "contacted", ["title"] = "Müşteri arandı", ["status"] = "contacted", ["body"] = "{{şimdi}} tarihinde telefonla sizinle iletişime geçildi. İlginiz için teşekkür ederiz." },
            new JsonObject { ["id"] = "callback", ["title"] = "Belirtilen tarihte aranacak", ["status"] = "callback", ["body"] = "Talebinizle ilgili {{tarih}} tarihinde saat {{saat}}'te sizinle telefonla iletişime geçeceğiz." },
            new JsonObject { ["id"] = "info", ["title"] = "Ek bilgi / belge gerekli", ["status"] = "answered", ["body"] = "Talebinizi sonuçlandırabilmek için ek bilgi veya belgeye ihtiyacımız bulunmaktadır. Lütfen bizimle iletişime geçiniz." },
            new JsonObject { ["id"] = "forwarded", ["title"] = "İlgili birime yönlendirildi", ["status"] = "review", ["body"] = "Talebiniz ilgili birime yönlendirilmiştir. İnceleme tamamlandığında tarafınıza bilgi verilecektir." },
            new JsonObject { ["id"] = "answered", ["title"] = "Talebe cevap verildi", ["status"] = "answered", ["body"] = "Talebiniz hakkında gerekli inceleme yapılmıştır. Detaylı bilgi için bizimle iletişime geçebilirsiniz." },
            new JsonObject { ["id"] = "resolved", ["title"] = "İşlem tamamlandı", ["status"] = "resolved", ["body"] = "Talebiniz sonuçlandırılmıştır. Bizi tercih ettiğiniz için teşekkür ederiz." }
        );

        root["warranties"] ??= new JsonArray();
        root["warrantyHistory"] ??= new JsonArray();
        root["auditLog"] ??= new JsonArray();
        root["technicalProfiles"] ??= new JsonObject();
        root["technicalHistory"] ??= new JsonArray();
        Normalize(root["data"]?.AsObject());
        ReplaceLegacyFurnitureTerms(root["warranties"]);
        if (root["warranties"] is JsonArray warranties)
        {
            foreach (var node in warranties)
            {
                if (node is not JsonObject warranty) continue;
                warranty["businessName"] ??= "";
                warranty["invoiceNumber"] ??= "";
                warranty["deliveryDocumentNumber"] ??= "";
                warranty["warrantyNote"] ??= "";
                warranty["verificationCode"] ??= "";
            }
        }
    }
    static void Normalize(JsonObject? data)
    {
        var settings = data?["settings"]?.AsObject();
        if (settings == null) return;
        settings.Remove("logo");
        settings["headerImage"] ??= "/inokskar-header-brand.png";
        if ((settings["rate"]?.GetValue<decimal>() ?? 0) <= 0 && string.IsNullOrWhiteSpace(settings["lastRate"]?.ToString())) settings["autoRate"] = true;
        settings["rateSource"] ??= "";
        settings["rateDate"] ??= "";
        data!.Remove("posts");
        ReplaceLegacyFurnitureTerms(data);
        if (data["categories"] is JsonArray categories)
        {
            foreach (var node in categories)
            {
                if (node is not JsonObject category) continue;
                category["seoTitle"] ??= "";
                category["seoDescription"] ??= "";
            }
        }
        if (data["products"] is JsonArray products)
        {
            foreach (var node in products)
            {
                if (node is not JsonObject product) continue;
                product["code"] ??= "";
                product["euro"] ??= 0m;
                product["tl"] ??= 0m;
                product["vatRate"] ??= 20m;
                product["description"] ??= "";
                product["after"] ??= "";
                product["specs"] ??= new JsonArray();
                product["images"] ??= new JsonArray();
                product["pdf"] ??= "";
                product["status"] ??= "Bilgi alınız";
                product["visible"] ??= true;
                product["demo"] ??= false;
                product["seoTitle"] ??= "";
                product["seoDescription"] ??= "";
            }
        }
        if (settings["menu"] is JsonArray menu)
        {
            for (var i = menu.Count - 1; i >= 0; i--)
            {
                var url = menu[i]?["url"]?.ToString() ?? "";
                if (url == "/blog" || url.StartsWith("/blog/")) menu.RemoveAt(i);
            }
        }
    }

    static void ReplaceLegacyFurnitureTerms(JsonNode? node)
    {
        static string ReplaceText(string value)
        {
            var legacy = "sanayi " + "mobilya";
            static string ReplaceOne(string text, string from, string to) => Regex.Replace(text, Regex.Escape(from), m => char.IsUpper(m.Value[0]) ? char.ToUpperInvariant(to[0]) + to[1..] : to, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            foreach (var pair in new[]
            {
                (legacy + "sından", "ticari mobilyadan"), (legacy + "sında", "ticari mobilyada"), (legacy + "sının", "ticari mobilyanın"),
                (legacy + "sına", "ticari mobilyaya"), (legacy + "sını", "ticari mobilyayı"), (legacy + "sı", "ticari mobilya"),
                (legacy, "ticari mobilya")
            }) value = ReplaceOne(value, pair.Item1, pair.Item2);
            return value;
        }
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToArray())
            {
                var child = obj[key];
                if (child is JsonValue value && value.TryGetValue<string>(out var text)) obj[key] = ReplaceText(text);
                else ReplaceLegacyFurnitureTerms(child);
            }
        }
        else if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                var child = array[i];
                if (child is JsonValue value && value.TryGetValue<string>(out var text)) array[i] = ReplaceText(text);
                else ReplaceLegacyFurnitureTerms(child);
            }
        }
    }

    void Persist(JsonObject next)
    {
        var temp = file + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, next); stream.Flush(true); }
        File.Move(temp, file, true);
        state = next;
    }
    public JsonObject Snapshot() { lock (gate) return state.DeepClone().AsObject(); }
    public void EnsureRevision(int revision) { lock (gate) if (revision != state["revision"]!.GetValue<int>()) throw new ArgumentException("Başka bir değişiklik kaydedilmiş. Sayfayı yenileyin."); }
    public JsonObject Save(JsonObject body) => SaveCore(body, false);
    public JsonObject RestoreBackup(JsonObject data, int revision, string label, JsonArray? warranties = null, JsonObject? operations = null) => SaveCore(new JsonObject { ["revision"] = revision, ["data"] = data.DeepClone(), ["label"] = label, ["backupWarranties"] = warranties?.DeepClone(), ["backupOperations"] = operations?.DeepClone() }, true);
    JsonObject SaveCore(JsonObject body, bool preservePrices)
    {
        lock (gate)
        {
            var revision = body["revision"]?.GetValue<int>() ?? -1;
            if (revision != state["revision"]!.GetValue<int>()) throw new ArgumentException("Başka bir değişiklik kaydedilmiş. Sayfayı yenileyin.");
            var next = state.DeepClone().AsObject();
            var data = body["data"]?.DeepClone().AsObject() ?? throw new ArgumentException("İçerik gerekli.");
            var restoring = !string.IsNullOrEmpty(body["restore"]?.ToString());
            if (restoring)
            {
                var old = next["history"]!.AsArray().FirstOrDefault(h => h!["id"]!.ToString() == body["restore"]!.ToString()) ?? throw new ArgumentException("Geçmiş kaydı bulunamadı.");
                data = old["data"]!.DeepClone().AsObject();
                if (body["pricesOnly"]?.GetValue<bool>() == true)
                {
                    var current = next["data"]!.DeepClone().AsObject();
                    foreach (var p in current["products"]!.AsArray())
                    { var before = data["products"]!.AsArray().FirstOrDefault(x => x!["id"]!.ToString() == p!["id"]!.ToString()); if (before != null) { p!["euro"] = before["euro"]!.DeepClone(); p["tl"] = before["tl"]!.DeepClone(); p["vatRate"] = before["vatRate"]?.DeepClone() ?? 20m; } }
                    data = current;
                }
            }
            Normalize(data);
            if (body["backupWarranties"] is JsonArray restoredWarranties) WarrantyRules.ValidateStoredCollection(restoredWarranties, data);
            if (!restoring && !preservePrices)
            {
                var incomingSettings=data["settings"]!.AsObject();
                var previousSettings=next["data"]!["settings"]!.AsObject();
                var incomingRate=incomingSettings["rate"]!.GetValue<decimal>();
                var previousRate=previousSettings["rate"]!.GetValue<decimal>();
                var incomingAuto=incomingSettings["autoRate"]!.GetValue<bool>();
                var previousAuto=previousSettings["autoRate"]!.GetValue<bool>();
                var previousSource=previousSettings["rateSource"]?.ToString()??"";
                if(incomingAuto&&incomingRate!=previousRate)
                    throw new ArgumentException("Otomatik kur takibi açıkken manuel kur değiştirilemez. Önce otomatik takibi kapatın.");
                if(incomingAuto&&previousAuto&&previousSource==OfficialExchangeRate.SourceName)
                {
                    incomingSettings["rate"]=previousSettings["rate"]!.DeepClone();
                    incomingSettings["rateSource"]=previousSettings["rateSource"]!.DeepClone();
                    incomingSettings["rateDate"]=previousSettings["rateDate"]!.DeepClone();
                    incomingSettings["lastRate"]=previousSettings["lastRate"]!.DeepClone();
                }
                else if(incomingRate!=previousRate)
                {
                    incomingSettings["rateSource"]="Manuel";
                    incomingSettings["rateDate"]="";
                    incomingSettings["lastRate"]="";
                }
            }
            CatalogRules.Validate(data);
            if (!restoring && !preservePrices)
            {
                var rate = data["settings"]!["rate"]!.GetValue<decimal>();
                foreach (var p in data["products"]!.AsArray())
                {
                    var euro = decimal.Round(p!["euro"]!.GetValue<decimal>(), 2, MidpointRounding.AwayFromZero);
                    if (euro > 0 && rate <= 0) throw new ArgumentException("Fiyatlı ürünler için pozitif kur girin.");
                    var old = next["data"]!["products"]!.AsArray().FirstOrDefault(x => x!["id"]!.ToString() == p["id"]!.ToString());
                    p["euro"] = euro; p["tl"] = CatalogRules.Price(euro * rate, old?["tl"]?.GetValue<decimal>() ?? 0);
                }
            }
            next["history"]!.AsArray().Insert(0, new JsonObject { ["id"] = Guid.NewGuid().ToString("N"), ["label"] = body["label"]?.ToString() ?? "İçerik güncellemesi", ["created"] = DateTimeOffset.UtcNow.ToString("O"), ["data"] = next["data"]!.DeepClone() });
            while (next["history"]!.AsArray().Count > 50) next["history"]!.AsArray().RemoveAt(50);
            next["data"] = data;
            if (body["backupWarranties"] is JsonArray restoredWarranties2) next["warranties"] = restoredWarranties2.DeepClone();
            if (body["backupOperations"] is JsonObject ops)
            {
                foreach (var key in new[] { "inquiries", "warrantyHistory", "auditLog", "events", "technicalHistory" })
                    if (ops[key] is JsonArray rows) next[key] = rows.DeepClone();
                if (ops["technicalProfiles"] is JsonObject technicalProfiles) next["technicalProfiles"] = technicalProfiles.DeepClone();
            }
            next["revision"] = revision + 1; Persist(next);
            return new JsonObject { ["revision"] = revision + 1, ["data"] = data.DeepClone() };
        }
    }

    public void UpdateExchangeRate(ExchangeRateSnapshot snapshot, DateTimeOffset checkedAt)
    {
        if (snapshot.Rate <= 0) throw new ArgumentException("Kur pozitif olmalı.");
        lock (gate)
        {
            var next = state.DeepClone().AsObject();
            var data = next["data"]!.AsObject();
            var settings = data["settings"]!.AsObject();
            var normalizedRate = decimal.Round(snapshot.Rate, 4, MidpointRounding.AwayFromZero);
            var previousRate = settings["rate"]?.GetValue<decimal>() ?? 0;
            var previousSource=settings["rateSource"]?.ToString()??"";
            var previousDate=settings["rateDate"]?.ToString()??"";
            var rateChanged=previousRate!=normalizedRate;
            var metadataChanged=previousSource!=snapshot.Source||previousDate!=snapshot.EffectiveDate;
            settings["rate"] = normalizedRate;
            settings["lastRate"] = checkedAt.ToString("O");
            settings["rateSource"] = snapshot.Source;
            settings["rateDate"] = snapshot.EffectiveDate;
            if (rateChanged)
            {
                var before = state["data"]!.DeepClone();
                foreach (var p in data["products"]!.AsArray())
                {
                    var euro = decimal.Round(p!["euro"]!.GetValue<decimal>(), 2, MidpointRounding.AwayFromZero);
                    var published = p["tl"]?.GetValue<decimal>() ?? 0;
                    p["tl"] = CatalogRules.Price(euro * normalizedRate, published);
                }
                next["history"]!.AsArray().Insert(0, new JsonObject { ["id"] = Guid.NewGuid().ToString("N"), ["label"] = "Resmi EUR/TL kur güncellemesi", ["created"] = checkedAt.ToString("O"), ["data"] = before });
                while (next["history"]!.AsArray().Count > 50) next["history"]!.AsArray().RemoveAt(50);
                next["revision"] = (next["revision"]?.GetValue<int>() ?? 0) + 1;
            }
            else if(metadataChanged)
            {
                next["revision"] = (next["revision"]?.GetValue<int>() ?? 0) + 1;
            }
            Persist(next);
        }
    }
    public void Append(string collection, JsonObject item)
    { lock(gate) { var next=state.DeepClone().AsObject(); var list=next[collection]!.AsArray(); list.Insert(0,item); while(list.Count > (collection=="events"?100000:10000)) list.RemoveAt(list.Count-1); Persist(next); } }

    public bool RemoveInquiry(string id, string actor = "admin", string detail = "")
    {
        lock (gate)
        {
            var next = state.DeepClone().AsObject();
            var list = next["inquiries"]!.AsArray();
            var index = list.ToList().FindIndex(x => x?["id"]?.ToString() == id);
            if (index < 0) return false;
            list.RemoveAt(index); AddAudit(next, "Talep silindi", id, detail, actor); Persist(next); return true;
        }
    }

    public JsonObject? InquiryById(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return null;
        lock (gate)
        {
            var row = state["inquiries"]!.AsArray().FirstOrDefault(x => x?["id"]?.ToString() == id);
            return row?.DeepClone().AsObject();
        }
    }

    public JsonObject AddInquiry(JsonObject data, string actor = "Müşteri")
    {
        lock (gate)
        {
            var next = state.DeepClone().AsObject();
            var list = next["inquiries"]!.AsArray();
            var now = DateTimeOffset.UtcNow;
            var row = new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString("N"),
                ["requestCode"] = "INK-" + now.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                ["data"] = data.ToJsonString(),
                ["created"] = now.ToString("O"),
                ["status"] = "new",
                ["updated"] = now.ToString("O"),
                ["readAt"] = "",
                ["readBy"] = ""
            };
            list.Insert(0, row);
            AddAudit(next, "Talep oluşturuldu", row["id"]!.ToString(), data["type"]?.ToString() ?? "support", actor);
            Persist(next);
            return row.DeepClone().AsObject();
        }
    }

    public JsonObject? UpdateInquiryWorkflow(string id, JsonObject input, string actor = "admin")
    {
        lock (gate)
        {
            var next = state.DeepClone().AsObject();
            var list = next["inquiries"]!.AsArray();
            var index = list.ToList().FindIndex(x => x?["id"]?.ToString() == id);
            if (index < 0) return null;
            var row = list[index]!.AsObject();
            var parsed = JsonNode.Parse(row["data"]?.ToString() ?? "{}")?.AsObject() ?? new JsonObject();
            if (!string.Equals(parsed["type"]?.ToString(), "service", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Yalnız servis taleplerinin iş akışı güncellenebilir.");
            var status = (input["status"]?.ToString() ?? "new").Trim();
            var allowed = new[] { "new", "review", "scheduled", "parts", "completed", "cancelled" };
            if (!allowed.Contains(status, StringComparer.Ordinal)) throw new ArgumentException("Servis durumu geçersiz.");
            static string CleanOptional(JsonNode? node, int max, string label)
            {
                var value = (node?.ToString() ?? "").Trim();
                if (value.Length > max) throw new ArgumentException(label + " çok uzun.");
                return value;
            }
            parsed["serviceStatus"] = status;
            parsed["appointmentDate"] = CleanOptional(input["appointmentDate"], 30, "Servis tarihi");
            parsed["technician"] = CleanOptional(input["technician"], 120, "Teknisyen");
            parsed["internalNote"] = CleanOptional(input["internalNote"], 3000, "İç not");
            parsed["resolution"] = CleanOptional(input["resolution"], 3000, "Servis sonucu");
            parsed["parts"] = CleanOptional(input["parts"], 2000, "Değişen parçalar");
            row["data"] = parsed.ToJsonString();
            row["status"] = status;
            row["updated"] = DateTimeOffset.UtcNow.ToString("O");
            AddAudit(next, "Servis talebi güncellendi", id, status, actor);
            Persist(next);
            return row.DeepClone().AsObject();
        }
    }


    public JsonArray InquiryTemplates()
    {
        lock (gate) return state["inquiryTemplates"]?.DeepClone().AsArray() ?? new JsonArray();
    }

    public void SaveInquiryTemplate(string action, string id, string title, string status, string body, string actor)
    {
        lock (gate)
        {
            var next = state.DeepClone().AsObject();
            var list = next["inquiryTemplates"] as JsonArray;
            if (list == null) { list = new JsonArray(); next["inquiryTemplates"] = list; }
            var index = list.ToList().FindIndex(x => x?["id"]?.ToString() == id);
            if (action == "delete")
            {
                if (index < 0) throw new ArgumentException("Hazır cevap bulunamadı.");
                list.RemoveAt(index);
                AddAudit(next, "Hazır cevap silindi", id, "", actor);
            }
            else
            {
                title = title.Trim(); body = body.Trim(); status = status.Trim();
                if (title.Length is < 2 or > 80 || body.Length is < 2 or > 3000)
                    throw new ArgumentException("Hazır cevap başlığı 2-80, metni 2-3000 karakter olmalıdır.");
                if (!(new[] { "new", "review", "contacted", "callback", "answered", "resolved", "closed" }).Contains(status))
                    throw new ArgumentException("Hazır cevap durumu geçersiz.");
                if (index < 0)
                {
                    id = Guid.NewGuid().ToString("N");
                    list.Add(new JsonObject { ["id"] = id, ["title"] = title, ["status"] = status, ["body"] = body });
                }
                else
                {
                    var item = list[index]!.AsObject();
                    item["title"] = title; item["status"] = status; item["body"] = body;
                }
                AddAudit(next, "Hazır cevap kaydedildi", id, title, actor);
            }
            Persist(next);
        }
    }

    public JsonObject? UpdateInquiryCustomerWorkflow(string id, JsonObject input, string actor = "admin")
    {
        lock (gate)
        {
            var next = state.DeepClone().AsObject();
            var row = next["inquiries"]!.AsArray().FirstOrDefault(x => x?["id"]?.ToString() == id)?.AsObject();
            if (row == null) return null;
            var data = JsonNode.Parse(row["data"]?.ToString() ?? "{}")?.AsObject() ?? new JsonObject();
            var status = (input["status"]?.ToString() ?? "new").Trim().ToLowerInvariant();
            if (!(new[] { "new", "review", "contacted", "callback", "answered", "resolved", "closed" }).Contains(status))
                throw new ArgumentException("Talep süreç durumu geçersiz.");
            var callback = (input["callbackAt"]?.ToString() ?? "").Trim();
            if (callback.Length > 0 &&
                !DateTime.TryParseExact(callback, "yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _))
                throw new ArgumentException("Arama tarihini ve saatini kontrol edin.");
            if (status == "callback" && callback.Length == 0)
                throw new ArgumentException("Aranacak durumu için tarih ve saat seçin.");
            var reply = (input["publicReply"]?.ToString() ?? "").Trim();
            var note = (input["internalNote"]?.ToString() ?? "").Trim();
            if (reply.Length > 3000 || note.Length > 3000)
                throw new ArgumentException("Yanıt veya iç not 3000 karakteri geçemez.");
            var previousReply = data["publicReply"]?.ToString() ?? "";
            var now = DateTimeOffset.UtcNow.ToString("O");
            data["customerStatus"] = status;
            data["callbackAt"] = callback;
            data["publicReply"] = reply;
            data["workflowInternalNote"] = note;
            data["customerStatusUpdatedAt"] = now;
            if (reply.Length > 0 && reply != previousReply)
            {
                data["replyHistory"] ??= new JsonArray();
                var history = data["replyHistory"]!.AsArray();
                history.Add(new JsonObject { ["created"] = now, ["text"] = reply, ["actor"] = actor });
                while (history.Count > 30) history.RemoveAt(0);
            }
            row["data"] = data.ToJsonString();
            if (!string.Equals(data["type"]?.ToString(), "service", StringComparison.OrdinalIgnoreCase)) row["status"] = status;
            row["updated"] = now;
            AddAudit(next, "Talep iletişim süreci güncellendi", id, status, actor);
            Persist(next);
            return row.DeepClone().AsObject();
        }
    }

    public int InquiryCount()
    {
        lock (gate) return state["inquiries"]!.AsArray().Count;
    }

    public int UnreadInquiryCount(bool includeSupport = true, bool includeService = true)
    {
        lock (gate)
        {
            var count = 0;
            foreach (var node in state["inquiries"]!.AsArray())
            {
                if (node is not JsonObject row || !row.ContainsKey("readAt") || !string.IsNullOrWhiteSpace(row["readAt"]?.ToString())) continue;
                var parsed = JsonNode.Parse(row["data"]?.ToString() ?? "{}")?.AsObject();
                var service = string.Equals(parsed?["type"]?.ToString(), "service", StringComparison.OrdinalIgnoreCase);
                if (service ? !includeService : !includeSupport) continue;
                count++;
            }
            return count;
        }
    }

    public JsonObject? MarkInquiryRead(string id, string actor = "admin")
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return null;
        lock (gate)
        {
            var next = state.DeepClone().AsObject();
            var row = next["inquiries"]!.AsArray().FirstOrDefault(x => x?["id"]?.ToString() == id)?.AsObject();
            if (row == null) return null;
            if (!row.ContainsKey("readAt") || string.IsNullOrWhiteSpace(row["readAt"]?.ToString()))
            {
                row["readAt"] = DateTimeOffset.UtcNow.ToString("O");
                row["readBy"] = actor;
                Persist(next);
            }
            return row.DeepClone().AsObject();
        }
    }

    public JsonArray AuditLog(int max = 200)
    {
        lock (gate) return new JsonArray(state["auditLog"]!.AsArray().Take(Math.Clamp(max, 1, 1000)).Select(x => x!.DeepClone()).ToArray());
    }

    public JsonArray AuditForTarget(string target, int max = 50)
    {
        if (string.IsNullOrWhiteSpace(target)) return new JsonArray();
        lock (gate) return new JsonArray(state["auditLog"]!.AsArray()
            .Where(x => string.Equals(x?["target"]?.ToString(), target, StringComparison.OrdinalIgnoreCase))
            .Take(Math.Clamp(max, 1, 200))
            .Select(x => x!.DeepClone()).ToArray());
    }

    public void RecordAudit(string action, string target = "", string detail = "", string actor = "admin")
    {
        lock (gate)
        {
            var next = state.DeepClone().AsObject();
            AddAudit(next, action, target, detail, actor);
            Persist(next);
        }
    }

    static void AddAudit(JsonObject root, string action, string target, string detail, string actor = "admin")
    {
        var list = root["auditLog"]!.AsArray();
        list.Insert(0, new JsonObject { ["id"] = Guid.NewGuid().ToString("N"), ["action"] = action, ["target"] = target, ["detail"] = detail, ["created"] = DateTimeOffset.UtcNow.ToString("O"), ["actor"] = actor });
        while (list.Count > 10000) list.RemoveAt(list.Count - 1);
    }

    public JsonArray WarrantyList()
    {
        lock (gate) return new JsonArray(state["warranties"]!.AsArray().Select(x => (JsonNode)WarrantyRules.AdminView(x!.AsObject())).ToArray());
    }

    public JsonObject SaveWarranty(JsonObject input, string actor = "admin")
    {
        lock (gate)
        {
            var next = state.DeepClone().AsObject();
            var list = next["warranties"]!.AsArray();
            var id = input["id"]?.ToString() ?? "";
            var index = string.IsNullOrWhiteSpace(id) ? -1 : list.ToList().FindIndex(x => x?["id"]?.ToString() == id);
            var existing = index >= 0 ? list[index]!.AsObject() : null;
            var record = WarrantyRules.Prepare(input, next["data"]!.AsObject(), existing, out var issuedCode);
            var serial = WarrantyRules.NormalizeSerial(record["serialNumber"]?.ToString());
            for (var i = 0; i < list.Count; i++)
                if (i != index && WarrantyRules.NormalizeSerial(list[i]?["serialNumber"]?.ToString()) == serial)
                    throw new ArgumentException("Bu seri numarası başka bir garanti kaydında kullanılıyor.");
            var history = next["warrantyHistory"]!.AsArray();
            history.Insert(0, new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString("N"), ["warrantyId"] = record["id"]!.ToString(),
                ["action"] = existing == null ? "Garanti kaydı oluşturuldu" : "Garanti kaydı güncellendi",
                ["created"] = DateTimeOffset.UtcNow.ToString("O"), ["actor"] = actor, ["before"] = existing?.DeepClone()
            });
            while (history.Count > 5000) history.RemoveAt(history.Count - 1);
            if (index >= 0) list[index] = record; else list.Insert(0, record);
            AddAudit(next, existing == null ? "Garanti kaydı oluşturuldu" : "Garanti kaydı güncellendi", record["id"]!.ToString(), record["serialNumber"]?.ToString() ?? "", actor);
            Persist(next);
            return new JsonObject { ["record"] = WarrantyRules.AdminView(record), ["verificationCode"] = issuedCode };
        }
    }

    public bool DeleteWarranty(string id, string actor = "admin")
    {
        lock (gate)
        {
            var next = state.DeepClone().AsObject(); var list = next["warranties"]!.AsArray();
            var index = list.ToList().FindIndex(x => x?["id"]?.ToString() == id); if (index < 0) return false;
            var before = list[index]!.DeepClone(); list.RemoveAt(index);
            var history = next["warrantyHistory"]!.AsArray();
            history.Insert(0, new JsonObject { ["id"] = Guid.NewGuid().ToString("N"), ["warrantyId"] = id, ["action"] = "Garanti kaydı silindi", ["created"] = DateTimeOffset.UtcNow.ToString("O"), ["actor"] = actor, ["before"] = before });
            while (history.Count > 5000) history.RemoveAt(history.Count - 1);
            AddAudit(next, "Garanti kaydı silindi", id, "", actor);
            Persist(next); return true;
        }
    }

    public JsonObject? WarrantyById(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return null;
        lock (gate)
        {
            var row = state["warranties"]!.AsArray().FirstOrDefault(x => x?["id"]?.ToString() == id)?.AsObject();
            return row == null ? null : WarrantyRules.AdminView(row);
        }
    }

    public JsonArray TechnicalWarrantyList()
    {
        lock (gate)
        {
            return new JsonArray(state["warranties"]!.AsArray().Select(x =>
            {
                var w = WarrantyRules.AdminView(x!.AsObject());
                return (JsonNode)new JsonObject
                {
                    ["id"] = w["id"]?.ToString() ?? "", ["businessName"] = w["businessName"]?.ToString() ?? "", ["productName"] = w["productName"]?.ToString() ?? "",
                    ["productCode"] = w["productCode"]?.ToString() ?? "", ["serialNumber"] = w["serialNumber"]?.ToString() ?? "", ["deliveryDate"] = w["deliveryDate"]?.ToString() ?? "",
                    ["warrantyEndDate"] = w["warrantyEndDate"]?.ToString() ?? "", ["status"] = w["status"]?.ToString() ?? "", ["daysRemaining"] = w["daysRemaining"]?.GetValue<int>() ?? 0
                };
            }).ToArray());
        }
    }

    public JsonObject? TechnicalDevice(string warrantyId)
    {
        lock (gate)
        {
            var warranty = state["warranties"]!.AsArray().FirstOrDefault(x => x?["id"]?.ToString() == warrantyId)?.AsObject();
            if (warranty == null) return null;
            var profiles = state["technicalProfiles"]!.AsObject();
            var profile = profiles[warrantyId]?.DeepClone().AsObject() ?? NewTechnicalProfile(warrantyId, "");
            var history = new JsonArray(state["technicalHistory"]!.AsArray().Where(x => x?["warrantyId"]?.ToString() == warrantyId).Take(300).Select(x => x!.DeepClone()).ToArray());
            var serial = WarrantyRules.NormalizeSerial(warranty["serialNumber"]?.ToString());
            var services = new JsonArray();
            foreach (var node in state["inquiries"]!.AsArray())
            {
                if (node is not JsonObject inquiry) continue;
                var parsed = JsonNode.Parse(inquiry["data"]?.ToString() ?? "{}")?.AsObject();
                if (parsed == null || !string.Equals(parsed["type"]?.ToString(), "service", StringComparison.OrdinalIgnoreCase)) continue;
                if (WarrantyRules.NormalizeSerial(parsed["serialNumber"]?.ToString()) != serial) continue;
                services.Add(new JsonObject
                {
                    ["id"] = inquiry["id"]?.ToString() ?? "", ["requestCode"] = inquiry["requestCode"]?.ToString() ?? "", ["created"] = inquiry["created"]?.ToString() ?? "",
                    ["updated"] = inquiry["updated"]?.ToString() ?? "", ["status"] = inquiry["status"]?.ToString() ?? "", ["data"] = parsed.DeepClone()
                });
            }
            var warrantyView = WarrantyRules.AdminView(warranty);
            warrantyView.Remove("verificationCode");
            warrantyView.Remove("verificationHint");
            return new JsonObject { ["warranty"] = warrantyView, ["profile"] = profile, ["history"] = history, ["services"] = services };
        }
    }

    public JsonObject SaveTechnicalProfile(string warrantyId, JsonObject input, string actor)
    {
        lock (gate)
        {
            var next = state.DeepClone().AsObject();
            if (!next["warranties"]!.AsArray().Any(x => x?["id"]?.ToString() == warrantyId)) throw new ArgumentException("Garanti kaydı bulunamadı.");
            var profiles = next["technicalProfiles"]!.AsObject();
            var existing = profiles[warrantyId]?.DeepClone().AsObject();
            var clean = CleanTechnicalProfile(warrantyId, input, existing, actor);
            profiles[warrantyId] = clean;
            AddTechnicalHistory(next, warrantyId, "Teknik cihaz dosyası güncellendi", actor, existing);
            AddAudit(next, "Teknik cihaz dosyası güncellendi", warrantyId, "", actor);
            Persist(next);
            return clean.DeepClone().AsObject();
        }
    }

    public JsonObject AddTechnicalAttachment(string warrantyId, JsonObject metadata, string actor)
    {
        lock (gate)
        {
            var next = state.DeepClone().AsObject();
            if (!next["warranties"]!.AsArray().Any(x => x?["id"]?.ToString() == warrantyId)) throw new ArgumentException("Garanti kaydı bulunamadı.");
            var profiles = next["technicalProfiles"]!.AsObject();
            var profile = profiles[warrantyId]?.DeepClone().AsObject() ?? NewTechnicalProfile(warrantyId, actor);
            var attachments = profile["attachments"] as JsonArray ?? new JsonArray(); profile["attachments"] = attachments;
            if (attachments.Count >= 100) throw new ArgumentException("Bir cihaz dosyasında en fazla 100 teknik belge bulunabilir.");
            attachments.Insert(0, metadata.DeepClone()); profile["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"); profile["updatedBy"] = actor; profiles[warrantyId] = profile;
            AddTechnicalHistory(next, warrantyId, "Teknik belge yüklendi", actor, metadata.DeepClone());
            AddAudit(next, "Teknik belge yüklendi", warrantyId, metadata["title"]?.ToString() ?? "", actor); Persist(next); return metadata.DeepClone().AsObject();
        }
    }

    public JsonObject? TechnicalAttachment(string warrantyId, string attachmentId)
    {
        lock (gate)
        {
            var profile = state["technicalProfiles"]?[warrantyId]?.AsObject();
            return profile?["attachments"]?.AsArray().FirstOrDefault(x => x?["id"]?.ToString() == attachmentId)?.DeepClone().AsObject();
        }
    }

    public JsonObject? RemoveTechnicalAttachment(string warrantyId, string attachmentId, string actor)
    {
        lock (gate)
        {
            var next = state.DeepClone().AsObject(); var profiles = next["technicalProfiles"]!.AsObject(); var profile = profiles[warrantyId]?.AsObject(); if (profile == null) return null;
            var attachments = profile["attachments"] as JsonArray ?? new JsonArray(); var index = attachments.ToList().FindIndex(x => x?["id"]?.ToString() == attachmentId); if (index < 0) return null;
            var removed = attachments[index]!.DeepClone().AsObject(); attachments.RemoveAt(index); profile["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"); profile["updatedBy"] = actor;
            AddTechnicalHistory(next, warrantyId, "Teknik belge silindi", actor, removed.DeepClone()); AddAudit(next, "Teknik belge silindi", warrantyId, removed["title"]?.ToString() ?? "", actor); Persist(next); return removed;
        }
    }

    static JsonObject NewTechnicalProfile(string warrantyId, string actor) => new()
    {
        ["warrantyId"] = warrantyId, ["technicalNote"] = "", ["fields"] = new JsonArray(), ["recommendedParts"] = new JsonArray(), ["attachments"] = new JsonArray(),
        ["createdUtc"] = DateTimeOffset.UtcNow.ToString("O"), ["createdBy"] = actor, ["updatedUtc"] = DateTimeOffset.UtcNow.ToString("O"), ["updatedBy"] = actor
    };

    static JsonObject CleanTechnicalProfile(string warrantyId, JsonObject input, JsonObject? existing, string actor)
    {
        static string Text(JsonNode? node, int max, string label)
        {
            var value = (node?.ToString() ?? "").Trim(); if (value.Length > max) throw new ArgumentException(label + " çok uzun."); return value;
        }
        var fields = new JsonArray(); var incomingFields = input["fields"] as JsonArray ?? new JsonArray(); if (incomingFields.Count > 150) throw new ArgumentException("Teknik özellik sayısı çok fazla.");
        foreach (var node in incomingFields)
        {
            var row = node?.AsObject() ?? throw new ArgumentException("Teknik özellik satırı geçersiz."); var name = Text(row["name"], 100, "Teknik özellik adı"); if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Teknik özellik adı gerekli.");
            fields.Add(new JsonObject { ["group"] = Text(row["group"], 40, "Teknik grup"), ["name"] = name, ["value"] = Text(row["value"], 300, "Teknik değer"), ["unit"] = Text(row["unit"], 30, "Birim"), ["note"] = Text(row["note"], 500, "Teknik not") });
        }
        var parts = new JsonArray(); var incomingParts = input["recommendedParts"] as JsonArray ?? new JsonArray(); if (incomingParts.Count > 150) throw new ArgumentException("Önerilen parça sayısı çok fazla.");
        foreach (var node in incomingParts)
        {
            var row = node?.AsObject() ?? throw new ArgumentException("Parça satırı geçersiz."); var name = Text(row["name"], 140, "Parça adı"); if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Parça / malzeme adı gerekli.");
            parts.Add(new JsonObject { ["name"] = name, ["code"] = Text(row["code"], 100, "Parça kodu"), ["quantity"] = Text(row["quantity"], 40, "Miktar"), ["note"] = Text(row["note"], 500, "Parça notu") });
        }
        var now = DateTimeOffset.UtcNow.ToString("O");
        return new JsonObject
        {
            ["warrantyId"] = warrantyId, ["technicalNote"] = Text(input["technicalNote"], 8000, "Teknik servis notu"), ["fields"] = fields, ["recommendedParts"] = parts,
            ["attachments"] = existing?["attachments"]?.DeepClone() ?? new JsonArray(), ["createdUtc"] = existing?["createdUtc"]?.ToString() ?? now, ["createdBy"] = existing?["createdBy"]?.ToString() ?? actor,
            ["updatedUtc"] = now, ["updatedBy"] = actor
        };
    }

    static void AddTechnicalHistory(JsonObject root, string warrantyId, string action, string actor, JsonNode? detail)
    {
        var list = root["technicalHistory"]!.AsArray(); list.Insert(0, new JsonObject { ["id"] = Guid.NewGuid().ToString("N"), ["warrantyId"] = warrantyId, ["action"] = action, ["actor"] = actor, ["created"] = DateTimeOffset.UtcNow.ToString("O"), ["detail"] = detail?.DeepClone() }); while (list.Count > 20000) list.RemoveAt(list.Count - 1);
    }

    public JsonObject? QueryWarranty(string serialNumber, string verificationCode)
    {
        lock (gate)
        {
            var serial = WarrantyRules.NormalizeSerial(serialNumber);
            if (serial.Length is < 2 or > 100) return null;
            var record = state["warranties"]!.AsArray().FirstOrDefault(x => WarrantyRules.NormalizeSerial(x?["serialNumber"]?.ToString()) == serial)?.AsObject();
            if (record == null || !WarrantyRules.Verify(record, verificationCode)) return null;
            return WarrantyRules.PublicView(record);
        }
    }
}
