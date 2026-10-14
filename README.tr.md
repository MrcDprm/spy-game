# Casus — Spy Game

[English](README.md) | **Türkçe**

3-12 kişilik, herkesin kendi telefonundan oynadığı gerçek zamanlı bir parti oyunu. Casus (ya da casuslar) dışındaki herkes aynı gizli mekânı alır; casus sadece kategoriyi görür. Oyuncular birbirine sorular sorar, casusun kim olduğunu oylar; yakalanan casusun mekânı bilmek için son bir şansı vardır.

Bu, 2025'te ASP.NET Core MVC ile ilk kez yazdığım bir oyunun yeniden yazımı. O sürümde telefon masada elden ele dolaşıyordu. Siteye giren herkes aynı tek oyunu paylaşıyordu ve sunucu her oyuncunun rolünü bütün odaya gönderiyordu.

**Canlı:** [spygame.miracdeprem.com](https://spygame.miracdeprem.com)

![Basılı tutulan bir sivil kartı: gizli mekân, kategorisi ve tur süresi](docs/screenshot-card.png)

## Özellikler

- **Odalar:**
  - Oda kurulur; 6 karakterlik kodu, linki ya da QR kodu paylaşılır.
  - Odayı kuran kişi kategorileri, tur süresini (1–15 dakika) ve casus sayısını (1–3) seçer. Az kişi gelirse casus sayısı oyun başlarken azaltılır.
  - Sayfayı yenileyen ya da bağlantısı kopan oyuncu yerini kaybetmez.
- **Gizli kartlar:**
  - Her telefona sadece kendi kartı gider; kart yalnızca basılı tutulunca görünür.
  - Casus kategoriyi ve 15 olası mekânı görür, mekânın kendisini asla görmez.
  - Kartlar kapalıyken sivilin ve casusun ekranı birebir aynı görünür.
- **Canlı tur:**
  - Turun süresini sunucudaki saat sayar.
  - Oyuncuların yarıdan fazlası isterse "Oylamaya geçelim" soruları erken bitirir.
  - Herkes kendi telefonundan oy verir; oylar sonuçlara kadar gizli kalır.
  - Yakalanan casus mekânı bilirse yine kazanır.
- **Puanlar:** oda içinde skor tablosu ve doğrulanmış hesaplar için haftalık "Haftanın casusları" sıralaması.
- **Hesaplar (isteğe bağlı):**
  - E-posta doğrulamalı kayıtla takma ad kalıcı olarak senin olur ve sıralamaya girersin.
  - Misafirler 24 saat boyunca kendilerine ayrılan bir takma adla oynayabilir.
- **Yönetim sayfası:** bir haftanın puanlarını silme, bir hesabı sıralamadan çıkarma ve geri alma.
- **Mekân listesi:** 10 kategori ve 150 mekân, Türkçe ve İngilizce.
- **Arayüz:** Türkçe ve İngilizce, koyu ve açık tema, önce telefon için tasarlandı.

## Ekran Görüntüleri

| QR davetli lobi | Casusun kartı | Oylama |
|---|---|---|
| ![Oyuncular, ayarlar ve davet QR koduyla lobi](docs/screenshot-lobby.png) | ![Casusun kartı: kategori ve 15 olası mekân](docs/screenshot-spy.png) | ![Oyuncuların düğme olarak listelendiği oylama ekranı](docs/screenshot-voting.png) |

| Sonuç | Haftanın casusları | Ana sayfa |
|---|---|---|
| ![Sonuç: siviller kazandı, mekân, oylar ve puanlar](docs/screenshot-results.png) | ![Haftalık sıralama](docs/screenshot-leaderboard.png) | ![Oda kur ve katıl formlarıyla ana sayfa](docs/screenshot-home.png) |

## Kullanılan Teknolojiler

- **Web:** ASP.NET Core MVC (Model, View, Controller), Razor görünümler, .NET 10
- **Canlı güncellemeler:** SignalR. Hub sadece mesajları iletir; oyunun kuralları model katmanındadır.
- **Canlı veri:** Redis (yerelde Upstash, canlıda Render Key Value): odalar, oturumlar, takma adlar, süreler ve haftalık sıralı kümeler
- **Kalıcı veri:** PostgreSQL (Neon) ve Entity Framework Core: hesaplar, puan geçmişi ve veri koruma anahtarları
- **Hesaplar:** ASP.NET Core Identity, e-posta Resend ile
- **Diğer kütüphaneler:** davet QR kodu için QRCoder ve SignalR'ın tarayıcı istemcisi
- **Testler:** xUnit, 137 test; gerçek SignalR bağlantılarıyla oynanan tam turlar dahil
- **Yayın:** Render üzerinde Docker

## Kurulum ve Çalıştırma

Oynamanın en kolay yolu [canlı site](https://spygame.miracdeprem.com): üç telefonda açıp bir oda kurmak yeterli.

Yerelde çalıştırmak için .NET 10 SDK, bir PostgreSQL veritabanı (ücretsiz bir [Neon](https://neon.tech) veritabanı yeter) ve isteğe bağlı olarak bir Redis veritabanı (ücretsiz bir [Upstash](https://upstash.com) veritabanı yeter) gerekir.

```bash
git clone https://github.com/MrcDprm/spy-game.git
cd spy-game
dotnet user-secrets set DATABASE_URL "postgresql://kullanici:sifre@host/db?sslmode=require" --project src/SpyGame.Web
dotnet user-secrets set REDIS_URL "rediss://default:sifre@host:6379" --project src/SpyGame.Web   # isteğe bağlı
dotnet run --project src/SpyGame.Web
```

Tablolar ilk açılışta oluşturulur.

- **`REDIS_URL` yoksa:** canlı oyun bellekte tutulur.
- **`RESEND_API_KEY` yoksa:** e-postalar (ve doğrulama linkleri) loga yazılır.
- **Yönetici erişimi:** `ADMIN_EMAILS` değişkenine kendi adresini yaz.

### Testler

```bash
dotnet test --solution SpyGame.slnx
```

Testler hiçbir dış servise ihtiyaç duymaz. Uygulamanın tamamı bellekte çalışır; Postgres yerine SQLite, Redis yerine bellek içi depo kullanılır. `REDIS_URL` tanımlıysa Redis deposunun testleri gerçek bir Redis'te de çalışır.

### Proje yapısı

```
src/SpyGame.Web/
  Models/Game/          Kurallar: takma adlar, oda kodları, mekân listesi, kart dağıtma,
                        oylama, puanlama, odanın durum makinesi ve her oyuncunun ne görebileceği
  Models/ViewModels/    Form ve sayfa modelleri
  Controllers/          Ana sayfa, odalar, hesaplar, sıralama, yönetim
  Views/                Razor görünümleri
  Hubs/                 SignalR kanalı, oyuncuya özel mesajlar, bağlantı başına çağrı sınırı
  Services/             RoomService (bütün oyun işlemleri), oyun saati, oturumlar,
                        sıralama, e-posta, Redis ve bellek içi depolar
  Data/                 Entity Framework: hesaplar, puan geçmişi, migration'lar
  wwwroot/              Stiller, oda sayfasının betiği, yazı tipleri, SignalR istemcisi
tests/SpyGame.Tests/    Model, servis ve uçtan uca testler
```

### Güvenlik

- **Her telefona ne gidiyor:** her şeye sunucu karar verir ve her oyuncuya sadece kendi görünümünü gönderir. Casusun telefonuna mekân hiç gitmez, tur bitene kadar kimsenin telefonuna da casusların kim olduğu gitmez. Bir test, mekân ne olursa olsun casusa giden verinin bayt bayt aynı olduğunu kontrol eder.
- **Kim kimdir:** oyuncunun kimliği bağlantı açılırken HttpOnly oturum çerezinden alınır, sayfanın sonradan gönderdiği hiçbir şeyden alınmaz. Kimse başkası adına oy veremez ya da tahmin yapamaz.
- **Çerezler:** Redis'te oturumun sadece çerezin hash'i olarak saklanır.
- **Oyun kanalı:** sadece bu sitenin sayfaları açabilir. Tarayıcılar WebSocket bağlantılarında da çerez gönderir ve CORS bunları kapsamaz.
- **Sınırlar:** oda kurma, oda kodu deneme, hesap formları ve hub çağrıları sınırlıdır. Ziyaretçinin adresi Cloudflare'in `CF-Connecting-IP` başlığından alınır.
- **Hesaplar:**
  - Şifreler hash'lenir; giriş için doğrulanmış e-posta gerekir; 5 yanlış şifreden sonra hesap kilitlenir.
  - Kayıt ve şifre sıfırlama, bir adresin kayıtlı olup olmadığını asla belli etmez.
- **Formlar:** hepsinde antiforgery belirteci var.
- **Content Security Policy:**
  - Satır içi script yok, dış kaynak yok, Trusted Types açık.
  - Sayfa her şeyi `createElement` ve `textContent` ile kurar.
  - İzin verilen tek bağlantı bu siteye geri olandır.
- **Hatalar:** ziyaretçi dostça hata sayfaları görür; ayrıntılar logda kalır.

## Öğrendiklerim

- **Tarayıcıya sır emanet etmemek.** İlk sürüm her rolü bütün odaya gönderiyordu; geliştirici araçlarını açan herkes casusu görebiliyordu. Artık sunucu her oyuncu için ayrı bir görünüm hazırlıyor. Bir test, iki farklı mekân için casusa gidenleri karşılaştırıp bayt bayt aynı olduklarını kontrol ediyor. Ekranı yaparken aynı sızıntıyı başka bir biçimde buldum: kartın dışındaki "olası mekânlar" kutusu sadece casusun telefonunda görünüyordu, yani ekrana bir bakış casusu ele veriyordu. Kutu artık gizli kartın içinde.
- **Asıl hata ortak durumdu.** Eski oyun durumunu static alanlarda tutuyordu; siteye giren herkes aynı tek oyunu oynuyordu. Artık her oda Redis'te kendi nesnesi ve "karşılaştır ve yaz" betiğiyle kaydediliyor. Aynı milisaniyede oy veren iki oyuncu birbirinin üzerine yazamıyor; bir test 11 oyuncuyu aynı anda odaya sokuyor.
- **Kurallar bir durum makinesi.** Lobi → sorular → oylama → son tahmin → sonuç, zamanı parametre olarak alan sade C# fonksiyonlarından oluşuyor. Bu her kuralı kolay test edilebilir yaptı ve gerçek bir hata buldu: bir duraklamadan sonra (örneğin sunucu uykudan uyanınca) sonraki aşama "şimdi"den başlıyor ve oyunculara zaten geçmiş bir süreyi yeniden veriyordu. Aşamalar artık bir önceki son tarihten başlıyor.
- **Redis'te bir saat.** Son tarihler sıralı bir kümede duruyor; oyun saati süresi geçmiş bütün odaları saniyede iki kez tek sorguyla buluyor.
- **Redis'e rağmen kaybolmayan sıralama.** Haftalık puanlar hızlı okuma için Redis'te sıralı küme, geçmiş için veritabanında tablo. Sıralı küme yoksa, bir şey eklenmeden önce veritabanından yeniden kuruluyor; puan hiç kaybolmuyor.
- **Bütünü test etmek.** Uçtan uca testler gerçek uygulamayı bellekte çalıştırıyor ve birkaç "tarayıcı" ile gerçek SignalR bağlantıları üzerinden turlar oynuyor; her birine gelen ham JSON'u kontrol ediyor.

## Gelecek Planları

- Odaya özel kelime listeleri
- Seyirci modu
- Birbirini tanıyan casuslar gibi farklı oyun modları

## Katkılar

- Yazı tipleri: [Archivo](https://github.com/Omnibus-Type/Archivo) ve [JetBrains Mono](https://github.com/JetBrains/JetBrainsMono) (SIL Open Font License), sitenin kendisinden sunulur
- [SignalR JavaScript istemcisi](https://github.com/dotnet/aspnetcore) (MIT) ve [QRCoder](https://github.com/codebude/QRCoder) (MIT)

## Lisans

[MIT](LICENSE)
