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
    static string PurposeLabel(string? value, bool service)
    {
        if (service) return "Teknik servis";
        return (value ?? "").Trim().ToLowerInvariant() switch
        {
            "genel" => "Genel destek",
            "teklif" => "Teklif",
            "servis" => "Teknik servis",
            "yedek" => "Yedek parça",
            _ => ""
        };
    }

    static string WorkflowLabel(string? value) => (value ?? "new").Trim().ToLowerInvariant() switch
    {
        "review" => "İnceleniyor",
        "contacted" => "Arandı",
        "callback" => "Aranacak",
        "answered" => "Cevap verildi",
        "resolved" => "Çözüldü",
        "closed" => "Kapatıldı",
        _ => "Yeni"
    };
    static string WorkflowOptions(string current) =>
        Option("new", "Yeni", current) + Option("review", "İnceleniyor", current) +
        Option("contacted", "Arandı", current) + Option("callback", "Aranacak", current) +
        Option("answered", "Cevap verildi", current) + Option("resolved", "Çözüldü", current) +
        Option("closed", "Kapatıldı", current);

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
            + Field("Müşteri Tercihi", PurposeLabel(b["purpose"]?.ToString(), service))
            + Field("Ad / Yetkili", b["name"]?.ToString())
            + Field("E-posta", b["email"]?.ToString())
            + Field("Telefon", b["phone"]?.ToString())
            + (!string.IsNullOrWhiteSpace(b["contact"]?.ToString()) ? Field("İletişim", b["contact"]?.ToString()) : "")
            + (service ? Field("İşletme Adı", b["businessName"]?.ToString()) + Field("Servis Adresi", b["address"]?.ToString(), true) : "")
            + Field("Ürün", b["productName"]?.ToString())
            + Field("Ürün Kodu", b["productCode"]?.ToString())
            + Field("Seri Numarası", b["serialNumber"]?.ToString())
            + Field(service ? "Arıza / Servis Açıklaması" : "Mesaj", b["message"]?.ToString(), true);
        var workflowCurrent = b["customerStatus"]?.ToString() ?? "new";
        var workflowPanel = """
<section class="inquiry-card customer-workflow"><div class="workflow-title"><div><p>MÜŞTERİ İLETİŞİM SÜRECİ</p><h2>Süreci yönet</h2></div><span>@@WORKFLOW_LABEL@@</span></div><form method="post" action="/admin/inquiries/@@ID@@/workflow"><div class="workflow-grid"><label>Durum<select name="status">@@WORKFLOW_OPTIONS@@</select></label><label>Planlanan arama tarihi / saati<input name="callbackAt" type="datetime-local" value="@@CALLBACK@@"></label></div><label>Müşterinin göreceği cevap<textarea name="publicReply" rows="4" maxlength="3000" placeholder="Örn. Talebiniz incelendi. 08.10.2026 saat 14:00'te sizinle iletişime geçeceğiz.">@@PUBLIC_REPLY@@</textarea><small>Bu metin telefon numarasıyla yapılan talep takibinde müşteriye gösterilir.</small></label><label>Yalnız yönetimin göreceği iç not<textarea name="internalNote" rows="3" maxlength="3000" placeholder="Müşteriye gösterilmez.">@@WORKFLOW_INTERNAL@@</textarea></label><button class="primary-button" type="submit">Süreci güncelle</button></form></section>
"""
            .Replace("@@ID@@", Uri.EscapeDataString(id), StringComparison.Ordinal)
            .Replace("@@WORKFLOW_LABEL@@", E(WorkflowLabel(workflowCurrent)), StringComparison.Ordinal)
            .Replace("@@WORKFLOW_OPTIONS@@", WorkflowOptions(workflowCurrent), StringComparison.Ordinal)
            .Replace("@@CALLBACK@@", E(b["callbackAt"]?.ToString()), StringComparison.Ordinal)
            .Replace("@@PUBLIC_REPLY@@", E(b["publicReply"]?.ToString()), StringComparison.Ordinal)
            .Replace("@@WORKFLOW_INTERNAL@@", E(b["workflowInternalNote"]?.ToString()), StringComparison.Ordinal);
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
<!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"><meta name="theme-color" content="#252b33"><meta name="mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-status-bar-style" content="black-translucent"><meta name="apple-mobile-web-app-title" content="İNOKSKAR"><title>@@TYPE@@ Detayı | İnokskar</title><link rel="stylesheet" href="/r98-inquiry.css"><link rel="stylesheet" href="/r123-professional.css"><script src="/r123-security.js" defer></script><style>.customer-workflow,.service-workflow{margin-top:18px}.workflow-title{display:flex;align-items:center;justify-content:space-between;gap:14px;margin-bottom:14px}.workflow-title p{margin:0 0 4px;color:#2769b7;font-size:.72rem;font-weight:900;letter-spacing:.1em}.workflow-title h2{margin:0}.workflow-title>span{display:inline-flex;padding:6px 10px;border:1px solid #cbdcf0;border-radius:999px;background:#eef5ff;color:#1d5d9d;font-size:.75rem;font-weight:850}.customer-workflow form,.service-workflow form{display:grid;gap:14px}.customer-workflow label,.service-workflow label{display:grid;gap:7px;font-weight:700}.customer-workflow label small{font-weight:500;color:#667b91;line-height:1.45}.customer-workflow input,.customer-workflow textarea,.customer-workflow select,.service-workflow input,.service-workflow textarea,.service-workflow select{width:100%;padding:11px 12px;border:1px solid #ccd9eb;border-radius:8px;font:inherit;background:#fff}.service-workflow{margin-top:18px}.service-workflow h2{margin-top:0}.service-workflow form{display:grid;gap:14px}.service-workflow label{display:grid;gap:7px;font-weight:700}.service-workflow input,.service-workflow textarea,.service-workflow select{width:100%;padding:11px 12px;border:1px solid #ccd9eb;border-radius:8px;font:inherit;background:#fff}.workflow-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}.primary-button{justify-self:start;border:0;border-radius:8px;background:#1455c0;color:#fff;padding:11px 16px;font-weight:800}@media(max-width:760px){.workflow-grid{grid-template-columns:1fr}}</style><link rel="stylesheet" href="/r13-interface.css"><link rel="stylesheet" href="/r13-pages.css?v=r22-3"><script src="/r13-pages.js?v=r22-3" defer></script></head>
<body><header class="inquiry-top private-brand-header"><a class="private-brand-link" href="/" aria-label="İNOKSKAR ana sayfa"><img class="private-brand-image" src="/inokskar-header-brand.png" alt="İNOKSKAR Soğutma ve Endüstriyel Mutfak"></a><nav><a href="/admin?tab=inquiries">← Müşteri taleplerine dön</a></nav></header><main class="inquiry-wrap"><section class="inquiry-head"><div><p>YÖNETİM ALANI</p><h1>Talep detayı</h1><span class="inquiry-badge@@BADGE_CLASS@@">@@TYPE@@</span></div><form method="post" action="/admin/inquiries/@@ID@@/delete" onsubmit="return confirm('Bu müşteri talebi kalıcı olarak silinsin mi?')"><button class="danger-button" type="submit">Sil</button></form></section><section class="inquiry-card"><div class="inquiry-meta"><span>Talep tarihi</span><strong>@@DATE@@</strong></div>@@FIELDS@@</section>@@WORKFLOW_PANEL@@@@SERVICE_PANEL@@@@AUDIT_TIMELINE@@</main></body></html>
""";
        return html
            .Replace("@@TYPE@@", E(type), StringComparison.Ordinal)
            .Replace("@@BADGE_CLASS@@", service ? " service" : "", StringComparison.Ordinal)
            .Replace("@@ID@@", Uri.EscapeDataString(id), StringComparison.Ordinal)
            .Replace("@@DATE@@", E(DateText(inquiry)), StringComparison.Ordinal)
            .Replace("@@FIELDS@@", fields, StringComparison.Ordinal)
            .Replace("@@WORKFLOW_PANEL@@", workflowPanel, StringComparison.Ordinal)
            .Replace("@@SERVICE_PANEL@@", servicePanel, StringComparison.Ordinal)
            .Replace("@@AUDIT_TIMELINE@@", AuditTimeline(audit), StringComparison.Ordinal);
    }
    public static string PublicTracking() => """
