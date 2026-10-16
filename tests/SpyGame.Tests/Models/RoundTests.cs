using SpyGame.Web.Models.Game;

namespace SpyGame.Tests.Models;

/// <summary>Returns the given numbers in order, so a test knows exactly what is drawn.</summary>
internal sealed class FixedRandom(params int[] values) : IRandomSource
{
    private int _next;

    public int Next(int maxExclusive)
    {
        var value = values[_next++ % values.Length];
        Assert.InRange(value, 0, maxExclusive - 1);
        return value;
    }
}

public class WordBankTests
{
    private static IEnumerable<Word> All => WordBank.Groups.SelectMany(g => g.Words);

    [Fact]
    public void Four_numbered_groups_of_places_in_both_languages()
    {
        Assert.Equal([1, 2, 3, 4], WordBank.Groups.Select(g => g.Number));
        Assert.Equal([27, 25, 25, 25], WordBank.Groups.Select(g => g.Words.Count));
        Assert.All(All, w =>
        {
            Assert.Matches("^[a-z]+(-[a-z]+)*$", w.Id);
            Assert.False(string.IsNullOrWhiteSpace(w.Tr) || string.IsNullOrWhiteSpace(w.En));
        });
    }

    [Fact]
    public void No_place_appears_twice()
    {
        // The spy picks from one list of every chosen place, so ids and names must be unique across groups.
        var count = All.Count();
        Assert.Equal(count, All.Select(w => w.Id).Distinct().Count());
        Assert.Equal(count, All.Select(w => w.Tr).Distinct().Count());
        Assert.Equal(count, All.Select(w => w.En).Distinct().Count());
    }

    [Fact]
    public void A_new_room_starts_with_the_first_two_groups()
    {
        Assert.Equal(52, WordBank.DefaultPlaceIds.Count);
        Assert.Null(new RoomSettings { PlaceIds = [.. WordBank.DefaultPlaceIds] }.Problem());
        Assert.Equal("Kumsal", WordBank.Find("beach")!.Tr);
        Assert.Null(WordBank.Find("nope"));
    }
}

public class RoundRulesTests
{
    private static readonly string[] Five = ["a", "b", "c", "d", "e"];

    [Theory]
    [InlineData(3, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(8, 2)]
    [InlineData(9, 3)]
    [InlineData(12, 3)]
    public void Spies_stay_a_small_group(int players, int max) => Assert.Equal(max, RoundRules.MaxSpiesFor(players));

    [Theory]
    [InlineData(3, 10, 3)] // asked for 3, 10 came
    [InlineData(3, 4, 1)]  // asked for 3, only 4 came: lowered
    [InlineData(2, 7, 2)]
    [InlineData(0, 7, 1)]  // at least one spy
    public void Requested_spy_count_is_lowered_when_needed(int requested, int players, int expected) =>
        Assert.Equal(expected, RoundRules.EffectiveSpyCount(requested, players));

    [Fact]
    public void A_deal_follows_the_drawn_numbers()
    {
        // place #2 of the room = Beach; spy swap at 0 picks index 3; first asker index 1
        var deal = RoundRules.Deal(Five, TestPlaces.Ten, 1, new FixedRandom(2, 3, 1));
        Assert.Equal(["d"], deal.SpyIds);
        Assert.Equal("beach", deal.PlaceId);
        Assert.Equal("Kumsal", deal.Word.Tr);
        Assert.Equal("b", deal.FirstAskerId);
        Assert.True(deal.IsSpy("d"));
        Assert.False(deal.IsSpy("a"));
    }

    [Fact]
    public void Several_spies_are_all_different_players()
    {
        var players = Enumerable.Range(1, 9).Select(i => $"p{i}").ToArray();
        var random = new CryptoRandomSource();
        for (var i = 0; i < 300; i++)
        {
            var deal = RoundRules.Deal(players, TestPlaces.Ten, 3, random);
            Assert.Equal(3, deal.SpyIds.Count);
            Assert.Equal(3, deal.SpyIds.Distinct().Count());
            Assert.All(deal.SpyIds, id => Assert.Contains(id, players));
            Assert.Contains(deal.PlaceId, TestPlaces.Ten);
        }
    }

    [Fact]
    public void Everyone_is_the_spy_about_equally_often()
    {
        var random = new CryptoRandomSource();
        var times = Five.ToDictionary(p => p, _ => 0);
        const int rounds = 10_000;
        for (var i = 0; i < rounds; i++) times[RoundRules.Deal(Five, TestPlaces.Ten, 1, random).SpyIds[0]]++;
        // 2000 expected each; a fair draw stays well inside +-15%.
        Assert.All(times.Values, n => Assert.InRange(n, 1700, 2300));
    }

    [Fact]
    public void The_last_two_first_askers_sit_out_the_draw()
    {
        var random = new CryptoRandomSource();
        for (var i = 0; i < 500; i++)
            Assert.DoesNotContain(RoundRules.PickFirstAsker(Five, ["a", "b"], random), new[] { "a", "b" });

        // Four players: still two sit out, two stay in the draw.
        string[] four = ["a", "b", "c", "d"];
        var drawn = Enumerable.Range(0, 500).Select(_ => RoundRules.PickFirstAsker(four, ["a", "b"], random)).ToHashSet();
        Assert.Equal(["c", "d"], drawn.Order());
    }

    [Fact]
    public void With_three_players_only_the_last_first_asker_sits_out()
    {
        var random = new CryptoRandomSource();
        string[] three = ["a", "b", "c"];
        var drawn = Enumerable.Range(0, 500).Select(_ => RoundRules.PickFirstAsker(three, ["a", "b"], random)).ToHashSet();
        Assert.Equal(["a", "c"], drawn.Order()); // b asked first last round; a and c stay in the draw
    }

    [Fact]
    public void A_first_asker_who_left_does_not_shrink_the_draw()
    {
        var random = new CryptoRandomSource();
        // "x" is gone; only "a" (still here) sits out.
        var drawn = Enumerable.Range(0, 500).Select(_ => RoundRules.PickFirstAsker(Five, ["a", "x"], random)).ToHashSet();
        Assert.Equal(["b", "c", "d", "e"], drawn.Order());
    }

    [Fact]
    public void Bad_setups_are_refused()
    {
        var random = new CryptoRandomSource();
        Assert.Throws<ArgumentException>(() => RoundRules.Deal(["a", "b"], TestPlaces.Ten, 1, random));
        Assert.Throws<ArgumentException>(() => RoundRules.Deal(Enumerable.Range(0, 13).Select(i => $"p{i}").ToArray(), TestPlaces.Ten, 1, random));
        Assert.Throws<ArgumentException>(() => RoundRules.Deal(["a", "a", "b"], TestPlaces.Ten, 1, random));
        Assert.Throws<ArgumentException>(() => RoundRules.Deal(Five, [], 1, random));
        Assert.Throws<ArgumentException>(() => RoundRules.Deal(Five, ["no-such-place"], 1, random));
    }
}

public class VotingAndScoringTests
{
    private static readonly string[] Players = ["a", "b", "c", "d", "e"];

