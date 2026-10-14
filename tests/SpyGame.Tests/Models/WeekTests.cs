using SpyGame.Web.Models.Game;

namespace SpyGame.Tests.Models;

public class WeekTests
{
    [Fact]
    public void A_week_ends_on_Sunday_at_midnight_in_Istanbul()
    {
        // Sunday 18 October 2026, 23:59 in Istanbul is 20:59 UTC.
        Assert.Equal("2026-W42", Week.Of(new DateTimeOffset(2026, 10, 18, 20, 59, 0, TimeSpan.Zero)));
        // One minute later it is Monday in Istanbul (still Sunday in London).
        Assert.Equal("2026-W43", Week.Of(new DateTimeOffset(2026, 10, 18, 21, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Weeks_across_the_new_year_follow_ISO_numbering()
    {
        Assert.Equal("2026-W53", Week.Of(new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero))); // 2026 has 53 ISO weeks
        Assert.Equal("2026-W53", Week.Previous("2027-W01"));
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.FromHours(3)), Week.StartOf("2026-W42"));
    }

    [Theory]
    [InlineData("2026-W42", true)]
    [InlineData("2026-W53", true)]
    [InlineData("2027-W53", false)] // 2027 has only 52
    [InlineData("2026-W00", false)]
    [InlineData("2026-42", false)]
    [InlineData("'; drop table", false)]
    [InlineData(null, false)]
    public void Only_real_weeks_are_valid(string? week, bool valid) => Assert.Equal(valid, Week.IsValid(week));
}
