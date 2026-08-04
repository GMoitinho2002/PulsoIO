using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulsoIO.Modules.Identity;
using PulsoIO.Modules.Identity.Authentication;
using PulsoIO.Modules.Identity.Domain;
using PulsoIO.Modules.Identity.Infrastructure;
using Xunit;

namespace PulsoIO.Modules.Identity.Tests;

public sealed class UserProfileServiceTests
{
    private const string CurrentPassword = "Current@1";
    private static readonly DateTimeOffset Now =
        new(2026, 8, 3, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EmailChangeRequiresPasswordUpdatesStampAndRevokesEverySession()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var previousStamp = user.SecurityStamp;
        await fixture.AddRefreshTokenAsync(user, 'A');

        var result = await fixture.Profile.UpdateEmailAsync(
            user.Id,
            "novo@example.com",
            CurrentPassword,
            CancellationToken.None);

        Assert.True(result.WasFound);
        Assert.True(result.IdentityResult.Succeeded);
        fixture.Database.ChangeTracker.Clear();
        var persisted = await fixture.Database.Users.AsNoTracking().SingleAsync();
        var token = await fixture.Database.RefreshTokens.AsNoTracking().SingleAsync();
        Assert.Equal("novo@example.com", persisted.Email);
        Assert.Equal("novo@example.com", persisted.UserName);
        Assert.True(persisted.EmailConfirmed);
        Assert.NotEqual(previousStamp, persisted.SecurityStamp);
        Assert.Equal(Now, token.RevokedAtUtc);
    }

    [Fact]
    public async Task InvalidCurrentPasswordLeavesEmailStampAndSessionUnchanged()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var previousStamp = user.SecurityStamp;
        await fixture.AddRefreshTokenAsync(user, 'B');

        var result = await fixture.Profile.UpdateEmailAsync(
            user.Id,
            "novo@example.com",
            "Wrong@1",
            CancellationToken.None);

        Assert.True(result.CurrentPasswordInvalid);
        Assert.False(result.IdentityResult.Succeeded);
        fixture.Database.ChangeTracker.Clear();
        var persisted = await fixture.Database.Users.AsNoTracking().SingleAsync();
        var token = await fixture.Database.RefreshTokens.AsNoTracking().SingleAsync();
        Assert.Equal("user@example.com", persisted.Email);
        Assert.Equal(previousStamp, persisted.SecurityStamp);
        Assert.Null(token.RevokedAtUtc);
    }

    [Fact]
    public async Task PasswordChangeUpdatesHashAndStampAndRevokesEverySession()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var previousStamp = user.SecurityStamp;
        await fixture.AddRefreshTokenAsync(user, 'C');

        var result = await fixture.Profile.UpdatePasswordAsync(
            user.Id,
            CurrentPassword,
            "Changed@2",
            CancellationToken.None);

        Assert.True(result.IdentityResult.Succeeded);
        fixture.Database.ChangeTracker.Clear();
        var persisted = await fixture.UserManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(persisted);
        Assert.True(await fixture.UserManager.CheckPasswordAsync(persisted, "Changed@2"));
        Assert.False(await fixture.UserManager.CheckPasswordAsync(persisted, CurrentPassword));
        Assert.NotEqual(previousStamp, persisted.SecurityStamp);
        var token = await fixture.Database.RefreshTokens.AsNoTracking().SingleAsync();
        Assert.Equal(Now, token.RevokedAtUtc);
    }

    [Fact]
    public async Task PhotoCanBeStoredAndRemovedWithoutChangingSecurityStamp()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var user = await fixture.CreateUserAsync();
        var previousStamp = user.SecurityStamp;
        var photo = new byte[] { 0xFF, 0xD8, 0xFF, 0x00 };

        var updateResult = await fixture.Profile.SetPhotoAsync(user.Id, photo, "image/jpeg");

        Assert.NotNull(updateResult);
        Assert.True(updateResult.Succeeded);
        fixture.Database.ChangeTracker.Clear();
        var persisted = await fixture.Database.Users.AsNoTracking().SingleAsync();
        Assert.Equal(photo, persisted.ProfilePhoto);
        Assert.Equal("image/jpeg", persisted.ProfilePhotoContentType);
        Assert.Equal(previousStamp, persisted.SecurityStamp);

        var removeResult = await fixture.Profile.RemovePhotoAsync(user.Id);

        Assert.NotNull(removeResult);
        Assert.True(removeResult.Succeeded);
        fixture.Database.ChangeTracker.Clear();
        persisted = await fixture.Database.Users.AsNoTracking().SingleAsync();
        Assert.Null(persisted.ProfilePhoto);
        Assert.Null(persisted.ProfilePhotoContentType);
    }

    private sealed class ProfileFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;

        private ProfileFixture(
            SqliteConnection connection,
            ServiceProvider provider,
            AsyncServiceScope scope)
        {
            _connection = connection;
            _provider = provider;
            _scope = scope;
            Database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            UserManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            Profile = scope.ServiceProvider.GetRequiredService<UserProfileService>();
        }

        public IdentityDbContext Database { get; }

        public UserManager<User> UserManager { get; }

        public UserProfileService Profile { get; }

        public static async Task<ProfileFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
            services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(connection));
            services
                .AddIdentityCore<User>(IdentityModule.ConfigureIdentityOptions)
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<IdentityDbContext>()
                .AddPasswordValidator<SpecialCharacterPasswordValidator>();
            services.AddScoped<UserProfileService>();
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateAsyncScope();
            var fixture = new ProfileFixture(connection, provider, scope);
            await fixture.Database.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async Task<User> CreateUserAsync()
        {
            var user = new User("User", "user@example.com")
            {
                EmailConfirmed = true
            };
            Assert.True((await UserManager.CreateAsync(user, CurrentPassword)).Succeeded);
            return user;
        }

        public async Task AddRefreshTokenAsync(User user, char hashCharacter)
        {
            Database.RefreshTokens.Add(new RefreshToken(
                user.Id,
                Guid.NewGuid(),
                new string(hashCharacter, 64),
                user.SecurityStamp!,
                Now.AddMinutes(-5),
                Now.AddDays(1)));
            await Database.SaveChangesAsync();
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
