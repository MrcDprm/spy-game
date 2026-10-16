using SpyGame.Web.Models.Game;

namespace SpyGame.Tests.Models;

/// <summary>Place lists for tests: the smallest allowed room uses the first ten places of Dossier 1.</summary>
internal static class TestPlaces
{
    /// <summary>Plane, Bank, Beach, Theatre, Casino, Cathedral, Circus Tent, Office Party, Crusader Army, Day Spa.</summary>
    public static readonly IReadOnlyList<string> Ten = WordBank.Groups[0].Words.Take(WordBank.MinPlaces).Select(w => w.Id).ToList();

    public static List<string> Of(int count) => WordBank.Groups.SelectMany(g => g.Words).Take(count).Select(w => w.Id).ToList();
}
