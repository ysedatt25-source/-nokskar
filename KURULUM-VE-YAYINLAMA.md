# İNOKSKAR — tam kaynak proje

## Visual Studio ile çalıştırma
1. ZIP dosyasını tamamen çıkarın; ZIP içinden çalıştırmayın.
2. Visual Studio 2022 içinde ASP.NET ve web geliştirme iş yükü ile .NET 8 SDK kurulu olmalıdır.
3. INOKSKAR.sln dosyasını açın. IndustrialCatalog başlangıç projesidir.
4. IndustrialCatalog profilini seçip F5 veya Ctrl+F5 kullanın. Adres: http://127.0.0.1:5093
5. Güncel derlenmiş arayüz wwwroot içinde bulunur. Node.js yoksa bu arayüz kullanılır. React kaynaklarını değiştirecekseniz Node.js 22.13 veya üzerini kurun; derleme npm install ve npm run build çalıştırır.

## İlk yönetici hesabı
Güvenlik için pakette yönetici parolası bulunmaz. IndustrialCatalog klasöründe terminal açın:
```powershell
dotnet run -- --hash-password
```
Parolanızı terminalde girin; ekranda üretilen parola özetini kopyalayın. Ardından:
```powershell
dotnet user-secrets set "Admin:Email" "sizin-epostaniz@example.com"
dotnet user-secrets set "Admin:PasswordHash" "URETILEN_PAROLA_OZETI"
```
Uygulamayı yeniden başlatıp /login sayfasından giriş yapın. Parola özetini paylaşmayın.

## Visual Studio ile yayımlama
IndustrialCatalog projesine sağ tıklayın → Yayımla → mevcut Folder profilini seçin → Yayımla. Çıktı çözüm klasöründeki publish klasörüne yazılır. Alternatif:
```powershell
dotnet publish IndustrialCatalog/IndustrialCatalog.csproj -c Release -o publish
```
Sunucuda .NET 8 ASP.NET Core Runtime bulunmalıdır. IIS kullanılacaksa .NET 8 Hosting Bundle gereklidir; uygulama havuzu No Managed Code olarak ayarlanır. HTTPS sertifikası ve alan adı sunucu üzerinde yapılandırılır. Yayımlanan dosyaların tamamını hedef uygulama klasörüne aktarın.

## Canlı ortam ayarları
Development kullanıcı sırları canlıya taşınmaz. Sunucunun güvenli ortam değişkenlerinde Admin__Email, Admin__PasswordHash, Site__PublicBaseUrl (https://alanadiniz), Storage__Path (kalıcı ve yazılabilir veri klasörü) ayarlarını yapın. ASPNETCORE_ENVIRONMENT=Production kullanın. E-posta gönderimini sağlayıcı bilgileri yapılandırıldıktan sonra yönetim panelinden test edin.

## Veri koruma
Bu paket tam uygulama kaynaklarını ve güncel derlenmiş arayüzü içerir. Canlı Railway veri diski, müşteri kayıtları, yüklemeler, yönetici parolaları ve gizli anahtarlar kaynak ZIP'ine dahil değildir. Mevcut canlı verileri taşıyacaksanız sunucu yedeğini ayrı alın; App_Data/veri klasörü ve Data Protection keys klasörünü koruyun. Yeni bir kurulum başlangıç verileriyle açılır. Sunucuda uygulamanın veri klasörüne yazma izni olmalıdır.

## Yayın sonrası kontrol
/healthz yanıtını, ana sayfayı, /iletisim, /talep-sorgula, giriş ve yönetim ekranlarını kontrol edin. Logo ışıltısı sayfa açılışında ve URL değişiminde bir kez çalışır; zamanlı tekrar yoktur. Sisteminizde hareket azaltma tercihi açıkken animasyon gösterilmez.

## Doğrulama kapsamı
Ön yüz derlemesi ve logo geçiş mantığı kontrol edildi. Canlı Railway yayını aynı kaynakla doğrulanır. Windows Visual Studio/IIS kurulumu bu Linux çalışma ortamında çalıştırılamadığı için işletim sistemi, sunucu izinleri ve alan adı yapılandırması ayrıca doğrulanmalıdır.
