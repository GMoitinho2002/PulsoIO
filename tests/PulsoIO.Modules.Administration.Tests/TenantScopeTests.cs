using System.Security.Claims;
using PulsoIO.BuildingBlocks.Tenancy;
using Xunit;

namespace PulsoIO.Modules.Administration.Tests;

public sealed class TenantScopeTests
{
    [Fact]
    public void MissingClientClaimRepresentsRootAdministrator()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var scope = AdministrationEndpoints.TenantScope.From(principal);

        Assert.True(scope.IsValid);
        Assert.Null(scope.ClientId);
    }

    [Fact]
    public void ValidClientClaimLimitsAdministratorToThatClient()
    {
        var clientId = Guid.NewGuid();
        var principal = PrincipalWithClaims(new Claim(
            TenantClaimTypes.ClientId,
            clientId.ToString()));

        var scope = AdministrationEndpoints.TenantScope.From(principal);

        Assert.True(scope.IsValid);
        Assert.Equal(clientId, scope.ClientId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void InvalidClientClaimIsRejected(string value)
    {
        var scope = AdministrationEndpoints.TenantScope.From(
            PrincipalWithClaims(new Claim(TenantClaimTypes.ClientId, value)));

        Assert.False(scope.IsValid);
        Assert.Null(scope.ClientId);
    }

    [Fact]
    public void MultipleClientClaimsAreRejected()
    {
        var principal = PrincipalWithClaims(
            new Claim(TenantClaimTypes.ClientId, Guid.NewGuid().ToString()),
            new Claim(TenantClaimTypes.ClientId, Guid.NewGuid().ToString()));

        var scope = AdministrationEndpoints.TenantScope.From(principal);

        Assert.False(scope.IsValid);
    }

    private static ClaimsPrincipal PrincipalWithClaims(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
