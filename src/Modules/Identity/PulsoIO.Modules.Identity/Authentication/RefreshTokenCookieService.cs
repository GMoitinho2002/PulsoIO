using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace PulsoIO.Modules.Identity.Authentication;

internal sealed class RefreshTokenCookieService(IWebHostEnvironment environment, TimeProvider timeProvider)
{
    public void Append(HttpResponse response, IssuedRefreshToken refreshToken)
    {
        var maxAge = refreshToken.Entity.ExpiresAtUtc - timeProvider.GetUtcNow();

        response.Cookies.Append(
            IdentityConstants.RefreshTokenCookieName,
            refreshToken.Value,
            CreateOptions(refreshToken.Entity.ExpiresAtUtc, maxAge));
    }

    public void Delete(HttpResponse response)
    {
        response.Cookies.Delete(
            IdentityConstants.RefreshTokenCookieName,
            CreateOptions(DateTimeOffset.UnixEpoch, TimeSpan.Zero));
    }

    private CookieOptions CreateOptions(DateTimeOffset expires, TimeSpan maxAge)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = IdentityConstants.RefreshTokenCookiePath,
            Expires = expires,
            MaxAge = maxAge,
            IsEssential = true
        };
    }
}
