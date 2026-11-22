namespace SpyGame.Web.Models.Game;

public enum Team
{
    Civilians,
    Spies,
}

/// <summary>
/// How a round ended. <paramref name="RevealedId"/> is a spy who stopped the questions to name the place;
/// then nobody was accused.
/// </summary>
public sealed record RoundOutcome(Team Winner, string? AccusedId, bool AccusedWasSpy, bool SpyGuessedWord, string? RevealedId = null)
{
    /// <summary>The spy who named a place: the one who revealed themselves, or the one who was caught.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? GuesserId => RevealedId ?? (AccusedWasSpy ? AccusedId : null);
}

/// <summary>Who won the round and how many points everyone gets.</summary>
public static class Scoring
{
    public const int SpyWin = 2;          // every spy, when the spies win
    public const int SpyGuessBonus = 1;   // the spy who named the place, caught or of their own accord
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

    /// <summary>
    /// A spy stopped the questions and named a place: right, and the spies win; wrong, and the civilians
    /// win. Nobody voted, so there is no accusation.
    /// </summary>
    public static RoundOutcome Revealed(string spyId, bool namedThePlace) =>
        new(namedThePlace ? Team.Spies : Team.Civilians, AccusedId: null, AccusedWasSpy: false, SpyGuessedWord: namedThePlace, RevealedId: spyId);

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
            if (isSpy && outcome.SpyGuessedWord && id == outcome.GuesserId) score += SpyGuessBonus;
            if (!isSpy && outcome.Winner == Team.Civilians) score += CivilianWin;
            if (!isSpy && votes.TryGetValue(id, out var suspect) && spyIds.Contains(suspect)) score += CorrectVoteBonus;
            points[id] = score;
        }
        return points;
    }
}