    private static Dictionary<string, string> Votes(params (string Voter, string Suspect)[] votes) =>
        votes.ToDictionary(v => v.Voter, v => v.Suspect);

    [Fact]
    public void The_most_voted_player_is_accused()
    {
        var outcome = Voting.Tally(Players, Votes(("a", "e"), ("b", "e"), ("c", "e"), ("d", "a"), ("e", "a")));
        Assert.Equal("e", outcome.AccusedId);
        Assert.Equal(3, outcome.Counts["e"]);
    }

    [Fact]
    public void A_tie_or_a_single_vote_accuses_no_one()
    {
        Assert.Null(Voting.Tally(Players, Votes(("a", "e"), ("b", "e"), ("c", "d"), ("d", "c"), ("e", "d"))).AccusedId);
        Assert.Null(Voting.Tally(Players, Votes(("a", "e"))).AccusedId);
        Assert.Null(Voting.Tally(Players, Votes()).AccusedId);
    }

    [Fact]
    public void Self_votes_and_strangers_do_not_count()
    {
        var outcome = Voting.Tally(Players, Votes(("a", "a"), ("b", "a"), ("x", "a"), ("c", "zz")));
        Assert.Equal(1, outcome.Counts["a"]);
        Assert.False(outcome.Counts.ContainsKey("zz"));
        Assert.Null(outcome.AccusedId);
    }

    [Fact]
    public void Catching_the_spy_wins_for_the_civilians()
    {
        var votes = Votes(("a", "e"), ("b", "e"), ("c", "e"), ("d", "a"), ("e", "a"));
        var outcome = Scoring.Decide(Voting.Tally(Players, votes), ["e"], caughtSpyGuessedWord: false);
        Assert.Equal(Team.Civilians, outcome.Winner);

        var points = Scoring.Points(Players, ["e"], votes, outcome);
        Assert.Equal(2, points["a"]); // win + right vote
        Assert.Equal(1, points["d"]); // win, wrong vote
        Assert.Equal(0, points["e"]);
    }

    [Fact]
    public void A_caught_spy_who_names_the_place_still_wins()
    {
        var votes = Votes(("a", "e"), ("b", "e"), ("c", "e"), ("d", "a"), ("e", "a"));
        var outcome = Scoring.Decide(Voting.Tally(Players, votes), ["e"], caughtSpyGuessedWord: true);
        Assert.Equal(Team.Spies, outcome.Winner);
        Assert.True(outcome.SpyGuessedWord);

        var points = Scoring.Points(Players, ["e"], votes, outcome);
        Assert.Equal(Scoring.SpyWin + Scoring.SpyGuessBonus, points["e"]);
        Assert.Equal(1, points["a"]); // no win, but the vote was right
    }

    [Fact]
    public void Accusing_the_wrong_person_lets_every_spy_win()
    {
        string[] spies = ["d", "e"];
        var votes = Votes(("a", "b"), ("c", "b"), ("d", "b"), ("e", "b"), ("b", "e"));
        var outcome = Scoring.Decide(Voting.Tally(Players, votes), spies, caughtSpyGuessedWord: false);
        Assert.Equal(Team.Spies, outcome.Winner);
        Assert.False(outcome.AccusedWasSpy);

        var points = Scoring.Points(Players, spies, votes, outcome);
        Assert.Equal(2, points["d"]);
        Assert.Equal(2, points["e"]);
        Assert.Equal(1, points["b"]); // b voted for a spy
        Assert.Equal(0, points["a"]);
    }

    [Fact]
    public void A_guess_only_matters_after_a_spy_is_accused()
    {
        var votes = Votes(("a", "b"), ("c", "b"));
        var outcome = Scoring.Decide(Voting.Tally(Players, votes), ["e"], caughtSpyGuessedWord: true);
        Assert.False(outcome.SpyGuessedWord);
        Assert.Equal(Team.Spies, outcome.Winner);
    }
}
