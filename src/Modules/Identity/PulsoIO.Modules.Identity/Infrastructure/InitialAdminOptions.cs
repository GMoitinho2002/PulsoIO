namespace PulsoIO.Modules.Identity.Infrastructure;

internal sealed class InitialAdminOptions
{
    public const string SectionName = "Authentication:InitialAdmin";

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
