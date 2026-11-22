using System.Text.Json;
using System.Text.RegularExpressions;

namespace SpyGame.Web.Models.Game;

/// <summary>A place, with a stable id (what rooms store) and its Turkish and English names.</summary>
public sealed record Word(string Id, string Tr, string En)
{
    public string In(string lang) => lang == "en" ? En : Tr;
}

/// <summary>Who a civilian is at the place ("Nurse" at the hospital), in both languages.</summary>
public sealed record Role(string Tr, string En);

/// <summary>A numbered group of places ("Dossier 1"). The number is its only name: it gives no hint.</summary>
public sealed record WordGroup(int Number, IReadOnlyList<Word> Words);

/// <summary>
/// The places players describe without naming them, in numbered groups, each place with the roles a
/// civilian can have there. The room's creator chooses whole groups or single places; the spy then sees
/// every chosen place in one list, without groups. The content lives in places.json (built into the
/// app), so adding places does not touch the code; it is checked once, when the app starts.
/// </summary>
public static partial class WordBank
{
    /// <summary>Fewer places would make the spy's guess too easy.</summary>
    public const int MinPlaces = 10;

    /// <summary>Every place has this many roles. With more civilians than roles, roles repeat.</summary>
    public const int RolesPerPlace = 7;

    private const string Resource = "SpyGame.Web.places.json";

    public static IReadOnlyList<WordGroup> Groups { get; }

    /// <summary>
    /// What a new room starts with before the creator changes anything: 25 well-known places from
    /// several groups. Enough for the spy to have a real chance, few enough to read on a phone.
    /// </summary>
    public static IReadOnlyList<string> DefaultPlaceIds { get; } =
    [
        "plane", "bank", "beach", "hospital", "hotel", "restaurant", "school", "supermarket", "police-station",
        "amusement-park", "prison", "library", "stadium", "underground", "wedding", "zoo",
        "gym", "cinema", "dentist", "tea-garden", "shopping-mall",
        "turkish-bath", "barber", "ferry", "five-a-side-pitch",
    ];

    private static readonly Dictionary<string, Word> ById;
    private static readonly Dictionary<string, IReadOnlyList<Role>> RolesById;

    static WordBank()
    {
        var groups = new List<WordGroup>();
        ById = [];
        RolesById = [];
        foreach (var group in Load())
        {
            if (group.Group != groups.Count + 1) throw Broken($"group {group.Group} is out of order");
            var words = new List<Word>();
            foreach (var place in group.Places)
            {
                if (!IdPattern().IsMatch(place.Id)) throw Broken($"bad id '{place.Id}'");
                if (string.IsNullOrWhiteSpace(place.Tr) || string.IsNullOrWhiteSpace(place.En)) throw Broken($"'{place.Id}' has no name");
                var roles = place.Roles.Select(r => r is [{ Length: > 0 } tr, { Length: > 0 } en]
                    ? new Role(tr, en)
                    : throw Broken($"'{place.Id}' has a role without both names")).ToList();
                if (roles.Count != RolesPerPlace || roles.DistinctBy(r => r.Tr).Count() != roles.Count || roles.DistinctBy(r => r.En).Count() != roles.Count)
                    throw Broken($"'{place.Id}' needs {RolesPerPlace} different roles");

                var word = new Word(place.Id, place.Tr, place.En);
                if (!ById.TryAdd(word.Id, word)) throw Broken($"'{place.Id}' appears twice");
                RolesById[word.Id] = roles;
                words.Add(word);
            }
            groups.Add(new WordGroup(group.Group, words));
        }
        Groups = groups;

        var all = Groups.SelectMany(g => g.Words).ToList();
        // The spy picks from one list of every chosen place, so names must be unique across groups too.
        if (all.DistinctBy(w => w.Tr).Count() != all.Count || all.DistinctBy(w => w.En).Count() != all.Count)
            throw Broken("two places share a name");
        if (DefaultPlaceIds.FirstOrDefault(id => !ById.ContainsKey(id)) is { } missing) throw Broken($"default place '{missing}' does not exist");
    }

    public static Word? Find(string id) => ById.GetValueOrDefault(id);

    public static bool Exists(string id) => ById.ContainsKey(id);

    /// <summary>The roles a civilian can have at this place (empty for an unknown place).</summary>
    public static IReadOnlyList<Role> RolesOf(string placeId) => RolesById.GetValueOrDefault(placeId) ?? [];

    private static List<GroupData> Load()
    {
        using var stream = typeof(WordBank).Assembly.GetManifestResourceStream(Resource)
            ?? throw Broken("the file is not built into the app");
        return JsonSerializer.Deserialize<List<GroupData>>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw Broken("the file is empty");
    }

    private static InvalidOperationException Broken(string why) => new($"places.json: {why}.");

    [GeneratedRegex("^[a-z]+(-[a-z]+)*$")]
    private static partial Regex IdPattern();

    private sealed record GroupData(int Group, List<PlaceData> Places);

    private sealed record PlaceData(string Id, string Tr, string En, List<string[]> Roles);
}
