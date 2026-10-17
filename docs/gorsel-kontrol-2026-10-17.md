# İnokskar — görsel kontrol ve değişiklik notları

17 Ekim 2026. Canlı adres: https://inokskar-web-preview.up.railway.app/

## Yedek ve geri dönüş

Değişiklikten önce kaynak kodu `backup/2026-10-17-before-visual-review` dalında, `1a9fe7ae641c70dc403a4d580ad2b85be16df087` sürümünde korundu. Yönetimden tam ZIP yedeği indirildi; ZIP bütünlük kontrolü başarılı (27 dosya, 45.865.735 bayt). Katalog, dosyalar ve uygulamanın ZIP'e dahil ettiği operasyon verilerini içerir. Ayrı sunucu kimlik bilgileri / tüm çalışma ortamının disk görüntüsü olduğu iddia edilmez. Dosya adı: `inokskar-before-review-20261017-1215.zip`.

Kaynak geri dönüşü yedek daldan yapılabilir. Veri geri yüklemesi, yedekten sonra girilen kayıtları da etkileyebileceğinden yalnızca gerçekten gerekirse uygulanmalıdır. Railway önceki başarılı dağıtımları da geri alınabilir durumda tutuyor.

## Yöntem ve kapsam

Sayfalar tarayıcıda tek tek açılarak, ekran görüntülerine bakılarak incelendi. Otomatik tarama puanı görsel kontrol yerine kullanılmadı. Telefon, tablet ve masaüstü için sırasıyla 390, 768 ve 1280 piksel genişlikler kullanıldı. Bunlar aynı Chrome ortamındaki üç ekran ölçüsüdür; üç fiziksel cihaz / Safari / Android yazdırma hizmeti testi değildir.

Üç ölçüde gözle incelenen sayfa aileleri:

- Ana sayfa, ürün listesi, üç ana kategori, dört ürünün detay sayfaları.
- Kebap Dolabı büyütülmüş görsel penceresi; telefon, tablet ve masaüstünde görselin tamamı sığıyor.
- Hakkımızda, iletişim / destek, garanti sorgulama, talep sorgulama, referanslar, gizlilik / KVKK / çerez sayfaları.
- Giriş, müşteri kaydı, şifre hatırlatma (form görünümü; yeni hesap veya şifre değişikliği yapılmadı).
- Yönetim genel bakış, ürünler / ürün formu, kategoriler, referanslar, fiyat yönetimi, istatistikler, müşteri talepleri, geçmiş / yedek.
- Site ayarlarının kategori görselleri, marka, ana sayfa, iletişim, bildirim, kur ve üst menü bölümleri.
- Teklif listesi ve editörü; ölçü/adet değişimleri ve satır hesapları; mevcut 15 satırlı belgenin iki A4 sayfası.
- Garanti kayıt formu / listesi, teknik cihaz listesi ve örnek cihazın teknik bilgiler, parça, belge, servis ve geçmiş sekmeleri.
- Kullanıcılar / yeni kullanıcı penceresi, müşteri üyelikleri, güvenlik, sistem durumu, katalog sürümleri, otomatik yedekler, e-posta kuyruğu.
- SMS, e-posta, API sağlayıcıları ve canlı destek yönetimi (boş sohbet durumu).

Bu liste her sayfanın her olası veri/izin/hata durumunun eksiksiz görüldüğü anlamına gelmez. Uzun sayfaların bütün kaydırma konumları ve tüm kayıtların ayrıntıları için %100 doğrulama iddiası yoktur. Boş talep, sohbet ve e-posta kuyruklarında dolu kayıt görünümü canlı veriden incelenemedi. Müşteri hesabının özel paneli, şifre yenileme bağlantısının token gerektiren devamı ve gerçek telefon/yazıcı sonucu ayrıca doğrulanmalıdır.

## Bulgu ve yapılan düzeltmeler

