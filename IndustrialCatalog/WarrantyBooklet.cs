using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public static class WarrantyBooklet
{
    static string E(string? s)=>WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(s)?"—":s);
    public static string Build(JsonObject w,JsonObject? settings,string record)
    {
        string V(string k)=>E(w[k]?.ToString());
        var mode=w["technicalLabel"]?["mode"]?.ToString()??"none";
        if(!new[]{"none","cooling","heating","both"}.Contains(mode))mode="none";
        string Frame(int n,string title,string body)=>$"<article class=\"booklet-page booklet-page-{n}\" data-booklet-page=\"{n}\"><header class=\"booklet-head\"><span>İNOKSKAR · GARANTİ & KULLANIM</span><b>{title}</b></header><div class=\"booklet-body\">{body}</div><footer class=\"booklet-foot\"><span>{V("productCode")} · {V("serialNumber")}</span><b>{n} / 4</b></footer></article>";
        var cover=$"<div class=\"booklet-cover\"><img class=\"booklet-logo\" src=\"/inokskar-logo.svg\" alt=\"İNOKSKAR\"><p class=\"booklet-eyebrow\">PROFESYONEL MUTFAK EKİPMANLARI</p><h1>GARANTİ VE<br>KULLANIM<br>KİTAPÇIĞI</h1><div class=\"booklet-cover-product\"><small>ÜRÜN / MODEL</small><h2>{V("productName")}</h2><p>Ürün kodu: {V("productCode")}<br>Seri numarası: {V("serialNumber")}</p></div><div class=\"booklet-cover-bottom\"><p>Kurulum · Güvenli kullanım · Servis</p><p>Bu kitapçığı fatura ve ürüne özel kılavuzla birlikte saklayın.</p></div></div>";
        var terms="""
<h2>Garanti şartları</h2>
<ol class="booklet-list">
<li><strong>Süre ve kayıt.</strong> Garanti başlangıcı teslim tarihidir. Süre ve cihaz bilgileri 2. sayfada belirtilmiştir. Belgeyi faturayla birlikte saklayın.</li>
<li><strong>Servis başvurusu.</strong> Arızayı firma / yetkili servise; seri numarası, teslim tarihi ve arıza açıklamasıyla bildirin. Tehlikeli durumda cihazı kullanmayın.</li>
<li><strong>Kapsam.</strong> Garanti kapsamındaki imalat ve malzeme kaynaklı arızalar değerlendirilir. Ücretli işlem öncesinde kapsam ve bedel açıklanır. Tüketici işlemlerindeki zorunlu kapsam, bileşen tablosuyla daraltılamaz.</li>
<li><strong>Kullanım kaynaklı hasar.</strong> Hatalı bağlantı, amaç dışı kullanım, yetersiz temizlik veya yetkisiz müdahalenin neden olduğu hasar servis incelemesiyle değerlendirilir; yalnız bu durumların varlığı tüm garantiyi kendiliğinden kaldırmaz.</li>
</ol>
<section class="booklet-box" data-booklet-mode="cooling both"><h3>Soğutucuya özel değerlendirme</h3><p>Hava kanallarının kapatılması, kirli kondenser, kapı / conta hasarı ve soğutma devresine müdahalenin arızayla ilişkisi incelenir. Gaz eksilmesinin nedeni belirlenmeden otomatik kullanım hatası sayılmaz.</p></section>
<section class="booklet-box" data-booklet-mode="heating both"><h3>Isıtıcıya özel değerlendirme</h3><p>Uygunsuz elektrik / gaz beslemesi, cihazın izin vermediği susuz veya yağsız çalışma, biriken yanmış artıklar ve emniyet parçalarının iptaliyle oluşan hasar teknik incelemeyle değerlendirilir.</p></section>
<section class="booklet-box booklet-rights"><h3>Yasal haklar</h3><p>Tüketici işlemlerinde kanuni şartlarla ücretsiz onarım, değişim, indirim veya bedel iadesi hakları saklıdır. Garanti içi onarım süresi garantiye eklenir; ilgili azami tamir süresine uyulur. Kullanım hatası raporu müşteriye verilir. Uyuşmazlıklarda görevli hakem heyeti / mahkemeye başvurulabilir. Ticari işlemlerde ilgili mevzuat ve sözleşme uygulanır.</p></section>
""";
        var instructions="""
<h2>Güvenli kullanım</h2>
<p class="booklet-callout">Bu özet, modelin kendi kılavuzunun yerine geçmez. Etiket değerleri, bağlantı şeması ve yetkili servis talimatları esas alınır.</p>
<section class="booklet-box"><h3>01 · Kurulum ve hazırlık</h3><p>Montaj ve enerji bağlantısını yetkili personele yaptırın. Cihazı dengeli yerleştirin; havalandırma ve servis erişimini açık tutun. Elektrik beslemesi, topraklama ve korumalar cihaz etiketine uygun olmalıdır. Eğitilmemiş kişiler cihazı kullanmamalıdır.</p></section>
<section class="booklet-box" data-booklet-mode="cooling both"><h3>02 · Sanayi soğutma</h3><p>Taşıma sonrası bekleme ve ilk çalıştırma için model kılavuzunu izleyin. Uygun sıcaklığa ulaşmadan ürün yüklemeyin. Sıcak gıda koymayın; rafları aşırı doldurmayın ve hava dolaşımını engellemeyin. Kapıyı gereksiz açık bırakmayın. Sıcaklık alarmında gıdayı güvenli koşullara alın ve servise başvurun.</p><p>Soğutma borularını delmeyin; buz çözmede kesici alet veya üreticinin izin vermediği yöntemleri kullanmayın. Gaz devresi ve kondenser bakımı yetkili personelce, modelin bakım planına göre yapılmalıdır.</p></section>
<section class="booklet-box" data-booklet-mode="heating both"><h3>02 · Endüstriyel mutfak ısıtıcıları</h3><p>Kontrol ve termostatları model talimatına göre ayarlayın. Sıcak yüzey ve buhara karşı uygun koruyucu kullanın; yanıcıları uzak tutun. Su / yağ hazneli cihazlarda gerekli dolum seviyesini koruyun. Emniyet termostatını devre dışı bırakmayın.</p><p>İş bitiminde kontrolü kapalı konuma alın. Gazlı modelde bağlantı / kaçak kontrolünü yetkili personele yaptırın. Gaz kokusunda ateş ve elektrik anahtarı kullanmayın; güvenli ise gazı kapatın, ortamı havalandırın ve alanı terk ederek uzman yardımı isteyin.</p></section>
<section class="booklet-box"><h3>03 · Temizlik ve bakım</h3><p>Temizlikten önce enerjiyi kesin; ısıtıcıyı soğutun. Uygun bez ve yüzeye uygun temizleyici kullanın. Elektrik bölümlerine su tutmayın; basınçlı su / buhar ve aşındırıcı araç kullanmayın. Conta, tahliye ve yüzeyleri kontrol edin; arızalı cihazı çalıştırmayın.</p></section>
""";
        var contact=E(string.Join(" · ",new[]{settings?["phone"]?.ToString(),settings?["email"]?.ToString()}.Where(x=>!string.IsNullOrWhiteSpace(x))));
        var tech=TechnicalLabel.Html(w["technicalLabel"]);
        var extra=tech==""?"":"<section class=\"booklet-tech\"><h3>Cihaz teknik bilgileri</h3><div>"+tech+"</div></section>";
        // Reuse the validated record content, with a dedicated readable A5 layout.
        record=Regex.Replace(record,@"<header class=""certificate-head""[\s\S]*?</header>","",RegexOptions.CultureInvariant);
        record=record.Replace("class=\"certificate\"","class=\"certificate booklet-record\"",StringComparison.Ordinal);
        record=record.Replace("<div class=\"certificate-signatures\">",extra+"<div class=\"certificate-signatures\">",StringComparison.Ordinal);
        instructions+=$"<div class=\"booklet-service\"><strong>Servis ve garanti sorgulama</strong><p>{contact}</p><p><span data-query-address></span></p><p>QR kodu 2. sayfadadır. CE belgesi, kayıtta varsa sorgu ekranından açılır. Modele özel kullanım kılavuzu ayrıca teslim edilir.</p></div>";
        return $"<div class=\"warranty-booklet\" data-mode=\"{mode}\">"+Frame(1,"Garanti ve kullanım kitapçığı",cover)+Frame(2,"Garanti belgesi",record+(string.IsNullOrWhiteSpace(w["verificationCode"]?.ToString())?"<div class=\"booklet-qr\">QR kodu kayıt sonrasında oluşturulur.</div>":"<div class=\"booklet-qr\"><img data-warranty-qr alt=\"Garanti sorgulama QR kodu\"><span>Garantiyi doğrula</span></div>"))+Frame(3,"Garanti şartları",terms)+Frame(4,"Kullanım talimatı",instructions)+"</div>";
    }
}
