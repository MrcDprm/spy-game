# Casus — Spy Game

[English](README.md) | **Türkçe**

> Yapım aşamasında: 2025'te ASP.NET Core MVC ile yazdığım casus oyununun, her oyuncunun kendi telefonundan katıldığı gerçek zamanlı bir parti oyunu olarak yeniden yazımı.

Herkes kendi telefonundan bir odaya katılır. Casus (ya da casuslar) dışındaki bütün oyuncular aynı gizli kelimeyi alır; casus sadece kategoriyi görür. Oyuncular birbirine sorular sorar, sonra casusun kim olduğunu oylar. Yakalanan casusun kelimeyi tahmin etmek için son bir şansı vardır.

**Canlı:** spygame.miracdeprem.com (yakında)

## Özellikler (planlanan)

- **Odalar:** 6 karakterlik kod, link ya da QR kodla oda kurulur. Oda kurulurken kategoriler, tur süresi ve casus sayısı (1–3) seçilir. En fazla 12 oyuncu. Sayfa yenilense ya da bağlantı kopsa da oyuncu yerini kaybetmez.
- **Turlar:** casusları ve kelimeyi sunucu seçer. Her oyuncuya sadece kendi kartı gider; kart basılı tutulunca görünür. Casus kelimeyi değil, olası kelimelerin listesini görür.
- **Oylama:** herkes kendi telefonundan oy verir, sonuç canlı görünür. Yakalanan casus kelimeyi bilirse yine kazanır.
- **Puanlar:**
  - Odada turlar boyunca biriken bir skor tablosu (Redis).
  - Haftalık "Haftanın casusları" sıralaması. Sıralamada sadece e-postası doğrulanmış hesaplar yer alır; yönetici bir hesabı ya da puanı kaldırabilir.
- **Hesap (isteğe bağlı):** misafir olarak takma adla oynanabilir; adını korumak ve sıralamaya girmek için doğrulanmış e-postayla kayıt olunur.
- **Takma adlar:**
  - Sadece küçük harf `a-z` ve rakam, 3–20 karakter, harfle başlar.
  - Hesap adı kalıcı olarak ayrılır; misafir adı oturumu sürdükçe ayrılı kalır.
- **10 kategori ve yaklaşık 150 kelime**, Türkçe ve İngilizce.
- **Türkçe ve İngilizce**, koyu ve açık tema, önce telefon için tasarım.

## Kullanılan Teknolojiler

- ASP.NET Core MVC (Model, View, Controller), Razor görünümler
- Canlı kısımlar için SignalR; oyun kuralları sunucuda
- Canlı odalar ve sıralama için Redis
- Hesaplar ve puan geçmişi için PostgreSQL (Neon) ve Entity Framework Core
- ASP.NET Core Identity, e-posta doğrulaması Resend ile
- xUnit testleri
- Render üzerinde Docker

## Gelecek Planları

- Odaya özel kelime listeleri
- Seyirci modu
- Farklı oyun modları
