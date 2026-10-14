namespace SpyGame.Web.Models.Game;

/// <summary>What the server decided for one round. Only the server ever holds all of it.</summary>
public sealed record RoundDeal(IReadOnlyList<string> SpyIds, string CategoryId, int WordIndex, string FirstAskerId)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Word Word => WordBank.Find(CategoryId)!.Words[WordIndex];

    public bool IsSpy(string playerId) => SpyIds.Contains(playerId);
}

/// <summary>Who plays, how many spies, and dealing the cards.</summary>
public static class RoundRules
{
    public const int MinPlayers = 3;
    public const int MaxPlayers = 12;
    public const int MinSpies = 1;
    public const int MaxSpies = 3;

    /// <summary>
    /// Spies must stay a small group, or there is no one left to catch them:
    /// 3-5 players: 1 spy, 6-8: up to 2, 9-12: up to 3.
    /// </summary>
    public static int MaxSpiesFor(int players) => players switch
    {
        < 6 => 1,
        < 9 => 2,
        _ => 3,
    };

    /// <summary>The number chosen when the room was created, lowered if too few people came.</summary>
    public static int EffectiveSpyCount(int requested, int players) =>
        Math.Clamp(requested, MinSpies, MaxSpiesFor(players));

    /// <summary>
    /// Picks the spies, the place and who asks the first question. The place is drawn from all
    /// places of the chosen categories, so every place is equally likely.
    /// </summary>
    public static RoundDeal Deal(IReadOnlyList<string> playerIds, IReadOnlyCollection<string> categoryIds, int requestedSpies, IRandomSource random)
    {
        if (playerIds.Count is < MinPlayers or > MaxPlayers)
            throw new ArgumentException($"A round needs {MinPlayers}-{MaxPlayers} players.", nameof(playerIds));
        if (playerIds.Distinct().Count() != playerIds.Count)
            throw new ArgumentException("Players must be distinct.", nameof(playerIds));

        var places = categoryIds
            .Distinct()
            .Select(id => WordBank.Find(id) ?? throw new ArgumentException($"Unknown category '{id}'.", nameof(categoryIds)))
            .SelectMany(category => category.Words.Select((_, index) => (category.Id, index)))
            .ToList();
        if (places.Count == 0) throw new ArgumentException("Choose at least one category.", nameof(categoryIds));

        var (categoryId, wordIndex) = places[random.Next(places.Count)];

        // Partial Fisher-Yates shuffle: the first `spies` slots end up as a fair random pick.
        var order = playerIds.ToArray();
        var spies = EffectiveSpyCount(requestedSpies, order.Length);
        for (var i = 0; i < spies; i++)
        {
            var j = i + random.Next(order.Length - i);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var firstAsker = playerIds[random.Next(playerIds.Count)];
        return new RoundDeal(order[..spies], categoryId, wordIndex, firstAsker);
    }
}
