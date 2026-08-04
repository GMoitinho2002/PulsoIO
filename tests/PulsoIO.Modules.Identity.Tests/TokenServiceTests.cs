using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PulsoIO.Modules.Identity.Authentication;
using PulsoIO.Modules.Identity.Domain;
using PulsoIO.BuildingBlocks.Tenancy;
using Xunit;

namespace PulsoIO.Modules.Identity.Tests;

public sealed class TokenServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 3, 1, 30, 0, TimeSpan.Zero);

    [Fact]
    public void IssueAccessTokenCreatesShortLivedSignedJwtWithRequiredClaims()
    {
        var user = new User("Gustavo", "gustavo@example.com");
        var service = CreateService();

        var issued = service.IssueAccessToken(user, ["Admin"]);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.Value);

        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
        Assert.Equal("JWT", jwt.Header.Typ);
        Assert.Equal("PulsoIO.Tests", jwt.Issuer);
        Assert.Contains("PulsoIO.Tests.Web", jwt.Audiences);
        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.False(string.IsNullOrWhiteSpace(
            jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Jti).Value));
        Assert.Equal("Admin", jwt.Claims.Single(claim => claim.Type == "role").Value);
        Assert.Equal(user.SecurityStamp, jwt.Claims.Single(claim => claim.Type == "security_stamp").Value);
        Assert.Equal(Now.AddMinutes(15), issued.ExpiresAtUtc);
    }

    [Fact]
    public void IssueRefreshTokenPersistsOnlyHashAndRotatesWithinFamily()
    {
        var user = new User("Gustavo", "gustavo@example.com");
        var service = CreateService();

        var first = service.IssueRefreshToken(user);
        var replacement = service.IssueRefreshToken(
            user,
            first.Entity.FamilyId,
            first.Entity.ExpiresAtUtc);

        Assert.NotEqual(first.Value, first.Entity.TokenHash);
        Assert.Equal(TokenService.HashRefreshToken(first.Value), first.Entity.TokenHash);
        Assert.Equal(64, first.Entity.TokenHash.Length);
        Assert.Equal(first.Entity.FamilyId, replacement.Entity.FamilyId);
        Assert.Equal(first.Entity.ExpiresAtUtc, replacement.Entity.ExpiresAtUtc);
        Assert.NotEqual(first.Entity.TokenHash, replacement.Entity.TokenHash);
        Assert.Equal(Now.AddDays(7), first.Entity.ExpiresAtUtc);
        Assert.Equal(user.SecurityStamp, first.Entity.SecurityStamp);
    }

    [Fact]
    public void ClientScopedAccessTokenIncludesTenantClaimWhileRootTokenDoesNot()
    {
        var clientId = Guid.NewGuid();
        var service = CreateService();
        var clientUser = new User(
            "Cliente",
            "cliente@example.com",
            clientId: clientId);
        var rootUser = new User("Root", "root@example.com");

        var clientJwt = new JwtSecurityTokenHandler().ReadJwtToken(
            service.IssueAccessToken(clientUser, []).Value);
        var rootJwt = new JwtSecurityTokenHandler().ReadJwtToken(
            service.IssueAccessToken(rootUser, []).Value);

        Assert.Equal(
            clientId.ToString(),
            clientJwt.Claims.Single(claim => claim.Type == TenantClaimTypes.ClientId).Value);
        Assert.DoesNotContain(rootJwt.Claims, claim => claim.Type == TenantClaimTypes.ClientId);
    }

    [Fact]
    public void JwtOptionsRejectsSigningKeysShorterThanThirtyTwoBytes()
    {
        var options = new JwtOptions
        {
            Issuer = "PulsoIO.Tests",
            Audience = "PulsoIO.Tests.Web",
            SigningKey = "short",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        };

        var exception = Assert.Throws<InvalidOperationException>(() => JwtOptions.Validate(options));

        Assert.Contains("32 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("stamp", "stamp", true)]
    [InlineData("stamp", "other", false)]
    [InlineData(null, "stamp", false)]
    public void SecurityStampComparisonIsStrict(string? current, string? supplied, bool expected)
    {
        Assert.Equal(expected, JwtBearerSetup.SecurityStampsMatch(current, supplied));
    }

    private static TokenService CreateService()
    {
        return new TokenService(
            Options.Create(CreateOptions()),
            new FixedTimeProvider(Now));
    }

    private static JwtOptions CreateOptions()
    {
        return new JwtOptions
        {
            Issuer = "PulsoIO.Tests",
            Audience = "PulsoIO.Tests.Web",
            SigningKey = "test-only-signing-key-with-at-least-32-bytes",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
