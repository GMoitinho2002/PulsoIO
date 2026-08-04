using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PulsoIO.Modules.Identity.Authentication;
using PulsoIO.Modules.Identity.Domain;

namespace PulsoIO.Modules.Identity.Infrastructure;

internal sealed class UserProfileService(
    IdentityDbContext database,
    UserManager<User> userManager,
    TimeProvider timeProvider)
{
    public async Task<UserProfileUpdateResult> UpdateEmailAsync(
        Guid userId,
        string email,
        string currentPassword,
        CancellationToken cancellationToken)
    {
        try
        {
            return await UpdateEmailCoreAsync(userId, email, currentPassword, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UserProfileUpdateResult.Conflict();
        }
        catch (PostgresException exception)
            when (UserAdministrationService.IsTransactionalConflict(exception.SqlState))
        {
            return UserProfileUpdateResult.Conflict();
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgresException &&
                UserAdministrationService.IsTransactionalConflict(postgresException.SqlState))
        {
            return UserProfileUpdateResult.Conflict();
        }
    }

    private async Task<UserProfileUpdateResult> UpdateEmailCoreAsync(
        Guid userId,
        string email,
        string currentPassword,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !AuthenticationEligibility.IsAllowed(user))
        {
            return UserProfileUpdateResult.NotFound();
        }

        if (!await userManager.CheckPasswordAsync(user, currentPassword))
        {
            return UserProfileUpdateResult.InvalidCurrentPassword(user);
        }

        var emailResult = await userManager.SetEmailAsync(user, email.Trim());
        if (!emailResult.Succeeded)
        {
            return UserProfileUpdateResult.Failure(user, emailResult);
        }

        user.EmailConfirmed = true;
        var userNameResult = await userManager.SetUserNameAsync(user, email.Trim());
        if (!userNameResult.Succeeded)
        {
            return UserProfileUpdateResult.Failure(user, userNameResult);
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            return UserProfileUpdateResult.Failure(user, stampResult);
        }

        await RevokeRefreshTokensAsync(userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserProfileUpdateResult.Success(user);
    }

    public async Task<UserProfileUpdateResult> UpdatePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        try
        {
            return await UpdatePasswordCoreAsync(
                userId,
                currentPassword,
                newPassword,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UserProfileUpdateResult.Conflict();
        }
        catch (PostgresException exception)
            when (UserAdministrationService.IsTransactionalConflict(exception.SqlState))
        {
            return UserProfileUpdateResult.Conflict();
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgresException &&
                UserAdministrationService.IsTransactionalConflict(postgresException.SqlState))
        {
            return UserProfileUpdateResult.Conflict();
        }
    }

    private async Task<UserProfileUpdateResult> UpdatePasswordCoreAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !AuthenticationEligibility.IsAllowed(user))
        {
            return UserProfileUpdateResult.NotFound();
        }

        var passwordResult = await userManager.ChangePasswordAsync(
            user,
            currentPassword,
            newPassword);
        if (!passwordResult.Succeeded)
        {
            return UserProfileUpdateResult.Failure(user, passwordResult);
        }

        // ChangePasswordAsync currently rotates the stamp, and this explicit update
        // keeps that security invariant independent from the Identity implementation.
        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            return UserProfileUpdateResult.Failure(user, stampResult);
        }

        await RevokeRefreshTokensAsync(userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UserProfileUpdateResult.Success(user);
    }

    public async Task<IdentityResult?> SetPhotoAsync(
        Guid userId,
        byte[] content,
        string contentType)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !AuthenticationEligibility.IsAllowed(user))
        {
            return null;
        }

        user.SetProfilePhoto(content, contentType);
        return await userManager.UpdateAsync(user);
    }

    public async Task<IdentityResult?> RemovePhotoAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !AuthenticationEligibility.IsAllowed(user))
        {
            return null;
        }

        user.RemoveProfilePhoto();
        return await userManager.UpdateAsync(user);
    }

    private Task<int> RevokeRefreshTokensAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return database.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAtUtc, now),
                cancellationToken);
    }
}

internal sealed record UserProfileUpdateResult(
    User? User,
    IdentityResult IdentityResult,
    bool CurrentPasswordInvalid,
    bool HasConflict)
{
    public bool WasFound => User is not null;

    public static UserProfileUpdateResult NotFound() =>
        new(null, IdentityResult.Success, false, false);

    public static UserProfileUpdateResult Success(User user) =>
        new(user, IdentityResult.Success, false, false);

    public static UserProfileUpdateResult Failure(User user, IdentityResult result) =>
        new(user, result, false, false);

    public static UserProfileUpdateResult InvalidCurrentPassword(User user) =>
        new(user, IdentityResult.Failed(new IdentityError
        {
            Code = "InvalidCurrentPassword",
            Description = "A senha atual está incorreta."
        }), true, false);

    public static UserProfileUpdateResult Conflict() =>
        new(null, IdentityResult.Success, false, true);
}
