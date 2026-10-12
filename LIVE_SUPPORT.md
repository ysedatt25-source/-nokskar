# İnokskar canlı destek

Yönetim alanı: `/admin/live-support`. Kurucu Süper Admin ve `customerSupport.view` yetkisine sahip Admin hesapları sohbetleri görebilir. Yanıt, durum değişikliği, sohbet devri/kapatma ve arama başlatma için `customerSupport.edit` gerekir. Teknik servis ve müşteri rolleri destek yetkilisi olarak davranamaz.

## Çalışma şekli

- Sayfalardaki bileşen Shadow DOM kullanır; mevcut site stilleri sohbetin içine sızmaz.
- Hazır yetkililer 20 saniyede bir kalp atışı gönderir. Sekmeler ayrı izlenir; 75 saniye kalp atışı gelmeyen bağlantı çevrimdışı sayılır. Meşgul yetkililer yeni destek için çevrimiçi sayılmaz.
- Sohbet, okundu, yazıyor ve arama sinyalleri iki saniyede bir güncellenir. Bağlantısı kurulan aramalarda kapatma bilgisi ayrıca WebRTC veri kanalından doğrudan gönderilir.
- Ziyaretçi kimliği rastgele, HttpOnly, SameSite=Strict çerezle korunur. Sohbet ve dosya erişimi sunucuda sahiplik/yetki kontrolünden geçer. Kullanıcı hesaplarının mevcut kimlik doğrulaması ve CSRF koruması kullanılır.
- Sohbetler ve ekler mevcut kalıcı veri dizininde `live-support/` altında saklanır. Ziyaretçiler fotoğraf veya PDF için en fazla 5 MB gönderebilir. Her sohbet en fazla 5.000 mesaj içerir; görünüm son 300 mesajı gösterir. Kapalı sohbetler yönetim listesinden görüntülenebilir.

## Yazılı çeviri

Kurucu Süper Admin, sohbet işlemleri menüsündeki **Çeviri ayarları** alanına OpenAI API anahtarını girer. Anahtar mevcut Data Protection altyapısıyla şifrelenerek saklanır; API üzerinden geri verilmez. Alternatif olarak `LiveSupport__OpenAIKey` ortam değişkeni kullanılabilir; bu değer varsa paneldeki anahtardan önceliklidir.

Çeviri anahtarı yoksa veya sağlayıcı hata verirse özgün mesaj gösterilir. Çeviri kapalı gönderilen mesajlar sunucuda da çeviri dışı tutulur. Yalnızca çevrilmesine izin verilen önceki altı metin bağlam için kullanılır. Model `gpt-4.1-mini`; yanıt JSON olarak alınır. Ürün kodları, sayılar, bağlantılar ve belirtilen ölçü birimleri değişen yanıtlar reddedilir. Model doğruluğu için yüzde garantisi yoktur; alıcı özgün mesajı her zaman görüntüleyebilir. OpenAI kullanım ücretleri anahtar sahibinin hesabına aittir.

## Sesli / görüntülü arama

WebRTC kullanılır. Aramayı yalnızca yanıt yetkili Admin başlatır; ziyaretçi kabul veya reddeder. Tercüme yapılmaz. Mikrofon/kamera izni tarayıcıda kullanıcı tarafından verilir. Reddedilen, yanıtsız kalan veya bağlantısı kurulamayan arama otomatik yeniden denenmez.

STUN sunucuları hazırdır. Kısıtlı NAT / kurumsal ağlarda güvenilir bağlantı için TURN hizmeti gerekir. TURN REST geçici kimlik bilgileri destekleyen bir sunucu için:

- `LiveSupport__TurnUrl`: örneğin `turns:turn.example.com:5349?transport=tcp`
- `LiveSupport__TurnSecret`: TURN sunucusunun paylaşılmış REST kimlik doğrulama sırrı

Geçici TURN kimlik bilgileri yalnızca ilgili aramanın katılımcılarına verilir ve 10 dakika geçerlidir. Kalıcı TURN sırrı istemciye verilmez. Bu değerler tanımlanmadığında doğrudan bağlantı denenir; her ağda bağlantı garantisi verilemez.

## Kontroller

`dotnet IndustrialCatalog.dll --live-support-self-test`: rol sınırları, şifreli anahtar, çeviri sözleşmesi/sağlayıcı hata davranışı, kod/sayı koruması, çeviri kapatma ve tekrar gönderim kontrolü. Gerçek sağlayıcı çağrısı yapmaz. Docker derlemesi bu kontrolü ve mevcut üyelik kontrollerini çalıştırır.

Uygulama ayrıca izole yerel veriyle üç ekran boyutunda test edildi: 1440×1000, 820×1180, 390×844. Sohbet gönderme/kapatma, çevrimdışı mesaj, yetki/erişim, dosya denetimi, okundu/yazıyor ve yapay kamera/mikrofonla iki taraflı WebRTC ses/video bağlantısı ile iki yönlü kapatma doğrulandı. Bu testler gerçek OpenAI anahtarı veya gerçek kullanıcı mesajı kullanmaz.
