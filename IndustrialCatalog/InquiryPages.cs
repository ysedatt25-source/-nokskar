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
            ? created.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM.yyyy HH:mm:ss") : inquiry["created"]?.ToString() ?? "—";
    }
    static string Field(string label, string? value, bool multiline = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var css = multiline ? "inquiry-value preserve" : "inquiry-value";
        return $"<div class='inquiry-field'><span>{E(label)}</span><div class='{css}'>{E(value)}</div></div>";
    }
    static string SummaryCard(string label, string? value)
    {
        var content = string.IsNullOrWhiteSpace(value) ? "—" : value;
        return "<div class='inquiry-summary-item'><span>" + E(label) +
            "</span><strong>" + E(content) + "</strong></div>";
    }

    public static string PurposeLabel(string? value, bool service = false) => service ? "Teknik Servis" : (value ?? "").Trim().ToLowerInvariant() switch
    {
        "teklif" => "Teklif Al",
        "proje" => "Proje & Danışmanlık",
        "servis" => "Teknik Servis",
        _ => "Genel Destek"
    };
    public static string PreferenceLabel(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "email" => "E-posta ile dönüş",
        "system" => "Talep ekranından yazılı yanıt",
        "phone" => "Telefonla dönüş",
        _ => "Belirtilmedi"
    };
    static string ServiceTopicLabel(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "ariza" => "Arıza / Onarım", "bakim" => "Bakım", "yedek-parca" => "Yedek Parça",
        "kurulum" => "Kurulum / Montaj", "diger" => "Diğer", _ => ""
    };
    static string ProjectTypeLabel(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "restoran" => "Restoran / Lokanta", "otel" => "Otel / Konaklama",
        "kafe" => "Kafe / Pastane", "mutfak" => "Endüstriyel mutfak kurulumu",
        "ozel" => "Özel üretim / Diğer", _ => ""
    };
    static string ProjectTimelineLabel(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "belirsiz" => "Henüz belirlenmedi", "hemen" => "En kısa sürede",
        "1-3ay" => "1–3 ay içinde", "3-6ay" => "3–6 ay içinde",
        "6ay" => "6 aydan sonra", _ => ""
    };

    static string TopicLabel(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "bilgi" => "Bilgi talebi", "oneri" => "Öneri", "sikayet" => "Şikâyet", "diger" => "Diğer", _ => ""
    };

    static string Option(string value, string label, string current) => $"<option value='{E(value)}'{(string.Equals(value, current, StringComparison.Ordinal) ? " selected" : "")}>{E(label)}</option>";


    static string WorkflowLabel(string? value) => (value ?? "new").Trim().ToLowerInvariant() switch
    {
        "review" => "İnceleniyor", "contacted" => "Arandı",
        "callback" => "Aranacak", "answered" => "Cevap verildi",
        "resolved" => "Çözüldü", "closed" => "Kapatıldı", _ => "Yeni"
    };
    static string WorkflowOptions(string current) =>
        Option("new", "Yeni", current) + Option("review", "İnceleniyor", current) +
        Option("contacted", "Arandı", current) + Option("callback", "Aranacak", current) +
        Option("answered", "Cevap verildi", current) + Option("resolved", "Çözüldü", current) +
        Option("closed", "Kapatıldı", current);
    static string ReplyHistory(JsonObject data)
    {
        var rows = data["replyHistory"] as JsonArray;
        if (rows == null || rows.Count == 0) return "";
        var html = rows.OfType<JsonObject>().TakeLast(30).Reverse().Select(row =>
        {
            var at = DateTimeOffset.TryParse(row["created"]?.ToString(), out var date) ?
                date.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM.yyyy HH:mm") : "—";
            return "<article class='icw-history-item'><div><strong>İnokskar yanıtı</strong><time>" + E(at) + "</time></div><p>" +
                E(row["text"]?.ToString()) + "</p></article>";
        });
        return "<div class='icw-history'><h3>Yayınlanan yanıt geçmişi</h3>" + string.Join("", html) + "</div>";
    }
    static string TemplateManagement(JsonArray templates, string id)
    {
        var url = "/admin/inquiries/" + Uri.EscapeDataString(id) + "/templates";
        var list = templates.OfType<JsonObject>().Select(t =>
            "<form class='icw-template-edit' method='post' action='" + url + "'>" +
            "<input type='hidden' name='id' value='" + E(t["id"]?.ToString()) + "'>" +
            "<label>Başlık<input name='title' maxlength='80' required value='" + E(t["title"]?.ToString()) + "'></label>" +
            "<label>Durum<select name='status'>" + WorkflowOptions(t["status"]?.ToString() ?? "answered") + "</select></label>" +
            "<label class='icw-template-text'>Hazır cevap metni<textarea name='body' rows='2' maxlength='3000' required>" + E(t["body"]?.ToString()) + "</textarea></label>" +
            "<div class='icw-template-actions'><button type='submit' name='action' value='save'>Kaydet</button>" +
            "<button type='submit' name='action' value='delete' formnovalidate onclick='return confirm(&quot;Bu hazır cevap silinsin mi?&quot;)'>Sil</button></div></form>");
        return "<details class='icw-manager'><summary>Hazır cevapları yönet <span>Yeni ekle · düzenle · sil</span></summary>" +
            "<div class='icw-manager-content'>" + string.Join("", list) +
            "<form class='icw-template-edit' method='post' action='" + url + "'>" +
            "<label>Yeni cevap başlığı<input name='title' maxlength='80' required placeholder='Ör. Ürün kontrolü'></label>" +
            "<label>Durum<select name='status'>" + WorkflowOptions("answered") + "</select></label>" +
            "<label class='icw-template-text'>Cevap metni<textarea name='body' rows='2' maxlength='3000' required placeholder='Müşteriye gönderilecek metin'></textarea></label>" +
            "<div class='icw-template-actions'><button type='submit' name='action' value='save'>Yeni hazır cevap ekle</button></div></form></div></details>";
    }

    static string AuditTimeline(JsonArray? audit)
    {
        if (audit == null || audit.Count == 0) return "<section class='inquiry-card inquiry-audit'><div class='audit-heading'><div><p>İŞLEM GEÇMİŞİ</p><h2>Talep zaman çizelgesi</h2></div></div><div class='audit-empty'>Bu talep için henüz işlem kaydı yok.</div></section>";
        var rows = audit.OfType<JsonObject>().Take(30).Select(row =>
        {
            var created = DateTimeOffset.TryParse(row["created"]?.ToString(), out var dt) ? dt.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM.yyyy HH:mm") : row["created"]?.ToString() ?? "—";
            var action = row["action"]?.ToString() ?? "İşlem";
            var actor = row["actor"]?.ToString() ?? "Sistem";
            var detail = row["detail"]?.ToString() ?? "";
            return $"<article class='audit-item'><span class='audit-dot' aria-hidden='true'></span><div><div class='audit-item-top'><strong>{E(action)}</strong><time>{E(created)}</time></div><p><span>{E(actor)}</span>{(string.IsNullOrWhiteSpace(detail) ? "" : " · " + E(detail))}</p></div></article>";
        });
        return "<section class='inquiry-card inquiry-audit'><div class='audit-heading'><div><p>İŞLEM GEÇMİŞİ</p><h2>Talep zaman çizelgesi</h2></div><span>Son işlemler</span></div><div class='audit-timeline'>" + string.Join("", rows) + "</div></section>";
    }

    public static string AdminDetail(JsonObject inquiry, JsonArray? audit = null, JsonArray? templates = null)
    {
        var b = Data(inquiry);
        var id = inquiry["id"]?.ToString() ?? "";
        var requestCode = inquiry["requestCode"]?.ToString() ?? id;
        var service = string.Equals(b["type"]?.ToString(), "service", StringComparison.OrdinalIgnoreCase);
        var type = service ? "Servis Talebi" : "Destek Talebi";
        var summaryCards = SummaryCard("Talep tarihi", DateText(inquiry))
            + SummaryCard("Talep numarası", requestCode)
            + SummaryCard("Ad / Yetkili", b["name"]?.ToString())
            + SummaryCard("E-posta", b["email"]?.ToString())
            + SummaryCard("Telefon", b["phone"]?.ToString());
        var purposeLabel = PurposeLabel(b["purpose"]?.ToString(), service);
        var preferenceLabel = PreferenceLabel(b["contactPreference"]?.ToString());
        var purposePanel = "<div class='inquiry-purpose-card' aria-label='Talep amacı ve müşteri tercihi'>" +
            "<div><span>TALEP AMACI</span><strong>" + E(purposeLabel) + "</strong></div>" +
            "<div><span>MÜŞTERİ TERCİHİ</span><strong>" + E(preferenceLabel) + "</strong></div></div>";
        var extraFields = (service ? Field("Servis Türü", ServiceTopicLabel(b["serviceTopic"]?.ToString()))
            + Field("İşletme Adı", b["businessName"]?.ToString())
            + Field("Servis Adresi", b["address"]?.ToString(), true) : "")
            + Field("Talep Konusu", TopicLabel(b["topic"]?.ToString()))
            + Field("Teklif Adedi", b["quantity"]?.ToString())
            + Field("Proje Türü", ProjectTypeLabel(b["projectType"]?.ToString()))
            + Field("Proje Konumu", b["projectCity"]?.ToString())
            + Field("Proje Ölçüleri", b["projectSize"]?.ToString())
            + Field("Planlanan Zaman", ProjectTimelineLabel(b["projectTimeline"]?.ToString()))
            + Field("Ürün", b["productName"]?.ToString())
            + Field("Ürün Kodu", b["productCode"]?.ToString())
            + Field("Seri Numarası", b["serialNumber"]?.ToString());
        var message = Field(service ? "Arıza / Servis Açıklaması" : "Mesaj", b["message"]?.ToString(), true);
        var detailsBlock = "<section class='inquiry-card inquiry-details-overview' aria-label='Talep bilgileri'>" +
            purposePanel + "<div class='inquiry-summary-grid'>" + summaryCards + "</div>" +
            (string.IsNullOrWhiteSpace(extraFields) ? "" : "<div class='inquiry-extra-grid'>" + extraFields + "</div>") +
            "</section>" +
            "<section class='inquiry-card inquiry-message-card' aria-label='Talep mesajı'>" +
            (string.IsNullOrWhiteSpace(message) ? "<div class='inquiry-field'><span>" +
              (service ? "Arıza / Servis Açıklaması" : "Mesaj") + "</span><div class='inquiry-value'>—</div></div>" : message) +
            "</section>";
        if (b["attachments"] is JsonArray documents && documents.Count > 0 && !service)
        {
            var documentRows = string.Join("", documents.OfType<JsonObject>().Select(file =>
            {
                var attachmentId = file["id"]?.ToString() ?? "";
                var attachmentName = file["originalName"]?.ToString() ?? "Proje belgesi";
                var isPdf = file["mime"]?.ToString() == "application/pdf";
                var sizeBytes = long.TryParse(file["size"]?.ToString(), out var length) ? length : 0;
                var fileSize = (sizeBytes / 1048576d).ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) + " MB";
                var prefix = "/api/inquiries/" + Uri.EscapeDataString(id) + "/attachments/" + Uri.EscapeDataString(attachmentId);
                return "<div class='project-doc-row'><span class='project-doc-kind'>" + (isPdf?"PDF":"GÖRSEL") +
                    "</span><span class='project-doc-name'>" + E(attachmentName) + "<small>" + E(fileSize) +
                    "</small></span><a href='" + E(prefix + "/view") + "' target='_blank' rel='noopener noreferrer'>Görüntüle</a>" +
                    "<a href='" + E(prefix + "/download") + "'>İndir</a></div>";
            }));
            detailsBlock += "<section class='inquiry-card project-documents' aria-label='Gizli proje ekleri'>" +
                "<div class='project-doc-title'><div><p>YALNIZCA YETKİLİ PERSONEL</p><h2>Proje Ekleri</h2>" +
                "<span>Müşterinizin paylaştığı çizimler ve görseller. Müşteri sorgulama ekranında gösterilmez.</span></div>" +
                "<strong>" + documents.Count + " dosya</strong></div>" +
                "<div class='project-doc-rows'>" + documentRows + "</div></section>";
        }
        var currentStatus = b["customerStatus"]?.ToString() ?? "new";
        var activeTemplates = templates ?? new JsonArray();
        var templateOptions = string.Join("", activeTemplates.OfType<JsonObject>().Select(t =>
            "<option value='" + E(t["id"]?.ToString()) + "' data-status='" + E(t["status"]?.ToString()) +
            "' data-reply='" + E(t["body"]?.ToString()) + "'>" + E(t["title"]?.ToString()) + "</option>"));
        var workflowPanel = """
<section id="inquiry-customer-workflow" class="inquiry-card icw-panel" aria-label="Müşteri iletişim süreci">
  <div class="icw-head"><div><p>MÜŞTERİ İLETİŞİM SÜRECİ</p><h2>Süreci yönet ve yanıtla</h2></div><span>@@CURRENT_LABEL@@</span></div>
  <form method="post" action="/admin/inquiries/@@ID@@/workflow" class="icw-form">
    <div class="icw-grid">
      <label>Talep durumu<select id="icw-status" name="status">@@STATUS_OPTIONS@@</select></label>
      <label>Aranacak tarih ve saat<input id="icw-callback" name="callbackAt" type="datetime-local" value="@@CALLBACK@@"></label>
    </div>
    <label>Hazır cevap seç <select id="icw-preset"><option value="">Hazır cevap seçiniz…</option>@@PRESETS@@</select></label>
    <label>Müşterinin göreceği yanıt<textarea id="icw-reply" name="publicReply" maxlength="3000" rows="4" placeholder="Müşterinin telefonla sorguladığında göreceği yanıtı yazın.">@@REPLY@@</textarea><small>Hazır cevap metnini değiştirebilirsiniz. Yanıt yalnız kaydettiğinizde müşteriye görünür.</small></label>
    <label>Yalnız yönetim için iç not<textarea name="internalNote" maxlength="3000" rows="3" placeholder="Bu not müşteriye gösterilmez.">@@NOTE@@</textarea></label>
    <div class="icw-footer"><span>“Aranacak” için tarih ve saat zorunludur.</span><button type="submit">Süreci kaydet ve yanıtı yayınla</button></div>
  </form>
  @@REPLY_HISTORY@@
  @@TEMPLATE_MANAGER@@
</section>
"""
            .Replace("@@ID@@", Uri.EscapeDataString(id), StringComparison.Ordinal)
            .Replace("@@CURRENT_LABEL@@", E(WorkflowLabel(currentStatus)), StringComparison.Ordinal)
            .Replace("@@STATUS_OPTIONS@@", WorkflowOptions(currentStatus), StringComparison.Ordinal)
            .Replace("@@CALLBACK@@", E(b["callbackAt"]?.ToString()), StringComparison.Ordinal)
            .Replace("@@PRESETS@@", templateOptions, StringComparison.Ordinal)
            .Replace("@@REPLY@@", E(b["publicReply"]?.ToString()), StringComparison.Ordinal)
            .Replace("@@NOTE@@", E(b["workflowInternalNote"]?.ToString()), StringComparison.Ordinal)
            .Replace("@@REPLY_HISTORY@@", ReplyHistory(b), StringComparison.Ordinal)
            .Replace("@@TEMPLATE_MANAGER@@", TemplateManagement(activeTemplates, id), StringComparison.Ordinal);
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
<!doctype html><html lang="tr"><head><link rel='stylesheet' href='/r129-feedback.css?v=1'><script defer src='/r129-feedback.js?v=1'></script><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"><meta name="theme-color" content="#252b33"><meta name="mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-status-bar-style" content="black-translucent"><meta name="apple-mobile-web-app-title" content="İNOKSKAR"><title>@@TYPE@@ Detayı | İnokskar</title><link rel="stylesheet" href="/r98-inquiry.css?v=r145-project-docs"><link rel="stylesheet" href="/r123-professional.css"><script src="/r123-security.js" defer></script><style>.service-workflow{margin-top:18px}.service-workflow h2{margin-top:0}.service-workflow form{display:grid;gap:14px}.service-workflow label{display:grid;gap:7px;font-weight:700}.service-workflow input,.service-workflow textarea,.service-workflow select{width:100%;padding:11px 12px;border:1px solid #ccd9eb;border-radius:8px;font:inherit;background:#fff}.workflow-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}.primary-button{justify-self:start;border:0;border-radius:8px;background:#1455c0;color:#fff;padding:11px 16px;font-weight:800}@media(max-width:760px){.workflow-grid{grid-template-columns:1fr}}</style>
<style>
#inquiry-customer-workflow{margin-top:16px}
#inquiry-customer-workflow *,#inquiry-customer-workflow *::before,#inquiry-customer-workflow *::after{box-sizing:border-box}
#inquiry-customer-workflow .icw-head{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:17px}
#inquiry-customer-workflow .icw-head p{margin:0 0 4px;color:#1d67b4;font-size:.7rem;font-weight:850;letter-spacing:.11em}
#inquiry-customer-workflow .icw-head h2{margin:0;font-size:1.3rem;line-height:1.2;color:#173553}
#inquiry-customer-workflow .icw-head>span{border-radius:999px;padding:6px 11px;background:#eaf3ff;color:#185b9b;font-size:.75rem;font-weight:800}
#inquiry-customer-workflow .icw-form{display:grid;gap:13px}
#inquiry-customer-workflow .icw-grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}
#inquiry-customer-workflow label{display:grid;gap:6px;min-width:0;color:#294765;font-size:.87rem;font-weight:750}
#inquiry-customer-workflow label small{font-size:.75rem;color:#657b91;font-weight:500;line-height:1.5}
#inquiry-customer-workflow :is(input,select,textarea){width:100%;min-width:0;border:1px solid #cbd9e8;border-radius:10px;background:#fff;padding:10px 12px;color:#163653;font:inherit;font-size:16px;line-height:1.4}
#inquiry-customer-workflow :is(input,select){min-height:44px}
#inquiry-customer-workflow textarea{resize:vertical}
#inquiry-customer-workflow :is(input,select,textarea):focus-visible{outline:2px solid #2a77cc;outline-offset:2px}
#inquiry-customer-workflow .icw-footer{display:flex;gap:12px;justify-content:space-between;align-items:center;flex-wrap:wrap}
#inquiry-customer-workflow .icw-footer span{font-size:.76rem;color:#667b91}
#inquiry-customer-workflow button{min-height:42px;padding:9px 16px;border:0;border-radius:10px;background:linear-gradient(135deg,#246ed0,#134b91);color:#fff;font:inherit;font-size:.85rem;font-weight:800;cursor:pointer}
#inquiry-customer-workflow button:hover{background:linear-gradient(135deg,#3182ea,#185db1)}
#inquiry-customer-workflow .icw-history{margin-top:20px;padding-top:16px;border-top:1px solid #e2eaf3}
#inquiry-customer-workflow .icw-history h3{margin:0 0 9px;font-size:1rem}
#inquiry-customer-workflow .icw-history-item{padding:11px 13px;margin-top:8px;border-left:3px solid #2d74d2;background:#f4f8fd;border-radius:9px}
#inquiry-customer-workflow .icw-history-item>div{display:flex;justify-content:space-between;gap:10px;font-size:.75rem}
#inquiry-customer-workflow .icw-history-item time{color:#627c96}
#inquiry-customer-workflow .icw-history-item p{white-space:pre-wrap;overflow-wrap:anywhere;margin:6px 0 0;line-height:1.5}
#inquiry-customer-workflow .icw-manager{border-top:1px solid #e3eaf3;margin-top:19px;padding-top:14px}
#inquiry-customer-workflow .icw-manager summary{cursor:pointer;color:#1e5d9e;font-weight:800}
#inquiry-customer-workflow .icw-manager summary span{font-size:.73rem;color:#71859a;font-weight:500}
#inquiry-customer-workflow .icw-manager-content{display:grid;gap:10px;margin-top:13px}
#inquiry-customer-workflow .icw-template-edit{display:grid;grid-template-columns:minmax(0,1fr) minmax(130px,.6fr);gap:8px;padding:12px;border:1px solid #e1e9f1;border-radius:11px;background:#fafcff}
#inquiry-customer-workflow .icw-template-edit .icw-template-text{grid-column:1/-1}
#inquiry-customer-workflow .icw-template-actions{grid-column:1/-1;display:flex;gap:8px;justify-content:flex-end;flex-wrap:wrap}
#inquiry-customer-workflow .icw-template-actions button{min-height:36px;font-size:.75rem;padding:7px 11px}
#inquiry-customer-workflow .icw-template-actions button[value=delete]{background:#fff0f1;border:1px solid #efc9cc;color:#a72f43}
@media(max-width:680px){#inquiry-customer-workflow{padding:17px 14px}#inquiry-customer-workflow .icw-grid,#inquiry-customer-workflow .icw-template-edit{grid-template-columns:1fr}#inquiry-customer-workflow .icw-head{align-items:flex-start}#inquiry-customer-workflow .icw-footer button{width:100%}#inquiry-customer-workflow .icw-template-actions{justify-content:stretch}#inquiry-customer-workflow .icw-template-actions button{flex:1}}
</style>
<style>
/* Inquiry detail only — five summary tiles plus purpose/preference banner. */
.inquiry-wrap .inquiry-purpose-card{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px;margin-bottom:14px}
.inquiry-wrap .inquiry-purpose-card>div{display:grid;gap:6px;min-width:0;padding:16px 18px;border:1px solid #bad6f4;border-radius:13px;background:linear-gradient(135deg,#eaf4ff,#f7fbff)}
.inquiry-wrap .inquiry-purpose-card>div:nth-child(2){background:linear-gradient(135deg,#f8faff,#eef3f9);border-color:#d9e5f0}
.inquiry-wrap .inquiry-purpose-card span{font-size:.72rem;font-weight:900;letter-spacing:.065em;color:#3d6897}
.inquiry-wrap .inquiry-purpose-card strong{font-size:1.02rem;font-weight:850;color:#154b82;line-height:1.3;overflow-wrap:anywhere}
@media(max-width:600px){.inquiry-wrap .inquiry-purpose-card{gap:8px}.inquiry-wrap .inquiry-purpose-card>div{padding:12px 10px}.inquiry-wrap .inquiry-purpose-card span{font-size:.61rem}.inquiry-wrap .inquiry-purpose-card strong{font-size:.82rem}}

.inquiry-wrap .inquiry-details-overview{padding:17px}
.inquiry-wrap .inquiry-summary-grid{
  display:grid;grid-template-columns:repeat(3,minmax(0,1fr));
  gap:10px;width:100%;align-items:stretch
}
.inquiry-wrap .inquiry-summary-item{
  display:flex;flex-direction:column;gap:8px;min-width:0;min-height:94px;
  padding:15px 14px;border:1px solid #dce6f0;border-radius:13px;
  background:linear-gradient(155deg,#fbfdff,#f0f5fb)
}
.inquiry-wrap .inquiry-summary-item>span{
  display:block;color:#627992;font-size:.72rem;font-weight:850;
  letter-spacing:.035em;text-transform:uppercase;line-height:1.3
}
.inquiry-wrap .inquiry-summary-item>strong{
  color:#1a3859;font-size:.95rem;font-weight:780;line-height:1.4;
  white-space:normal;overflow-wrap:anywhere;word-break:normal
}
.inquiry-wrap .inquiry-extra-grid{
  display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px;
  margin-top:12px
}
.inquiry-wrap .inquiry-extra-grid .inquiry-field{
  min-width:0;display:flex;flex-direction:column;gap:6px;
  padding:11px 13px;border:1px solid #e0e8f2;border-radius:11px;
  background:#fafcff
}
.inquiry-wrap .inquiry-extra-grid .inquiry-field>span{
  font-size:.72rem;line-height:1.3
}
.inquiry-wrap .inquiry-extra-grid .inquiry-value{
  white-space:normal;overflow-wrap:anywhere;font-size:.88rem
}
.inquiry-wrap .inquiry-message-card{
  margin-top:12px;padding:18px 20px;
  border-left:3px solid #b94c0b
}
.inquiry-wrap .inquiry-message-card .inquiry-field{
  display:block;border-bottom:0;padding:0
}
.inquiry-wrap .inquiry-message-card .inquiry-field>span{
  display:block;margin-bottom:10px;color:#9b500d;font-size:.76rem
}
.inquiry-wrap .inquiry-message-card .inquiry-value{
  white-space:pre-wrap;overflow-wrap:anywhere;line-height:1.55;
  font-weight:550;font-size:.94rem
}
@media(max-width:750px){
  .inquiry-wrap .inquiry-details-overview{padding:12px}
  .inquiry-wrap .inquiry-summary-grid{gap:7px}
  .inquiry-wrap .inquiry-summary-item{
    min-height:83px;gap:7px;padding:11px 9px;border-radius:10px
  }
  .inquiry-wrap .inquiry-summary-item>span{
    font-size:.63rem;letter-spacing:.01em
  }
  .inquiry-wrap .inquiry-summary-item>strong{
    font-size:.76rem;line-height:1.35
  }
  .inquiry-wrap .inquiry-extra-grid{gap:7px;margin-top:9px}
  .inquiry-wrap .inquiry-message-card{
    padding:14px 15px;margin-top:10px
  }
  .inquiry-wrap .inquiry-head{
    margin-bottom:13px;gap:12px
  }
  .inquiry-wrap .inquiry-head h1{
    font-size:clamp(1.55rem,6vw,2rem);line-height:1.12
  }
}
@media(max-width:370px){
  .inquiry-wrap .inquiry-summary-item{padding:10px 6px;gap:6px}
  .inquiry-wrap .inquiry-summary-item>span{font-size:.59rem}
  .inquiry-wrap .inquiry-summary-item>strong{font-size:.69rem}
  .inquiry-wrap .inquiry-extra-grid{grid-template-columns:1fr}
}

<style>
/* R22.16 — inquiry detail only: preserve every remaining value on one line.
   Two wider tiles per mobile row; the final phone tile spans both columns. */
.inquiry-wrap .inquiry-summary-item > strong{
  white-space:nowrap;
  overflow-x:auto;
  overflow-y:hidden;
  overflow-wrap:normal;
  word-break:normal;
  scrollbar-width:none;
  max-width:100%;
  width:100%;
  font-variant-numeric:tabular-nums;
}
.inquiry-wrap .inquiry-summary-item > strong::-webkit-scrollbar{display:none}
.inquiry-wrap .inquiry-summary-item > span{
  white-space:nowrap;
  font-size:clamp(.62rem,.88vw,.72rem);
}
@media(max-width:750px){
  .inquiry-wrap .inquiry-summary-grid{
    grid-template-columns:repeat(2,minmax(0,1fr));
    gap:8px;
  }
  .inquiry-wrap .inquiry-summary-item{
    min-height:67px;
    padding:10px 11px;
    gap:5px;
    border-radius:11px;
  }
  .inquiry-wrap .inquiry-summary-item > span{
    font-size:.69rem;
    line-height:1.2;
    letter-spacing:0;
  }
  .inquiry-wrap .inquiry-summary-item > strong{
    font-size:clamp(.66rem,2.9vw,.84rem);
    line-height:1.4;
    letter-spacing:-.02em;
  }
  .inquiry-wrap .inquiry-summary-item:nth-child(5){
    grid-column:1/-1;
    min-height:60px;
  }
}
@media(max-width:350px){
  .inquiry-wrap .inquiry-summary-item{
    padding-inline:8px;
  }
  .inquiry-wrap .inquiry-summary-item > strong{
    font-size:.66rem;
    letter-spacing:-.035em;
  }
}
</style></style><link rel="stylesheet" href="/r13-interface.css?v=r22-clean"><link rel="stylesheet" href="/r13-pages.css?v=r22-2"><link rel="stylesheet" href="/r22-public-shell.css?v=r22-4"><link rel="stylesheet" href="/r22-layout-fixes.css?v=r22-32">
<style>
/* R22.14: Inquiry detail header only. Place the delete action beside the title,
   keeping the six information cards immediately below the heading. */
body[data-ui-page="inquiry"] .inquiry-wrap{
  margin-top:14px;
}
.inquiry-wrap > .inquiry-head{
  display:grid;
  grid-template-columns:minmax(0,1fr) auto;
  align-items:center;
  column-gap:14px;
  row-gap:8px;
  margin:0 0 13px;
}
.inquiry-wrap > .inquiry-head > div{
  min-width:0;
}
.inquiry-wrap > .inquiry-head > div > p{
  margin:0 0 5px;
}
.inquiry-wrap > .inquiry-head > div > h1{
  margin:0 0 9px;
  line-height:1.12;
}
.inquiry-wrap > .inquiry-head > form{
  justify-self:end;
  align-self:center;
  min-width:0;
  width:auto;
  margin:0;
}
.inquiry-wrap > .inquiry-head > form > .danger-button{
  display:inline-flex;
  align-items:center;
  justify-content:center;
  box-sizing:border-box;
  min-height:44px;
  min-width:78px;
  width:auto;
  margin:0;
  padding:9px 15px;
  border-radius:11px;
  font-size:.89rem;
  line-height:1.2;
  font-weight:850;
  white-space:nowrap;
}
@media(max-width:620px){
  body[data-ui-page="inquiry"] .inquiry-wrap{
    margin-top:10px;
  }
  .inquiry-wrap > .inquiry-head{
    grid-template-columns:minmax(0,1fr) auto;
    align-items:center;
    column-gap:10px;
    row-gap:0;
    margin-bottom:11px;
  }
  .inquiry-wrap > .inquiry-head > div > p{
    font-size:.7rem;
    margin-bottom:4px;
  }
  .inquiry-wrap > .inquiry-head > div > h1{
    margin:0 0 8px;
    font-size:clamp(1.55rem,6vw,2rem);
    line-height:1.13;
  }
  .inquiry-wrap > .inquiry-head > form > .danger-button{
    min-width:72px;
    min-height:44px;
    padding:9px 13px;
    font-size:.86rem;
  }
}
</style><script src="/r13-pages.js?v=r22-2" defer></script></head>
<body><header class="inquiry-top private-brand-header"><a class="private-brand-link" href="/" aria-label="İNOKSKAR ana sayfa"><img class="private-brand-image" src="/inokskar-header-brand.png" alt="İNOKSKAR Soğutma ve Endüstriyel Mutfak"></a><nav><a href="/admin?tab=inquiries">← Müşteri taleplerine dön</a></nav></header><main class="inquiry-wrap"><section class="inquiry-head"><div><p>YÖNETİM ALANI</p><h1>Talep detayı</h1><span class="inquiry-badge@@BADGE_CLASS@@">@@TYPE@@</span></div><form method="post" action="/admin/inquiries/@@ID@@/delete" onsubmit="return confirm('Bu müşteri talebi kalıcı olarak silinsin mi?')"><button class="danger-button" type="submit">Sil</button></form></section>@@DETAILS_BLOCK@@@@WORKFLOW_PANEL@@@@SERVICE_PANEL@@@@AUDIT_TIMELINE@@</main><script>
(()=>{const root=document.getElementById('inquiry-customer-workflow');if(!root)return;
const select=root.querySelector('#icw-preset'),status=root.querySelector('#icw-status'),date=root.querySelector('#icw-callback'),reply=root.querySelector('#icw-reply');
let generated='';
const localParts=()=>{const v=date.value;if(!v)return {tarih:'[tarih seçiniz]',saat:'[saat seçiniz]'};const p=v.split('T');const d=p[0].split('-');return {tarih:d.length===3?d[2]+'.'+d[1]+'.'+d[0]:'[tarih seçiniz]',saat:p[1]||'[saat seçiniz]'};};
const fill=()=>{const option=select.selectedOptions[0];if(!option||!option.value)return;const p=localParts(),now=new Date().toLocaleString('tr-TR',{dateStyle:'short',timeStyle:'short'});generated=(option.dataset.reply||'').replaceAll('{{tarih}}',p.tarih).replaceAll('{{saat}}',p.saat).replaceAll('{{şimdi}}',now);reply.value=generated;if(option.dataset.status)status.value=option.dataset.status;};
select.addEventListener('change',()=>{if(select.value)fill();});
date.addEventListener('change',()=>{if(select.value&&reply.value===generated)fill();});
})();
</script></body></html>
""";
        return html
            .Replace("@@TYPE@@", E(type), StringComparison.Ordinal)
            .Replace("@@BADGE_CLASS@@", service ? " service" : "", StringComparison.Ordinal)
            .Replace("@@ID@@", Uri.EscapeDataString(id), StringComparison.Ordinal)
            .Replace("@@DATE@@", E(DateText(inquiry)), StringComparison.Ordinal)
            .Replace("@@DETAILS_BLOCK@@", detailsBlock, StringComparison.Ordinal)
            .Replace("@@WORKFLOW_PANEL@@", workflowPanel, StringComparison.Ordinal)
            .Replace("@@SERVICE_PANEL@@", servicePanel, StringComparison.Ordinal)
            .Replace("@@AUDIT_TIMELINE@@", AuditTimeline(audit), StringComparison.Ordinal);
    }
    public static string NotFound() => "<!doctype html><html lang='tr'><meta charset='utf-8'><title>Talep bulunamadı</title><body><h1>Talep bulunamadı</h1><p><a href='/admin?tab=inquiries'>Müşteri taleplerine dön</a></p></body></html>";
}
