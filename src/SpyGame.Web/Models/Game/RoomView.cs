namespace SpyGame.Web.Models.Game;

// What one player is allowed to see of a room. The server builds a separate view for every
// player and sends only that: the place goes to the civilians, and nobody learns who the spies
// are before the round ends. The room's list of places is the same for everyone, so everyone in
// the round gets it: it tells no one who the spy is.

public sealed record PlayerView(string Id, string Nickname, bool Connected, int Score, bool IsHost, bool InRound, bool Ready, bool HasVoted);

/// <summary>The room's places with the struck ones marked: the same for everyone, so it gives no one away.</summary>
public sealed record SettingsView(IReadOnlyList<Word> Places, IReadOnlyList<string> Struck, int RoundSeconds, int SpyCount);

/// <summary>A civilian gets the place, a spy does not; both get every place of the room (no groups) and the number of spies.</summary>
public sealed record CardView(bool IsSpy, Word? Place, IReadOnlyList<Word> Places, int SpyCount);

public sealed record VotingView(string? YourVote, int VotesCast, int Voters);

/// <summary>The list of places goes only to the accused spy, who has to pick from it.</summary>
public sealed record GuessView(string AccusedId, bool YouAreAccused, IReadOnlyList<Word>? Places);

/// <summary>After the round everything is revealed.</summary>
public sealed record ResultView(
    Team Winner,
    IReadOnlyList<string> SpyIds,
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
        // The places the round is dealt from, in the order they were chosen; the page sorts them for the reader's language.
        var places = room.Settings.ActivePlaceIds.Select(id => WordBank.Find(id)!).ToList();

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
            room.Settings.PlaceIds.Select(id => WordBank.Find(id)!).ToList(),
            room.Settings.StruckPlaceIds,
            room.Settings.RoundSeconds,
            room.Settings.SpyCount);

        CardView? card = null;
        if (inRound)
        {
            card = round!.Deal.IsSpy(playerId)
                ? new CardView(true, Place: null, places, round.Deal.SpyIds.Count)
                : new CardView(false, round.Deal.Word, places, round.Deal.SpyIds.Count);
        }

        VotingView? voting = room.Phase == Phase.Voting && inRound
            ? new VotingView(round!.Votes.GetValueOrDefault(playerId), round.Votes.Count, round.PlayerIds.Count)
            : null;

        GuessView? guess = room.Phase == Phase.LastGuess
            ? new GuessView(round!.AccusedId!, round.AccusedId == playerId, round.AccusedId == playerId ? places : null)
            : null;

        ResultView? result = null;
        if (room.Phase == Phase.Results && round?.Outcome is { } outcome)
        {
            result = new ResultView(
                outcome.Winner,
                round.Deal.SpyIds,
                round.Deal.Word,
                outcome.AccusedId,
                outcome.SpyGuessedWord,
                round.GuessPlaceId is { } g ? WordBank.Find(g) : null,
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
