using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PulsoIO.Modules.Identity.Domain;
using IdentityConstants = PulsoIO.Modules.Identity.Authentication.IdentityConstants;

namespace PulsoIO.Modules.Identity.Infrastructure;

internal sealed class UserAdministrationService(
    IdentityDbContext database,
    UserManager<User> userManager,
    TimeProvider timeProvider)
{
    public async Task<UserStatusUpdateResult> SetActiveStatusAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        User? user = null;

        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return UserStatusUpdateResult.NotFound();
            }

            var statusChanged = user.SetActiveStatus(isActive);
            if (!statusChanged)
            {
                var barrierResult = await userManager.UpdateAsync(user);
                if (!barrierResult.Succeeded)
                {
                    return MapIdentityFailure(user, barrierResult);
                }

                await transaction.CommitAsync(cancellationToken);
                return UserStatusUpdateResult.Success(user);
            }

            if (!isActive &&
                await userManager.IsInRoleAsync(user, IdentityConstants.AdministratorRole) &&
                !await HasAnotherActiveAdministratorAsync(userId, cancellationToken))
            {
                return UserStatusUpdateResult.Conflict(
                    user,
                    UserStatusConflictKind.LastActiveAdministrator);
            }

            var identityResult = await userManager.UpdateSecurityStampAsync(user);
            if (!identityResult.Succeeded)
            {
                return MapIdentityFailure(user, identityResult);
            }

            var now = timeProvider.GetUtcNow();
            await database.RefreshTokens
                .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(token => token.RevokedAtUtc, now),
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return UserStatusUpdateResult.Success(user);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UserStatusUpdateResult.Conflict(user, UserStatusConflictKind.ConcurrentUpdate);
        }
        catch (PostgresException exception) when (IsTransactionalConflict(exception.SqlState))
        {
            return UserStatusUpdateResult.Conflict(user, UserStatusConflictKind.ConcurrentUpdate);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgresException &&
                IsTransactionalConflict(postgresException.SqlState))
        {
            return UserStatusUpdateResult.Conflict(user, UserStatusConflictKind.ConcurrentUpdate);
        }
    }

    internal static bool IsTransactionalConflict(string sqlState)
    {
        return string.Equals(
                sqlState,
                PostgresErrorCodes.SerializationFailure,
                StringComparison.Ordinal) ||
            string.Equals(
                sqlState,
                PostgresErrorCodes.DeadlockDetected,
                StringComparison.Ordinal);
    }

    private static UserStatusUpdateResult MapIdentityFailure(
        User user,
        IdentityResult identityResult)
    {
        return Authentication.LoginAttemptService.IsConcurrencyFailure(identityResult)
            ? UserStatusUpdateResult.Conflict(user, UserStatusConflictKind.ConcurrentUpdate)
            : UserStatusUpdateResult.Failure(user, identityResult);
    }

    private Task<bool> HasAnotherActiveAdministratorAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var normalizedRoleName = userManager.NormalizeName(IdentityConstants.AdministratorRole);

        return (
            from candidate in database.Users
            join userRole in database.UserRoles on candidate.Id equals userRole.UserId
            join role in database.Roles on userRole.RoleId equals role.Id
            where candidate.Id != userId &&
                candidate.IsActive &&
                role.NormalizedName == normalizedRoleName
            select candidate.Id)
            .AnyAsync(cancellationToken);
    }
}

internal sealed record UserStatusUpdateResult(
    User? User,
    IdentityResult IdentityResult,
    UserStatusConflictKind ConflictKind)
{
    public bool WasFound => User is not null;

    public bool HasConflict => ConflictKind is not UserStatusConflictKind.None;

    public static UserStatusUpdateResult NotFound()
    {
        return new UserStatusUpdateResult(
            null,
            IdentityResult.Success,
            UserStatusConflictKind.None);
    }

    public static UserStatusUpdateResult Success(User user)
    {
        return new UserStatusUpdateResult(
            user,
            IdentityResult.Success,
            UserStatusConflictKind.None);
    }

    public static UserStatusUpdateResult Failure(User user, IdentityResult identityResult)
    {
        return new UserStatusUpdateResult(
            user,
            identityResult,
            UserStatusConflictKind.None);
    }

    public static UserStatusUpdateResult Conflict(
        User? user,
        UserStatusConflictKind conflictKind)
    {
        return new UserStatusUpdateResult(user, IdentityResult.Success, conflictKind);
    }
}

internal enum UserStatusConflictKind
{
    None,
    LastActiveAdministrator,
    ConcurrentUpdate
}
