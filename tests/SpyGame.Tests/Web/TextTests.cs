using System.Text.Json;

namespace SpyGame.Tests.Web;

/// <summary>The interface texts in Resources/tr.json and en.json.</summary>
public class TextTests
{
    private static Dictionary<string, string> Load(string lang)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SpyGame.slnx"))) dir = dir.Parent;
        var path = Path.Combine(dir!.FullName, "src", "SpyGame.Web", "Resources", $"{lang}.json");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
    }

    [Fact]
    public void Both_languages_have_the_same_texts()
    {
        Assert.Equal(Load("tr").Keys.Order(), Load("en").Keys.Order());
    }

    [Fact]
    public void Texts_follow_the_writing_rules()
    {
        // No circumflex letters in Turkish, no brackets or quotation marks in either language. The one
        // exception is the search title, which names the genre for people searching for it.
        Assert.All(Load("tr"), t => Assert.DoesNotMatch("[âîûÂÎÛ]", t.Value));
        foreach (var lang in new[] { "tr", "en" })
        {
            Assert.All(Load(lang).Where(t => t.Key != "seo.home.title"), t => Assert.DoesNotMatch("[()\\[\\]\"“”«»]", t.Value));
        }
    }

    [Fact]
    public void Forty_suggested_questions_reach_the_room_page_in_both_languages()
    {
        // The room script receives every "js." text; it picks the questions by their numbered keys.
        foreach (var lang in new[] { "tr", "en" })
        {
            var questions = Load(lang).Where(t => System.Text.RegularExpressions.Regex.IsMatch(t.Key, @"^js\.suggest\.\d+$")).ToList();
            Assert.Equal(40, questions.Count);
            Assert.Equal(40, questions.Select(q => q.Value).Distinct().Count());
        }
    }
}
