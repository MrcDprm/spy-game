# Casus — Spy Game

[English](README.md) | **Türkçe**

3-12 kişilik, herkesin kendi telefonundan oynadığı gerçek zamanlı bir casus parti oyunu. Casus dışındaki herkes aynı gizli mekanı ve o mekandaki kendi rolünü alır; casus sadece olası mekanların listesini görür. Oyuncular birbirine sorular sorar ve casusun kim olduğunu oylar, casus da ağızlardan kaçacak bir ipucunu bekler: mekanı sorular sırasında ya da yakalandıktan sonra bilirse turu casuslar kazanır.

Bu, 2025'te ASP.NET Core MVC ile ilk kez yazdığım bir oyunun yeniden yazımı. O sürümde telefon masada elden ele dolaşıyordu, siteye giren herkes aynı tek oyunu paylaşıyordu ve sunucu her oyuncunun rolünü bütün odaya gönderiyordu.

**Canlı:** [spygame.miracdeprem.com](https://spygame.miracdeprem.com)

![Basılı tutulan bir sivil kartı: mekan, rol ve tur süresi](docs/screenshot-card.png)

## Özellikler

- **Odalar:**
  - Oda kurulur; 6 karakterlik kodu, linki ya da QR kodu paylaşılır.
  - Odayı kuran kişi mekanları, 1 ile 15 dakika arasında tur süresini, 1 ile 3 arasında casus sayısını ve grubun aynı ortamda mı uzaktan mı oynadığını seçer. Az kişi gelirse casus sayısı oyun başlarken azaltılır.
  - Oda sahibi turlar arasında bir dokunuşla mekanların üstünü çizip sonraki turlardan çıkarabilir ya da geri alabilir. Sadece çıkmış olanlar değil, istenen her mekan çizilebilir; en az 10 mekan oyunda kalır.
  - Sayfayı yenileyen ya da bağlantısı kopan oyuncu yerini kaybetmez.
- **Mekanlar ve roller:**
  - Numaralı sekiz dosyada 202 mekan, Türkçe ve İngilizce. Hastane, Berber, Vapur gibi gündelik yerler ve Buzdolabının İçi, Uzaylı Gemisi, Apartman Toplantısı gibi uçuk olanlar.
  - Her mekanın yedi rolü var. Her sivile rastgele biri düşer, hastanede hemşire ya da hasta yakını gibi; böylece cevaplar kendiliğinden farklılaşır ve casus kalabalığa karışabilir.
  - Odayı kuran dosyaların tamamını işaretler ya da dosyayı açıp mekanları tek tek seçer; en az 10 mekan gerekir. Yeni bir oda bilinen 25 mekanla başlar, son seçim o cihazda hatırlanır.
- **Gizli kartlar:**
  - Her telefona sadece kendi kartı gider; kart yalnızca basılı tutulunca görünür.
  - Sivilin kartında mekan ve rolü yazar. Casusun kartında ikisi de yazmaz ama düzeni ve renkleri aynıdır; masanın karşısından görülen açık bir kart casusu ele vermez.
  - Turdaki herkes olası mekanların aynı listesini alır ve liste her telefonda açık gelir; listeye bakmak kimseyi öne çıkarmaz. Kartlar kapalıyken bütün ekranlar birebir aynı görünür.
- **Canlı tur:**
  - Turun süresini sunucudaki saat sayar.
  - İlk soru el değiştirir: son iki turda ilk soruyu soranlar o çekilişe girmez. Casus her tur tamamen rastgele seçilir. Sırayla casus olma kuralı, sıranın kimde olduğunu oyunculara belli ederdi.
  - Casus soruların ortasında istediği an turu durdurup mekanı tahmin edebilir. Bilirse casuslar, bilemezse siviller kazanır. Bu herkesi dikkatli tutar: mekanı ele veren bir soru turu kaybettirebilir.
  - Soru öner, ne soracağını bulamayan herkese her mekana uyan bir soru verir.
  - Oyuncuların yarıdan fazlası isterse Oylamaya geçelim soruları erken bitirir. Herkes kendi telefonundan oy verir; oylar sonuçlara kadar gizli kalır.
  - Yakalanan casus mekanı bilirse yine kazanır. Sonuç ekranında her sivilin rolü ve her oy görünür.
- **Uzaktan oynamak:** isteğe bağlı, aynı yerde olmayan arkadaşlar için.
  - **Sırayla sorular:** Sırası gelen kişi birini seçip sorusunu yazar ya da kutuyu önerilen bir soruyla doldurur. Soruyu ve cevabı herkes görür, sıra cevaplayana geçer. Bir dakika içinde cevap gelmezse soran başkasına sorabilir, oda sahibi de sırayı geçebilir. Bütün döküm sonuç ekranında görünür.
  - **Oda sohbeti:** Her zaman açık, sadece odayla birlikte son 50 satır tutulur, kişi başına hız sınırı vardır.
  - **Telefona göre tasarım:** Sorular ve Sohbet sekmeli, okunmamış sayılı bir alt panel. Sıra sana gelince kendiliğinden açılır, ekran klavyesinin üstünde kalır ve başkaları yazarken yazdığın metin kaybolmaz.
  - **Oda sahibi kontrolleri:** Bir sohbet satırını gizleme ya da bir oyuncuyu odadan çıkarma; çıkarılan kişi o odaya tekrar giremez.
- **Puanlar:** oda içinde, turlar arasında herkesin kaç turda casus olduğunu da gösteren bir skor tablosu. Haftanın casusları, doğrulanmış hesaplar için haftalık sıralamadır.
- **Hesaplar:** isteğe bağlı.
  - E-posta doğrulamalı kayıtla takma ad kalıcı olarak senin olur ve sıralamaya girersin.
  - Misafirler 24 saat boyunca kendilerine ayrılan bir takma adla oynayabilir.
- **Yönetim sayfası:** bir haftanın puanlarını silme, bir hesabı sıralamadan çıkarma ve geri alma.
- **Yenilikler:** yeni özellikleri anlatan kısa bir not her tarayıcıda bir kez açılır, oynanan bir turun üstüne asla çıkmaz.
- **Arayüz:** Türkçe ve İngilizce, koyu ve açık tema, önce telefon için tasarlandı.

## Ekran Görüntüleri

| QR davetli lobi | Casusun kartı | Oylama |
|---|---|---|
| ![Oyuncular, ayarlar ve davet QR koduyla lobi](docs/screenshot-lobby.png) | ![Basılı tutulan casus kartı: mekan yok, rol Casus, altta açık olası mekanlar listesi](docs/screenshot-spy.png) | ![Oyuncuların düğme olarak listelendiği oylama ekranı](docs/screenshot-voting.png) |

| Casus mekanı tahmin ediyor | Sonuç | Haftanın casusları |
|---|---|---|
| ![Casusun sorular sırasında mekanı tahmin ettiği liste](docs/screenshot-reveal.png) | ![Sonuç: siviller kazandı, mekan, herkesin rolü, oylar ve puanlar](docs/screenshot-results.png) | ![Haftalık sıralama](docs/screenshot-leaderboard.png) |

| Ana sayfa | Turlar arasında mekanların üstünü çizmek |
|---|---|
| ![Ana sayfa: mekan dosyalarıyla oda kurma formu ve katılma formu](docs/screenshot-home.png) | ![Bir turdan sonra oda sahibinin mekan listesi: üç mekanın üstü çizili](docs/screenshot-places.png) |

| Uzaktan: soru sırası sende | Şimdiye kadarki sorular | Oda sohbeti |
|---|---|---|
| ![Telefondaki alt panel: bir oyuncu seçmek, kutuda önerilen bir soru](docs/screenshot-apart-ask.png) | ![İki soru, cevapları ve sıranın kimde olduğu](docs/screenshot-apart-questions.png) | ![Tur sırasında sohbet mesajları](docs/screenshot-apart-chat.png) |

## Kullanılan Teknolojiler

- **Web:** ASP.NET Core MVC, Razor görünümler, .NET
- **Canlı güncellemeler:** SignalR. Hub sadece mesajları iletir; oyunun kuralları model katmanındadır.
- **Canlı veri:** Redis: odalar, oturumlar, takma adlar, süreler ve haftalık sıralı kümeler. Geliştirme makinesinde Upstash, sunucuda Valkey.
- **Kalıcı veri:** Neon'da PostgreSQL ve Entity Framework Core: hesaplar, puan geçmişi ve veri koruma anahtarları
- **Oyun içeriği:** mekanlar ve rolleri, uygulamanın içine gömülü tek bir JSON dosyasında; uygulama açılırken denetlenir
- **Hesaplar:** ASP.NET Core Identity, e-posta Resend ile
- **Diğer kütüphaneler:** davet QR kodu için QRCoder ve SignalR'ın tarayıcı istemcisi
- **Testler:** xUnit, 205 test; gerçek SignalR bağlantılarıyla oynanan tam turlar ve casus çekilişinin istatistiksel kontrolleri dahil
- **Yayın:** Oracle Cloud sunucusu, önde Caddy, systemd servisi; ayrıntılar [deploy/README.md](deploy/README.md) dosyasında

## Kurulum ve Çalıştırma

Oynamanın en kolay yolu [canlı site](https://spygame.miracdeprem.com): üç telefonda açıp bir oda kurmak yeterli.

Yerelde çalıştırmak için .NET SDK, bir PostgreSQL veritabanı ve isteğe bağlı olarak bir Redis veritabanı gerekir. Ücretsiz [Neon](https://neon.tech) ve [Upstash](https://upstash.com) veritabanları yeterli.

```bash
git clone https://github.com/MrcDprm/spy-game.git
cd spy-game
dotnet user-secrets set DATABASE_URL "postgresql://kullanici:sifre@host/db?sslmode=require" --project src/SpyGame.Web
dotnet user-secrets set REDIS_URL "rediss://default:sifre@host:6379" --project src/SpyGame.Web   # isteğe bağlı
dotnet run --project src/SpyGame.Web
```

Tablolar ilk açılışta oluşturulur.

- **`REDIS_URL` yoksa:** canlı oyun bellekte tutulur.
- **`RESEND_API_KEY` yoksa:** e-postalar ve doğrulama linkleri loga yazılır.
- **Yönetici erişimi:** `ADMIN_EMAILS` değişkenine kendi adresini yaz.

### Testler

```bash
dotnet test --solution SpyGame.slnx
```

Testler hiçbir dış servise ihtiyaç duymaz. Uygulamanın tamamı bellekte çalışır; Postgres yerine SQLite, Redis yerine bellek içi depo kullanılır. `REDIS_URL` tanımlıysa Redis deposunun testleri gerçek bir Redis'te de çalışır.

### Proje yapısı

```
src/SpyGame.Web/
  Models/Game/          Kurallar: takma adlar, oda kodları, places.json'daki mekanlar ve roller,
                        kart ve rol dağıtma, oylama, puanlama, odanın durum makinesi
                        ve her oyuncunun ne görebileceği
  Models/ViewModels/    Form ve sayfa modelleri
  Controllers/          Ana sayfa, odalar, hesaplar, sıralama, yönetim
  Views/                Razor görünümleri
  Hubs/                 SignalR kanalı, oyuncuya özel mesajlar, bağlantı başına çağrı sınırı
  Services/             Bütün oyun işlemleri için RoomService, oyun saati, oturumlar,
                        sıralama, e-posta, Redis ve bellek içi depolar
  Data/                 Entity Framework: hesaplar, puan geçmişi, migration'lar
  Resources/            Türkçe ve İngilizce arayüz metinleri
  wwwroot/              Stiller, oda sayfasının betiği, yazı tipleri, SignalR istemcisi
tests/SpyGame.Tests/    Model, servis ve uçtan uca testler
```

### Güvenlik

- **Her telefona ne gidiyor:** her şeye sunucu karar verir ve her oyuncuya sadece kendi görünümünü gönderir. Casusun telefonuna mekan da rol de hiç gitmez, sivilin telefonuna sadece kendi rolü gider, tur bitene kadar kimsenin telefonuna casusların kim olduğu gitmez. Bir test, mekan ne olursa olsun casusa giden verinin bayt bayt aynı olduğunu kontrol eder.
- **Skor tablosundan bir şey okunmaz:** casusluk sayıları sadece tur bitince değişir, oynanan turun casusunu asla ima etmez.
- **Kim kimdir:** oyuncunun kimliği bağlantı açılırken HttpOnly oturum çerezinden alınır, sayfanın sonradan gönderdiği hiçbir şeyden alınmaz. Kimse başkası adına oy veremez ya da tahmin yapamaz. Sorular sırasında mekanı sadece casus tahmin edebilir; bunu sunucu denetler.
- **Çerezler:** Redis'te oturumun sadece çerezin hash'i olarak saklanır.
- **Oyun kanalı:** sadece bu sitenin sayfaları açabilir. Tarayıcılar WebSocket bağlantılarında da çerez gönderir ve CORS bunları kapsamaz.
- **Sınırlar:** oda kurma, oda kodu deneme, hesap formları ve hub çağrıları sınırlıdır. Ziyaretçinin adresi Caddy'nin gördüğü adrestir; iletilen başlıklara yalnızca aynı makinedeki Caddy'den gelince inanılır, bir test sahte başlıkların sınırı sıfırlamadığını denetler.
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

- **Tarayıcıya sır emanet etmemek.** İlk sürüm her rolü bütün odaya gönderiyordu; geliştirici araçlarını açan herkes casusu görebiliyordu. Artık sunucu her oyuncu için ayrı bir görünüm hazırlıyor. Bir test, iki farklı mekan için casusa gidenleri karşılaştırıp bayt bayt aynı olduklarını kontrol ediyor. Ekranı yaparken aynı sızıntıyı başka bir biçimde buldum: kartın dışındaki olası mekanlar kutusu sadece casusun telefonunda görünüyordu, yani ekrana bir bakış casusu ele veriyordu. Artık turdaki herkes aynı listeyi alıyor; liste kimseye bir şey söylemiyor.
- **Bir sır ekranın görünüşünden de sızar.** Arkadaşlarım casusun hemen belli olduğunu söyledi. Bunun bir kısmı ekrandandı: açık casus kartında büyük, eğik, kırmızı bir damga vardı, sivilin kartında düz yazı; masanın karşısından bir bakış yetiyordu. Mekan listesini açan da sadece casustu. Artık iki kartta aynı satırlar aynı stilde, liste her telefonda açık geliyor ve casusun tahmin düğmesi herkesin ekranında duruyor.
- **Oyun dengesi de kodun işi.** Casus ancak yakalandıktan sonra tahmin edebiliyordu; siviller mekanı ele veren sorular sorsa da hiçbir şey kaybetmiyordu. Roller de yoktu, bütün siviller aynı gözle cevap veriyordu. Bu türdeki oyunların kullandığı iki kural bunu düzeltti: casus mekanı istediği an tahmin edebiliyor ve her sivilin bir rolü var. Sivilin rolü sadece oturduğu sıraya ve tek bir karıştırmaya bağlı, casusların kim olduğuna hiç bağlı değil; bu yüzden onlar hakkında bir şey ele vermiyor.
- **İçerik veri olarak.** 202 mekan ve 1.414 rol, uygulamanın içine gömülü tek bir JSON dosyasında. Dosya uygulama açılırken denetleniyor: benzersiz kimlikler ve adlar, her mekanda yedi farklı rol, iki dil. Bozuk bir dosya oyunun ortasında değil, açılışta durduruyor; bir test de eski 102 mekandan hiçbirinin kaybolmadığını kontrol ediyor.
- **Asıl hata ortak durumdu.** Eski oyun durumunu static alanlarda tutuyordu; siteye giren herkes aynı tek oyunu oynuyordu. Artık her oda Redis'te kendi nesnesi ve karşılaştır ve yaz betiğiyle kaydediliyor. Aynı milisaniyede oy veren iki oyuncu birbirinin üzerine yazamıyor; bir test 11 oyuncuyu aynı anda odaya sokuyor.
- **Kurallar bir durum makinesi.** Lobi, sorular, oylama, son tahmin ve sonuç, zamanı parametre olarak alan sade C# fonksiyonları. Bu her kuralı kolay test edilebilir yaptı ve gerçek bir hata buldu: sunucu uykudan uyanınca olduğu gibi bir duraklamadan sonra sonraki aşama o anki saatten başlıyor ve oyunculara zaten geçmiş bir süreyi yeniden veriyordu. Aşamalar artık bir önceki son tarihten başlıyor.
- **Redis'te bir saat.** Son tarihler sıralı bir kümede duruyor; oyun saati süresi geçmiş bütün odaları saniyede iki kez tek sorguyla buluyor.
- **Redis'e rağmen kaybolmayan sıralama.** Haftalık puanlar hızlı okuma için Redis'te sıralı küme, geçmiş için veritabanında tablo. Sıralı küme yoksa, bir şey eklenmeden önce veritabanından yeniden kuruluyor; puan hiç kaybolmuyor.
- **Yazdığını silen yeniden çizim.** Oda sayfası her güncellemede ekranı baştan çiziyor; oyuncular yazı yazmaya başlayana kadar bu sorun değildi: başkasının her sohbet satırı yazılmakta olan soruyu siliyordu. Sorular ve sohbet paneli artık yeniden çizilen alanın dışında; listeleri yenileniyor, yazı kutusu ise sadece oyuncunun yapabileceği şey değişince yeniden kuruluyor.
- **Adaletsizlik hissini ölçmek, iki kez.** Önce bir arkadaşım sürekli ilk soran oldu, sonra bir başkası üst üste dokuz kez casus olduğunu söyledi. İkisinde de gerçek oyun motorunu oynattım, ikincisinde 1,3 milyon tur. Her oyuncunun payı beklenenden en fazla yarım puan saptı ve bir turda casus olmak sonraki turu hiç etkilemedi. Dört kişide üst üste dokuz kez casus olmak yaklaşık 4.000 akşamda bir olur; üç dört kez üst üste ise neredeyse her akşam olur ve akılda kalan da bunlardır. Bu yüzden casus tamamen rastgele kalıyor, bir test turların birbirinden bağımsız olduğunu denetliyor ve skor tablosu herkesin kaç kez casus olduğunu gösteriyor.
- **Ekranları oynayarak denemek.** Bir betik başsız tarayıcıda dört telefonla tam turlar oynuyor ve bu sayfadaki ekran görüntülerini alıyor. Birim testlerinin göremediklerini buldu: koyu temada kağıt kartın üstünde görünmeyen bir düğme, son satırına kaymış açılan bir pencere ve önceki kaydırma yerinde kalan yeni bir aşama.
- **Bütünü test etmek.** Uçtan uca testler gerçek uygulamayı bellekte çalıştırıyor ve birkaç tarayıcıyla gerçek SignalR bağlantıları üzerinden turlar oynuyor; her birine gelen ham JSON'u kontrol ediyor.

## Katkılar

- Yazı tipleri: [Archivo](https://github.com/Omnibus-Type/Archivo) ve [JetBrains Mono](https://github.com/JetBrains/JetBrainsMono), SIL Open Font License ile, sitenin kendisinden sunulur
- [SignalR JavaScript istemcisi](https://github.com/dotnet/aspnetcore) ve [QRCoder](https://github.com/codebude/QRCoder), ikisi de MIT lisansıyla

## Lisans

[MIT](LICENSE)