| Bulgu | Yapılan işlem |
| --- | --- |
| Tarayıcıdan yazdırırken araya boş sayfa girmesi | Ölçekli ekran önizleme çerçevesinin baskıda kutu üretmesi kaldırıldı; sayfa sonu tek A4 katmanında yönetiliyor. 296 mm baskı yüksekliği ve içerik sığdırmada emniyet payı eklendi. |
| Ölçü ile adet aynı alanda kullanılıyordu | Standart / metre / M² / çap türü, ilgili ölçü ve adet ayrı alanlara ayrıldı. |
| 2 metre × 3 adet yazılamıyordu | Metre/M² satırı tutarı = ölçü × adet × birim fiyat. Çap boyuttur; çap satırı tutarı = adet × birim fiyat. İskonto ve KDV bundan sonra uygulanır. |
| Eski metre/M² tekliflerinin anlamı | Eski miktar ölçüye, adet 1'e taşınır; toplam korunur. Tekrar açıp kaydetmek ikinci kez çarpmaz. |
| Belgedeki miktar bilgisi yetersizdi | Aynı sütunda Ölçü / Adet iki satırla gösteriliyor; çap ürün açıklamasında, adet miktar sütununda korunuyor. |
| Ürün fiyatının metre/M² başına olduğu görünmüyordu | Liste ve detay fiyatlarına uygun birim eklendi. |
| Boş kategoride yanıltıcı arama mesajı ve etkisiz filtre temizleme | Kategoriye uygun boş durum, tüm ürünler ve teklif bağlantıları; filtre temizleme yalnız aktif filtre varsa. |
| Referanslar başlığında anlamsal ana başlık eksikti | Sayfa başlığı h1 oldu. |
| Tablet destek/garanti formunu yüzen düğmeler örtüyordu | 1100 piksele kadar form üzerindeki yüzen iletişim düğmeleri gizlendi. Talep takibine ve boş kategori eylemlerine de aynı kural eklendi. |
| Fiyat tablosunda seçim/sayım ve yatay kaydırma anlaşılmıyordu | Seçili/toplam sayısı ve küçük ekranlarda kaydırma ipucu eklendi. |
| Yönetim listelerinde mobil arama satırı sıkışıyordu | Arama tam satıra, kayıt sayısı ayrı hizaya alındı. |
| Silinmiş ürüne ait istatistik genel WhatsApp gibi görünüyordu | Katalogdan kaldırılmış ürün olarak belirtiliyor. |
| Teknik cihaz kodu/seri ve eylemler bitişikti | Bilgiler aralıklı alt alta; eylemler arasında boşluk. |
| Özel teknik sayfada müşteri canlı destek başlatıcısı görünüyordu | Teknik dosya yolları özel sayfa olarak hariç tutuldu. |
| Teknik dosya açıklaması uygulama terimleri içeriyordu | Yetkiye ilişkin anlaşılır Türkçe metin kullanıldı. |
| Yeni kullanıcı penceresinde yatay taşma ve çift kaydırma | Form genişliği sınırlandı, arka sayfa kilitlendi; başlık kenar boşluğu düzeltildi. Mobil canlı kontrolde yatay taşma yok ve tek kaydırma alanı var. |
| Mobil e-posta kaydet/dön düğmelerinin boyu ve hizası farklıydı | Tam genişlikte alt alta eylemler; sağlayıcı bağlantıları arasında boşluk. |
| Mobil giriş yardımcı bağlantıları dengesiz sarılıyordu | İki sütun, tek kalan son bağlantı tam genişlik. Teknik cookie açıklaması yerine hesap kullanımına ilişkin kısa metin. |

## İçerik için gerekli bilgiler

- Endüstriyel Mutfak ve Ticari Mobilya kategorilerinde ürün yok. Gerçek ürün isimleri, kodları, fotoğrafları ve fiyatları gerekli; uydurma ürün eklenmedi.
- Yuvarlak çikolata dolabının açıklama/teknik bilgilerinin tamamlanması gerekiyor. Gerçek çap, kapasite, sıcaklık ve malzeme bilgisi gerekli; görselden teknik değer türetilmedi.
- Site adında “SOĞUMA” yazıyor; kurumsal metnin “SOĞUTMA” olarak düzeltilmesi önerilir. Kayıtlı işletme içeriği değiştirilmedi.
- Görsel ölçüsü etiketleri işletme tarafından doğrulanmalı; özellikle yuvarlak üründeki metre ibaresi ile gerçek çap bilgisi aynı kavram değil.

## Doğrulama ve sınırlar

Ön yüz üretim derlemesi, TypeScript, stil sözleşmesi ve mevcut form kontrolleri geçti. Railway Docker derlemesinde teklif/marka, üyelik, teknik, yedek, canlı destek ve diğer mevcut kontroller geçti. Yeni teklif kontrolleri 2 metre × 3 adet, M², Ø100 × 2 adet, geçersiz ölçü ve eski teklif geçişini kapsıyor. Bu kod kontrolleri manuel görsel incelemenin yerine sayılmadı.

Canlı belge 15 satırı iki A4 sayfasına yerleştiriyor. Bu ortam işletim sisteminin yazdırma önizlemesini göstermediğinden, Android'deki doğrudan Yazdır sonucunda boş sayfa kalmadığı kesin olarak doğrulanmış değildir. Aynı teklifin telefonda Yazdır menüsünde son kontrolü gerekir. Fiziksel yazıcıya çıktı gönderilmedi.
