namespace SpyGame.Web.Models.Game;

/// <summary>One typed question of a round and its answer. Everyone in the room sees them.</summary>
public sealed class QuestionEntry
{
    public string AskerId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string Question { get; set; } = "";
    public string? Answer { get; set; }
    public bool Unanswered { get; set; } // given up on: no answer came, the turn moved on
    public DateTimeOffset AskedAt { get; set; }
}

/// <summary>
/// Questions in turn, for groups playing apart. Whoever has the turn picks someone and types a
/// question; the answer passes the turn to the one who answered. A question nobody answers can
/// be given up on by the asker after <see cref="ReaskSeconds"/>, and the host can pass the turn on.
/// </summary>
public static class QuestionTurns
{
    public const int ReaskSeconds = 60;
    public const int MaxQuestions = 300; // keeps a stored room small, whatever happens

    public static QuestionEntry? Pending(RoundState round) =>
        round.Questions.Count > 0 && round.Questions[^1] is { Answer: null, Unanswered: false } last ? last : null;

    public static string? Ask(Room room, string playerId, string targetId, string? text, DateTimeOffset now)
    {
        if (CheckRound(room) is { } problem) return problem;
        var round = room.Round!;
        if (round.TurnId != playerId) return "not_your_turn";
        if (targetId == playerId) return "cannot_ask_self";
        if (!round.PlayerIds.Contains(targetId) || room.Find(targetId) is not { Left: false }) return "unknown_player";
        if (PlayerText.Clean(text) is not { } question) return "invalid_text";
        if (round.Questions.Count >= MaxQuestions) return "too_many_questions";

        if (Pending(round) is { } pending)
        {
            // Still waiting for an answer: only after the wait may the asker turn to someone else.
            if (now < pending.AskedAt.AddSeconds(ReaskSeconds)) return "question_pending";
            pending.Unanswered = true;
        }
        round.Questions.Add(new QuestionEntry { AskerId = playerId, TargetId = targetId, Question = question, AskedAt = now });
        return null;
    }

    public static string? Answer(Room room, string playerId, string? text)
    {
        if (CheckRound(room) is { } problem) return problem;
        var round = room.Round!;
        if (Pending(round) is not { } pending || pending.TargetId != playerId) return "nothing_to_answer";
        if (PlayerText.Clean(text) is not { } answer) return "invalid_text";
        pending.Answer = answer;
        round.TurnId = playerId;
        return null;
    }

    /// <summary>
    /// The host's way out when someone does not play on: an open question is given up on and the
    /// turn goes back to its asker; a turn nobody uses goes to the next player still here.
    /// </summary>
    public static string? SkipTurn(Room room, string byPlayerId)
    {
        if (room.HostId != byPlayerId) return "not_host";
        if (CheckRound(room) is { } problem) return problem;
        var round = room.Round!;
        if (Pending(round) is { } pending)
        {
            pending.Unanswered = true;
            round.TurnId = pending.AskerId;
            return null;
        }
        var order = round.PlayerIds.Where(id => room.Find(id) is { Left: false }).ToList();
        if (order.Count == 0) return null;
        var at = order.IndexOf(round.TurnId ?? "");
        round.TurnId = order[(at + 1) % order.Count];
        return null;
    }

    private static string? CheckRound(Room room)
    {
        if (!room.Settings.Remote) return "not_remote";
        if (room.Phase != Phase.Asking || room.Round is null) return "wrong_phase";
        return null;
    }
}
