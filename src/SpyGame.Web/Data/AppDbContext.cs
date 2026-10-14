using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SpyGame.Web.Data;

/// <summary>
/// The lasting data (Neon Postgres): accounts, the score history behind the weekly leaderboard,
/// and the keys ASP.NET uses to protect cookies and e-mail links, so a restart does not log
/// everyone out or break the links already sent. Live rooms stay in Redis.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<Account>(options), IDataProtectionKeyContext
{
    public DbSet<ScoreEntry> Scores => Set<ScoreEntry>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ScoreEntry>(e =>
        {
            e.Property(s => s.Week).HasMaxLength(8);
            e.Property(s => s.RoomCode).HasMaxLength(6);
            e.HasIndex(s => new { s.Week, s.AccountId });
            // One entry per account per round: recording the same round twice changes nothing.
            e.HasIndex(s => new { s.AccountId, s.RoomCode, s.Round, s.Week }).IsUnique();
            e.HasOne(s => s.Account).WithMany().HasForeignKey(s => s.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
