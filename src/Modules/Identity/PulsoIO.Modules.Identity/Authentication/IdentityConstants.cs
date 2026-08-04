namespace PulsoIO.Modules.Identity.Authentication;

internal static class IdentityConstants
{
    public const string AdministratorRole = "Admin";
    public const string AdministratorPolicy = "Admin";
    public const string LoginRateLimitPolicy = "identity-login";
    public const string RefreshTokenCookieName = "pulsoio_refresh_token";
    public const string RefreshTokenCookiePath = "/api/identity/auth";
    public const string CsrfHeaderName = "X-Pulso-CSRF";
    public const string CsrfHeaderValue = "1";
    public const string SecurityStampClaim = "security_stamp";
    public const string NameClaim = "name";
    public const string RoleClaim = "role";
}
