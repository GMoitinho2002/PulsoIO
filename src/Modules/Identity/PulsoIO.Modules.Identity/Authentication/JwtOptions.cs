namespace PulsoIO.Modules.Identity.Authentication;

internal sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; }

    public int RefreshTokenDays { get; init; }

    public static void Validate(JwtOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SigningKey);

        if (System.Text.Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                $"{SectionName}:SigningKey deve conter pelo menos 32 bytes.");
        }

        if (options.AccessTokenMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException(
                $"{SectionName}:AccessTokenMinutes deve estar entre 1 e 60.");
        }

        if (options.RefreshTokenDays is < 1 or > 30)
        {
            throw new InvalidOperationException(
                $"{SectionName}:RefreshTokenDays deve estar entre 1 e 30.");
        }
    }
}
