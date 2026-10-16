namespace SpyGame.Web.Models.Game;

/// <summary>A chat line. The nickname is kept with it, so it still reads right after its writer has left.</summary>
public sealed class ChatMessage
{
    public int Id { get; set; }
    public string PlayerId { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Hidden { get; set; } // removed by the host: the text is gone, a note stays
    public DateTimeOffset At { get; set; }
}

/// <summary>
/// The room chat, for groups playing apart. Only the last <see cref="MaxMessages"/> lines are
/// kept, inside the room itself, so they disappear with it. The host can hide a line.
/// </summary>
public static class RoomChat
{
    public const int MaxMessages = 50;
    public const int BurstMessages = 5;  // at most this many lines...
    public const int BurstSeconds = 10;  // ...per player in this many seconds

    public static string? Send(Room room, string playerId, string? text, DateTimeOffset now)
    {
        if (!room.Settings.Remote) return "not_remote";
        if (room.Find(playerId) is not { Left: false } player) return "not_in_room";
        if (PlayerText.Clean(text) is not { } line) return "invalid_text";
        var since = now.AddSeconds(-BurstSeconds);
        if (room.Chat.Count(m => m.PlayerId == playerId && m.At > since) >= BurstMessages) return "too_fast";

        room.Chat.Add(new ChatMessage { Id = ++room.ChatSeq, PlayerId = playerId, Nickname = player.Nickname, Text = line, At = now });
        if (room.Chat.Count > MaxMessages) room.Chat.RemoveRange(0, room.Chat.Count - MaxMessages);
        return null;
    }

    public static string? Hide(Room room, string byPlayerId, int messageId)
    {
        if (room.HostId != byPlayerId) return "not_host";
        if (room.Chat.FirstOrDefault(m => m.Id == messageId) is not { } message) return "unknown_message";
        message.Hidden = true;
        message.Text = "";
        return null;
    }
}
