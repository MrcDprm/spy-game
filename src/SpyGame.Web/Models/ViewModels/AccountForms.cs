namespace SpyGame.Web.Models.ViewModels;

public sealed record RegisterForm
{
    public string? Nickname { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }
    public string? ConfirmPassword { get; init; }
}

public sealed record LoginForm
{
    public string? Email { get; init; }
    public string? Password { get; init; }
    public string? ReturnUrl { get; init; }
}

public sealed record ForgotForm
{
    public string? Email { get; init; }
}

public sealed record ResetForm
{
    public string? UserId { get; init; }
    public string? Code { get; init; }
    public string? Password { get; init; }
    public string? ConfirmPassword { get; init; }
}
