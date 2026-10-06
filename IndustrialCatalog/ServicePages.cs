using System.Net;
using System.Text;
using System.Text.Json.Nodes;

public static class ServicePages
{
    static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    public static string Public(JsonObject catalog, IQueryCollection query, CustomerAccountRecord? customer = null)
    {
        var s = catalog["settings"]!.AsObject();
        var name = s["name"]?.ToString() ?? "İNOKSKAR";
        var tagline = s["tagline"]?.ToString() ?? "";
        var headerImage = s["headerImage"]?.ToString() ?? "/inokskar-header-brand.png";
        var productName = query["productName"].ToString();
        var productCode = query["productCode"].ToString();
        var serial = query["serial"].ToString();
        var customerName = customer?.Name ?? "";
        var customerEmail = customer?.Email ?? "";
        var customerPhone = customer?.Phone ?? "";
        var customerBusiness = customer?.BusinessName ?? "";
        var customerAddress = customer?.Address ?? "";
        var nav = new StringBuilder();
        foreach (var c in catalog["categories"]!.AsArray().Where(x => x?["menu"]?.GetValue<bool>() == true))
            nav.Append($"<a href='/kategori/{Uri.EscapeDataString(c!["id"]!.ToString())}'>{E(c["name"]?.ToString())}</a>");
        foreach (var m in s["menu"]!.AsArray().Where(x => x?["url"]?.ToString() != "/blog" && x?["url"]?.ToString() != "/garanti-sorgulama" && x?["url"]?.ToString() != "/servis-talebi"))
            nav.Append($"<a href='{E(m!["url"]?.ToString())}'>{E(m["name"]?.ToString())}</a>");
        nav.Append("<a href='/garanti-sorgulama'>Garanti Sorgula</a><a class='active' href='/servis-talebi'>Servis Talebi</a>");
        var html = """
<!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"><meta name="theme-color" content="#252b33"><meta name="mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-status-bar-style" content="black-translucent"><meta name="apple-mobile-web-app-title" content="İNOKSKAR"><title>Servis Talebi | @@NAME@@</title><meta name="description" content="İnokskar ürününüz için servis talebi oluşturun."><meta name="robots" content="index,follow"><link rel="stylesheet" href="/r95-warranty.css"><link rel="stylesheet" href="/r98-service.css"><link rel="stylesheet" href="/r99-brand.css"><link rel="stylesheet" href="/r103-public.css"><link rel="stylesheet" href="/r13-interface.css"><link rel="stylesheet" href="/r13-pages.css?v=r18-6"><script src="/r13-pages.js?v=r18-6" defer></script></head><body class="warranty-public-body service-request-body">
<header class="site-header"><a class="brand brand-image-link" href="/"><img class="header-brand-image" src="@@HEADER_IMAGE@@" alt="@@NAME@@ @@TAGLINE@@"></a><nav class="main-nav warranty-nav">@@NAV@@</nav><div class="header-actions"><a class="small-btn" href="/iletisim">Destek</a></div></header>
<div class="public-home-return-wrap"><a class="public-home-return" href="/">← Ana sayfaya dön</a></div>
<main class="service-page"><section class="wrap service-intro"><p class="eyebrow">İNOKSKAR SATIŞ SONRASI HİZMETLER</p><h1>Servis talebi oluşturun</h1><p>Servis ekibimizin doğru işletmeye ve doğru cihaza yönlendirilebilmesi için iletişim, işletme ve servis adresi bilgilerini eksiksiz girin.</p></section><section class="wrap service-layout"><aside class="service-info"><h2>Servis süreci</h2><p>Talebiniz kayıt altına alınır. Ekibimiz talebinizi inceleyerek sizinle iletişime geçer.</p><a href="/garanti-sorgulama">Garanti sorgula</a><br><a href="/iletisim">Genel destek talebi oluştur</a></aside><form id="service-request-form" class="service-form"><div class="service-two-col"><label>Ad Soyad / Yetkili<input name="name" maxlength="100" required autocomplete="name" value="@@CUSTOMER_NAME@@"></label><label>E-posta<input name="email" type="email" maxlength="200" required autocomplete="email" value="@@CUSTOMER_EMAIL@@"></label></div><div class="service-two-col"><label>Telefon<input name="phone" type="tel" maxlength="40" required autocomplete="tel" value="@@CUSTOMER_PHONE@@"></label><label>İşletme Adı<input name="businessName" maxlength="180" required autocomplete="organization" placeholder="İşletme veya firma adı" value="@@CUSTOMER_BUSINESS@@"></label></div><label>Servis Adresi<textarea name="address" maxlength="1200" rows="4" required placeholder="Mahalle, cadde/sokak, bina no, ilçe, il">@@CUSTOMER_ADDRESS@@</textarea></label><div class="service-product-box"><h2>Ürün bilgileri <small>(garanti sorgusundan gelmediyseniz bildiğiniz kadarını girin)</small></h2><div class="service-two-col"><label>Ürün<input name="productName" maxlength="180" value="@@PRODUCT_NAME@@"></label><label>Ürün Kodu<input name="productCode" maxlength="100" value="@@PRODUCT_CODE@@"></label></div><label>Seri Numarası<input name="serialNumber" maxlength="100" value="@@SERIAL@@"></label></div><label>Arıza / Servis Talebi Açıklaması<textarea name="message" maxlength="5000" rows="6" required placeholder="Arızayı, belirtileri ve servis ekibinin bilmesi gereken ayrıntıları yazın."></textarea></label><input name="website" class="service-honeypot" tabindex="-1" autocomplete="off" aria-hidden="true"><p class="service-note"></p>@@SAVE_PROFILE@@<div id="service-request-message" aria-live="polite"></div><button type="submit" class="button">Servis talebini gönder</button></form></section></main><script src="/r98-service-request.js" defer></script></body></html>
""";
        return html
            .Replace("@@NAME@@", E(name), StringComparison.Ordinal)
            .Replace("@@TAGLINE@@", E(tagline), StringComparison.Ordinal)
            .Replace("@@HEADER_IMAGE@@", E(headerImage), StringComparison.Ordinal)
            .Replace("@@NAV@@", nav.ToString(), StringComparison.Ordinal)
            .Replace("@@PRODUCT_NAME@@", E(productName), StringComparison.Ordinal)
            .Replace("@@PRODUCT_CODE@@", E(productCode), StringComparison.Ordinal)
            .Replace("@@SERIAL@@", E(serial), StringComparison.Ordinal)
            .Replace("@@CUSTOMER_NAME@@", E(customerName), StringComparison.Ordinal)
            .Replace("@@CUSTOMER_EMAIL@@", E(customerEmail), StringComparison.Ordinal)
            .Replace("@@CUSTOMER_PHONE@@", E(customerPhone), StringComparison.Ordinal)
            .Replace("@@CUSTOMER_BUSINESS@@", E(customerBusiness), StringComparison.Ordinal)
            .Replace("@@CUSTOMER_ADDRESS@@", E(customerAddress), StringComparison.Ordinal)
            .Replace("@@SAVE_PROFILE@@", customer == null ? "" : "<label class='service-save-profile'><input type='checkbox' name='saveProfile' value='true'> <span>Bu talepte değiştirdiğim iletişim ve servis bilgilerini profilime de kaydet</span></label>", StringComparison.Ordinal);
    }
}
