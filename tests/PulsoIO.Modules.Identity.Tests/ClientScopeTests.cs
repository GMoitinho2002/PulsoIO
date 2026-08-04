using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using PulsoIO.BuildingBlocks.Tenancy;
using PulsoIO.Modules.Identity;
using PulsoIO.Modules.Identity.Authentication;
using PulsoIO.Modules.Identity.Domain;
using Xunit;

namespace PulsoIO.Modules.Identity.Tests;

public sealed class ClientScopeTests
{
    [Fact]
    public async Task RootUserDoesNotDependOnClientDirectory()
    {
        var directory = new FakeClientDirectory(active: false);
        var user = new User("Root", "root@example.com");

        var allowed = await ClientAccessEligibility.IsAllowedAsync(
            user,
            directory,
            CancellationToken.None);

        Assert.True(allowed);
        Assert.Equal(0, directory.ExistsCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClientUserRequiresAnActiveClient(bool active)
    {
        var clientId = Guid.NewGuid();
        var directory = new FakeClientDirectory(active);
        var user = new User("Cliente", "cliente@example.com", clientId: clientId);

        var allowed = await ClientAccessEligibility.IsAllowedAsync(
            user,
            directory,
            CancellationToken.None);

        Assert.Equal(active, allowed);
        Assert.Equal(clientId, directory.LastClientId);
    }

    [Fact]
    public void RootAdministratorCanManageRootAndClientUsers()
    {
        var principal = CreatePrincipal(null);

        Assert.True(IdentityEndpoints.CanManageClient(principal, null));
        Assert.True(IdentityEndpoints.CanManageClient(principal, Guid.NewGuid()));
    }

    [Fact]
    public void ClientAdministratorCanManageOnlyItsOwnClient()
    {
        var clientId = Guid.NewGuid();
        var principal = CreatePrincipal(clientId);

        Assert.True(IdentityEndpoints.CanManageClient(principal, clientId));
        Assert.False(IdentityEndpoints.CanManageClient(principal, null));
        Assert.False(IdentityEndpoints.CanManageClient(principal, Guid.NewGuid()));
    }

    [Fact]
    public void InvalidClientClaimNeverGrantsRootAccess()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(TenantClaimTypes.ClientId, "invalid")],
            "Test"));

        Assert.False(IdentityEndpoints.CanManageClient(principal, null));
        Assert.False(IdentityEndpoints.CanManageClient(principal, Guid.NewGuid()));
    }

    [Fact]
    public void JwtClientClaimMustMatchCurrentUserScope()
    {
        var clientId = Guid.NewGuid();

        Assert.True(JwtBearerSetup.ClientClaimsMatch(null, null));
        Assert.True(JwtBearerSetup.ClientClaimsMatch(clientId, clientId.ToString()));
        Assert.False(JwtBearerSetup.ClientClaimsMatch(null, clientId.ToString()));
        Assert.False(JwtBearerSetup.ClientClaimsMatch(clientId, null));
        Assert.False(JwtBearerSetup.ClientClaimsMatch(clientId, Guid.NewGuid().ToString()));
    }

    private static ClaimsPrincipal CreatePrincipal(Guid? clientId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())
        };
        if (clientId is Guid value)
        {
            claims.Add(new Claim(TenantClaimTypes.ClientId, value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private sealed class FakeClientDirectory(bool active) : IClientDirectory
    {
        public int ExistsCalls { get; private set; }

        public Guid? LastClientId { get; private set; }

        public Task<bool> ExistsActiveAsync(Guid clientId, CancellationToken cancellationToken)
        {
            ExistsCalls++;
            LastClientId = clientId;
            return Task.FromResult(active);
        }

        public Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
            IReadOnlyCollection<Guid> clientIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<Guid, string> result = new Dictionary<Guid, string>();
            return Task.FromResult(result);
        }
    }
}
