namespace SpyGame.Web.Models.Game;

/// <summary>
/// The rules of a room as a state machine: Lobby → Asking → Voting → (LastGuess) → Results.
/// Every action returns null when it was applied, or an error code when it was not allowed;
/// a refused action leaves the room unchanged. Time is passed in, so tests control the clock.
/// </summary>
public static class RoomEngine
{
    public const int VotingSeconds = 60;
    public const int GuessSeconds = 30;

    public static Room Create(string code, string hostId, string hostNickname, RoomSettings settings, DateTimeOffset now, string? hostAccountId = null) => new()
    {
        Code = code,
        HostId = hostId,
        Settings = settings,
        CreatedAt = now,
        Players = [new RoomPlayer { Id = hostId, Nickname = hostNickname, AccountId = hostAccountId, JoinedAt = now }],
    };

    /// <summary>Joining in the middle of a round is fine: the newcomer plays from the next one.</summary>
    public static string? Join(Room room, string playerId, string nickname, DateTimeOffset now, string? accountId = null)
    {
        var existing = room.Find(playerId);
        if (existing is not null)
        {
            existing.Connected = true;
            existing.Left = false;
            return null;
        }
        if (room.Players.Count(p => !p.Left) >= RoundRules.MaxPlayers) return "room_full";
        if (room.Players.Any(p => p.Nickname == nickname)) return "nickname_taken";
        room.Players.Add(new RoomPlayer { Id = playerId, Nickname = nickname, AccountId = accountId, JoinedAt = now });
        return null;
    }

    public static void SetConnected(Room room, string playerId, bool connected)
    {
        if (room.Find(playerId) is { } player) player.Connected = connected;
    }

    /// <summary>
    /// Outside a round the player is removed. During a round they stay in it (their vote and
    /// score still count) until it ends. If the host leaves, the longest-present player takes over.
    /// </summary>
    public static string? Leave(Room room, string playerId)
    {
        var player = room.Find(playerId);
        if (player is null) return "not_in_room";
        if (room.InRound(playerId))
        {
            player.Left = true;
            player.Connected = false;
        }
        else
        {
            room.Players.Remove(player);
        }
        if (room.HostId == playerId) PickNewHost(room);
        return null;
    }

    public static string? StartRound(Room room, string byPlayerId, DateTimeOffset now, IRandomSource random)
    {
        if (room.HostId != byPlayerId) return "not_host";
        if (room.Phase is not (Phase.Lobby or Phase.Results)) return "wrong_phase";
        var players = room.Present.Select(p => p.Id).ToList();
        if (players.Count < RoundRules.MinPlayers) return "not_enough_players";

        var deal = RoundRules.Deal(players, room.Settings.PlaceIds, room.Settings.SpyCount, random);
        room.Round = new RoundState
        {
            Number = room.RoundsPlayed + 1,
            Deal = deal,
            PlayerIds = players,
            StartedAt = now,
            AskingEndsAt = now.AddSeconds(room.Settings.RoundSeconds),
        };
        room.Phase = Phase.Asking;
        return null;
    }

    /// <summary>Players can end the questions early: when more than half of those present agree.</summary>
    public static string? ReadyToVote(Room room, string playerId, DateTimeOffset now)
    {
        if (room.Phase != Phase.Asking) return "wrong_phase";
        if (!room.InRound(playerId)) return "not_in_round";
        var round = room.Round!;
        round.ReadyToVote.Add(playerId);
        var present = PresentInRound(room);
        if (round.ReadyToVote.Count(present.Contains) * 2 > present.Count) BeginVoting(room, now);
        return null;
    }

    /// <summary>A vote can be changed until voting closes. When everyone present has voted, it closes.</summary>
    public static string? Vote(Room room, string voterId, string suspectId, DateTimeOffset now)
    {
        if (room.Phase != Phase.Voting) return "wrong_phase";
        if (!room.InRound(voterId)) return "not_in_round";
        if (!room.Round!.PlayerIds.Contains(suspectId)) return "unknown_player";
        if (voterId == suspectId) return "cannot_vote_self";
        room.Round.Votes[voterId] = suspectId;
        if (PresentInRound(room).All(room.Round.Votes.ContainsKey)) CloseVoting(room, now);
        return null;
    }

    /// <summary>Only the accused spy can guess, once, from the places of the room.</summary>
    public static string? Guess(Room room, string playerId, string placeId, DateTimeOffset now)
    {
        if (room.Phase != Phase.LastGuess) return "wrong_phase";
        var round = room.Round!;
        if (round.AccusedId != playerId) return "not_the_accused";
        if (!room.Settings.PlaceIds.Contains(placeId)) return "invalid_guess";
        round.GuessPlaceId = placeId;
        Finish(room, guessed: placeId == round.Deal.PlaceId);
        return null;
    }

    /// <summary>
    /// Moves the room on when a deadline has passed. Returns true if anything changed.
    /// The next phase starts at the deadline, not at "now": if the server was late (asleep,
    /// restarted), the phases that should already have ended are not given their time again.
    /// </summary>
    public static bool Tick(Room room, DateTimeOffset now)
    {
        var changed = false;
        while (NextDeadline(room) is { } deadline && now >= deadline)
        {
            switch (room.Phase)
            {
                case Phase.Asking: BeginVoting(room, deadline); break;
                case Phase.Voting: CloseVoting(room, deadline); break;
                case Phase.LastGuess: Finish(room, guessed: false); break;
            }
            changed = true;
        }
        return changed;
    }

    public static DateTimeOffset? NextDeadline(Room room) => room.Phase switch
    {
        Phase.Asking => room.Round!.AskingEndsAt,
        Phase.Voting => room.Round!.VotingEndsAt,
        Phase.LastGuess => room.Round!.GuessEndsAt,
        _ => null,
    };

    private static HashSet<string> PresentInRound(Room room) =>
        room.Round!.PlayerIds.Where(id => room.Find(id) is { Connected: true, Left: false }).ToHashSet();

    private static void BeginVoting(Room room, DateTimeOffset now)
    {
        room.Phase = Phase.Voting;
        room.Round!.VotingEndsAt = now.AddSeconds(VotingSeconds);
    }

    private static void CloseVoting(Room room, DateTimeOffset now)
    {
        var round = room.Round!;
        var outcome = Voting.Tally(round.PlayerIds, round.Votes);
        round.AccusedId = outcome.AccusedId;
        if (outcome.AccusedId is not null && round.Deal.IsSpy(outcome.AccusedId))
        {
            room.Phase = Phase.LastGuess;
            round.GuessEndsAt = now.AddSeconds(GuessSeconds);
        }
        else
        {
            Finish(room, guessed: false);
        }
    }

    private static void Finish(Room room, bool guessed)
    {
        var round = room.Round!;
        var vote = Voting.Tally(round.PlayerIds, round.Votes);
        round.Outcome = Scoring.Decide(vote, round.Deal.SpyIds, guessed);
        round.Points = Scoring.Points(round.PlayerIds, round.Deal.SpyIds, round.Votes, round.Outcome).ToDictionary();
        foreach (var (id, points) in round.Points)
        {
            if (room.Find(id) is { } player) player.Score += points;
        }
        room.Phase = Phase.Results;
        room.RoundsPlayed = round.Number;
        room.Players.RemoveAll(p => p.Left);
        if (room.Find(room.HostId) is null) PickNewHost(room);
    }

    private static void PickNewHost(Room room)
    {
        var next = room.Players.Where(p => !p.Left).OrderByDescending(p => p.Connected).ThenBy(p => p.JoinedAt).FirstOrDefault();
        room.HostId = next?.Id ?? "";
    }
}
