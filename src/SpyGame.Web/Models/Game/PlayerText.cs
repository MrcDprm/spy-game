using System.Text;

namespace SpyGame.Web.Models.Game;

/// <summary>What players type (questions, answers, chat): one line, no control characters, a length limit.</summary>
public static class PlayerText
{
    public const int MaxLength = 200;

    /// <summary>The cleaned text, or null when nothing is left or it is too long.</summary>
    public static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var line = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.Normalize(NormalizationForm.FormC))
        {
            // Line breaks and tabs become a space; other control and formatting characters
            // (including invisible direction marks that could make a message read backwards) are dropped.
            if (char.IsWhiteSpace(c)) { space = line.Length > 0; continue; }
            if (char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format) continue;
            if (space) { line.Append(' '); space = false; }
            line.Append(c);
        }
        return line.Length is > 0 and <= MaxLength ? line.ToString() : null;
    }
}
