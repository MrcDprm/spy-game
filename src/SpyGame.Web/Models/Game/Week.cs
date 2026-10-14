using System.Globalization;
using System.Text.RegularExpressions;

namespace SpyGame.Web.Models.Game;

/// <summary>
/// Leaderboard weeks: ISO weeks (Monday to Sunday) in Istanbul time, written "2026-W42".
/// A round that ends on Sunday at 23:59 in Istanbul still counts for that week.
/// </summary>
public static partial class Week
{
    private static readonly TimeSpan Istanbul = TimeSpan.FromHours(3); // no daylight saving since 2016

    public static string Of(DateTimeOffset moment)
    {
        var local = moment.ToOffset(Istanbul).DateTime;
        return $"{ISOWeek.GetYear(local)}-W{ISOWeek.GetWeekOfYear(local):00}";
    }

    public static string Previous(string week) => Of(StartOf(week).AddDays(-7));

    /// <summary>Monday 00:00 of the week, in Istanbul time.</summary>
    public static DateTimeOffset StartOf(string week)
    {
        var match = Format().Match(week);
        if (!match.Success) throw new ArgumentException($"Not a week: {week}", nameof(week));
        var monday = ISOWeek.ToDateTime(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), DayOfWeek.Monday);
        return new DateTimeOffset(monday, Istanbul);
    }

    public static bool IsValid(string? week) =>
        week is not null && Format().Match(week) is { Success: true } m
        && int.Parse(m.Groups[2].Value) is >= 1 and <= 53
        && int.Parse(m.Groups[2].Value) <= ISOWeek.GetWeeksInYear(int.Parse(m.Groups[1].Value));

    [GeneratedRegex(@"^(\d{4})-W(\d{2})$")]
    private static partial Regex Format();
}
