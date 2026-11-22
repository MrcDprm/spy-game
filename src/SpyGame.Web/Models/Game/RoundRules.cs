namespace SpyGame.Web.Models.Game;

/// <summary>
/// What the server decided for one round. Only the server ever holds all of it. <paramref name="Roles"/>
/// maps each civilian to a role of the place (an index into <see cref="WordBank.RolesOf"/>); spies have none.
/// Rounds saved before roles existed have no map.
/// </summary>
public sealed record RoundDeal(IReadOnlyList<string> SpyIds, string PlaceId, string FirstAskerId, IReadOnlyDictionary<string, int>? Roles = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Word Word => WordBank.Find(PlaceId)!;

    public bool IsSpy(string playerId) => SpyIds.Contains(playerId);

    /// <summary>This player's role at the place, or null for a spy.</summary>
    public Role? RoleOf(string playerId) =>
        !IsSpy(playerId) && Roles is not null && Roles.TryGetValue(playerId, out var index) && WordBank.RolesOf(PlaceId) is { } roles && index >= 0 && index < roles.Count
            ? roles[index]
            : null;
}

/// <summary>Who plays, how many spies, and dealing the cards.</summary>
public static class RoundRules
{
    public const int MinPlayers = 3;
    public const int MaxPlayers = 12;
    public const int MinSpies = 1;
    public const int MaxSpies = 3;

    /// <summary>How many recent first askers sit out the draw for the first question.</summary>
    public const int FirstAskerRest = 2;

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
    /// Picks the spies, the place, who asks the first question and the civilians' roles. Every chosen place
    /// is equally likely, and so is every player as a spy, whatever happened in earlier rounds: nothing
    /// about past rounds goes into the spy draw. The first asker is drawn from everyone except the first
    /// askers of the last rounds (<paramref name="recentFirstAskers"/>, newest last), so nobody opens twice in a row.
    /// </summary>
    public static RoundDeal Deal(IReadOnlyList<string> playerIds, IReadOnlyCollection<string> placeIds, int requestedSpies, IRandomSource random,
        IReadOnlyList<string>? recentFirstAskers = null)
    {
        if (playerIds.Count is < MinPlayers or > MaxPlayers)
            throw new ArgumentException($"A round needs {MinPlayers}-{MaxPlayers} players.", nameof(playerIds));
        if (playerIds.Distinct().Count() != playerIds.Count)
            throw new ArgumentException("Players must be distinct.", nameof(playerIds));

        var places = placeIds
            .Distinct()
            .Select(id => WordBank.Exists(id) ? id : throw new ArgumentException($"Unknown place '{id}'.", nameof(placeIds)))
            .ToList();
        if (places.Count == 0) throw new ArgumentException("Choose at least one place.", nameof(placeIds));

        var placeId = places[random.Next(places.Count)];

        // Partial Fisher-Yates shuffle: the first `spies` slots end up as a fair random pick.
        var order = playerIds.ToArray();
        var spies = EffectiveSpyCount(requestedSpies, order.Length);
        for (var i = 0; i < spies; i++)
        {
            var j = i + random.Next(order.Length - i);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var firstAsker = PickFirstAsker(playerIds, recentFirstAskers ?? [], random);
        var spyIds = order[..spies];
        return new RoundDeal(spyIds, placeId, firstAsker, DealRoles(playerIds, spyIds, WordBank.RolesOf(placeId).Count, random));
    }

    /// <summary>
    /// One shuffle of the place's roles; the players then take them by seat, starting again from the top
    /// when there are more players than roles. A civilian's role depends only on their seat and the
    /// shuffle, never on who the spies are, so it tells them nothing about the spies. Spies get none.
    /// </summary>
    public static Dictionary<string, int> DealRoles(IReadOnlyList<string> playerIds, IReadOnlyCollection<string> spyIds, int roleCount, IRandomSource random)
    {
        var roles = new Dictionary<string, int>();
        if (roleCount == 0) return roles;
        var shuffle = Enumerable.Range(0, roleCount).ToArray();
        for (var i = roleCount - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffle[i], shuffle[j]) = (shuffle[j], shuffle[i]);
        }
        for (var seat = 0; seat < playerIds.Count; seat++)
        {
            if (!spyIds.Contains(playerIds[seat])) roles[playerIds[seat]] = shuffle[seat % roleCount];
        }
        return roles;
    }

    /// <summary>
    /// Leaves out the newest recent first askers who are still playing, but always keeps at least two
    /// people in the draw: with three players only the last one sits out, so it stays a real draw.
    /// </summary>
    public static string PickFirstAsker(IReadOnlyList<string> playerIds, IReadOnlyList<string> recentFirstAskers, IRandomSource random)
    {
        var rest = Math.Min(FirstAskerRest, playerIds.Count - 2);
        var resting = recentFirstAskers.Where(playerIds.Contains).Reverse().Distinct().Take(Math.Max(rest, 0)).ToHashSet();
        var candidates = playerIds.Where(id => !resting.Contains(id)).ToList();
        return candidates[random.Next(candidates.Count)];
    }
}
