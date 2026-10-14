using SpyGame.Web.Models.Game;
using SpyGame.Web.Services.Storage;
using StackExchange.Redis;

namespace SpyGame.Tests.Services;

/// <summary>A clock the test moves by hand.</summary>
public sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>
/// The same checks for both stores. The Redis ones run when REDIS_URL points at a test Redis
/// (dotnet user-secrets or an environment variable); otherwise they are skipped.
/// </summary>
public abstract class StoreContract
{
    protected abstract (IRoomStore Rooms, ISessionStore Sessions, INicknameRegistry Nicknames) Store();

    // Codes and names unique to each run, so tests sharing one Redis do not see each other.
    private static string NewCode() => RoomCode.Generate(new CryptoRandomSource());
    private static string NewName() => "t" + Guid.NewGuid().ToString("N")[..12];

    private static Room NewRoom(string code) => RoomEngine.Create(code, "host", "host", new RoomSettings { CategoryIds = ["art"] }, DateTimeOffset.UtcNow);

    [Fact]
    public async Task A_room_survives_being_saved_and_read_back()
    {
        var (rooms, _, _) = Store();
        var code = NewCode();
        var room = NewRoom(code);
        RoomEngine.Join(room, "p2", "two", DateTimeOffset.UtcNow);
        RoomEngine.Join(room, "p3", "three", DateTimeOffset.UtcNow);
        RoomEngine.StartRound(room, "host", DateTimeOffset.UtcNow, new CryptoRandomSource());
        Assert.True(await rooms.CreateAsync(room));
        Assert.False(await rooms.CreateAsync(NewRoom(code))); // the code is taken

        var back = (await rooms.GetAsync(code))!;
        Assert.Equal(GameJson.Write(room), GameJson.Write(back));
        Assert.Equal(room.Round!.Deal.Word, back.Round!.Deal.Word);
        Assert.Equal(Phase.Asking, back.Phase);
    }

    [Fact]
    public async Task Changes_are_saved_and_an_empty_room_is_deleted()
    {
        var (rooms, _, _) = Store();
        var code = NewCode();
        await rooms.CreateAsync(NewRoom(code));

        var joined = await rooms.UpdateAsync(code, r => RoomEngine.Join(r, "p2", "two", DateTimeOffset.UtcNow));
        Assert.True(joined.Found);
        Assert.Null(joined.Result);
        Assert.Equal(2, (await rooms.GetAsync(code))!.Players.Count);

        await rooms.UpdateAsync(code, r => RoomEngine.Leave(r, "p2"));
        var last = await rooms.UpdateAsync(code, r => RoomEngine.Leave(r, "host"));
        Assert.True(last.Found);
        Assert.Null(last.Room);
        Assert.Null(await rooms.GetAsync(code));
        Assert.False((await rooms.UpdateAsync(code, r => 0)).Found);
    }

    [Fact]
    public async Task Players_joining_at_the_same_moment_are_all_kept()
    {
        var (rooms, _, _) = Store();
        var code = NewCode();
        await rooms.CreateAsync(NewRoom(code));
        await Task.WhenAll(Enumerable.Range(1, 11).Select(i =>
            rooms.UpdateAsync(code, r => RoomEngine.Join(r, $"p{i}", $"player{i}", DateTimeOffset.UtcNow))));
        Assert.Equal(12, (await rooms.GetAsync(code))!.Players.Count);
    }

    [Fact]
    public async Task Deadlines_come_due_in_time()
    {
        var (rooms, _, _) = Store();
        var (a, b) = (NewCode(), NewCode());
        var now = DateTimeOffset.UtcNow;
        await rooms.SetDeadlineAsync(a, now.AddSeconds(-1));
        await rooms.SetDeadlineAsync(b, now.AddMinutes(5));
        var due = await rooms.DueAsync(now);
        Assert.Contains(a, due);
        Assert.DoesNotContain(b, due);
        await rooms.SetDeadlineAsync(a, null);
        await rooms.SetDeadlineAsync(b, null);
        Assert.DoesNotContain(a, await rooms.DueAsync(now));
    }

    [Fact]
    public async Task Sessions_are_found_by_their_token()
    {
        var (_, sessions, _) = Store();
        var token = Guid.NewGuid().ToString("N");
        Assert.Null(await sessions.GetAsync(token));
        await sessions.SaveAsync(token, new PlayerSession("p1", "ali"));
        Assert.Equal(new PlayerSession("p1", "ali"), await sessions.GetAsync(token));
        Assert.Null(await sessions.GetAsync(token + "x"));
    }

    [Fact]
    public async Task A_nickname_belongs_to_one_player_at_a_time()
    {
        var (_, _, nicknames) = Store();
        var name = NewName();
        Assert.True(await nicknames.TryReserveAsync(name, "p1"));
        Assert.True(await nicknames.TryReserveAsync(name, "p1"));  // renewing is fine
        Assert.False(await nicknames.TryReserveAsync(name, "p2")); // taken
        await nicknames.ReleaseAsync(name, "p2");                  // not theirs to free
        Assert.False(await nicknames.TryReserveAsync(name, "p2"));
        await nicknames.ReleaseAsync(name, "p1");
        Assert.True(await nicknames.TryReserveAsync(name, "p2"));
        await nicknames.ReleaseAsync(name, "p2");
    }

    [Fact]
    public async Task Two_players_racing_for_one_name_cannot_both_get_it()
    {
        var (_, _, nicknames) = Store();
        var name = NewName();
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => nicknames.TryReserveAsync(name, $"p{i}")));
        Assert.Equal(1, results.Count(r => r));
    }
}

public class InMemoryStoreTests : StoreContract
{
    protected override (IRoomStore, ISessionStore, INicknameRegistry) Store()
    {
        var store = new InMemoryGameStore(TimeProvider.System);
        return (store, store, store);
    }

    [Fact]
    public async Task Reservations_and_sessions_expire_after_a_day()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var store = new InMemoryGameStore(clock);
        await store.TryReserveAsync("ali", "p1");
        await ((ISessionStore)store).SaveAsync("tok", new PlayerSession("p1", "ali"));
        clock.Advance(TimeSpan.FromHours(23));
        Assert.False(await store.TryReserveAsync("ali", "p2"));
        clock.Advance(TimeSpan.FromHours(2));
        Assert.True(await store.TryReserveAsync("ali", "p2"));
        Assert.Null(await ((ISessionStore)store).GetAsync("tok"));
    }
}

public class RedisStoreTests : StoreContract
{
    private static readonly Lazy<IConnectionMultiplexer?> Connection = new(() =>
    {
        var url = TestSettings.RedisUrl;
        return url is null ? null : ConnectionMultiplexer.Connect(RedisConnection.Options(url));
    });

    protected override (IRoomStore, ISessionStore, INicknameRegistry) Store()
    {
        Assert.SkipWhen(Connection.Value is null, "REDIS_URL is not set");
        var store = new RedisGameStore(Connection.Value!);
        return (store, store, store);
    }
}
