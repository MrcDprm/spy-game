using System.Text.Json;
using SpyGame.Web.Models.Game;

namespace SpyGame.Tests.Models;

public class RoomEngineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 15, 18, 0, 0, TimeSpan.Zero);

    private static Room NewRoom(int players = 4, int spies = 1, int seconds = 300)
    {
        var settings = new RoomSettings { PlaceIds = [.. TestPlaces.Ten], RoundSeconds = seconds, SpyCount = spies };
        var room = RoomEngine.Create("ABC234", "p1", "host", settings, T0);
        for (var i = 2; i <= players; i++) Assert.Null(RoomEngine.Join(room, $"p{i}", $"player{i}", T0.AddSeconds(i)));
        return room;
    }

    /// <summary>Place #wordIndex of the room (4 = Casino, 9 = Day Spa); the spy is player (spySlot + 1); p1 asks first.</summary>
    private static Room Started(int wordIndex = 4, int spySlot = 3, int players = 4)
    {
        var room = NewRoom(players);
        Assert.Null(RoomEngine.StartRound(room, "p1", T0.AddMinutes(1), new FixedRandom(wordIndex, spySlot, 0)));
        return room;
    }

    [Fact]
    public void Settings_are_checked()
    {
        Assert.Null(new RoomSettings { PlaceIds = [.. TestPlaces.Ten] }.Problem());
        Assert.Null(new RoomSettings { PlaceIds = TestPlaces.Of(102) }.Problem()); // every place of every dossier
        Assert.Equal("invalid_places", new RoomSettings { PlaceIds = [] }.Problem());
        Assert.Equal("invalid_places", new RoomSettings { PlaceIds = TestPlaces.Of(9) }.Problem()); // one short
        Assert.Equal("invalid_places", new RoomSettings { PlaceIds = [.. TestPlaces.Of(9), "nope"] }.Problem());
        Assert.Equal("invalid_places", new RoomSettings { PlaceIds = [.. TestPlaces.Of(9), "plane"] }.Problem()); // twice
        Assert.Equal("invalid_round_length", new RoomSettings { PlaceIds = [.. TestPlaces.Ten], RoundSeconds = 30 }.Problem());
        Assert.Equal("invalid_round_length", new RoomSettings { PlaceIds = [.. TestPlaces.Ten], RoundSeconds = 901 }.Problem());
        Assert.Equal("invalid_spy_count", new RoomSettings { PlaceIds = [.. TestPlaces.Ten], SpyCount = 4 }.Problem());
    }

    [Fact]
    public void Joining_rules()
    {
        var room = NewRoom(players: 2);
        Assert.Equal("nickname_taken", RoomEngine.Join(room, "p9", "player2", T0));
        Assert.Null(RoomEngine.Join(room, "p2", "player2", T0)); // rejoining is fine
        for (var i = 3; i <= 12; i++) Assert.Null(RoomEngine.Join(room, $"p{i}", $"player{i}", T0));
        Assert.Equal("room_full", RoomEngine.Join(room, "p13", "player13", T0));
    }

    [Fact]
    public void Only_the_host_starts_and_only_with_three_present()
    {
        var room = NewRoom(players: 3);
        var random = new CryptoRandomSource();
        Assert.Equal("not_host", RoomEngine.StartRound(room, "p2", T0, random));
        RoomEngine.SetConnected(room, "p3", false);
        Assert.Equal("not_enough_players", RoomEngine.StartRound(room, "p1", T0, random));
        RoomEngine.SetConnected(room, "p3", true);
        Assert.Null(RoomEngine.StartRound(room, "p1", T0, random));
        Assert.Equal(Phase.Asking, room.Phase);
        Assert.Equal("wrong_phase", RoomEngine.StartRound(room, "p1", T0, random));
    }

    [Fact]
    public void The_spy_count_chosen_at_creation_is_lowered_for_a_small_table()
    {
        var room = NewRoom(players: 4, spies: 3);
        Assert.Null(RoomEngine.StartRound(room, "p1", T0, new CryptoRandomSource()));
        Assert.Single(room.Round!.Deal.SpyIds);
    }

    [Fact]
    public void A_full_round_where_the_spy_is_caught()
    {
        var room = Started(spySlot: 3); // p4 is the spy
        Assert.Equal(T0.AddMinutes(1).AddSeconds(300), RoomEngine.NextDeadline(room));

        // More than half of the 4 must be ready: 2 is not enough, 3 is.
        Assert.Null(RoomEngine.ReadyToVote(room, "p1", T0.AddMinutes(2)));
        Assert.Null(RoomEngine.ReadyToVote(room, "p2", T0.AddMinutes(2)));
        Assert.Equal(Phase.Asking, room.Phase);
        Assert.Null(RoomEngine.ReadyToVote(room, "p3", T0.AddMinutes(2)));
        Assert.Equal(Phase.Voting, room.Phase);

        Assert.Equal("cannot_vote_self", RoomEngine.Vote(room, "p1", "p1", T0.AddMinutes(3)));
        Assert.Equal("unknown_player", RoomEngine.Vote(room, "p1", "nobody", T0.AddMinutes(3)));
        Assert.Null(RoomEngine.Vote(room, "p1", "p2", T0.AddMinutes(3)));
        Assert.Null(RoomEngine.Vote(room, "p1", "p4", T0.AddMinutes(3))); // changed their mind
        Assert.Null(RoomEngine.Vote(room, "p2", "p4", T0.AddMinutes(3)));
        Assert.Null(RoomEngine.Vote(room, "p3", "p4", T0.AddMinutes(3)));
        Assert.Equal(Phase.Voting, room.Phase);
        Assert.Null(RoomEngine.Vote(room, "p4", "p1", T0.AddMinutes(3))); // last vote closes it

        Assert.Equal(Phase.LastGuess, room.Phase);
        Assert.Equal("p4", room.Round!.AccusedId);
        Assert.Equal("not_the_accused", RoomEngine.Guess(room, "p1", "casino", T0.AddMinutes(4)));
        Assert.Equal("invalid_guess", RoomEngine.Guess(room, "p4", "zoo", T0.AddMinutes(4))); // not a place of this room
        Assert.Null(RoomEngine.Guess(room, "p4", "plane", T0.AddMinutes(4))); // wrong place

        Assert.Equal(Phase.Results, room.Phase);
        Assert.Equal(Team.Civilians, room.Round.Outcome!.Winner);
        Assert.Equal([2, 2, 2, 0], room.Players.Select(p => p.Score)); // win + right vote each
        Assert.Equal(1, room.RoundsPlayed);
    }

    [Fact]
    public void A_caught_spy_who_names_the_place_wins()
    {
        var room = Started(wordIndex: 4, spySlot: 3);
        foreach (var id in new[] { "p1", "p2", "p3" }) RoomEngine.ReadyToVote(room, id, T0.AddMinutes(2));
        foreach (var id in new[] { "p1", "p2", "p3" }) RoomEngine.Vote(room, id, "p4", T0.AddMinutes(3));
        RoomEngine.Vote(room, "p4", "p1", T0.AddMinutes(3));
        Assert.Null(RoomEngine.Guess(room, "p4", "casino", T0.AddMinutes(4)));
        Assert.Equal(Team.Spies, room.Round!.Outcome!.Winner);
        Assert.Equal(Scoring.SpyWin + Scoring.SpyGuessBonus, room.Find("p4")!.Score);
        Assert.Equal("Kumarhane", RoomView.For(room, "p1", T0.AddMinutes(4)).Result!.Guess!.Tr);
    }

    [Fact]
    public void A_caught_spy_who_names_the_wrong_place_loses()
    {
        var room = Started(wordIndex: 4, spySlot: 3);
        foreach (var id in new[] { "p1", "p2", "p3" }) RoomEngine.ReadyToVote(room, id, T0.AddMinutes(2));
        foreach (var id in new[] { "p1", "p2", "p3" }) RoomEngine.Vote(room, id, "p4", T0.AddMinutes(3));
        RoomEngine.Vote(room, "p4", "p1", T0.AddMinutes(3));
        Assert.Null(RoomEngine.Guess(room, "p4", "beach", T0.AddMinutes(4)));
        Assert.Equal(Team.Civilians, room.Round!.Outcome!.Winner);
        Assert.False(room.Round.Outcome.SpyGuessedWord);
    }

    [Fact]
    public void Deadlines_move_the_room_on_by_themselves()
    {
        var room = Started(spySlot: 3);
        Assert.False(RoomEngine.Tick(room, T0.AddMinutes(5)));          // 1 second before the end
        Assert.True(RoomEngine.Tick(room, T0.AddMinutes(6)));           // questions over
        Assert.Equal(Phase.Voting, room.Phase);

        RoomEngine.Vote(room, "p1", "p4", T0.AddMinutes(6));
        RoomEngine.Vote(room, "p2", "p4", T0.AddMinutes(6));
        Assert.True(RoomEngine.Tick(room, T0.AddMinutes(7)));           // voting time over (60 s)
        Assert.Equal(Phase.LastGuess, room.Phase);
        Assert.True(RoomEngine.Tick(room, T0.AddMinutes(8)));           // the spy never guessed (30 s)
        Assert.Equal(Phase.Results, room.Phase);
        Assert.Equal(Team.Civilians, room.Round!.Outcome!.Winner);
        Assert.Null(RoomEngine.NextDeadline(room));
    }

    [Fact]
    public void A_long_pause_can_skip_several_phases_at_once()
    {
        var room = Started();
        Assert.True(RoomEngine.Tick(room, T0.AddHours(1)));
        Assert.Equal(Phase.Results, room.Phase); // no votes: no accusation, the spy wins
        Assert.Equal(Team.Spies, room.Round!.Outcome!.Winner);
    }

    [Fact]
    public void Someone_who_leaves_mid_round_counts_until_it_ends_and_the_host_is_passed_on()
    {
        var room = Started(spySlot: 3);
        Assert.Null(RoomEngine.Leave(room, "p1")); // the host leaves during the round
        Assert.Equal("p2", room.HostId);
        Assert.NotNull(room.Find("p1"));
        RoomEngine.Tick(room, T0.AddHours(1));
        Assert.Null(room.Find("p1")); // gone once the round is over
        Assert.Equal(3, room.Players.Count);
    }

    [Fact]
    public void A_late_comer_waits_for_the_next_round()
    {
        var room = Started();
        Assert.Null(RoomEngine.Join(room, "p5", "late", T0.AddMinutes(2)));
        Assert.False(room.InRound("p5"));
        Assert.Equal("not_in_round", RoomEngine.ReadyToVote(room, "p5", T0.AddMinutes(2)));
        var view = RoomView.For(room, "p5", T0.AddMinutes(2));
        Assert.Null(view.Card);
    }

    // --- Striking places out between rounds ---

    private static Room RoomWith(int places)
    {
        var room = RoomEngine.Create("ABC234", "p1", "host", new RoomSettings { PlaceIds = TestPlaces.Of(places) }, T0);
        for (var i = 2; i <= 4; i++) Assert.Null(RoomEngine.Join(room, $"p{i}", $"player{i}", T0.AddSeconds(i)));
        return room;
    }

    [Fact]
    public void The_host_strikes_places_out_between_rounds_and_can_bring_them_back()
    {
        var room = RoomWith(12);
        Assert.Equal("not_host", RoomEngine.StrikePlace(room, "p2", "zoo"));
        Assert.Equal("invalid_place", RoomEngine.StrikePlace(room, "p1", "zoo")); // not one of this room's places
        Assert.Null(RoomEngine.StrikePlace(room, "p1", "casino")); // any place, not only one that came up
        Assert.Null(RoomEngine.StrikePlace(room, "p1", "plane"));
        Assert.Equal("too_few_places", RoomEngine.StrikePlace(room, "p1", "bank")); // 10 must stay in play
        Assert.Equal(["casino", "plane"], room.Settings.StruckPlaceIds);

        Assert.Null(RoomEngine.StrikePlace(room, "p1", "casino")); // tapped again: back in play
        Assert.Equal(["plane"], room.Settings.StruckPlaceIds);
        Assert.Null(room.Settings.Problem());
    }

    [Fact]
    public void Places_cannot_be_struck_during_a_round()
    {
        var room = RoomWith(12);
        Assert.Null(RoomEngine.StartRound(room, "p1", T0.AddMinutes(1), new FixedRandom(0, 3, 0)));
        Assert.Equal("wrong_phase", RoomEngine.StrikePlace(room, "p1", "bank"));
        RoomEngine.Tick(room, T0.AddHours(1)); // the round is over: results
        Assert.Null(RoomEngine.StrikePlace(room, "p1", "plane")); // the place that just came up
    }

    [Fact]
    public void A_struck_place_is_never_dealt_or_guessed()
    {
        var room = RoomWith(11);
        Assert.Null(RoomEngine.StrikePlace(room, "p1", "plane"));
        // Draw #0 would be Plane; with it struck out, #0 is Bank.
        Assert.Null(RoomEngine.StartRound(room, "p1", T0.AddMinutes(1), new FixedRandom(0, 3, 0)));
        Assert.Equal("bank", room.Round!.Deal.PlaceId);

        var view = RoomView.For(room, "p4", T0.AddMinutes(2));
        Assert.DoesNotContain(view.Card!.Places, w => w.Id == "plane"); // not in the round's list
        Assert.Equal(11, view.Settings.Places.Count);                   // but still in the room's list, struck
        Assert.Equal(["plane"], view.Settings.Struck);

        foreach (var id in new[] { "p1", "p2", "p3" }) RoomEngine.ReadyToVote(room, id, T0.AddMinutes(2));
        foreach (var id in new[] { "p1", "p2", "p3", "p4" }) RoomEngine.Vote(room, id, id == "p4" ? "p1" : "p4", T0.AddMinutes(3));
        Assert.Equal("invalid_guess", RoomEngine.Guess(room, "p4", "plane", T0.AddMinutes(4)));
    }

    // --- What each player can see ---

    private static string Json(RoomView view) => JsonSerializer.Serialize(view);

    [Fact]
    public void A_spy_sees_exactly_the_same_whatever_the_place_is()
    {
        // Same room, same spy, two different secret places.
        var a = Started(wordIndex: 4, spySlot: 3);
        var b = Started(wordIndex: 9, spySlot: 3);
        var now = T0.AddMinutes(2);
        Assert.Equal(Json(RoomView.For(a, "p4", now)), Json(RoomView.For(b, "p4", now)));

        var card = RoomView.For(a, "p4", now).Card!;
        Assert.True(card.IsSpy);
        Assert.Null(card.Place);
        Assert.Equal(TestPlaces.Ten, card.Places.Select(w => w.Id)); // the room's places, without groups
    }

    [Fact]
    public void A_civilian_cannot_tell_who_the_spy_is()
    {
        // Same place, different spies: what p1 (a civilian in both) sees must not differ.
        var a = Started(wordIndex: 4, spySlot: 3); // spy p4
        var b = Started(wordIndex: 4, spySlot: 2); // spy p3
        var now = T0.AddMinutes(2);
        Assert.Equal(Json(RoomView.For(a, "p1", now)), Json(RoomView.For(b, "p1", now)));

        var card = RoomView.For(a, "p1", now).Card!;
        Assert.False(card.IsSpy);
        Assert.Equal("Kumarhane", card.Place!.Tr);
        Assert.Equal(TestPlaces.Ten, card.Places.Select(w => w.Id)); // the same list as the spy's
    }

    [Fact]
    public void Only_the_accused_spy_gets_the_list_to_guess_from()
    {
        var room = Started(spySlot: 3);
        foreach (var id in new[] { "p1", "p2", "p3" }) RoomEngine.ReadyToVote(room, id, T0.AddMinutes(2));
        foreach (var id in new[] { "p1", "p2", "p3" }) RoomEngine.Vote(room, id, "p4", T0.AddMinutes(3));
        RoomEngine.Vote(room, "p4", "p1", T0.AddMinutes(3));

        Assert.NotNull(RoomView.For(room, "p4", T0.AddMinutes(3)).Guess!.Places);
        Assert.Null(RoomView.For(room, "p1", T0.AddMinutes(3)).Guess!.Places);
    }

    [Fact]
    public void Votes_stay_secret_until_the_results()
    {
        var room = Started(spySlot: 3);
        RoomEngine.Tick(room, T0.AddMinutes(6));
        RoomEngine.Vote(room, "p1", "p4", T0.AddMinutes(6));
        var other = RoomView.For(room, "p2", T0.AddMinutes(6));
        Assert.Equal(1, other.Voting!.VotesCast);
        Assert.Null(other.Voting.YourVote);
        Assert.True(other.Players.Single(p => p.Id == "p1").HasVoted);
        Assert.DoesNotContain("\"p4\"", Json(other).Replace("\"Id\":\"p4\"", "")); // p1's choice is not in it
        Assert.Null(other.Result);

        RoomEngine.Tick(room, T0.AddHours(1));
        var after = RoomView.For(room, "p2", T0.AddHours(1));
        Assert.Equal(["p4"], after.Result!.SpyIds);
        Assert.Equal("p4", after.Result.Votes["p1"]);
    }
}
