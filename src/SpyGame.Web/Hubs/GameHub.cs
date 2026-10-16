using Microsoft.AspNetCore.SignalR;
using SpyGame.Web.Models.Game;
using SpyGame.Web.Services;

namespace SpyGame.Web.Hubs;

/// <summary>
/// The live channel of a room page. Who the player is comes from their session cookie when the
/// connection opens, never from what the browser sends later, so nobody can act for someone
/// else. Every method only passes the request on to <see cref="RoomService"/> and returns its
/// answer: null when done, otherwise an error code. The new state reaches everyone through
/// <see cref="HubNotifier"/>, each player getting only their own view.
/// </summary>
public sealed class GameHub(RoomService rooms, PlayerSessionService sessions, ConnectionTracker connections) : Hub
{
    public const string Path = "/hubs/game";

    private string PlayerId => (string)Context.Items["player"]!;
    private string Code => (string)Context.Items["room"]!;

    public static string PlayerGroup(string code, string playerId) => $"{code}:{playerId}";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext()!;
        var code = RoomCode.Normalize(http.Request.Query["room"]);
        var session = await sessions.FindAsync(http);
        if (session is null || !RoomCode.IsValid(code) || await rooms.ViewAsync(code, session.PlayerId) is null)
        {
            Context.Abort(); // no session, or not a player of this room
            return;
        }

        Context.Items["player"] = session.PlayerId;
        Context.Items["room"] = code;
        await Groups.AddToGroupAsync(Context.ConnectionId, PlayerGroup(code, session.PlayerId));

        if (connections.Opened(code, session.PlayerId)) await rooms.SetConnectedAsync(session.PlayerId, code, true);
        // Either way this tab needs the current state.
        await Clients.Caller.SendAsync("room", await rooms.ViewAsync(code, session.PlayerId));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.ContainsKey("player") && connections.Closed(Code, PlayerId))
            await rooms.SetConnectedAsync(PlayerId, Code, false);
    }

    public Task<string?> StartRound() => rooms.StartRoundAsync(PlayerId, Code);

    public Task<string?> ReadyToVote() => rooms.ReadyToVoteAsync(PlayerId, Code);

    public Task<string?> Vote(string suspectId) =>
        suspectId is { Length: > 0 and <= 40 } ? rooms.VoteAsync(PlayerId, Code, suspectId) : Task.FromResult<string?>("unknown_player");

    public Task<string?> Guess(string placeId) => rooms.GuessAsync(PlayerId, Code, placeId);

    public Task<string?> Leave() => rooms.LeaveAsync(PlayerId, Code);
}
