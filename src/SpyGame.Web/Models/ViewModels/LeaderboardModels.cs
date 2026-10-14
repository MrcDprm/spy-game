using SpyGame.Web.Services;

namespace SpyGame.Web.Models.ViewModels;

public sealed class LeaderboardViewModel
{
    public required string Week { get; init; }
    public bool IsLastWeek { get; init; }
    public DateTimeOffset Starts { get; init; }
    public required IReadOnlyList<LeaderboardLine> Lines { get; init; }
    public (long Position, long Points)? You { get; init; }
    public string? YourAccountId { get; init; }
}

public sealed record BannedAccount(string Id, string Nickname);

public sealed class AdminViewModel
{
    public required string Week { get; init; }
    public required IReadOnlyList<LeaderboardLine> Lines { get; init; }
    public required IReadOnlyList<BannedAccount> Banned { get; init; }
}
