using SpyGame.Web.Models.Game;

namespace SpyGame.Tests.Models;

public class NicknameTests
{
    [Theory]
    [InlineData("ali")]
    [InlineData("ayse42")]
    [InlineData("a12")]
    [InlineData("abcdefghijklmnopqrst")] // 20
    [InlineData("  mehmet  ")] // spaces from a phone keyboard are trimmed
    public void Allowed_names(string name) => Assert.Null(Nickname.Problem(name));

    [Theory]
    [InlineData(null, "nickname_required")]
    [InlineData("", "nickname_required")]
    [InlineData("   ", "nickname_required")]
    [InlineData("ab", "nickname_too_short")]
    [InlineData("abcdefghijklmnopqrstu", "nickname_too_long")] // 21
    [InlineData("1ali", "nickname_must_start_with_letter")]
    [InlineData("007bond", "nickname_must_start_with_letter")]
    [InlineData("Ali", "nickname_invalid_characters")] // capitals are not allowed
    [InlineData("ayşe", "nickname_invalid_characters")] // no Turkish letters
    [InlineData("ılgaz", "nickname_invalid_characters")]
    [InlineData("ali_veli", "nickname_invalid_characters")]
    [InlineData("ali veli", "nickname_invalid_characters")]
    [InlineData("_ali", "nickname_invalid_characters")]
    [InlineData("ali<script>", "nickname_invalid_characters")]
    [InlineData("admin", "nickname_reserved")]
    [InlineData("casus", "nickname_reserved")]
    public void Refused_names(string? name, string problem) => Assert.Equal(problem, Nickname.Problem(name));
}

public class RoomCodeTests
{
    [Fact]
    public void Generated_codes_use_only_readable_characters()
    {
        var random = new CryptoRandomSource();
        for (var i = 0; i < 500; i++)
        {
            var code = RoomCode.Generate(random);
            Assert.True(RoomCode.IsValid(code), code);
            Assert.DoesNotContain(code, c => "IL0O1".Contains(c));
        }
    }

    [Fact]
    public void Codes_are_not_repeated_in_practice()
    {
        var random = new CryptoRandomSource();
        // Birthday paradox: among 5,000 codes from 887 million, a single repeat happens about 1.4%
        // of the time (creating a room then just draws again). A broken generator repeats far more.
        var codes = Enumerable.Range(0, 5000).Select(_ => RoomCode.Generate(random)).ToHashSet();
        Assert.True(codes.Count >= 4997, $"{5000 - codes.Count} repeats");
    }

    [Theory]
    [InlineData("abc2de", "ABC2DE")]
    [InlineData(" ab-c 2de ", "ABC2DE")]
    [InlineData(null, "")]
    public void Typed_codes_are_normalized(string? typed, string expected) => Assert.Equal(expected, RoomCode.Normalize(typed));

    [Theory]
    [InlineData("ABC2DE", true)]
    [InlineData("ABC2D", false)]
    [InlineData("ABC2DEF", false)]
    [InlineData("ABC1DE", false)] // 1 is left out
    [InlineData("abc2de", false)] // normalize first
    [InlineData(null, false)]
    public void Validity(string? code, bool valid) => Assert.Equal(valid, RoomCode.IsValid(code));
}
