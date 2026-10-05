using System.Net;
using System.Text.Json.Nodes;

public static class InquiryPages
{
    static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    static JsonObject Data(JsonObject inquiry)
    {
        try { return JsonNode.Parse(inquiry["data"]?.ToString() ?? "{}")?.AsObject() ?? new JsonObject(); }
        catch { return new JsonObject(); }
    }
    static string DateText(JsonObject inquiry)
    {
        return DateTimeOffset.TryParse(inquiry["created"]?.ToString(), out var created)
            ? created.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss") : inquiry["created"]?.ToString() ?? "—";
    }
    static string Field(string label, string? value, bool multiline = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var css = multiline ? "inquiry-value preserve" : "inquiry-value";
        return $"<div class='inquiry-field'><span>{E(label)}</span><div class='{css}'>{E(value)}</div></div>";
    }
    static string Option(string value, string label, string current) => $"<option value='{E(value)}'{(string.Equals(value, current, StringComparison.Ordinal) ? " selected" : "")}>{E(label)}</option>";

    static string AuditTimeline(JsonArray? audit)
    {
        if (audit == null || audit.Count == 0) return "<section class='inquiry-card inquiry-audit'><div class='audit-heading'><div><p>İŞLEM GEÇMİŞİ</p><h2>Talep zaman çizelgesi</h2></div></div><div class='audit-empty'>Bu talep için henüz işlem kaydı yok.</div></section>";
        var rows = audit.OfType<JsonObject>().Take(30).Select(row =>
        {
            var created = DateTimeOffset.TryParse(row["created"]?.ToString(), out var dt) ? dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : row["created"]?.ToString() ?? "—";
            var action = row["action"]?.ToString() ?? "İşlem";
            var actor = row["actor"]?.ToString() ?? "Sistem";
            var detail = row["detail"]?.ToString() ?? "";
            return $"<article class='audit-item'><span class='audit-dot' aria-hidden='true'></span><div><div class='audit-item-top'><strong>{E(action)}</strong><time>{E(created)}</time></div><p><span>{E(actor)}</span>{(string.IsNullOrWhiteSpace(detail) ? "" : " · " + E(detail))}</p></div></article>";
        });
        return "<section class='inquiry-card inquiry-audit'><div class='audit-heading'><div><p>İŞLEM GEÇMİŞİ</p><h2>Talep zaman çizelgesi</h2></div><span>Son işlemler</span></div><div class='audit-timeline'>" + string.Join("", rows) + "</div></section>";
    }

