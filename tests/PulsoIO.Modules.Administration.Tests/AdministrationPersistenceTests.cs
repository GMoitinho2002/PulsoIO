using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PulsoIO.Modules.Administration.Domain;
using PulsoIO.Modules.Administration.Infrastructure;
using Xunit;

namespace PulsoIO.Modules.Administration.Tests;

public sealed class AdministrationPersistenceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 3, 5, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ClientDirectoryOnlyAcceptsActiveClientsButResolvesEveryKnownName()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var active = new Client("Doca Livre", true, Now);
        var inactive = new Client("Cliente inativo", false, Now);
        fixture.Database.Clients.AddRange(active, inactive);
        await fixture.Database.SaveChangesAsync();

        var directory = new AdministrationClientDirectory(fixture.Database);

        Assert.True(await directory.ExistsActiveAsync(active.Id, CancellationToken.None));
        Assert.False(await directory.ExistsActiveAsync(inactive.Id, CancellationToken.None));
        Assert.False(await directory.ExistsActiveAsync(Guid.NewGuid(), CancellationToken.None));
        var names = await directory.GetNamesAsync(
            [active.Id, inactive.Id, active.Id, Guid.Empty],
            CancellationToken.None);
        Assert.Equal("Doca Livre", names[active.Id]);
        Assert.Equal("Cliente inativo", names[inactive.Id]);
        Assert.Equal(2, names.Count);
    }

    [Fact]
    public async Task CompositeForeignKeyPreventsIntegrationFromCrossingClientBoundary()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var firstClient = new Client("Doca Livre", true, Now);
        var secondClient = new Client("Outro cliente", true, Now);
        fixture.Database.Clients.AddRange(firstClient, secondClient);
        var firstEnvironment = new ClientEnvironment(
            firstClient.Id,
            "Produção",
            EnvironmentKind.Production,
            true,
            Now);
        fixture.Database.Environments.Add(firstEnvironment);
        await fixture.Database.SaveChangesAsync();
        fixture.Database.ChangeTracker.Clear();

        fixture.Database.Integrations.Add(new Integration(
            secondClient.Id,
            firstEnvironment.Id,
            "Integração inválida",
            IntegrationDirection.Inbound,
            "Origem",
            "Destino",
            null,
            null,
            true,
            Now));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => fixture.Database.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrencyTokenDetectsOverlappingClientUpdates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AdministrationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new AdministrationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Clients.Add(new Client("Doca Livre", true, Now));
            await setup.SaveChangesAsync();
        }

        await using var first = new AdministrationDbContext(options);
        await using var second = new AdministrationDbContext(options);
        var firstClient = await first.Clients.SingleAsync();
        var secondClient = await second.Clients.SingleAsync();
        firstClient.Update("Doca Livre A", true, Now.AddMinutes(1));
        secondClient.Update("Doca Livre B", true, Now.AddMinutes(2));

        await first.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.SaveChangesAsync());
    }

    private sealed class DatabaseFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private DatabaseFixture(SqliteConnection connection, AdministrationDbContext database)
        {
            _connection = connection;
            Database = database;
        }

        public AdministrationDbContext Database { get; }

        public static async Task<DatabaseFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AdministrationDbContext>()
                .UseSqlite(connection)
                .Options;
            var database = new AdministrationDbContext(options);
            await database.Database.EnsureCreatedAsync();
            return new DatabaseFixture(connection, database);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
