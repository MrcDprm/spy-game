using SpyGame.Tests.Models;
using SpyGame.Web.Models.Game;
using SpyGame.Web.Services;
using SpyGame.Web.Services.Storage;

namespace SpyGame.Tests.Services;

/// <summary>Registered names for tests: a fixed list of name → account id.</summary>
internal sealed class FixedRegisteredNames(Dictionary<string, string>? names = null) : IRegisteredNames
{
    public Task<string?> AccountIdForAsync(string nickname) => Task.FromResult(names?.GetValueOrDefault(nickname));
}

internal sealed class RecordingNotifier : IRoomNotifier
{
    public List<Room> Sent { get; } = [];

    public Task RoomChangedAsync(Room room)
    {
        Sent.Add(room);
        return Task.CompletedTask;
    }
}

public class RoomServiceTests
{
    private readonly ManualClock _clock = new(new DateTimeOffset(2026, 10, 15, 18, 0, 0, TimeSpan.Zero));
    private readonly InMemoryGameStore _store;
    private readonly RecordingNotifier _notifier = new();
    private readonly RoomService _service;

    public RoomServiceTests()
    {
        _store = new InMemoryGameStore(_clock);
        _service = new RoomService(_store, _store, new FixedRegisteredNames(new() { ["mira"] = "acc-1" }), new CryptoRandomSource(), _clock, _notifier);
    }

    private static readonly RoomSettings Settings = new() { CategoryIds = ["food"], RoundSeconds = 120, SpyCount = 1 };

    private async Task<PlayerSession> Player(string id, string nickname)
    {
        var (error, session) = await _service.ChooseNicknameAsync(new PlayerSession(id, null), nickname);
        Assert.Null(error);
        return session;
    }

    private async Task<string> RoomWithPlayers(int count)
    {
        var host = await Player("p1", "host");
        var (error, code) = await _service.CreateAsync(host, Settings);
        Assert.Null(error);
        for (var i = 2; i <= count; i++) Assert.Null(await _service.JoinAsync(await Player($"p{i}", $"player{i}"), code!));
        return code!;
    }

    [Fact]
    public async Task Nicknames_are_checked_reserved_and_swapped()
    {
        var (error, _) = await _service.ChooseNicknameAsync(new PlayerSession("p1", null), "Ali");
        Assert.Equal("nickname_invalid_characters", error);

        var ali = await Player("p1", "ali");
        Assert.Equal("ali", ali.Nickname);
        var (taken, _) = await _service.ChooseNicknameAsync(new PlayerSession("p2", null), "ali");
        Assert.Equal("nickname_in_use", taken);

        // Changing the name frees the old one for others.
        var (changed, renamed) = await _service.ChooseNicknameAsync(ali, "  veli ");
        Assert.Null(changed);
        Assert.Equal("veli", renamed.Nickname);
        Assert.Equal("ali", (await Player("p2", "ali")).Nickname);
    }

    [Fact]
    public async Task An_account_name_is_only_for_its_account()
    {
        var (guest, _) = await _service.ChooseNicknameAsync(new PlayerSession("p1", null), "mira");
        Assert.Equal("nickname_registered", guest);
        var (owner, session) = await _service.ChooseNicknameAsync(new PlayerSession("p2", null, AccountId: "acc-1"), "mira");
        Assert.Null(owner);
        Assert.Equal("mira", session.Nickname);
        // The owner can use it from a second browser too (no guest-style reservation in the way).
        var (again, _) = await _service.ChooseNicknameAsync(new PlayerSession("p3", null, AccountId: "acc-1"), "mira");
        Assert.Null(again);
    }

    [Fact]
    public async Task A_room_needs_a_nickname_and_good_settings()
    {
        Assert.Equal("nickname_required", (await _service.CreateAsync(new PlayerSession("p1", null), Settings)).Error);
        var host = await Player("p1", "host");
        Assert.Equal("invalid_categories", (await _service.CreateAsync(host, new RoomSettings { CategoryIds = ["x"] })).Error);
        var (error, code) = await _service.CreateAsync(host, Settings);
        Assert.Null(error);
        Assert.True(RoomCode.IsValid(code));
    }

