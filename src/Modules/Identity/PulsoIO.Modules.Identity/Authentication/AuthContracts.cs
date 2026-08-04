namespace PulsoIO.Modules.Identity.Authentication;

public sealed record LoginRequest(string Email, string Password);

public sealed record CreateUserRequest(
    string Name,
    string Email,
    string Password,
    bool IsActive,
    Guid? ClientId);

public sealed record UpdateUserStatusRequest(bool IsActive);

public sealed record AuthUserResponse(
    Guid Id,
    string Name,
    string Email,
    IReadOnlyCollection<string> Roles,
    Guid? ClientId,
    string? ClientName,
    bool IsRoot,
    bool HasProfilePhoto);

public sealed record AuthSessionResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    AuthUserResponse User);

public sealed record UserSummaryResponse(
    Guid Id,
    string Name,
    string Email,
    bool IsActive,
    Guid? ClientId,
    string? ClientName,
    bool IsRoot,
    bool HasProfilePhoto);

public sealed record UpdateProfileEmailRequest(string Email, string CurrentPassword);

public sealed record UpdateProfilePasswordRequest(string CurrentPassword, string NewPassword);

internal sealed record IssuedAccessToken(string Value, DateTimeOffset ExpiresAtUtc);

internal sealed record IssuedRefreshToken(string Value, Domain.RefreshToken Entity);
