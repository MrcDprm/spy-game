using SpyGame.Web.Models.Game;

namespace SpyGame.Web.Services.Storage;

/// <summary>The result of a change to a room: whether it existed, the room afterwards, and what the change returned.</summary>
public sealed record RoomUpdate<T>(bool Found, Room? Room, T? Result);

/// <summary>Live rooms. Kept in Redis in production so a restart does not end the games.</summary>
public interface IRoomStore
{
    /// <summary>Saves a new room; false if a room with that code already exists.</summary>
    Task<bool> CreateAsync(Room room);

    Task<Room?> GetAsync(string code);

    /// <summary>
    /// Reads the room, applies <paramref name="change"/> and saves it, retrying if someone else
    /// saved the room in between (two players voting at the same moment). A room left with no
    /// players is deleted.
    /// </summary>
    Task<RoomUpdate<T>> UpdateAsync<T>(string code, Func<Room, T> change);

    /// <summary>When the room's current phase ends (null: no deadline).</summary>
    Task SetDeadlineAsync(string code, DateTimeOffset? at);

    /// <summary>Rooms whose deadline has passed.</summary>
    Task<IReadOnlyList<string>> DueAsync(DateTimeOffset now, int max = 100);
}

/// <summary>A browser's player: a random id and the nickname they chose.</summary>
public sealed record PlayerSession(string PlayerId, string? Nickname);

/// <summary>Guest sessions, found by the (hashed) token in the browser's cookie.</summary>
public interface ISessionStore
{
    Task<PlayerSession?> GetAsync(string token);

    /// <summary>Saves the session and keeps it for another 24 hours.</summary>
    Task SaveAsync(string token, PlayerSession session);
}

/// <summary>Nicknames in use. A name belongs to one player until they have been away for 24 hours.</summary>
public interface INicknameRegistry
{
    /// <summary>True if the name was free or already this player's; the reservation is renewed.</summary>
    Task<bool> TryReserveAsync(string nickname, string playerId);

    /// <summary>Frees the name, but only if it belongs to this player.</summary>
    Task ReleaseAsync(string nickname, string playerId);
}

internal static class StoreKeys
{
    public static readonly TimeSpan RoomLifetime = TimeSpan.FromHours(3);
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan NicknameLifetime = TimeSpan.FromHours(24);

    public const string Deadlines = "sg:deadlines";
    public static string Room(string code) => $"sg:room:{code}";
    public static string Session(string token) => $"sg:session:{Hash(token)}";
    public static string Nickname(string name) => $"sg:nick:{name}";

    // Only a hash of the cookie is stored: someone who reads Redis cannot take over a player.
    private static string Hash(string token) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
