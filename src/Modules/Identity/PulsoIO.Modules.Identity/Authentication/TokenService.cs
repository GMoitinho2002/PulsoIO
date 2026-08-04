using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PulsoIO.BuildingBlocks.Tenancy;
using PulsoIO.Modules.Identity.Domain;

namespace PulsoIO.Modules.Identity.Authentication;

internal sealed class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    private readonly JwtOptions _options = options.Value;
    private readonly SigningCredentials _signingCredentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public IssuedAccessToken IssueAccessToken(User user, IReadOnlyCollection<string> roles)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAtUtc = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(IdentityConstants.NameClaim, user.Name),
            new(IdentityConstants.SecurityStampClaim, user.SecurityStamp ?? string.Empty)
        };

        if (user.ClientId is Guid clientId)
        {
            claims.Add(new Claim(TenantClaimTypes.ClientId, clientId.ToString()));
        }

        claims.AddRange(roles.Select(role => new Claim(IdentityConstants.RoleClaim, role)));

        var jwt = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now.UtcDateTime,
            expiresAtUtc.UtcDateTime,
            _signingCredentials);

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(jwt), expiresAtUtc);
    }

    public IssuedRefreshToken IssueRefreshToken(
        User user,
        Guid? familyId = null,
        DateTimeOffset? absoluteExpiresAtUtc = null)
    {
        var now = timeProvider.GetUtcNow();
        var value = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken(
            user.Id,
            familyId ?? Guid.NewGuid(),
            HashRefreshToken(value),
            user.SecurityStamp ?? string.Empty,
            now,
            absoluteExpiresAtUtc ?? now.AddDays(_options.RefreshTokenDays));

        return new IssuedRefreshToken(value, entity);
    }

    public static string HashRefreshToken(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
