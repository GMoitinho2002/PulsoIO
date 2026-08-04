using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PulsoIO.Modules.Identity;
using PulsoIO.Modules.Identity.Authentication;
using PulsoIO.Modules.Identity.Domain;
using PulsoIO.Modules.Identity.Infrastructure;
using Xunit;
using PulsoIdentityConstants = PulsoIO.Modules.Identity.Authentication.IdentityConstants;

namespace PulsoIO.Modules.Identity.Tests;

public sealed class InitialAdminBootstrapperTests
{
    [Fact]
    public async Task CreatesRootAdministratorWhenDatabaseHasNoAdministrator()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();
        var bootstrapper = fixture.CreateBootstrapper("configured@example.com");

        await bootstrapper.StartAsync(CancellationToken.None);

        var users = await fixture.Database.Users.AsNoTracking().ToArrayAsync();
        Assert.Single(users);
        Assert.Null(users[0].ClientId);
        Assert.True(await fixture.UserManager.IsInRoleAsync(users[0], PulsoIdentityConstants.AdministratorRole));
    }

    [Fact]
    public async Task ChangedEmailDoesNotCauseDuplicateAdministratorOnNextBootstrap()
    {
        await using var fixture = await BootstrapFixture.CreateAsync();
        var administrator = new User("Administrador", "changed@example.com")
        {
            EmailConfirmed = true
        };
        Assert.True((await fixture.UserManager.CreateAsync(administrator, "Current@1")).Succeeded);
        Assert.True((await fixture.RoleManager.CreateAsync(
            new IdentityRole<Guid>(PulsoIdentityConstants.AdministratorRole)
            {
                Id = Guid.NewGuid()
            })).Succeeded);
        Assert.True((await fixture.UserManager.AddToRoleAsync(
            administrator,
            PulsoIdentityConstants.AdministratorRole)).Succeeded);
        var bootstrapper = fixture.CreateBootstrapper("old-configured@example.com");

        await bootstrapper.StartAsync(CancellationToken.None);

        fixture.Database.ChangeTracker.Clear();
        var users = await fixture.Database.Users.AsNoTracking().ToArrayAsync();
        Assert.Single(users);
        Assert.Equal("changed@example.com", users[0].Email);
    }

    private sealed class BootstrapFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;

        private BootstrapFixture(
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
        }

        public IdentityDbContext Database { get; }

        public UserManager<User> UserManager { get; }

        public RoleManager<IdentityRole<Guid>> RoleManager { get; }

        public static async Task<BootstrapFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(connection));
            services
                .AddIdentityCore<User>(IdentityModule.ConfigureIdentityOptions)
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<IdentityDbContext>()
                .AddPasswordValidator<SpecialCharacterPasswordValidator>();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            var fixture = new BootstrapFixture(connection, provider, scope);
            await fixture.Database.Database.EnsureCreatedAsync();
            return fixture;
        }

        public InitialAdminBootstrapper CreateBootstrapper(string email)
        {
            return new InitialAdminBootstrapper(
                _provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new InitialAdminOptions
                {
                    Name = "Administrador",
                    Email = email,
                    Password = "Current@1"
                }),
                NullLogger<InitialAdminBootstrapper>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