    public static string AdminDetail(JsonObject inquiry, JsonArray? audit = null)
    {
        var b = Data(inquiry);
        var id = inquiry["id"]?.ToString() ?? "";
        var requestCode = inquiry["requestCode"]?.ToString() ?? id;
        var service = string.Equals(b["type"]?.ToString(), "service", StringComparison.OrdinalIgnoreCase);
        var type = service ? "Servis Talebi" : "Destek Talebi";
        var fields = Field("Talep Numarası", requestCode)
            + Field("Ad / Yetkili", b["name"]?.ToString())
            + Field("E-posta", b["email"]?.ToString())
            + Field("Telefon", b["phone"]?.ToString())
            + (!string.IsNullOrWhiteSpace(b["contact"]?.ToString()) ? Field("İletişim", b["contact"]?.ToString()) : "")
            + (service ? Field("İşletme Adı", b["businessName"]?.ToString()) + Field("Servis Adresi", b["address"]?.ToString(), true) : "")
            + Field("Ürün", b["productName"]?.ToString())
            + Field("Ürün Kodu", b["productCode"]?.ToString())
            + Field("Seri Numarası", b["serialNumber"]?.ToString())
            + Field(service ? "Arıza / Servis Açıklaması" : "Mesaj", b["message"]?.ToString(), true);
        var servicePanel = "";
        if (service)
        {
            var current = b["serviceStatus"]?.ToString() ?? "new";
            servicePanel = """
<section class="inquiry-card service-workflow"><h2>Servis iş akışı</h2><form method="post" action="/admin/inquiries/@@ID@@/service"><div class="workflow-grid"><label>Durum<select name="status">@@OPTIONS@@</select></label><label>Planlanan servis tarihi<input name="appointmentDate" type="date" value="@@APPOINTMENT@@"></label><label>Teknisyen / Sorumlu<input name="technician" maxlength="120" value="@@TECHNICIAN@@"></label></div><label>İç servis notu<textarea name="internalNote" rows="4" maxlength="3000">@@INTERNAL_NOTE@@</textarea></label><label>Değişen parçalar<textarea name="parts" rows="3" maxlength="2000">@@PARTS@@</textarea></label><label>Servis sonucu / çözüm<textarea name="resolution" rows="4" maxlength="3000">@@RESOLUTION@@</textarea></label><button class="primary-button" type="submit">Servis kaydını güncelle</button></form></section>
"""
                .Replace("@@ID@@", Uri.EscapeDataString(id), StringComparison.Ordinal)
                .Replace("@@OPTIONS@@", Option("new", "Yeni", current) + Option("review", "İnceleniyor", current) + Option("scheduled", "Servis planlandı", current) + Option("parts", "Parça bekliyor", current) + Option("completed", "Tamamlandı", current) + Option("cancelled", "İptal", current), StringComparison.Ordinal)
                .Replace("@@APPOINTMENT@@", E(b["appointmentDate"]?.ToString()), StringComparison.Ordinal)
                .Replace("@@TECHNICIAN@@", E(b["technician"]?.ToString()), StringComparison.Ordinal)
                .Replace("@@INTERNAL_NOTE@@", E(b["internalNote"]?.ToString()), StringComparison.Ordinal)
                .Replace("@@PARTS@@", E(b["parts"]?.ToString()), StringComparison.Ordinal)
                .Replace("@@RESOLUTION@@", E(b["resolution"]?.ToString()), StringComparison.Ordinal);
        }
        var html = """
<!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"><meta name="theme-color" content="#252b33"><meta name="mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-status-bar-style" content="black-translucent"><meta name="apple-mobile-web-app-title" content="İNOKSKAR"><title>@@TYPE@@ Detayı | İnokskar</title><link rel="stylesheet" href="/r98-inquiry.css"><link rel="stylesheet" href="/r123-professional.css"><script src="/r123-security.js" defer></script><style>.service-workflow{margin-top:18px}.service-workflow h2{margin-top:0}.service-workflow form{display:grid;gap:14px}.service-workflow label{display:grid;gap:7px;font-weight:700}.service-workflow input,.service-workflow textarea,.service-workflow select{width:100%;padding:11px 12px;border:1px solid #ccd9eb;border-radius:8px;font:inherit;background:#fff}.workflow-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}.primary-button{justify-self:start;border:0;border-radius:8px;background:#1455c0;color:#fff;padding:11px 16px;font-weight:800}@media(max-width:760px){.workflow-grid{grid-template-columns:1fr}}</style><link rel="stylesheet" href="/r13-interface.css"><link rel="stylesheet" href="/r13-pages.css?v=r15-6"><script src="/r13-pages.js?v=r15-6" defer></script></head>
<body><header class="inquiry-top"><a href="/admin?tab=inquiries">← Müşteri taleplerine dön</a><strong>İNOKSKAR · Yönetim</strong></header><main class="inquiry-wrap"><section class="inquiry-head"><div><p>YÖNETİM ALANI</p><h1>Talep detayı</h1><span class="inquiry-badge@@BADGE_CLASS@@">@@TYPE@@</span></div><form method="post" action="/admin/inquiries/@@ID@@/delete" onsubmit="return confirm('Bu müşteri talebi kalıcı olarak silinsin mi?')"><button class="danger-button" type="submit">Sil</button></form></section><section class="inquiry-card"><div class="inquiry-meta"><span>Talep tarihi</span><strong>@@DATE@@</strong></div>@@FIELDS@@</section>@@SERVICE_PANEL@@@@AUDIT_TIMELINE@@</main></body></html>
""";
        return html
            .Replace("@@TYPE@@", E(type), StringComparison.Ordinal)
            .Replace("@@BADGE_CLASS@@", service ? " service" : "", StringComparison.Ordinal)
            .Replace("@@ID@@", Uri.EscapeDataString(id), StringComparison.Ordinal)
            .Replace("@@DATE@@", E(DateText(inquiry)), StringComparison.Ordinal)
            .Replace("@@FIELDS@@", fields, StringComparison.Ordinal)
            .Replace("@@SERVICE_PANEL@@", servicePanel, StringComparison.Ordinal)
            .Replace("@@AUDIT_TIMELINE@@", AuditTimeline(audit), StringComparison.Ordinal);
    }
    public static string NotFound() => "<!doctype html><html lang='tr'><meta charset='utf-8'><title>Talep bulunamadı</title><body><h1>Talep bulunamadı</h1><p><a href='/admin?tab=inquiries'>Müşteri taleplerine dön</a></p></body></html>";
}
