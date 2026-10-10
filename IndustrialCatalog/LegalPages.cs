using System.Net;
using System.Text;
using System.Text.Json.Nodes;
public static class LegalPages
{
    static string E(string? value)=>WebUtility.HtmlEncode(value??"");
    public static string Render(string kind,JsonObject catalog)
    {
        var s=catalog["settings"]!.AsObject();var name=s["name"]?.ToString()??"İNOKSKAR";var tagline=s["tagline"]?.ToString()??"";var headerImage=s["headerImage"]?.ToString()??"/inokskar-header-brand.png";var email=s["email"]?.ToString()??"";var address=s["address"]?.ToString()??"";
        var nav=new StringBuilder();
        foreach(var c in catalog["categories"]!.AsArray().Where(x=>x?["menu"]?.GetValue<bool>()==true))nav.Append($"<a href='/kategori/{Uri.EscapeDataString(c!["id"]!.ToString())}'>{E(c["name"]?.ToString())}</a>");
        foreach(var m in s["menu"]!.AsArray().Where(x=>x?["url"]?.ToString()!="/blog"&&x?["url"]?.ToString()!="/garanti-sorgulama"&&x?["url"]?.ToString()!="/servis-talebi"))nav.Append($"<a href='{E(m!["url"]?.ToString())}'>{E(m["name"]?.ToString())}</a>");
        nav.Append("<a href='/garanti-sorgulama'>Garanti Sorgula</a><a href='/iletisim?amac=servis'>Teknik Destek</a>");
        var (title,body)=kind switch
        {
            "kvkk"=>("KVKK Aydınlatma Metni",$"Bu sayfa, {name} internet sitesi üzerinden iletilen iletişim ve servis taleplerindeki kişisel verilerin talebin değerlendirilmesi, iletişim kurulması, servis organizasyonu ve kayıt güvenliğinin sağlanması amaçlarıyla işlenmesine ilişkin genel bilgilendirmeyi içerir. Veriler yalnız gerekli süre boyunca ve ilgili mevzuat çerçevesinde korunur. Kişisel verilerinizle ilgili talepleriniz için {email} adresinden iletişime geçebilirsiniz."),
            "cerez"=>("Çerez Politikası","Site; yönetici oturumu, güvenlik ve temel site işlevleri için zorunlu teknik çerezler kullanabilir. Zorunlu olmayan analiz veya reklam çerezleri ayrıca etkinleştirilmedikçe kullanılmaz."),
            _=>("Gizlilik Politikası",$"{name}, ziyaretçi ve müşteri bilgilerinin gizliliğini önemser. İletişim, garanti ve servis formlarında sağlanan bilgiler yalnız ilgili talebin yürütülmesi, hizmet kalitesinin sağlanması ve yasal yükümlülüklerin yerine getirilmesi amaçlarıyla kullanılır. Bilgiler yetkisiz erişime karşı teknik ve idari tedbirlerle korunur.")
        };
        var addressBlock=!string.IsNullOrWhiteSpace(address)?$"<p><strong>İletişim adresi:</strong> {E(address)}</p>":"";
        var emailBlock=!string.IsNullOrWhiteSpace(email)?$"<p><strong>E-posta:</strong> <a href='mailto:{E(email)}'>{E(email)}</a></p>":"";
        var html = """
<!doctype html><html lang="tr"><head><link rel='stylesheet' href='/r129-feedback.css?v=1'><script defer src='/r129-feedback.js?v=1'></script><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"><meta name="theme-color" content="#252b33"><meta name="mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-status-bar-style" content="black-translucent"><meta name="apple-mobile-web-app-title" content="İNOKSKAR"><title>@@TITLE@@ | @@NAME@@</title><link rel="stylesheet" href="/r99-brand.css"><link rel="stylesheet" href="/r103-public.css"><link rel="stylesheet" href="/r13-interface.css?v=r22-clean"><link rel="stylesheet" href="/r13-pages.css?v=r22-2"><link rel="stylesheet" href="/r22-public-shell.css?v=r22-4"><link rel="stylesheet" href="/r22-layout-fixes.css?v=responsive-audit-2"><script src="/r13-pages.js?v=central-logo-4" defer></script></head><body class="legal-public-body"><header class="site-header"><a class="brand brand-image-link" href="/"><img class="header-brand-image" src="@@HEADER_IMAGE@@" alt="@@NAME@@ @@TAGLINE@@"></a><nav class="main-nav warranty-nav">@@NAV@@</nav></header><div class="public-home-return-wrap"><a class="public-home-return" href="/">← Ana sayfaya dön</a></div><main class="legal-page"><p class="eyebrow">KURUMSAL BİLGİLENDİRME</p><h1>@@TITLE@@</h1><p class="legal-summary">Bilgilerinizin kullanımı ve korunmasına ilişkin bilgilendirme.</p><section class="legal-copy"><h2>Bilgileriniz ve gizliliğiniz</h2><p>@@BODY@@</p></section><section class="legal-contact"><h2>Bizimle iletişime geçin</h2>@@ADDRESS_BLOCK@@@@EMAIL_BLOCK@@</section><nav class="legal-related" aria-label="Diğer bilgilendirme sayfaları"><a href="/kvkk">KVKK Aydınlatma Metni <span>→</span></a><a href="/cerez">Çerez Politikası <span>→</span></a></nav></main></body></html>
""";
        return html.Replace("@@TITLE@@",E(title),StringComparison.Ordinal).Replace("@@NAME@@",E(name),StringComparison.Ordinal).Replace("@@TAGLINE@@",E(tagline),StringComparison.Ordinal).Replace("@@HEADER_IMAGE@@",E(headerImage),StringComparison.Ordinal).Replace("@@NAV@@",nav.ToString(),StringComparison.Ordinal).Replace("@@BODY@@",E(body),StringComparison.Ordinal).Replace("@@ADDRESS_BLOCK@@",addressBlock,StringComparison.Ordinal).Replace("@@EMAIL_BLOCK@@",emailBlock,StringComparison.Ordinal);
    }
}
