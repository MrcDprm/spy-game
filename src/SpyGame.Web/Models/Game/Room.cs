using System.Text.Json.Serialization;

namespace SpyGame.Web.Models.Game;

/// <summary>Where a room is in the game.</summary>
public enum Phase
{
    Lobby,     // waiting for players
    Asking,    // the round's questions, against the clock
    Voting,    // everyone names a suspect
    LastGuess, // a caught spy tries to name the place
    Results,   // the round is over; the host can start the next one
}

/// <summary>Chosen by whoever creates the room.</summary>
public sealed class RoomSettings
{
    public const int MinRoundSeconds = 60;
    public const int MaxRoundSeconds = 900;
    public const int DefaultRoundSeconds = 480;

    /// <summary>The places this room plays with (at least <see cref="WordBank.MinPlaces"/>).</summary>
    public List<string> PlaceIds { get; set; } = [];

    /// <summary>Places the host struck out between rounds: still listed, but never dealt.</summary>
    public List<string> StruckPlaceIds { get; set; } = [];

    /// <summary>The places the next round can be dealt from, in the chosen order.</summary>
    [JsonIgnore]
    public IEnumerable<string> ActivePlaceIds => PlaceIds.Where(id => !StruckPlaceIds.Contains(id));
    public int RoundSeconds { get; set; } = DefaultRoundSeconds;
    public int SpyCount { get; set; } = 1;

    /// <summary>Playing apart: questions are typed in turn and a chat is open. Together: questions are asked out loud.</summary>
    public bool Remote { get; set; }

    /// <summary>Null when the settings are usable, otherwise an error code.</summary>
    public string? Problem()
    {
        if (PlaceIds.Count < WordBank.MinPlaces || PlaceIds.Distinct().Count() != PlaceIds.Count || !PlaceIds.All(WordBank.Exists))
            return "invalid_places";
        if (!StruckPlaceIds.All(PlaceIds.Contains) || ActivePlaceIds.Count() < WordBank.MinPlaces) return "invalid_places";
        if (RoundSeconds is < MinRoundSeconds or > MaxRoundSeconds) return "invalid_round_length";
        if (SpyCount is < RoundRules.MinSpies or > RoundRules.MaxSpies) return "invalid_spy_count";
        return null;
    }
}

public sealed class RoomPlayer
{
    public string Id { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string? AccountId { get; set; } // a confirmed account: its points count for the leaderboard
    public bool Connected { get; set; } = true;
    public bool Left { get; set; } // left during a round; removed when the round ends
    public int Score { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}

/// <summary>One round. The deal (spies and place) never leaves the server as a whole.</summary>
public sealed class RoundState
{
    public int Number { get; set; }
    public RoundDeal Deal { get; set; } = null!;
    public List<string> PlayerIds { get; set; } = []; // who plays this round (others wait for the next)
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset AskingEndsAt { get; set; }
    public HashSet<string> ReadyToVote { get; set; } = [];
    public DateTimeOffset? VotingEndsAt { get; set; }
    public Dictionary<string, string> Votes { get; set; } = [];
    public string? AccusedId { get; set; }
    public DateTimeOffset? GuessEndsAt { get; set; }
    public string? GuessPlaceId { get; set; }
    public RoundOutcome? Outcome { get; set; }
    public Dictionary<string, int>? Points { get; set; }
    public bool Recorded { get; set; } // the points went to the leaderboard (exactly once)

    // Groups playing apart: whose turn it is to ask, and the typed questions so far.
    public string? TurnId { get; set; }
    public List<QuestionEntry> Questions { get; set; } = [];
}

public sealed class Room
{
    public string Code { get; set; } = "";
    public string HostId { get; set; } = "";
    public RoomSettings Settings { get; set; } = new();
    public List<RoomPlayer> Players { get; set; } = [];
    public Phase Phase { get; set; } = Phase.Lobby;
    public RoundState? Round { get; set; }
    public int RoundsPlayed { get; set; }

    /// <summary>Who asked first in the last rounds, newest last: they are skipped next time, so the first question moves around.</summary>
    public List<string> RecentFirstAskers { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }

    public RoomPlayer? Find(string playerId) => Players.FirstOrDefault(p => p.Id == playerId);

    [JsonIgnore]
    public IEnumerable<RoomPlayer> Present => Players.Where(p => p.Connected && !p.Left);

    public bool InRound(string playerId) => Round is not null && Phase is not (Phase.Lobby or Phase.Results) && Round.PlayerIds.Contains(playerId);
}
