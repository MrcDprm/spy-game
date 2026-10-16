using SpyGame.Web.Models.Game;

namespace SpyGame.Tests.Models;

public class PlayerTextTests
{
    [Theory]
    [InlineData("  Burada üniforma var mı?  ", "Burada üniforma var mı?")]
    [InlineData("iki\nsatır\tbir   arada", "iki satır bir arada")]
    [InlineData("gizli\u202Eyön", "gizliyön")]    // a right-to-left override could make text read backwards
    [InlineData("zero\u200Bwidth", "zerowidth")]
    public void Typed_text_becomes_one_clean_line(string input, string expected) => Assert.Equal(expected, PlayerText.Clean(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t ")]
    [InlineData("\u200B\u200B")]
    public void Nothing_left_is_refused(string? input) => Assert.Null(PlayerText.Clean(input));

    [Fact]
    public void The_length_limit_counts_after_cleaning()
    {
        Assert.NotNull(PlayerText.Clean(new string('a', PlayerText.MaxLength)));
        Assert.Null(PlayerText.Clean(new string('a', PlayerText.MaxLength + 1)));
        Assert.NotNull(PlayerText.Clean("  " + new string('a', PlayerText.MaxLength) + "  "));
    }
}

public class QuestionTurnsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    /// <summary>A remote room of four mid-round; p1 asks first (the spy is p4).</summary>
    private static Room Asking(bool remote = true)
    {
        var settings = new RoomSettings { PlaceIds = [.. TestPlaces.Ten], RoundSeconds = 300, Remote = remote };
        var room = RoomEngine.Create("ABC234", "p1", "host", settings, T0);
        for (var i = 2; i <= 4; i++) Assert.Null(RoomEngine.Join(room, $"p{i}", $"player{i}", T0));
        Assert.Null(RoomEngine.StartRound(room, "p1", T0, new FixedRandom(4, 3, 0)));
        Assert.Equal("p1", room.Round!.TurnId);
        return room;
    }

    [Fact]
    public void The_turn_passes_to_whoever_answers()
    {
        var room = Asking();
        Assert.Null(QuestionTurns.Ask(room, "p1", "p3", "Burada üniforma giyen var mı?", T0.AddSeconds(5)));
        Assert.Null(QuestionTurns.Answer(room, "p3", "Bazen."));
        Assert.Equal("p3", room.Round!.TurnId);
        Assert.Null(QuestionTurns.Ask(room, "p3", "p1", "Sen en son ne zaman geldin?", T0.AddSeconds(20))); // asking back is allowed
        Assert.Null(QuestionTurns.Answer(room, "p1", "Geçen hafta."));
        Assert.Equal("p1", room.Round.TurnId);

        var view = RoomView.For(room, "p2", T0.AddSeconds(30)).Questions!;
        Assert.Equal(2, view.Entries.Count);
        Assert.Equal(("p1", "p3", "Bazen."), (view.Entries[0].AskerId, view.Entries[0].TargetId, view.Entries[0].Answer));
        Assert.Equal("p1", view.TurnId);
    }

    [Fact]
    public void Only_the_player_whose_turn_it_is_asks_and_only_the_one_asked_answers()
    {
        var room = Asking();
        Assert.Equal("not_your_turn", QuestionTurns.Ask(room, "p2", "p3", "Soru?", T0));
        Assert.Equal("cannot_ask_self", QuestionTurns.Ask(room, "p1", "p1", "Soru?", T0));
        Assert.Equal("unknown_player", QuestionTurns.Ask(room, "p1", "nobody", "Soru?", T0));
        Assert.Equal("invalid_text", QuestionTurns.Ask(room, "p1", "p2", "   ", T0));
        Assert.Equal("nothing_to_answer", QuestionTurns.Answer(room, "p2", "Cevap"));

        Assert.Null(QuestionTurns.Ask(room, "p1", "p2", "Soru?", T0));
        Assert.Equal("nothing_to_answer", QuestionTurns.Answer(room, "p3", "Ben cevaplayayım")); // not asked
        Assert.Equal("invalid_text", QuestionTurns.Answer(room, "p2", new string('x', 201)));
        Assert.Equal("p1", room.Round!.TurnId); // nothing changed hands
    }

    [Fact]
    public void An_unanswered_question_can_be_given_up_on_after_a_minute()
    {
        var room = Asking();
        Assert.Null(QuestionTurns.Ask(room, "p1", "p2", "Soru?", T0));
        Assert.Equal("question_pending", QuestionTurns.Ask(room, "p1", "p3", "Başka soru?", T0.AddSeconds(59)));
        Assert.Equal(T0.AddSeconds(QuestionTurns.ReaskSeconds), RoomView.For(room, "p1", T0).Questions!.ReaskAt);

        Assert.Null(QuestionTurns.Ask(room, "p1", "p3", "Başka soru?", T0.AddSeconds(60)));
        Assert.True(room.Round!.Questions[0].Unanswered);
        Assert.Equal("nothing_to_answer", QuestionTurns.Answer(room, "p2", "Geç kaldım")); // too late now
        Assert.Null(QuestionTurns.Answer(room, "p3", "Evet."));
    }

    [Fact]
    public void The_host_can_pass_the_turn_on()
    {
        var room = Asking();
        Assert.Equal("not_host", QuestionTurns.SkipTurn(room, "p2"));

        Assert.Null(QuestionTurns.SkipTurn(room, "p1")); // p1 does not ask: the next player gets the turn
        Assert.Equal("p2", room.Round!.TurnId);

        Assert.Null(QuestionTurns.Ask(room, "p2", "p4", "Soru?", T0));
        Assert.Null(QuestionTurns.SkipTurn(room, "p1")); // p4 does not answer: the question is dropped, p2 asks again
        Assert.True(room.Round.Questions[0].Unanswered);
        Assert.Equal("p2", room.Round.TurnId);
        Assert.Null(QuestionTurns.Ask(room, "p2", "p3", "Yeni soru?", T0.AddSeconds(1))); // no wait after a skip
    }

    [Fact]
    public void Typed_questions_only_exist_in_rooms_playing_apart_and_while_asking()
    {
        var together = Asking(remote: false);
        Assert.Equal("not_remote", QuestionTurns.Ask(together, "p1", "p2", "Soru?", T0));
        Assert.Null(RoomView.For(together, "p1", T0).Questions);

        var room = Asking();
        RoomEngine.Tick(room, T0.AddMinutes(5)); // questions are over: voting
        Assert.Equal("wrong_phase", QuestionTurns.Ask(room, "p1", "p2", "Soru?", T0.AddMinutes(5)));
        Assert.Null(RoomView.For(room, "p1", T0.AddMinutes(5)).Questions!.TurnId);
    }

    [Fact]
    public void The_questions_stay_for_the_results_and_a_new_round_starts_clean()
    {
        var room = Asking();
        Assert.Null(QuestionTurns.Ask(room, "p1", "p2", "Soru?", T0));
        Assert.Null(QuestionTurns.Answer(room, "p2", "Cevap."));
        RoomEngine.Tick(room, T0.AddHours(1));
        Assert.Equal(Phase.Results, room.Phase);
        Assert.Single(RoomView.For(room, "p3", T0.AddHours(1)).Questions!.Entries);

        Assert.Null(RoomEngine.StartRound(room, "p1", T0.AddHours(2), new FixedRandom(0, 0, 0)));
        Assert.Empty(RoomView.For(room, "p3", T0.AddHours(2)).Questions!.Entries);
        Assert.NotNull(room.Round!.TurnId);
    }
}
