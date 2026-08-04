using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PulsoIO.Modules.Identity.Authentication;
using PulsoIO.Modules.Identity.Domain;
using PulsoIO.Modules.Identity.Infrastructure;
using Xunit;

namespace PulsoIO.Modules.Identity.Tests;

public sealed class LoginAttemptServiceTests
{
    [Fact]
    public async Task RecordsLockoutAtThresholdAndRefusesResetWhileLocked()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(connection));
        services
            .AddIdentityCore<User>(options =>
            {
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>();
        services.AddScoped<LoginAttemptService>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var service = scope.ServiceProvider.GetRequiredService<LoginAttemptService>();
        await database.Database.EnsureCreatedAsync();

        var lockedUser = new User("Gustavo", "gustavo@example.com");
        Assert.True((await userManager.CreateAsync(lockedUser)).Succeeded);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await service.RecordFailureAsync(lockedUser.Id, CancellationToken.None);
        }

        database.ChangeTracker.Clear();
        var locked = await userManager.FindByIdAsync(lockedUser.Id.ToString());
        Assert.NotNull(locked);
        Assert.True(await userManager.IsLockedOutAsync(locked));
        Assert.Equal(0, await userManager.GetAccessFailedCountAsync(locked));
        Assert.False(await service.ResetFailuresAsync(locked.Id, CancellationToken.None));

        var resettableUser = new User("Operador", "operador@example.com");
        Assert.True((await userManager.CreateAsync(resettableUser)).Succeeded);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await service.RecordFailureAsync(resettableUser.Id, CancellationToken.None);
        }

        Assert.True(await service.ResetFailuresAsync(resettableUser.Id, CancellationToken.None));
        database.ChangeTracker.Clear();
        var reset = await userManager.FindByIdAsync(resettableUser.Id.ToString());
        Assert.NotNull(reset);
        Assert.Equal(0, await userManager.GetAccessFailedCountAsync(reset));
    }

    [Fact]
    public void RecognizesOnlyConcurrencyFailuresAsRetryable()
    {
        var concurrencyFailure = IdentityResult.Failed(new IdentityError
        {
            Code = "ConcurrencyFailure"
        });
        var validationFailure = IdentityResult.Failed(new IdentityError
        {
            Code = "InvalidUserName"
        });

        Assert.True(LoginAttemptService.IsConcurrencyFailure(concurrencyFailure));
        Assert.False(LoginAttemptService.IsConcurrencyFailure(validationFailure));
        Assert.False(LoginAttemptService.IsConcurrencyFailure(IdentityResult.Success));
    }
}
