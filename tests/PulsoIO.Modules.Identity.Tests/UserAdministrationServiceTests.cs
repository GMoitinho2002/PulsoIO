using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulsoIO.Modules.Identity.Authentication;
using PulsoIO.Modules.Identity.Domain;
using PulsoIO.Modules.Identity.Infrastructure;
using Xunit;

namespace PulsoIO.Modules.Identity.Tests;

public sealed class UserAdministrationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 3, 3, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task StatusChangeUpdatesStampAndRevokesEveryActiveRefreshToken()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var user = new User("Operador", "operador@example.com");
        Assert.True((await fixture.UserManager.CreateAsync(user)).Succeeded);
        var previousSecurityStamp = user.SecurityStamp;

        fixture.Database.RefreshTokens.Add(new RefreshToken(
            user.Id,
            Guid.NewGuid(),
            new string('A', 64),
            user.SecurityStamp!,
            Now.AddMinutes(-5),
            Now.AddDays(1)));
        fixture.Database.RefreshTokens.Add(new RefreshToken(
            user.Id,
            Guid.NewGuid(),
            new string('B', 64),
            user.SecurityStamp!,
            Now.AddMinutes(-5),
            Now.AddDays(1)));
        await fixture.Database.SaveChangesAsync();

        var result = await fixture.Administration.SetActiveStatusAsync(
            user.Id,
            isActive: false,
            CancellationToken.None);

        Assert.True(result.WasFound);
        Assert.True(result.IdentityResult.Succeeded);
        Assert.False(result.HasConflict);
        fixture.Database.ChangeTracker.Clear();

        var persistedUser = await fixture.Database.Users.AsNoTracking().SingleAsync();
        var tokens = await fixture.Database.RefreshTokens.AsNoTracking().ToArrayAsync();
        Assert.False(persistedUser.IsActive);
        Assert.NotEqual(previousSecurityStamp, persistedUser.SecurityStamp);
        Assert.All(tokens, token => Assert.Equal(Now, token.RevokedAtUtc));
    }

    [Fact]
    public async Task LastActiveAdministratorCannotBeDeactivated()
    {
        await using var fixture = await IdentityFixture.CreateAsync();
        var administrator = new User("Administrador", "admin@example.com");
        Assert.True((await fixture.UserManager.CreateAsync(administrator)).Succeeded);
        Assert.True((await fixture.RoleManager.CreateAsync(new IdentityRole<Guid>("Admin")
        {
            Id = Guid.NewGuid()
        })).Succeeded);
        Assert.True((await fixture.UserManager.AddToRoleAsync(administrator, "Admin")).Succeeded);
        var previousSecurityStamp = administrator.SecurityStamp;

        var result = await fixture.Administration.SetActiveStatusAsync(
            administrator.Id,
            isActive: false,
            CancellationToken.None);

        Assert.True(result.WasFound);
        Assert.True(result.HasConflict);
        Assert.Equal(UserStatusConflictKind.LastActiveAdministrator, result.ConflictKind);
        fixture.Database.ChangeTracker.Clear();
        var persisted = await fixture.Database.Users.AsNoTracking().SingleAsync();
        Assert.True(persisted.IsActive);
        Assert.Equal(previousSecurityStamp, persisted.SecurityStamp);
    }

    [Fact]
    public async Task StaleSimultaneousStatusChangeReturnsConflictInsteadOfOppositeSuccess()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateServices(connection).BuildServiceProvider();

        Guid userId;
        await using (var setupScope = provider.CreateAsyncScope())
        {
            var database = setupScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await database.Database.EnsureCreatedAsync();
            var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User("Operador", "operador@example.com");
            Assert.True((await userManager.CreateAsync(user)).Succeeded);
            userId = user.Id;
        }

        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstManager = firstScope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var secondManager = secondScope.ServiceProvider.GetRequiredService<UserManager<User>>();
        Assert.NotNull(await firstManager.FindByIdAsync(userId.ToString()));
        Assert.NotNull(await secondManager.FindByIdAsync(userId.ToString()));

        var firstAdministration =
            firstScope.ServiceProvider.GetRequiredService<UserAdministrationService>();
        var secondAdministration =
            secondScope.ServiceProvider.GetRequiredService<UserAdministrationService>();
        var firstResult = await firstAdministration.SetActiveStatusAsync(
            userId,
            isActive: false,
            CancellationToken.None);
        var staleResult = await secondAdministration.SetActiveStatusAsync(
            userId,
            isActive: true,
            CancellationToken.None);

        Assert.True(firstResult.IdentityResult.Succeeded);
        Assert.False(firstResult.HasConflict);
        Assert.True(staleResult.HasConflict);
        Assert.Equal(UserStatusConflictKind.ConcurrentUpdate, staleResult.ConflictKind);

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationDatabase =
            verificationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var persisted = await verificationDatabase.Users.AsNoTracking().SingleAsync();
        Assert.False(persisted.IsActive);
    }

    [Theory]
    [InlineData("40001", true)]
    [InlineData("40P01", true)]
    [InlineData("23505", false)]
    public void ClassifiesOnlyRetryablePostgreSqlTransactionStatesAsConflicts(
        string sqlState,
        bool expected)
    {
        Assert.Equal(expected, UserAdministrationService.IsTransactionalConflict(sqlState));
    }

    private static ServiceCollection CreateServices(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(connection));
        services
            .AddIdentityCore<User>(IdentityModule.ConfigureIdentityOptions)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddPasswordValidator<SpecialCharacterPasswordValidator>();
        services.AddScoped<UserAdministrationService>();
        return services;
    }

    private sealed class IdentityFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;

        private IdentityFixture(
            SqliteConnection connection,
            ServiceProvider provider,
            AsyncServiceScope scope)
        {
            _connection = connection;
            _provider = provider;
            _scope = scope;
            Database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            UserManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            RoleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            Administration = scope.ServiceProvider.GetRequiredService<UserAdministrationService>();
        }

        public IdentityDbContext Database { get; }

        public UserManager<User> UserManager { get; }

        public RoleManager<IdentityRole<Guid>> RoleManager { get; }

        public UserAdministrationService Administration { get; }

        public static async Task<IdentityFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var provider = CreateServices(connection).BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            var fixture = new IdentityFixture(connection, provider, scope);
            await fixture.Database.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
