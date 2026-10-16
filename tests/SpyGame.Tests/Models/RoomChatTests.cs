using SpyGame.Web.Models.Game;

namespace SpyGame.Tests.Models;

public class RoomChatTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    private static Room NewRoom(bool remote = true)
    {
        var room = RoomEngine.Create("ABC234", "p1", "host", new RoomSettings { PlaceIds = [.. TestPlaces.Ten], Remote = remote }, T0);
        for (var i = 2; i <= 4; i++) Assert.Null(RoomEngine.Join(room, $"p{i}", $"player{i}", T0));
        return room;
    }

    [Fact]
    public void Everyone_in_the_room_sees_the_chat_in_order()
    {
        var room = NewRoom();
        Assert.Null(RoomChat.Send(room, "p2", "  selam  herkes ", T0));
        Assert.Null(RoomChat.Send(room, "p3", "selam!", T0.AddSeconds(1)));
        var chat = RoomView.For(room, "p4", T0).Chat!;
        Assert.Equal([(1, "player2", "selam herkes"), (2, "player3", "selam!")], chat.Select(m => (m.Id, m.Nickname, m.Text!)));
    }

    [Fact]
    public void Only_rooms_playing_apart_have_a_chat()
    {
        var room = NewRoom(remote: false);
        Assert.Equal("not_remote", RoomChat.Send(room, "p2", "selam", T0));
        Assert.Null(RoomView.For(room, "p2", T0).Chat);
    }

    [Fact]
    public void Bad_lines_and_strangers_are_refused()
    {
        var room = NewRoom();
        Assert.Equal("invalid_text", RoomChat.Send(room, "p2", " \n ", T0));
        Assert.Equal("invalid_text", RoomChat.Send(room, "p2", new string('x', PlayerText.MaxLength + 1), T0));
        Assert.Equal("not_in_room", RoomChat.Send(room, "stranger", "selam", T0));
        Assert.Empty(room.Chat);
    }

    [Fact]
    public void A_player_cannot_flood_the_chat()
    {
        var room = NewRoom();
        for (var i = 0; i < RoomChat.BurstMessages; i++) Assert.Null(RoomChat.Send(room, "p2", $"m{i}", T0.AddSeconds(i)));
        Assert.Equal("too_fast", RoomChat.Send(room, "p2", "bir daha", T0.AddSeconds(5)));
        Assert.Null(RoomChat.Send(room, "p3", "ben yazabilirim", T0.AddSeconds(5))); // the limit is per player
        Assert.Null(RoomChat.Send(room, "p2", "şimdi oldu", T0.AddSeconds(RoomChat.BurstSeconds + 1)));
    }

    [Fact]
    public void Only_the_last_lines_are_kept_and_ids_keep_counting()
    {
        var room = NewRoom();
        var now = T0;
        for (var i = 1; i <= RoomChat.MaxMessages + 10; i++)
        {
            now = now.AddSeconds(3);
            Assert.Null(RoomChat.Send(room, $"p{i % 4 + 1}", $"line {i}", now));
        }
        Assert.Equal(RoomChat.MaxMessages, room.Chat.Count);
        Assert.Equal("line 11", room.Chat[0].Text);
        Assert.Equal(RoomChat.MaxMessages + 10, room.Chat[^1].Id);
    }

    [Fact]
    public void The_host_hides_a_line_and_its_text_is_gone()
    {
        var room = NewRoom();
        Assert.Null(RoomChat.Send(room, "p2", "kaba bir söz", T0));
        Assert.Equal("not_host", RoomChat.Hide(room, "p3", 1));
        Assert.Equal("unknown_message", RoomChat.Hide(room, "p1", 99));
        Assert.Null(RoomChat.Hide(room, "p1", 1));

        Assert.Equal("", room.Chat[0].Text); // not kept in the stored room either
        var line = RoomView.For(room, "p3", T0).Chat!.Single();
        Assert.True(line.Hidden);
        Assert.Null(line.Text);
    }

    [Fact]
    public void A_line_keeps_its_writer_name_after_they_leave()
    {
        var room = NewRoom();
        Assert.Null(RoomChat.Send(room, "p2", "görüşürüz", T0));
        Assert.Null(RoomEngine.Leave(room, "p2"));
        Assert.Equal("player2", RoomView.For(room, "p1", T0).Chat!.Single().Nickname);
    }
}

public class KickTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    private static Room NewRoom()
    {
        var room = RoomEngine.Create("ABC234", "p1", "host", new RoomSettings { PlaceIds = [.. TestPlaces.Ten] }, T0);
        for (var i = 2; i <= 4; i++) Assert.Null(RoomEngine.Join(room, $"p{i}", $"player{i}", T0));
        return room;
    }

    [Fact]
    public void Only_the_host_removes_others_and_the_removed_cannot_come_back()
    {
        var room = NewRoom();
        Assert.Equal("not_host", RoomEngine.Kick(room, "p2", "p3"));
        Assert.Equal("cannot_kick_self", RoomEngine.Kick(room, "p1", "p1"));
        Assert.Equal("unknown_player", RoomEngine.Kick(room, "p1", "nobody"));

        Assert.Null(RoomEngine.Kick(room, "p1", "p3"));
        Assert.Null(room.Find("p3"));
        Assert.Equal("kicked", RoomEngine.Join(room, "p3", "player3", T0.AddMinutes(1)));
        Assert.Equal("unknown_player", RoomEngine.Kick(room, "p1", "p3")); // already out
    }

    [Fact]
    public void A_player_removed_mid_round_is_out_but_the_round_goes_on()
    {
        var room = NewRoom();
        Assert.Null(RoomEngine.StartRound(room, "p1", T0, new FixedRandom(0, 3, 0))); // spy p4
        Assert.Null(RoomEngine.Kick(room, "p1", "p2"));
        Assert.True(room.Find("p2")!.Left); // still counted in this round, like someone who left
        Assert.Equal("kicked", RoomEngine.Join(room, "p2", "player2", T0.AddSeconds(5)));
        RoomEngine.Tick(room, T0.AddHours(1));
        Assert.Null(room.Find("p2")); // gone once the round is over
    }
}