    [Fact]
    public async Task Joining_with_a_typed_code_and_with_a_wrong_one()
    {
        var code = await RoomWithPlayers(1);
        var guest = await Player("p2", "konuk");
        Assert.Null(await _service.JoinAsync(guest, $" {code.ToLowerInvariant()[..3]}-{code[3..]} "));
        Assert.Equal("room_not_found", await _service.JoinAsync(guest, "ZZZZZZ"));
        Assert.Equal("room_not_found", await _service.JoinAsync(guest, "nope"));
    }

    [Fact]
    public async Task Every_change_is_announced_and_deadlines_are_kept()
    {
        var code = await RoomWithPlayers(3);
        _notifier.Sent.Clear();
        Assert.Null(await _service.StartRoundAsync("p1", code));
        Assert.Single(_notifier.Sent);
        Assert.Empty(await _store.DueAsync(_clock.Now));

        _clock.Advance(TimeSpan.FromSeconds(120));
        Assert.Equal([code], await _store.DueAsync(_clock.Now));
        await _service.TickAsync(code);
        Assert.Equal(Phase.Voting, (await _store.GetAsync(code))!.Phase);
        Assert.Equal(2, _notifier.Sent.Count);
        Assert.Empty(await _store.DueAsync(_clock.Now)); // the next deadline is a minute away
    }

    [Fact]
    public async Task A_refused_action_changes_nothing_and_tells_no_one()
    {
        var code = await RoomWithPlayers(3);
        _notifier.Sent.Clear();
        Assert.Equal("not_host", await _service.StartRoundAsync("p2", code));
        Assert.Equal("wrong_phase", await _service.VoteAsync("p1", code, "p2"));
        Assert.Equal("room_not_found", await _service.VoteAsync("p1", "ZZZZZZ", "p2"));
        Assert.Empty(_notifier.Sent);
    }

    [Fact]
    public async Task Only_players_in_the_room_get_a_view()
    {
        var code = await RoomWithPlayers(3);
        Assert.NotNull(await _service.ViewAsync(code, "p2"));
        Assert.Null(await _service.ViewAsync(code, "stranger"));
        Assert.Null(await _service.ViewAsync("ZZZZZZ", "p2"));
    }

    [Fact]
    public async Task When_everyone_leaves_the_room_is_gone()
    {
        var code = await RoomWithPlayers(2);
        Assert.Null(await _service.LeaveAsync("p2", code));
        Assert.Null(await _service.LeaveAsync("p1", code));
        Assert.Null(await _store.GetAsync(code));
        Assert.Equal("room_not_found", await _service.JoinAsync(await Player("p3", "late"), code));
    }

    [Fact]
    public async Task A_tick_for_a_room_that_no_longer_exists_clears_its_deadline()
    {
        await _store.SetDeadlineAsync("GONE22", _clock.Now.AddSeconds(-1));
        await _service.TickAsync("GONE22");
        Assert.Empty(await _store.DueAsync(_clock.Now));
    }
}

public class RedisConnectionTests
{
    [Fact]
    public void Upstash_style_urls_are_understood()
    {
        var options = RedisConnection.Options("rediss://default:s3cr%40t@eu1-fine-cat-123.upstash.io:6379");
        Assert.True(options.Ssl);
        Assert.Equal("default", options.User);
        Assert.Equal("s3cr@t", options.Password);
        Assert.Equal("eu1-fine-cat-123.upstash.io:6379", options.EndPoints.Single().ToString()?.Replace("Unspecified/", ""));
    }

    [Fact]
    public void Plain_redis_without_a_password_works_too()
    {
        var options = RedisConnection.Options("redis://localhost:6380");
        Assert.False(options.Ssl);
        Assert.Null(options.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("localhost:6379")]
    [InlineData("http://example.com")]
    public void Anything_else_is_refused(string url) => Assert.Throws<ArgumentException>(() => RedisConnection.Options(url));
}