<!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover"><meta name="theme-color" content="#252b33"><title>Talep Süreci Takibi | İNOKSKAR</title><link rel="stylesheet" href="/r13-interface.css"><link rel="stylesheet" href="/r13-pages.css?v=r22-3"><style>
*{box-sizing:border-box}body{margin:0;background:#eef3f8;color:#173653;font-family:Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif}.track-top{min-height:66px;display:flex;align-items:center;justify-content:space-between;gap:18px;padding:9px clamp(16px,4vw,42px);background:linear-gradient(180deg,#fff,#edf2f6);border-bottom:1px solid #d6e0ea}.track-top img{display:block;width:min(260px,58vw);height:48px;object-fit:contain}.track-top a{color:#1d5e9d;font-weight:800;text-decoration:none}.track-wrap{width:min(980px,calc(100% - 28px));margin:28px auto 80px}.track-hero{padding:clamp(24px,5vw,44px);border-radius:24px;background:linear-gradient(145deg,#082748,#0e477a);color:#fff;box-shadow:0 18px 46px rgba(13,45,76,.16)}.track-hero p{margin:0 0 8px;color:#8fc3ff;font-size:.76rem;font-weight:900;letter-spacing:.14em}.track-hero h1{margin:0 0 12px;font-size:clamp(2rem,5vw,3.35rem);line-height:1.02;letter-spacing:-.04em}.track-hero>span{display:block;max-width:650px;color:#d5e4f2;line-height:1.6}.track-card{margin-top:18px;padding:clamp(20px,4vw,32px);border:1px solid #d6e1ec;border-radius:22px;background:#fff;box-shadow:0 10px 30px rgba(18,48,78,.07)}.track-form{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:10px;align-items:end}.track-form label{display:grid;gap:8px;font-weight:800}.track-form input{min-height:52px;padding:0 15px;border:1px solid #cbd9e8;border-radius:12px;font:inherit;font-size:16px}.track-form button{min-height:52px;padding:0 22px;border:0;border-radius:12px;background:linear-gradient(180deg,#2681ec,#1565c6);color:#fff;font:inherit;font-weight:900;box-shadow:0 8px 18px rgba(21,101,198,.22)}.track-help{margin:10px 0 0;color:#6b7f93;font-size:.82rem;line-height:1.5}.track-message{margin-top:16px;padding:13px 14px;border-radius:12px;background:#f2f6fa;color:#526a82}.track-message.error{background:#fff0f0;color:#9e3333}.track-results{display:grid;gap:14px;margin-top:18px}.track-result{padding:18px;border:1px solid #d9e4ee;border-radius:17px;background:#fff}.track-result-head{display:flex;justify-content:space-between;align-items:start;gap:14px}.track-result-head strong{font-size:1.03rem}.track-result-head small{color:#718397}.track-status{display:inline-flex;align-items:center;min-height:30px;padding:5px 10px;border-radius:999px;background:#edf5ff;color:#175eaa;font-size:.75rem;font-weight:900}.track-meta{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px;margin-top:14px}.track-meta div{padding:10px;border-radius:11px;background:#f5f8fb}.track-meta span{display:block;color:#728397;font-size:.68rem;font-weight:850;text-transform:uppercase}.track-meta strong{display:block;margin-top:4px;font-size:.82rem;overflow-wrap:anywhere}.track-reply{margin-top:14px;padding:14px;border-left:4px solid #2477d8;border-radius:10px;background:#f2f7fd}.track-reply span{display:block;margin-bottom:5px;color:#52708d;font-size:.7rem;font-weight:900;text-transform:uppercase}.track-reply p{margin:0;white-space:pre-wrap;line-height:1.55}.track-callback{margin-top:12px;padding:11px 13px;border-radius:10px;background:#fff7e9;color:#8b5b13;font-weight:800}.track-empty{padding:20px;border:1px dashed #cddae7;border-radius:14px;text-align:center;color:#61768d}.track-privacy{margin-top:14px;color:#78899a;font-size:.75rem;line-height:1.5}@media(max-width:620px){.track-top{min-height:58px;padding:6px 14px}.track-top img{height:43px}.track-top a{font-size:.78rem}.track-wrap{width:calc(100% - 18px);margin:14px auto 90px}.track-hero{padding:22px 18px;border-radius:18px}.track-card{padding:17px 14px;border-radius:17px}.track-form{grid-template-columns:1fr}.track-form button{width:100%}.track-result-head{flex-direction:column}.track-meta{grid-template-columns:1fr 1fr}.track-meta div:last-child{grid-column:1/-1}}@media(max-width:390px){.track-meta{grid-template-columns:1fr}.track-meta div:last-child{grid-column:auto}}</style></head><body><header class="track-top"><a href="/" aria-label="İNOKSKAR ana sayfa"><img src="/inokskar-header-brand.png" alt="İNOKSKAR"></a><a href="/iletisim">Yeni talep oluştur</a></header><main class="track-wrap"><section class="track-hero"><p>TALEP TAKİBİ</p><h1>Süreci telefon numaranızla takip edin.</h1><span>Talep oluştururken kullandığınız telefon numarasını girin. Yönetimin güncel durumunu, planlanan arama zamanını ve size iletilen cevabı burada görebilirsiniz.</span></section><section class="track-card"><form id="track-form" class="track-form" novalidate><label>Talepte kullandığınız telefon numarası<input name="phone" type="tel" required maxlength="40" inputmode="tel" autocomplete="tel" placeholder="542 xxx xx xx veya +90 542 xxx xx xx"></label><button type="submit">Taleplerimi göster</button></form><p class="track-help"><strong>Ülke kodu isteğe bağlıdır.</strong> 542…, 0542…, 90542… veya +90542… biçimlerinin tümünü kullanabilirsiniz. Güvenlik için sonuçlarda adınız, e-posta adresiniz ve adresiniz gösterilmez.</p><div id="track-message" class="track-message" hidden></div><div id="track-results" class="track-results"></div><p class="track-privacy">Bu ekran yalnız talep durumunu görüntüler. Yeni bilgi veya belge göndermek için iletişim ya da servis talebi ekranını kullanın.</p></section></main><script>
(function(){const form=document.getElementById('track-form'),box=document.getElementById('track-results'),msg=document.getElementById('track-message');const phoneInput=form.elements.namedItem('phone');const normalizePhone=value=>{const raw=String(value||'').trim();if(!/^\+?[\d\s().-]+$/.test(raw))return null;let d=raw.replace(/\D/g,'');if(d.length===14&&d.startsWith('0090'))d=d.slice(4);else if(d.length===12&&d.startsWith('90'))d=d.slice(2);else if(d.length===11&&d.startsWith('0'))d=d.slice(1);return d.length===10?d:null;};const labels={new:'Yeni',review:'İnceleniyor',contacted:'Arandı',callback:'Aranacak',answered:'Cevap verildi',resolved:'Çözüldü',closed:'Kapatıldı',scheduled:'Servis planlandı',parts:'Parça bekliyor',completed:'Tamamlandı',cancelled:'İptal'};const purposes={genel:'Genel destek',teklif:'Teklif',servis:'Teknik servis',yedek:'Yedek parça'};const fmt=v=>{if(!v)return '—';const d=new Date(v);return Number.isNaN(d.getTime())?v:d.toLocaleString('tr-TR',{dateStyle:'medium',timeStyle:'short'});};const el=(tag,cls,text)=>{const x=document.createElement(tag);if(cls)x.className=cls;if(text!=null)x.textContent=text;return x;};form.addEventListener('submit',async e=>{e.preventDefault();box.replaceChildren();msg.hidden=true;const phone=String(new FormData(form).get('phone')||'').trim();const normalized=normalizePhone(phone);if(!normalized){msg.hidden=false;msg.className='track-message error';msg.textContent='Telefon numarasını 542 xxx xx xx, 0542 xxx xx xx veya +90 542 xxx xx xx biçiminde girin. Ülke kodu isteğe bağlıdır.';phoneInput&&phoneInput.focus();return;}const button=form.querySelector('button');button.disabled=true;button.textContent='Kontrol ediliyor…';try{const r=await fetch('/api/inquiry/track',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({phone})});const b=await r.json().catch(()=>({}));if(!r.ok)throw new Error(b.error||'Talep bilgileri alınamadı.');if(!b.found||!Array.isArray(b.records)||!b.records.length){msg.hidden=false;msg.className='track-message';msg.textContent='Bu telefon numarasıyla eşleşen kayıtlı talep bulunamadı.';return;}for(const row of b.records){const card=el('article','track-result');const head=el('div','track-result-head'),left=el('div');left.append(el('strong','',row.requestCode||'Talep'));left.append(el('small','',fmt(row.created)));head.append(left);head.append(el('span','track-status',labels[row.status]||'İnceleniyor'));card.append(head);const meta=el('div','track-meta');const a=el('div');a.append(el('span','','Talep türü'));a.append(el('strong','',row.type==='service'?'Teknik servis':(purposes[row.purpose]||'Destek')));const b1=el('div');b1.append(el('span','','Ürün'));b1.append(el('strong','',row.productName||row.productCode||'Genel talep'));const c=el('div');c.append(el('span','','Son güncelleme'));c.append(el('strong','',fmt(row.updated)));meta.append(a,b1,c);card.append(meta);if(row.callbackAt){const cb=el('div','track-callback','Planlanan iletişim: '+fmt(row.callbackAt));card.append(cb);}if(row.publicReply){const reply=el('div','track-reply');reply.append(el('span','','İnokskar yanıtı'));reply.append(el('p','',row.publicReply));card.append(reply);}box.append(card);}}catch(err){msg.hidden=false;msg.className='track-message error';msg.textContent=err&&err.message?err.message:'Talep bilgileri alınamadı.';}finally{button.disabled=false;button.textContent='Taleplerimi göster';}});})();
</script></body></html>
""";

    public static string NotFound() => "<!doctype html><html lang='tr'><meta charset='utf-8'><title>Talep bulunamadı</title><body><h1>Talep bulunamadı</h1><p><a href='/admin?tab=inquiries'>Müşteri taleplerine dön</a></p></body></html>";
}
