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

    public List<string> CategoryIds { get; set; } = [];
    public int RoundSeconds { get; set; } = DefaultRoundSeconds;
    public int SpyCount { get; set; } = 1;

    /// <summary>Null when the settings are usable, otherwise an error code.</summary>
    public string? Problem()
    {
        if (CategoryIds.Count == 0 || CategoryIds.Distinct().Count() != CategoryIds.Count || !CategoryIds.All(WordBank.Exists))
            return "invalid_categories";
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
    public int? GuessIndex { get; set; }
    public RoundOutcome? Outcome { get; set; }
    public Dictionary<string, int>? Points { get; set; }
    public bool Recorded { get; set; } // the points went to the leaderboard (exactly once)
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
    public DateTimeOffset CreatedAt { get; set; }

    public RoomPlayer? Find(string playerId) => Players.FirstOrDefault(p => p.Id == playerId);

    [JsonIgnore]
    public IEnumerable<RoomPlayer> Present => Players.Where(p => p.Connected && !p.Left);

    public bool InRound(string playerId) => Round is not null && Phase is not (Phase.Lobby or Phase.Results) && Round.PlayerIds.Contains(playerId);
}
