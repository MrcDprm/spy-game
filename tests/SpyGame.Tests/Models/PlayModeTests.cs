using SpyGame.Web.Models.Game;

namespace SpyGame.Tests.Models;

public class PlayModeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    private static Room NewRoom(bool remote) =>
        RoomEngine.Create("ABC234", "p1", "host", new RoomSettings { PlaceIds = [.. TestPlaces.Ten], Remote = remote }, T0);

    [Fact]
    public void Where_the_group_plays_is_part_of_the_settings()
    {
        Assert.True(RoomView.For(NewRoom(remote: true), "p1", T0).Settings.Remote);
        Assert.False(RoomView.For(NewRoom(remote: false), "p1", T0).Settings.Remote);
        Assert.False(new RoomSettings().Remote); // together unless chosen
    }
}
