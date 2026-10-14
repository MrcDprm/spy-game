using System.Text.RegularExpressions;

namespace SpyGame.Web.Models.Game;

/// <summary>
/// Nickname rules: lowercase a-z and digits only, 3-20 characters, starting with a letter.
/// No Turkish letters and no capitals, so two names cannot look alike ("ayse" / "ayşe", "i" / "ı").
/// </summary>
public static partial class Nickname
{
    public const int MinLength = 3;
    public const int MaxLength = 20;

    // Names that could pass for the site or its staff.
    private static readonly HashSet<string> Reserved =
    [
        "admin", "administrator", "yonetici", "moderator", "mod", "system", "sistem", "root",
        "support", "destek", "casus", "spy", "spygame", "guest", "misafir", "null", "undefined",
    ];

    [GeneratedRegex("^[a-z0-9]+$")]
    private static partial Regex AllowedCharacters();

    /// <summary>Leading and trailing spaces (from phone keyboards) are not part of the name.</summary>
    public static string Clean(string? value) => (value ?? string.Empty).Trim();

    /// <summary>Null when the name is allowed, otherwise an error code the page translates.</summary>
    public static string? Problem(string? value)
    {
        var name = Clean(value);
        if (name.Length == 0) return "nickname_required";
        if (!AllowedCharacters().IsMatch(name)) return "nickname_invalid_characters";
        if (name.Length < MinLength) return "nickname_too_short";
        if (name.Length > MaxLength) return "nickname_too_long";
        if (name[0] is < 'a' or > 'z') return "nickname_must_start_with_letter";
        if (Reserved.Contains(name)) return "nickname_reserved";
        return null;
    }

    public static bool IsValid(string? value) => Problem(value) is null;
}
