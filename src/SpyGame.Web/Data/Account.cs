using Microsoft.AspNetCore.Identity;

namespace SpyGame.Web.Data;

/// <summary>An optional account. Its user name is the player's nickname, kept for good.</summary>
public sealed class Account : IdentityUser
{
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set by the admin: this account's points no longer appear on the leaderboard.</summary>
    public bool LeaderboardBanned { get; set; }
}
