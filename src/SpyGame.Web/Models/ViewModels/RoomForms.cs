using SpyGame.Web.Models.Game;

namespace SpyGame.Web.Models.ViewModels;

/// <summary>The "new room" form. The person who creates the room chooses everything here.</summary>
public sealed class CreateRoomForm
{
    public string? Nickname { get; set; }
    public List<string> PlaceIds { get; set; } = [];
    public int RoundMinutes { get; set; } = RoomSettings.DefaultRoundSeconds / 60;
    public int SpyCount { get; set; } = 1;
    public bool Remote { get; set; }

    public RoomSettings ToSettings() => new()
    {
        PlaceIds = PlaceIds.Distinct().ToList(),
        RoundSeconds = RoundMinutes * 60,
        SpyCount = SpyCount,
        Remote = Remote,
    };
}

public sealed class JoinRoomForm
{
    public string? Nickname { get; set; }
    public string? Code { get; set; }
}

public sealed class HomeViewModel
{
    public CreateRoomForm Create { get; set; } = new();
    public JoinRoomForm Join { get; set; } = new();
    public string? Error { get; set; }
}

public sealed class PlayViewModel
{
    public required string Code { get; init; }

    /// <summary>The link friends open to join (also inside the QR code).</summary>
    public required string InviteUrl { get; init; }

    /// <summary>The QR code as a PNG data URL (an inline SVG would need inline styles, which the policy forbids).</summary>
    public required string QrDataUrl { get; init; }
}
