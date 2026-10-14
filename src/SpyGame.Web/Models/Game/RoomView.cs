namespace SpyGame.Web.Models.Game;

// What one player is allowed to see of a room. The server builds a separate view for every
// player and sends only that: the place goes to the civilians, the list of possible places to
// the spies, and nobody learns who the spies are before the round ends.

public sealed record CategoryRef(string Id, string NameTr, string NameEn)
{
    public static CategoryRef Of(Category c) => new(c.Id, c.NameTr, c.NameEn);
}

public sealed record PlayerView(string Id, string Nickname, bool Connected, int Score, bool IsHost, bool InRound, bool Ready, bool HasVoted);

public sealed record SettingsView(IReadOnlyList<CategoryRef> Categories, int RoundSeconds, int SpyCount);

/// <summary>A civilian gets the place; a spy gets the list it is hidden in, and the number of spies.</summary>
public sealed record CardView(bool IsSpy, CategoryRef Category, Word? Place, IReadOnlyList<Word>? Places, int SpyCount);

public sealed record VotingView(string? YourVote, int VotesCast, int Voters);

/// <summary>The list of places goes only to the accused spy, who has to pick from it.</summary>
public sealed record GuessView(string AccusedId, bool YouAreAccused, IReadOnlyList<Word>? Places);

/// <summary>After the round everything is revealed.</summary>
public sealed record ResultView(
    Team Winner,
    IReadOnlyList<string> SpyIds,
    CategoryRef Category,
    Word Place,
    string? AccusedId,
    bool SpyGuessed,
    Word? Guess,
    IReadOnlyDictionary<string, string> Votes,
    IReadOnlyDictionary<string, int> Points);

public sealed record RoomView(
    string Code,
    Phase Phase,
    int Round,
    DateTimeOffset? EndsAt,
    DateTimeOffset ServerNow,
    string YouId,
    bool YouAreHost,
    IReadOnlyList<PlayerView> Players,
    SettingsView Settings,
    CardView? Card,
    string? FirstAskerId,
    VotingView? Voting,
    GuessView? Guess,
    ResultView? Result)
{
    public static RoomView For(Room room, string playerId, DateTimeOffset now)
    {
        var round = room.Round;
        var active = round is not null && room.Phase is Phase.Asking or Phase.Voting or Phase.LastGuess;
        var inRound = active && round!.PlayerIds.Contains(playerId);
        var category = round is null ? null : WordBank.Find(round.Deal.CategoryId)!;

        var players = room.Players
            .Where(p => !p.Left)
            .OrderBy(p => p.JoinedAt)
            .Select(p => new PlayerView(
                p.Id, p.Nickname, p.Connected, p.Score, p.Id == room.HostId,
                InRound: active && round!.PlayerIds.Contains(p.Id),
                Ready: room.Phase == Phase.Asking && round!.ReadyToVote.Contains(p.Id),
                HasVoted: room.Phase == Phase.Voting && round!.Votes.ContainsKey(p.Id)))
            .ToList();

        var settings = new SettingsView(
            room.Settings.CategoryIds.Select(id => CategoryRef.Of(WordBank.Find(id)!)).ToList(),
            room.Settings.RoundSeconds,
            room.Settings.SpyCount);

        CardView? card = null;
        if (inRound)
        {
            card = round!.Deal.IsSpy(playerId)
                ? new CardView(true, CategoryRef.Of(category!), Place: null, category!.Words, round.Deal.SpyIds.Count)
                : new CardView(false, CategoryRef.Of(category!), round.Deal.Word, Places: null, round.Deal.SpyIds.Count);
        }

        VotingView? voting = room.Phase == Phase.Voting && inRound
            ? new VotingView(round!.Votes.GetValueOrDefault(playerId), round.Votes.Count, round.PlayerIds.Count)
            : null;

        GuessView? guess = room.Phase == Phase.LastGuess
            ? new GuessView(round!.AccusedId!, round.AccusedId == playerId, round.AccusedId == playerId ? category!.Words : null)
            : null;

        ResultView? result = null;
        if (room.Phase == Phase.Results && round?.Outcome is { } outcome)
        {
            result = new ResultView(
                outcome.Winner,
                round.Deal.SpyIds,
                CategoryRef.Of(category!),
                round.Deal.Word,
                outcome.AccusedId,
                outcome.SpyGuessedWord,
                round.GuessIndex is { } g ? category!.Words[g] : null,
                round.Votes,
                round.Points ?? []);
        }

        return new RoomView(
            room.Code,
            room.Phase,
            round?.Number ?? 0,
            RoomEngine.NextDeadline(room),
            now,
            playerId,
            room.HostId == playerId,
            players,
            settings,
            card,
            active ? round!.Deal.FirstAskerId : null,
            voting,
            guess,
            result);
    }
}
