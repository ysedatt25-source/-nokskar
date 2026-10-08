using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

public static class SeoPages
{
    static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    static string Text(JsonNode? value) => value?.ToString() ?? "";

    public static string? Origin(IConfiguration config)
    {
        return Uri.TryCreate(config["Site:PublicBaseUrl"], UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo)
            && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
            ? uri.GetLeftPart(UriPartial.Authority) : null;
    }

    public static IEnumerable<string> Paths(JsonObject data)
    {
        foreach (var path in new[] { "/", "/urunler", "/iletisim", "/hakkimizda", "/garanti-sorgulama", "/talep-sorgula", "/gizlilik", "/kvkk", "/cerez" }) yield return path;
        foreach (var (prefix, key) in new[] { ("/urun/", "products"), ("/kategori/", "categories") })
            foreach (var item in data[key]!.AsArray()) yield return prefix + Uri.EscapeDataString(Text(item!["id"]));
    }

    public static string Sitemap(JsonObject data, string origin)
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        return new XDocument(new XElement(ns + "urlset", Paths(data).Select(path => new XElement(ns + "url", new XElement(ns + "loc", origin + path))))).ToString();
    }

    static JsonObject OrganizationSchema(JsonNode settings, string brand, string? origin)
    {
        var schema = new JsonObject { ["@type"] = "Organization", ["name"] = brand };
        if (origin != null)
        {
            schema["@id"] = origin + "/#organization";
            schema["url"] = origin + "/";
            var logo = Text(settings["headerImage"]);
            if (string.IsNullOrWhiteSpace(logo)) logo = Text(settings["hero"]);
            if (!string.IsNullOrWhiteSpace(logo)) schema["logo"] = new Uri(new Uri(origin), logo).ToString();
        }
        var phone = Text(settings["phone"]); var email = Text(settings["email"]); var address = Text(settings["address"]);
        if (!string.IsNullOrWhiteSpace(phone) || !string.IsNullOrWhiteSpace(email))
        {
            var contact = new JsonObject { ["@type"] = "ContactPoint", ["contactType"] = "customer service" };
            if (!string.IsNullOrWhiteSpace(phone)) contact["telephone"] = phone;
            if (!string.IsNullOrWhiteSpace(email)) contact["email"] = email;
            schema["contactPoint"] = contact;
        }
        if (!string.IsNullOrWhiteSpace(address)) schema["address"] = new JsonObject { ["@type"] = "PostalAddress", ["streetAddress"] = address };
        return schema;
    }

    static JsonObject BreadcrumbSchema(IEnumerable<(string Name, string Path)> crumbs, string? origin)
    {
        var items = new JsonArray(); var position = 1;
        foreach (var crumb in crumbs)
        {
            var item = new JsonObject { ["@type"] = "ListItem", ["position"] = position++, ["name"] = crumb.Name };
            if (origin != null) item["item"] = origin + crumb.Path;
            items.Add(item);
        }
        return new JsonObject { ["@type"] = "BreadcrumbList", ["itemListElement"] = items };
    }

    static List<(string Name, string Path)> CategoryCrumbs(JsonObject data, JsonNode category)
    {
        var categories = data["categories"]!.AsArray();
        var chain = new List<JsonNode>(); JsonNode? current = category; var seen = new HashSet<string>(StringComparer.Ordinal);
        while (current != null)
        {
            var id = Text(current["id"]); if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) break;
            chain.Add(current); var parent = Text(current["parent"]); if (string.IsNullOrWhiteSpace(parent)) break;
            current = categories.FirstOrDefault(x => Text(x!["id"]) == parent);
        }
        chain.Reverse();
        var crumbs = new List<(string Name, string Path)> { ("Ana sayfa", "/"), ("Ürünler", "/urunler") };
        crumbs.AddRange(chain.Select(x => (Text(x["name"]), "/kategori/" + Uri.EscapeDataString(Text(x["id"])))));
        return crumbs;
    }

    public static string Render(string template, JsonObject data, string path, string? origin)
    {
        var settings = data["settings"]!;
        var brand = Text(settings["name"]);
        var title = path switch { "/urunler" => "Ürün kataloğu", "/iletisim" => "İletişim", "/hakkimizda" => "Hakkımızda", "/garanti-sorgulama" => "Garanti Sorgulama", "/talep-sorgula" => "Talep Takibi", _ => brand };
        var description = Text(settings["heroText"]);
        var image = Text(settings["hero"]);
        var content = $"<h1>{E(title)}</h1><p>{E(description)}</p>";
        var graph = new JsonArray { OrganizationSchema(settings, brand, origin) };
        JsonNode? matchedItem = null; string? matchedKey = null;

        foreach (var (prefix, key) in new[] { ("/urun/", "products"), ("/kategori/", "categories") })
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var id = Uri.UnescapeDataString(path[prefix.Length..]);
            var item = data[key]!.AsArray().FirstOrDefault(x => Text(x!["id"]) == id);
            if (item == null) continue;
            matchedItem = item; matchedKey = key;
            var customTitle = Text(item["seoTitle"]);
            var customDescription = Text(item["seoDescription"]);
            title = string.IsNullOrWhiteSpace(customTitle) ? Text(item["name"]) : customTitle;
            description = string.IsNullOrWhiteSpace(customDescription) ? Text(item["description"]) : customDescription;
            image = key == "products" ? Text(item["images"]!.AsArray().FirstOrDefault()) : Text(item["image"]);
            content = $"<h1>{E(title)}</h1><p>{E(description)}</p>";

            if (key == "products")
            {
                var code = Text(item["code"]); if (string.IsNullOrWhiteSpace(code)) code = Text(item["id"]);
                content += $"<p>Ürün kodu: {E(code)}</p>";
                foreach (var spec in item["specs"]!.AsArray()) content += $"<p>{E(Text(spec!["name"]))}: {E(Text(spec["value"]))}</p>";
                content += $"<p>{E(Text(item["after"]))}</p>";
                var price = item["tl"]?.GetValue<decimal>() ?? 0;
                var vat = item["vatRate"]?.GetValue<decimal>() ?? 20m;
                content += price > 0 ? $"<p>{price.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))} ₺ + %{vat.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))} KDV</p>" : "<p>Fiyat bilgisi için iletişime geçin.</p>";
                var productSchema = new JsonObject { ["@type"] = "Product", ["name"] = title, ["sku"] = code, ["description"] = description, ["brand"] = new JsonObject { ["@type"] = "Brand", ["name"] = brand } };
                if (origin != null) productSchema["url"] = origin + path;
                if (!string.IsNullOrWhiteSpace(image) && origin != null) productSchema["image"] = new Uri(new Uri(origin), image).ToString();
                graph.Add(productSchema);
                var categoryId = Text(item["category"]); var cat = data["categories"]!.AsArray().FirstOrDefault(x => Text(x!["id"]) == categoryId);
                var crumbs = cat == null ? new List<(string Name, string Path)> { ("Ana sayfa", "/"), ("Ürünler", "/urunler") } : CategoryCrumbs(data, cat);
                crumbs.Add((Text(item["name"]), path)); graph.Add(BreadcrumbSchema(crumbs, origin));
            }
            else
            {
                var pageSchema = new JsonObject { ["@type"] = "CollectionPage", ["name"] = title, ["description"] = description };
                if (origin != null) pageSchema["url"] = origin + path;
                graph.Add(pageSchema); graph.Add(BreadcrumbSchema(CategoryCrumbs(data, item), origin));
            }
        }

        if (path == "/hakkimizda") { description = Text(settings["about"]); content = $"<h1>{E(title)}</h1><p>{E(description)}</p>"; }
        if (path == "/iletisim") content += $"<p>{E(Text(settings["phone"]))}</p><p>{E(Text(settings["email"]))}</p><p>{E(Text(settings["address"]))}</p>";
        if (path == "/garanti-sorgulama") { description = "İnokskar ürününüzün garanti durumunu seri numarası ve doğrulama koduyla güvenli şekilde sorgulayın."; content = $"<h1>{E(title)}</h1><p>{E(description)}</p>"; }
        if (path == "/talep-sorgula")
        {
            description = "İnokskar destek ve servis taleplerinizin güncel durumunu telefon numaranızla güvenli şekilde takip edin.";
            content = $"<h1>{E(title)}</h1><p>{E(description)}</p>";
            graph.Add(BreadcrumbSchema(new[] { ("Ana sayfa", "/"), ("Talep Takibi", "/talep-sorgula") }, origin));
        }
        if (path == "/urunler")
        {
            var collection = new JsonObject { ["@type"] = "CollectionPage", ["name"] = title, ["description"] = description };
            if (origin != null) collection["url"] = origin + path; graph.Add(collection);
            graph.Add(BreadcrumbSchema(new[] { ("Ana sayfa", "/"), ("Ürünler", "/urunler") }, origin));
        }
        if (path == "/")
        {
            var website = new JsonObject { ["@type"] = "WebSite", ["name"] = brand };
            if (origin != null) { website["@id"] = origin + "/#website"; website["url"] = origin + "/"; website["publisher"] = new JsonObject { ["@id"] = origin + "/#organization" }; }
            graph.Add(website);
        }
        if (path is "/" or "/urunler")
            foreach (var product in data["products"]!.AsArray()) content += $"<p><a href='/urun/{E(Uri.EscapeDataString(Text(product!["id"])))}'>{E(Text(product["name"]))}</a></p>";

        var fullTitle = title == brand ? brand : title + " | " + brand;
        description = Regex.Replace(description, @"\s+", " ").Trim();
        if (description.Length > 180) description = description[..180];
        var ogType = matchedKey == "products" ? "product" : "website";
        var head = $"<title>{E(fullTitle)}</title><meta name='description' content='{E(description)}'><meta property='og:title' content='{E(fullTitle)}'><meta property='og:description' content='{E(description)}'><meta property='og:type' content='{ogType}'>";
        if (origin != null)
        {
            head += $"<link rel='canonical' href='{E(origin + path)}'><meta property='og:url' content='{E(origin + path)}'>";
            if (!string.IsNullOrWhiteSpace(image)) head += $"<meta property='og:image' content='{E(new Uri(new Uri(origin), image).ToString())}'>";
        }
        var schemaRoot = new JsonObject { ["@context"] = "https://schema.org", ["@graph"] = graph };
        head += "<script type='application/ld+json'>" + schemaRoot.ToJsonString() + "</script>";
        template = Regex.Replace(template, @"<title>.*?</title>", _ => head, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return template.Replace("<div id=\"root\"></div>", "<div id=\"root\"><main>" + content + "<p><a href='/'>Ana sayfa</a> · <a href='/urunler'>Ürünler</a> · <a href='/iletisim'>İletişim</a></p></main></div>");
    }
}
