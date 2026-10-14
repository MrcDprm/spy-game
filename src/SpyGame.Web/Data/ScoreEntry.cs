namespace SpyGame.Web.Data;

/// <summary>Points one account won in one round. The weekly leaderboard is the sum per week.</summary>
public sealed class ScoreEntry
{
    public long Id { get; set; }
    public string AccountId { get; set; } = "";
    public Account? Account { get; set; }
    public string Week { get; set; } = "";       // "2026-W42", in Istanbul time
    public int Points { get; set; }
    public string RoomCode { get; set; } = "";
    public int Round { get; set; }
    public DateTimeOffset At { get; set; }
}
