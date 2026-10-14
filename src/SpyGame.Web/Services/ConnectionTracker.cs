namespace SpyGame.Web.Services;

/// <summary>
/// Counts how many open connections (tabs, phones) each player has in a room, so closing one
/// tab does not mark someone as gone while another is still open. The site runs as a single
/// instance; with several, this would move to Redis too.
/// </summary>
public sealed class ConnectionTracker
{
    private readonly Lock _lock = new();
    private readonly Dictionary<(string Room, string Player), int> _open = [];

    /// <summary>Returns true for the player's first connection in the room.</summary>
    public bool Opened(string room, string player)
    {
        lock (_lock)
        {
            var count = _open.GetValueOrDefault((room, player)) + 1;
            _open[(room, player)] = count;
            return count == 1;
        }
    }

    /// <summary>Returns true when the player's last connection in the room has closed.</summary>
    public bool Closed(string room, string player)
    {
        lock (_lock)
        {
            var count = _open.GetValueOrDefault((room, player)) - 1;
            if (count > 0)
            {
                _open[(room, player)] = count;
                return false;
            }
            _open.Remove((room, player));
            return true;
        }
    }
}
