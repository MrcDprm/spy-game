using Microsoft.EntityFrameworkCore;
using SpyGame.Web.Data;

namespace SpyGame.Web.Services;

/// <summary>Looks names up in the accounts table. A fresh scope per call: RoomService lives for the whole app.</summary>
public sealed class DbRegisteredNames(IServiceScopeFactory scopes) : IRegisteredNames
{
    public async Task<string?> AccountIdForAsync(string nickname)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var normalized = nickname.ToUpperInvariant();
        return await db.Users.Where(u => u.NormalizedUserName == normalized).Select(u => u.Id).FirstOrDefaultAsync();
    }
}
