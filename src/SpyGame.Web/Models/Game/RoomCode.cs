namespace SpyGame.Web.Models.Game;

/// <summary>
/// Room codes: 6 characters people can read aloud. Letters and digits that look alike
/// (I/1, O/0, L) are left out, which leaves 31 symbols: about 887 million codes.
/// </summary>
public static class RoomCode
{
    public const int Length = 6;
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate(IRandomSource random) =>
        string.Create(Length, random, static (chars, rng) =>
        {
            for (var i = 0; i < chars.Length; i++) chars[i] = Alphabet[rng.Next(Alphabet.Length)];
        });

    /// <summary>"ab-c 2de" becomes "ABC2DE": typed codes may have spaces, dashes or small letters.</summary>
    public static string Normalize(string? input) =>
        new((input ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-').Select(char.ToUpperInvariant).ToArray());

    public static bool IsValid(string? code) =>
        code is { Length: Length } && code.All(c => Alphabet.Contains(c));
}
