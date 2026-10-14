namespace SpyGame.Web.Models.Game;

public sealed record Word(string Tr, string En)
{
    public string In(string lang) => lang == "en" ? En : Tr;
}

public sealed record Category(string Id, string NameTr, string NameEn, IReadOnlyList<Word> Words)
{
    public string NameIn(string lang) => lang == "en" ? NameEn : NameTr;
}

/// <summary>
/// The places players describe without naming them: 10 categories, 15 places each, from the
/// first version of the game. Every place has a Turkish and an English name.
/// </summary>
public static class WordBank
{
    public static IReadOnlyList<Category> Categories { get; } =
    [
        new("sports", "Spor", "Sports",
        [
            new("Stadyum", "Stadium"), new("Yüzme Havuzu", "Swimming Pool"), new("Tenis Kortu", "Tennis Court"),
            new("Basketbol Sahası", "Basketball Court"), new("Spor Salonu", "Gym"), new("Kayak Merkezi", "Ski Resort"),
            new("Golf Sahası", "Golf Course"), new("Maraton Parkuru", "Marathon Route"), new("Boks Salonu", "Boxing Gym"),
            new("Futbol Sahası", "Football Pitch"), new("Voleybol Sahası", "Volleyball Court"), new("Bisiklet Parkuru", "Cycling Track"),
            new("Okçuluk Alanı", "Archery Range"), new("Beyzbol Sahası", "Baseball Field"), new("Kriket Sahası", "Cricket Ground"),
        ]),
        new("holiday", "Tatil", "Holiday",
        [
            new("Plaj", "Beach"), new("Dağ Evi", "Mountain Cabin"), new("Kamp Alanı", "Campsite"),
            new("Tatil Köyü", "Holiday Resort"), new("Karavan", "Caravan"), new("Bungalov", "Bungalow"),
            new("Lüks Otel", "Luxury Hotel"), new("Pansiyon", "Guesthouse"), new("Tatil Adası", "Holiday Island"),
            new("Yat", "Yacht"), new("Dağ Zirvesi", "Mountain Summit"), new("Orman Kampı", "Forest Camp"),
            new("Termal Otel", "Thermal Spa Hotel"), new("Tatil Kasabası", "Seaside Town"), new("Çadır", "Tent"),
        ]),
        new("education", "Eğitim", "Education",
        [
            new("Okul", "School"), new("Kütüphane", "Library"), new("Üniversite", "University"),
            new("Dershane", "Tutoring Centre"), new("Sınıf", "Classroom"), new("Laboratuvar", "Laboratory"),
            new("Konferans Salonu", "Conference Hall"), new("Amfi", "Lecture Theatre"), new("Öğrenci Yurdu", "Student Dormitory"),
            new("Atölye", "Workshop"), new("Bilgisayar Odası", "Computer Lab"), new("Müzik Sınıfı", "Music Room"),
            new("Spor Salonu", "School Gym"), new("Öğretmenler Odası", "Staff Room"), new("Kantin", "Canteen"),
        ]),
        new("health", "Sağlık", "Health",
        [
            new("Hastane", "Hospital"), new("Eczane", "Pharmacy"), new("Klinik", "Clinic"),
            new("Ambulans", "Ambulance"), new("Dişçi", "Dentist"), new("Ameliyathane", "Operating Room"),
            new("Acil Servis", "Emergency Room"), new("Göz Hastanesi", "Eye Hospital"), new("Fizik Tedavi Merkezi", "Physiotherapy Centre"),
            new("Psikiyatri Kliniği", "Psychiatric Clinic"), new("Aile Sağlığı Merkezi", "Family Health Centre"), new("Doğumhane", "Maternity Ward"),
            new("Kan Merkezi", "Blood Bank"), new("Radyoloji", "Radiology"), new("Rehabilitasyon Merkezi", "Rehabilitation Centre"),
        ]),
        new("transport", "Ulaşım", "Transport",
        [
            new("Havalimanı", "Airport"), new("Tren Garı", "Train Station"), new("Otobüs Terminali", "Bus Terminal"),
            new("Liman", "Harbour"), new("Metro İstasyonu", "Metro Station"), new("Taksi Durağı", "Taxi Rank"),
            new("Feribot", "Ferry"), new("Otopark", "Car Park"), new("Helikopter Pisti", "Helipad"),
            new("Tramvay", "Tram"), new("Otoban", "Motorway"), new("Bisiklet Yolu", "Cycle Lane"),
            new("Gemi", "Ship"), new("Uçak", "Plane"), new("Otobüs", "Bus"),
        ]),
        new("food", "Yeme-İçme", "Food & Drink",
        [
            new("Restoran", "Restaurant"), new("Kafe", "Café"), new("Bar", "Bar"),
            new("Pastane", "Patisserie"), new("Fast Food", "Fast Food Place"), new("Lokanta", "Diner"),
            new("Çay Bahçesi", "Tea Garden"), new("Büfe", "Snack Kiosk"), new("Pideci", "Pide Shop"),
            new("Kebapçı", "Kebab House"), new("Balıkçı", "Fish Restaurant"), new("Kahve Dükkanı", "Coffee Shop"),
            new("Dondurmacı", "Ice Cream Shop"), new("Meze Barı", "Meze Bar"), new("Şarap Evi", "Wine Bar"),
        ]),
        new("art", "Sanat", "Art",
        [
            new("Tiyatro", "Theatre"), new("Sinema", "Cinema"), new("Müzikhol", "Music Hall"),
            new("Sergi Salonu", "Exhibition Hall"), new("Heykel Atölyesi", "Sculpture Studio"), new("Resim Galerisi", "Art Gallery"),
            new("Opera", "Opera House"), new("Bale Salonu", "Ballet Studio"), new("Konser Alanı", "Concert Venue"),
            new("Fotoğraf Stüdyosu", "Photo Studio"), new("Dans Stüdyosu", "Dance Studio"), new("Müzik Stüdyosu", "Recording Studio"),
            new("Sanat Okulu", "Art School"), new("Kukla Tiyatrosu", "Puppet Theatre"), new("Grafiti Alanı", "Graffiti Wall"),
        ]),
        new("shopping", "Alışveriş", "Shopping",
        [
            new("AVM", "Shopping Mall"), new("Süpermarket", "Supermarket"), new("Butik", "Boutique"),
            new("Manav", "Greengrocer"), new("Kuyumcu", "Jeweller"), new("Kitapçı", "Bookshop"),
            new("Elektronik Mağazası", "Electronics Store"), new("Ayakkabıcı", "Shoe Shop"), new("Pazaryeri", "Street Market"),
            new("Çiçekçi", "Florist"), new("Oyuncakçı", "Toy Shop"), new("Eczane", "Chemist"),
            new("Kırtasiye", "Stationery Shop"), new("Giyim Mağazası", "Clothes Shop"), new("Hediyelikçi", "Gift Shop"),
        ]),
        new("nature", "Doğa", "Nature",
        [
            new("Orman", "Forest"), new("Göl", "Lake"), new("Dağ", "Mountain"),
            new("Şelale", "Waterfall"), new("Mağara", "Cave"), new("Milli Park", "National Park"),
            new("Botanik Bahçesi", "Botanical Garden"), new("Çöl", "Desert"), new("Yayla", "Highland Pasture"),
            new("Vadi", "Valley"), new("Sazlık", "Reed Marsh"), new("Deniz", "Sea"),
            new("Kanyon", "Canyon"), new("Adacık", "Islet"), new("Fırtına Vadisi", "Fırtına Valley"),
        ]),
        new("fun", "Eğlence", "Entertainment",
        [
            new("Lunapark", "Funfair"), new("Oyun Salonu", "Arcade"), new("Bowling Salonu", "Bowling Alley"),
            new("Bilardo Salonu", "Pool Hall"), new("Karaoke Bar", "Karaoke Bar"), new("Disko", "Nightclub"),
            new("Paintball Alanı", "Paintball Field"), new("Go-Kart Pisti", "Go-Kart Track"), new("Kaçış Odası", "Escape Room"),
            new("Sanal Gerçeklik Merkezi", "VR Arcade"), new("Trambolin Parkı", "Trampoline Park"), new("Su Parkı", "Water Park"),
            new("Atlıkarınca", "Carousel"), new("Çocuk Parkı", "Playground"), new("Mini Golf", "Mini Golf"),
        ]),
    ];

    private static readonly Dictionary<string, Category> ById = Categories.ToDictionary(c => c.Id);

    public static Category? Find(string id) => ById.GetValueOrDefault(id);

    public static bool Exists(string id) => ById.ContainsKey(id);
}
