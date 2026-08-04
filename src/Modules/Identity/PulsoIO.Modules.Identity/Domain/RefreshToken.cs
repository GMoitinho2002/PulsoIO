namespace PulsoIO.Modules.Identity.Domain;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid userId,
        Guid familyId,
        string tokenHash,
        string securityStamp,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        SecurityStamp = securityStamp;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private init; }

    public Guid UserId { get; private init; }

    public Guid FamilyId { get; private init; }

    public string TokenHash { get; private init; } = string.Empty;

    public string SecurityStamp { get; private init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private init; }

    public DateTimeOffset ExpiresAtUtc { get; private init; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public User User { get; private init; } = null!;
}
