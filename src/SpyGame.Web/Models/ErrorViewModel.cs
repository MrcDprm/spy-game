namespace SpyGame.Web.Models;

public sealed record ErrorViewModel(int StatusCode)
{
    private string Kind => StatusCode switch { 404 => "404", 429 => "429", _ => "500" };
    public string TitleKey => $"error.{Kind}.title";
    public string TextKey => $"error.{Kind}.text";
}
