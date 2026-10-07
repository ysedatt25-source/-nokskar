using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public static class InquiryStatusPages
{
    public static string NormalizePhone(string? value)
    {
        var digits = Regex.Replace(value ?? "", @"\D", "");
        if (digits.StartsWith("0090", StringComparison.Ordinal) && digits.Length == 14) digits = digits[4..];
        else if (digits.StartsWith("90", StringComparison.Ordinal) && digits.Length == 12) digits = digits[2..];
        else if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length == 11) digits = digits[1..];
        return digits;
    }

    public static JsonArray Lookup(Store store, string? phone)
    {
        var normalized = NormalizePhone(phone);
        var result = new JsonArray();
        if (normalized.Length != 10) return result;

        var rows = store.Snapshot()["inquiries"]?.AsArray() ?? new JsonArray();
        foreach (var node in rows.OfType<JsonObject>().OrderByDescending(x => x["created"]?.ToString()).Take(250))
        {
            JsonObject data;
            try { data = JsonNode.Parse(node["data"]?.ToString() ?? "{}")?.AsObject() ?? new JsonObject(); }
            catch { continue; }

            if (!string.Equals(NormalizePhone(data["phone"]?.ToString()), normalized, StringComparison.Ordinal)) continue;

            var service = string.Equals(data["type"]?.ToString(), "service", StringComparison.OrdinalIgnoreCase);
            var status = service ? data["serviceStatus"]?.ToString() : node["status"]?.ToString();
            result.Add(new JsonObject
            {
                ["requestCode"] = node["requestCode"]?.ToString() ?? node["id"]?.ToString() ?? "",
                ["type"] = service ? "Servis Talebi" : "Destek Talebi",
                ["status"] = Status(status),
                ["statusKey"] = string.IsNullOrWhiteSpace(status) ? "new" : status,
                ["created"] = node["created"]?.ToString() ?? "",
                ["productName"] = data["productName"]?.ToString() ?? "",
                ["appointmentDate"] = service ? data["appointmentDate"]?.ToString() ?? "" : "",
                ["businessName"] = service ? data["businessName"]?.ToString() ?? "" : ""
            });
            if (result.Count >= 20) break;
        }
        return result;
    }

    static string Status(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "new" => "Yeni",
        "review" => "İnceleniyor",
        "scheduled" => "Servis planlandı",
        "parts" => "Parça bekliyor",
        "completed" => "Tamamlandı",
        "cancelled" => "İptal",
        _ => string.IsNullOrWhiteSpace(value) ? "Yeni" : value!
    };

    public static string Public()
    {
        return """
<!doctype html>
<html lang="tr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<meta name="theme-color" content="#252b33">
<title>Talep Durumu Sorgula | İnokskar</title>
<style>
:root{--navy:#102a46;--blue:#1767d4;--gold:#8b5d0d;--ink:#173553;--muted:#4f667d;--line:#dce6f1;--surface:#f3f7fb;--ok:#17734f;--warn:#b86a13;--danger:#b83645}*{box-sizing:border-box}html,body{margin:0;min-height:100%;font-family:Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif;color:var(--ink);background:linear-gradient(180deg,#eef4fa,#f8fafc 42%,#edf3f8)}a{color:inherit}.top{position:sticky;top:0;z-index:10;display:flex;align-items:center;justify-content:space-between;gap:16px;min-height:66px;padding:8px max(16px,calc((100% - 1120px)/2));background:radial-gradient(ellipse 55% 78% at 50% 50%,#fff 0%,#eef0f2 42%,#9ea5ab 73%,#293038 100%);border-bottom:1px solid rgba(26,37,48,.22);box-shadow:0 5px 18px rgba(12,28,47,.12)}.top img{display:block;width:auto;height:48px;max-width:min(280px,70vw);object-fit:contain}.top a{display:inline-flex;align-items:center;justify-content:center;min-height:42px;padding:0 14px;border:1px solid rgba(255,255,255,.18);border-radius:12px;background:#2d3742;color:#fff;text-decoration:none;font-size:.82rem;font-weight:800}.wrap{width:min(980px,calc(100% - 32px));margin:32px auto 64px}.hero{display:grid;grid-template-columns:minmax(0,1.2fr) minmax(280px,.8fr);gap:18px;align-items:stretch}.intro,.query-card,.result-card{border:1px solid var(--line);border-radius:22px;background:#fff;box-shadow:0 16px 38px rgba(14,39,68,.07)}.intro{padding:30px}.eyebrow{margin:0 0 8px;color:var(--gold);font-size:.72rem;font-weight:900;letter-spacing:.14em}.intro h1{margin:0 0 12px;font-size:clamp(2rem,5vw,3.5rem);line-height:1.02;letter-spacing:-.045em}.intro p{margin:0;color:var(--muted);line-height:1.7}.query-card{padding:24px;display:grid;align-content:center;gap:14px}.query-card label{display:grid;gap:7px;font-weight:820}.query-card small{color:var(--muted);font-weight:600;line-height:1.45}.phone-shell{display:grid;grid-template-columns:40px minmax(0,1fr);align-items:center;min-height:58px;border:1px solid #c9d8e8;border-radius:14px;background:#f9fbfd;overflow:hidden}.phone-shell span{display:grid;place-items:center;color:#41617f;font-weight:900}.phone-shell input{width:100%;height:56px;min-width:0;padding:0 13px;border:0;outline:0;background:transparent;color:#102a46;font:inherit;font-size:1.02rem;font-weight:700}.phone-shell:focus-within{border-color:#4d8de1;box-shadow:0 0 0 4px rgba(36,114,213,.11)}.query-card button{position:relative;overflow:hidden;min-height:52px;border:1px solid #1459bd;border-radius:13px;background:linear-gradient(180deg,#2c80e7,#1767d4);color:#fff;font:inherit;font-weight:900;box-shadow:0 10px 24px rgba(23,103,212,.22)}.query-card button:disabled{opacity:.62}.helper{display:grid;grid-template-columns:repeat(3,1fr);gap:8px}.helper span{padding:9px 8px;border:1px solid #e1e8f1;border-radius:10px;background:#f8fafc;text-align:center;color:#4f667d;font-size:.72rem;font-weight:750}.message{margin:18px 0 0;padding:14px 16px;border:1px solid #dbe6f1;border-radius:14px;background:#fff;color:#536a82;line-height:1.5}.message.error{border-color:#edcccc;background:#fff5f5;color:#9f3030}.results{display:grid;gap:12px;margin-top:18px}.result-card{padding:18px 20px}.result-top{display:flex;align-items:flex-start;justify-content:space-between;gap:14px}.result-top>div{display:grid;gap:4px}.result-type{color:#1767d4;font-size:.7rem;font-weight:900;letter-spacing:.1em;text-transform:uppercase}.result-code{font-size:1.18rem;font-weight:900}.status{display:inline-flex;align-items:center;justify-content:center;min-height:32px;padding:0 11px;border-radius:999px;background:#edf4ff;color:#165cae;font-size:.74rem;font-weight:900;white-space:nowrap}.status.completed{background:#eaf7f1;color:#176846}.status.cancelled{background:#fff0f1;color:#a83242}.status.scheduled{background:#fff5e8;color:#a45e0f}.result-meta{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:9px;margin-top:14px}.meta{padding:11px;border:1px solid #e3eaf2;border-radius:11px;background:#f9fbfd}.meta small{display:block;margin-bottom:3px;color:#526a83;font-size:.68rem;font-weight:800}.meta strong{display:block;color:#243f5a;font-size:.82rem;line-height:1.35;overflow-wrap:anywhere}.actions{display:flex;flex-wrap:wrap;gap:9px;margin-top:20px}.actions a{display:inline-flex;align-items:center;justify-content:center;min-height:42px;padding:0 13px;border:1px solid #d7e2ed;border-radius:11px;background:#fff;color:#274763;text-decoration:none;font-size:.78rem;font-weight:850}.privacy{margin-top:16px;color:#4f667d;font-size:.74rem;line-height:1.55}@media(max-width:760px){.top{min-height:58px;padding:5px 10px}.top img{height:42px;max-width:225px}.top a{min-height:38px;padding:0 10px}.wrap{width:calc(100% - 20px);margin:18px auto 42px}.hero{grid-template-columns:1fr;gap:11px}.intro{padding:20px 17px;border-radius:17px}.intro h1{font-size:2rem}.query-card{padding:16px;border-radius:17px}.helper{gap:6px}.helper span{font-size:.65rem;padding:8px 4px}.result-card{padding:15px;border-radius:16px}.result-top{align-items:center}.result-meta{grid-template-columns:1fr 1fr}.result-meta .meta:last-child{grid-column:1/-1}.actions{display:grid;grid-template-columns:1fr 1fr}.actions a{padding:0 8px}.privacy{padding-bottom:env(safe-area-inset-bottom)}} 
</style>
</head>
<body>
<header class="top"><a href="/" aria-label="Ana sayfa" style="padding:0;border:0;background:transparent"><img src="/inokskar-header-brand.png" alt="İNOKSKAR"></a><a href="/">Ana sayfa</a></header>
<main class="wrap">
<section class="hero">
<div class="intro"><p class="eyebrow">TALEP TAKİBİ</p><h1>Talebiniz hangi aşamada?</h1><p>Destek veya servis talebinde kullandığınız telefon numarasını girin. Türkiye numaralarında <strong>+90</strong>, <strong>0</strong> veya doğrudan <strong>5xx…</strong> biçimlerinin tamamı kabul edilir.</p></div>
<form id="inquiry-status-form" class="query-card" novalidate>
<label>Telefon numarası<div class="phone-shell"><span>☎</span><input id="status-phone" name="phone" type="tel" autocomplete="tel" inputmode="tel" maxlength="40" placeholder="542 000 00 00" required></div><small>Örnek: 5420000000 · 05420000000 · +905420000000</small></label>
<div class="helper"><span>+90 ile</span><span>0 ile</span><span>Kodsuz</span></div>
<button id="status-submit" type="submit">Talep durumunu sorgula</button>
</form>
</section>
<div id="status-message" class="message" hidden></div>
<section id="status-results" class="results" aria-live="polite"></section>
<div class="actions"><a href="/iletisim">Yeni destek talebi</a><a href="/servis-talebi">Yeni servis talebi</a></div>
<p class="privacy">Gizliliğiniz için bu ekranda yalnız talep numarası, talep türü, güncel durum, tarih ve servis planı gibi sınırlı bilgiler gösterilir.</p>
</main>
<script>
(()=>{
const form=document.getElementById('inquiry-status-form'),input=document.getElementById('status-phone'),button=document.getElementById('status-submit'),message=document.getElementById('status-message'),results=document.getElementById('status-results');
const normalize=value=>{let d=String(value||'').replace(/\D/g,'');if(d.startsWith('0090')&&d.length===14)d=d.slice(4);else if(d.startsWith('90')&&d.length===12)d=d.slice(2);else if(d.startsWith('0')&&d.length===11)d=d.slice(1);return d;};
const text=(tag,value,cls)=>{const el=document.createElement(tag);if(cls)el.className=cls;el.textContent=value;return el;};
const addMeta=(wrap,label,value)=>{const box=document.createElement('div');box.className='meta';box.append(text('small',label),text('strong',value||'—'));wrap.append(box);};
const render=rows=>{results.replaceChildren();for(const row of rows){const card=document.createElement('article');card.className='result-card';const top=document.createElement('div');top.className='result-top';const left=document.createElement('div');left.append(text('span',row.type,'result-type'),text('strong',row.requestCode,'result-code'));const status=text('span',row.status,'status '+String(row.statusKey||'').toLowerCase());top.append(left,status);const meta=document.createElement('div');meta.className='result-meta';const date=row.created?new Date(row.created).toLocaleString('tr-TR'):'—';addMeta(meta,'Talep tarihi',date);addMeta(meta,'Ürün / konu',row.productName||row.businessName||row.type);addMeta(meta,'Servis planı',row.appointmentDate?new Date(row.appointmentDate+'T00:00:00').toLocaleDateString('tr-TR'):'Henüz planlanmadı');card.append(top,meta);results.append(card);}};
form.addEventListener('submit',async e=>{e.preventDefault();message.hidden=true;message.className='message';results.replaceChildren();const normalized=normalize(input.value);if(normalized.length!==10){message.textContent='Geçerli bir Türkiye telefon numarası girin. +90, 0 veya ülke kodu olmadan yazabilirsiniz.';message.className='message error';message.hidden=false;input.focus();return;}button.disabled=true;button.textContent='Sorgulanıyor…';try{const response=await fetch('/api/inquiry-status',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({phone:input.value})});const body=await response.json().catch(()=>({}));if(!response.ok)throw new Error(body.error||'Talep durumu sorgulanamadı.');if(!body.inquiries?.length){message.textContent='Bu telefon numarasıyla eşleşen bir destek veya servis talebi bulunamadı.';message.hidden=false;return;}render(body.inquiries);message.textContent=body.inquiries.length+' talep bulundu.';message.hidden=false;}catch(err){message.textContent=err?.message||'Talep durumu sorgulanamadı.';message.className='message error';message.hidden=false;}finally{button.disabled=false;button.textContent='Talep durumunu sorgula';}});
})();
</script>
</body>
</html>
""";
    }
}
