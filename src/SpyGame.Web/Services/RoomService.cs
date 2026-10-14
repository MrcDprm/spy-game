using SpyGame.Web.Models.Game;
using SpyGame.Web.Services.Storage;

namespace SpyGame.Web.Services;

/// <summary>Nicknames that belong to an account (kept in the database, for good).</summary>
public interface IRegisteredNames
{
    /// <summary>The id of the account with this user name, or null.</summary>
    Task<string?> AccountIdForAsync(string nickname);
}

/// <summary>Tells every player in a room what they may now see (the hub does the sending).</summary>
public interface IRoomNotifier
{
    Task RoomChangedAsync(Room room);
}

/// <summary>
/// Every game action goes through here: Controllers (create, join) and the hub (start, vote,
/// guess) only pass on who the player is and what they asked for. The rules are in
/// <see cref="RoomEngine"/>; this class loads and saves the room, keeps the deadline list up
/// to date and has the players told. Methods return null on success or an error code.
/// </summary>
public sealed class RoomService(
    IRoomStore rooms,
    INicknameRegistry nicknames,
    IRegisteredNames registered,
    IRandomSource random,
    TimeProvider clock,
    IRoomNotifier notifier)
{
    private const int MaxCodeAttempts = 10;

    /// <summary>
    /// Gives the player a nickname for this session. The name is reserved for them; their old
    /// one is freed. Not possible in the middle of a game (the room knows them by that name).
    /// </summary>
    public async Task<(string? Error, PlayerSession Session)> ChooseNicknameAsync(PlayerSession session, string? nickname)
    {
        var name = Nickname.Clean(nickname);
        if (Nickname.Problem(name) is { } problem) return (problem, session);

        // An account's name is its own for good; the database is the authority, not the reservation.
        var owner = await registered.AccountIdForAsync(name);
        if (owner is not null)
        {
            if (owner != session.AccountId) return ("nickname_registered", session);
            if (session.Nickname is { } previous && previous != name) await nicknames.ReleaseAsync(previous, session.PlayerId);
            return (null, session with { Nickname = name });
        }

        if (name == session.Nickname)
        {
            await nicknames.TryReserveAsync(name, session.PlayerId); // renew it
            return (null, session);
        }
        if (!await nicknames.TryReserveAsync(name, session.PlayerId)) return ("nickname_in_use", session);
        if (session.Nickname is { } old) await nicknames.ReleaseAsync(old, session.PlayerId);
        return (null, session with { Nickname = name });
    }

    public async Task<(string? Error, string? Code)> CreateAsync(PlayerSession player, RoomSettings settings)
    {
        if (player.Nickname is null) return ("nickname_required", null);
        if (settings.Problem() is { } problem) return (problem, null);

        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var room = RoomEngine.Create(RoomCode.Generate(random), player.PlayerId, player.Nickname, settings, clock.GetUtcNow(), player.AccountId);
            if (await rooms.CreateAsync(room)) return (null, room.Code);
        }
        return ("try_again", null); // ten taken codes in a row: practically never
    }

    public async Task<string?> JoinAsync(PlayerSession player, string code)
    {
        if (player.Nickname is null) return "nickname_required";
        code = RoomCode.Normalize(code);
        if (!RoomCode.IsValid(code)) return "room_not_found";
        return await ChangeAsync(code, room => RoomEngine.Join(room, player.PlayerId, player.Nickname, clock.GetUtcNow(), player.AccountId));
    }

    public Task<string?> LeaveAsync(string playerId, string code) =>
        ChangeAsync(code, room => RoomEngine.Leave(room, playerId));

    public Task<string?> StartRoundAsync(string playerId, string code) =>
        ChangeAsync(code, room => RoomEngine.StartRound(room, playerId, clock.GetUtcNow(), random));

    public Task<string?> ReadyToVoteAsync(string playerId, string code) =>
        ChangeAsync(code, room => RoomEngine.ReadyToVote(room, playerId, clock.GetUtcNow()));

    public Task<string?> VoteAsync(string playerId, string code, string suspectId) =>
        ChangeAsync(code, room => RoomEngine.Vote(room, playerId, suspectId, clock.GetUtcNow()));

    public Task<string?> GuessAsync(string playerId, string code, int wordIndex) =>
        ChangeAsync(code, room => RoomEngine.Guess(room, playerId, wordIndex, clock.GetUtcNow()));

    /// <summary>A player's connection opened or closed (a page refresh, a phone going to sleep).</summary>
    public Task<string?> SetConnectedAsync(string playerId, string code, bool connected) =>
        ChangeAsync(code, room =>
        {
            if (room.Find(playerId) is null) return "not_in_room";
            RoomEngine.SetConnected(room, playerId, connected);
            return null;
        });

    /// <summary>Called by the timer for rooms whose deadline has passed.</summary>
    public async Task TickAsync(string code)
    {
        var update = await rooms.UpdateAsync(code, room => RoomEngine.Tick(room, clock.GetUtcNow()));
        if (!update.Found || update.Room is null)
        {
            await rooms.SetDeadlineAsync(code, null); // the room is gone; forget its deadline
            return;
        }
        if (update.Result) await AfterChangeAsync(update.Room);
        else await rooms.SetDeadlineAsync(code, RoomEngine.NextDeadline(update.Room)); // early call: just fix the list
    }

    /// <summary>What this player sees of the room, or null if they are not in it.</summary>
    public async Task<RoomView?> ViewAsync(string code, string playerId)
    {
        var room = await rooms.GetAsync(code);
        return room?.Find(playerId) is { Left: false } ? RoomView.For(room, playerId, clock.GetUtcNow()) : null;
    }

    private async Task<string?> ChangeAsync(string code, Func<Room, string?> change)
    {
        var update = await rooms.UpdateAsync(code, change);
        if (!update.Found) return "room_not_found";
        if (update.Result is null) await AfterChangeAsync(update.Room);
        return update.Result;
    }

    private async Task AfterChangeAsync(Room? room)
    {
        if (room is null) return; // the last player left and the room was deleted
        await rooms.SetDeadlineAsync(room.Code, RoomEngine.NextDeadline(room));
        await notifier.RoomChangedAsync(room);
    }
}
