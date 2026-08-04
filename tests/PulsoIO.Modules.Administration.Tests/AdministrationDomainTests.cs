using PulsoIO.Modules.Administration.Domain;
using Xunit;

namespace PulsoIO.Modules.Administration.Tests;

public sealed class AdministrationDomainTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 3, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ClientNormalizesNameAndMaintainsUtcAuditData()
    {
        var client = new Client("  Doca Livre  ", true, Now.ToOffset(TimeSpan.FromHours(-3)));

        Assert.Equal("Doca Livre", client.Name);
        Assert.Equal("DOCA LIVRE", client.NormalizedName);
        Assert.True(client.IsActive);
        Assert.Equal(TimeSpan.Zero, client.CreatedAtUtc.Offset);
        Assert.Equal(client.CreatedAtUtc, client.UpdatedAtUtc);

        var previousToken = client.ConcurrencyToken;
        client.Update("Doca Livre Piloto", false, Now.AddHours(1));

        Assert.False(client.IsActive);
        Assert.Equal("DOCA LIVRE PILOTO", client.NormalizedName);
        Assert.NotEqual(previousToken, client.ConcurrencyToken);
        Assert.Equal(Now.AddHours(1), client.UpdatedAtUtc);
    }

    [Fact]
    public void EnvironmentRequiresAClientBoundary()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ClientEnvironment(
            Guid.Empty,
            "Produção",
            EnvironmentKind.Production,
            true,
            Now));

        Assert.Equal("clientId", exception.ParamName);
    }

    [Fact]
    public void IntegrationNormalizesOptionalHttpMetadata()
    {
        var integration = new Integration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            " Pedidos ",
            IntegrationDirection.Inbound,
            " Doca Livre ",
            " Pulso I/O ",
            " post ",
            " /api/orders/{id} ",
            true,
            Now);

        Assert.Equal("Pedidos", integration.Name);
        Assert.Equal("PEDIDOS", integration.NormalizedName);
        Assert.Equal("Doca Livre", integration.SourceSystem);
        Assert.Equal("Pulso I/O", integration.TargetSystem);
        Assert.Equal("POST", integration.HttpMethod);
        Assert.Equal("/api/orders/{id}", integration.EndpointPattern);
    }
}
