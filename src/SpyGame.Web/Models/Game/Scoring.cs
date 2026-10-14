namespace SpyGame.Web.Models.Game;

public enum Team
{
    Civilians,
    Spies,
}

/// <summary>How a round ended.</summary>
public sealed record RoundOutcome(Team Winner, string? AccusedId, bool AccusedWasSpy, bool SpyGuessedWord);

/// <summary>Who won the round and how many points everyone gets.</summary>
public static class Scoring
{
    public const int SpyWin = 2;          // every spy, when the spies win
    public const int SpyGuessBonus = 1;   // the caught spy who still named the place
    public const int CivilianWin = 1;     // every civilian, when the civilians win
    public const int CorrectVoteBonus = 1; // a civilian who voted for a spy, win or lose

    /// <summary>
    /// The civilians win only by accusing a spy who then fails to name the place.
    /// No accusation, the wrong person, or a correct guess: the spies win.
    /// </summary>
    public static RoundOutcome Decide(VoteOutcome vote, IReadOnlyCollection<string> spyIds, bool caughtSpyGuessedWord)
    {
        var accusedIsSpy = vote.AccusedId is not null && spyIds.Contains(vote.AccusedId);
        var guessed = accusedIsSpy && caughtSpyGuessedWord;
        var winner = accusedIsSpy && !guessed ? Team.Civilians : Team.Spies;
        return new RoundOutcome(winner, vote.AccusedId, accusedIsSpy, guessed);
    }

    public static IReadOnlyDictionary<string, int> Points(
        IReadOnlyCollection<string> playerIds,
        IReadOnlyCollection<string> spyIds,
        IReadOnlyDictionary<string, string> votes,
        RoundOutcome outcome)
    {
        var points = new Dictionary<string, int>();
        foreach (var id in playerIds)
        {
            var isSpy = spyIds.Contains(id);
            var score = 0;
            if (isSpy && outcome.Winner == Team.Spies) score += SpyWin;
            if (isSpy && outcome.SpyGuessedWord && id == outcome.AccusedId) score += SpyGuessBonus;
            if (!isSpy && outcome.Winner == Team.Civilians) score += CivilianWin;
            if (!isSpy && votes.TryGetValue(id, out var suspect) && spyIds.Contains(suspect)) score += CorrectVoteBonus;
            points[id] = score;
        }
        return points;
    }
}
