namespace SpyGame.Web.Models.Game;

/// <summary>The vote count and who, if anyone, the table accuses.</summary>
public sealed record VoteOutcome(string? AccusedId, IReadOnlyDictionary<string, int> Counts);

public static class Voting
{
    /// <summary>
    /// Everyone votes for one other player. The player with the most votes is accused, but only
    /// with at least 2 votes and no tie: a split table accuses no one, and the spies get away.
    /// Votes from or for people outside the round, and votes for yourself, do not count.
    /// </summary>
    public static VoteOutcome Tally(IReadOnlyCollection<string> playerIds, IReadOnlyDictionary<string, string> votes)
    {
        var players = playerIds.ToHashSet();
        var counts = players.ToDictionary(id => id, _ => 0);
        foreach (var (voter, suspect) in votes)
        {
            if (players.Contains(voter) && players.Contains(suspect) && voter != suspect) counts[suspect]++;
        }

        var top = counts.Values.DefaultIfEmpty(0).Max();
        var leaders = counts.Where(c => c.Value == top).Select(c => c.Key).ToList();
        var accused = top >= 2 && leaders.Count == 1 ? leaders[0] : null;
        return new VoteOutcome(accused, counts);
    }
}
