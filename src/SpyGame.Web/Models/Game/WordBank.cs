namespace SpyGame.Web.Models.Game;

/// <summary>A place, with a stable id (what rooms store) and its Turkish and English names.</summary>
public sealed record Word(string Id, string Tr, string En)
{
    public string In(string lang) => lang == "en" ? En : Tr;
}

/// <summary>A numbered group of places ("Dossier 1"). The number is its only name: it gives no hint.</summary>
public sealed record WordGroup(int Number, IReadOnlyList<Word> Words);

/// <summary>
/// The places players describe without naming them: four groups. The room's creator chooses whole
/// groups or single places; the spy then sees every chosen place in one list, without groups.
/// </summary>
public static class WordBank
{
    /// <summary>Fewer places would make the spy's guess too easy.</summary>
    public const int MinPlaces = 10;

    public static IReadOnlyList<WordGroup> Groups { get; } =
    [
        new(1,
        [
            new("plane", "Uçak", "Plane"), new("bank", "Banka", "Bank"), new("beach", "Kumsal", "Beach"),
            new("theatre", "Tiyatro", "Theatre"), new("casino", "Kumarhane", "Casino"), new("cathedral", "Katedral", "Cathedral"),
            new("circus-tent", "Sirk Çadırı", "Circus Tent"), new("office-party", "İş Partisi", "Office Party"), new("crusader-army", "Haçlı Ordusu", "Crusader Army"),
            new("day-spa", "Spa", "Day Spa"), new("embassy", "Büyükelçilik", "Embassy"), new("hospital", "Hastane", "Hospital"),
            new("hotel", "Otel", "Hotel"), new("military-base", "Askeri Üs", "Military Base"), new("film-studio", "Film Stüdyosu", "Film Studio"),
            new("ocean-liner", "Yolcu Gemisi", "Ocean Liner"), new("passenger-train", "Yolcu Treni", "Passenger Train"), new("pirate-ship", "Korsan Gemisi", "Pirate Ship"),
            new("polar-station", "Kutup Araştırma Merkezi", "Polar Research Station"), new("police-station", "Polis Merkezi", "Police Station"), new("restaurant", "Restoran", "Restaurant"),
            new("school", "Okul", "School"), new("car-repair-shop", "Araba Tamircisi", "Car Repair Shop"), new("space-station", "Uzay İstasyonu", "Space Station"),
            new("submarine", "Denizaltı", "Submarine"), new("supermarket", "Süpermarket", "Supermarket"), new("university", "Üniversite", "University"),
        ]),
        new(2,
        [
            new("amusement-park", "Lunapark", "Amusement Park"), new("art-museum", "Sanat Müzesi", "Art Museum"), new("sweet-factory", "Şeker Fabrikası", "Sweet Factory"),
            new("cat-show", "Kedi Şovu", "Cat Show"), new("cemetery", "Mezarlık", "Cemetery"), new("coal-mine", "Kömür Madeni", "Coal Mine"),
            new("building-site", "İnşaat Alanı", "Building Site"), new("gaming-convention", "Oyun Fuarı", "Gaming Convention"), new("petrol-station", "Benzin İstasyonu", "Petrol Station"),
            new("harbour", "Liman", "Harbour"), new("ice-hockey-arena", "Buz Hokeyi Stadı", "Ice Hockey Arena"), new("prison", "Hapishane", "Prison"),
            new("jazz-club", "Caz Kulübü", "Jazz Club"), new("library", "Kütüphane", "Library"), new("nightclub", "Gece Kulübü", "Nightclub"),
            new("race-track", "Yarış Pisti", "Race Track"), new("retirement-home", "Huzurevi", "Retirement Home"), new("rock-concert", "Rock Konseri", "Rock Concert"),
            new("sightseeing-bus", "Gezi Otobüsü", "Sightseeing Bus"), new("stadium", "Stadyum", "Stadium"), new("underground", "Metro", "Underground"),
            new("united-nations", "B.M.", "The UN"), new("vineyard", "Üzüm Bağı", "Vineyard"), new("wedding", "Düğün", "Wedding"),
            new("zoo", "Hayvanat Bahçesi", "Zoo"),
        ]),
        new(3,
        [
            new("swimming-pool", "Yüzme Havuzu", "Swimming Pool"), new("gym", "Spor Salonu", "Gym"), new("ski-resort", "Kayak Merkezi", "Ski Resort"),
            new("golf-course", "Golf Sahası", "Golf Course"), new("boxing-gym", "Boks Salonu", "Boxing Gym"), new("campsite", "Kamp Alanı", "Campsite"),
            new("holiday-resort", "Tatil Köyü", "Holiday Resort"), new("caravan", "Karavan", "Caravan"), new("yacht", "Yat", "Yacht"),
            new("mountain-cabin", "Dağ Evi", "Mountain Cabin"), new("tutoring-centre", "Dershane", "Tutoring Centre"), new("laboratory", "Laboratuvar", "Laboratory"),
            new("student-dormitory", "Öğrenci Yurdu", "Student Dormitory"), new("conference-hall", "Konferans Salonu", "Conference Hall"), new("pharmacy", "Eczane", "Chemist"),
            new("dentist", "Dişçi", "Dentist"), new("ambulance", "Ambulans", "Ambulance"), new("operating-room", "Ameliyathane", "Operating Theatre"),
            new("airport", "Havalimanı", "Airport"), new("bus-terminal", "Otobüs Terminali", "Bus Terminal"), new("taxi-rank", "Taksi Durağı", "Taxi Rank"),
            new("car-park", "Otopark", "Car Park"), new("cinema", "Sinema", "Cinema"), new("opera-house", "Opera", "Opera House"),
            new("photo-studio", "Fotoğraf Stüdyosu", "Photo Studio"),
        ]),
        new(4,
        [
            new("cafe", "Kafe", "Café"), new("botanical-garden", "Botanik Bahçesi", "Botanical Garden"), new("patisserie", "Pastane", "Patisserie"),
            new("tea-garden", "Çay Bahçesi", "Tea Garden"), new("kebab-house", "Kebapçı", "Kebab House"), new("fish-restaurant", "Balıkçı", "Fish Restaurant"),
            new("ice-cream-shop", "Dondurmacı", "Ice Cream Shop"), new("shopping-mall", "AVM", "Shopping Mall"), new("boutique", "Butik", "Boutique"),
            new("greengrocer", "Manav", "Greengrocer"), new("jeweller", "Kuyumcu", "Jeweller"), new("bookshop", "Kitapçı", "Bookshop"),
            new("street-market", "Pazaryeri", "Street Market"), new("florist", "Çiçekçi", "Florist"), new("toy-shop", "Oyuncakçı", "Toy Shop"),
            new("arcade", "Oyun Salonu", "Arcade"), new("bowling-alley", "Bowling Salonu", "Bowling Alley"), new("pool-hall", "Bilardo Salonu", "Pool Hall"),
            new("karaoke-bar", "Karaoke Bar", "Karaoke Bar"), new("escape-room", "Kaçış Odası", "Escape Room"), new("paintball-field", "Paintball Alanı", "Paintball Field"),
            new("water-park", "Su Parkı", "Water Park"), new("playground", "Çocuk Parkı", "Playground"), new("puppet-theatre", "Kukla Tiyatrosu", "Puppet Theatre"),
            new("dance-studio", "Dans Stüdyosu", "Dance Studio"),
        ]),
    ];

    private static readonly Dictionary<string, Word> ById = Groups.SelectMany(g => g.Words).ToDictionary(w => w.Id);

    /// <summary>What a new room starts with before the creator changes anything: the first two groups.</summary>
    public static IReadOnlyList<string> DefaultPlaceIds { get; } = Groups.Take(2).SelectMany(g => g.Words).Select(w => w.Id).ToList();

    public static Word? Find(string id) => ById.GetValueOrDefault(id);

    public static bool Exists(string id) => ById.ContainsKey(id);
}
