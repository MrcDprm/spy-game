using Microsoft.AspNetCore.SignalR;
using SpyGame.Web.Models.Game;
using SpyGame.Web.Services;

namespace SpyGame.Web.Hubs;

/// <summary>
/// After every change, each player in the room gets their own view on their own channel.
/// There is no message to the whole room: what a civilian sees and what a spy sees differ.
/// </summary>
public sealed class HubNotifier(IHubContext<GameHub> hub, TimeProvider clock) : IRoomNotifier
{
    public Task RoomChangedAsync(Room room)
    {
        var now = clock.GetUtcNow();
        return Task.WhenAll(room.Players
            .Where(p => !p.Left)
            .Select(p => hub.Clients.Group(GameHub.PlayerGroup(room.Code, p.Id)).SendAsync("room", RoomView.For(room, p.Id, now))));
    }
}
