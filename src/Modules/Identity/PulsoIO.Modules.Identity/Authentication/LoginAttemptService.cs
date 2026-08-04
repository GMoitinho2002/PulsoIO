using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PulsoIO.Modules.Identity.Domain;
using PulsoIO.Modules.Identity.Infrastructure;

namespace PulsoIO.Modules.Identity.Authentication;

internal sealed class LoginAttemptService(
    UserManager<User> userManager,
    IdentityDbContext database)
{
    private const int MaximumConcurrencyRetries = 16;
    private const string ConcurrencyFailureCode = "ConcurrencyFailure";

    public async Task RecordFailureAsync(Guid userId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaximumConcurrencyRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null || await userManager.IsLockedOutAsync(user))
            {
                return;
            }

            var result = await userManager.AccessFailedAsync(user);
            if (result.Succeeded)
            {
                return;
            }

            if (!IsConcurrencyFailure(result))
            {
                ThrowIdentityFailure("Não foi possível registrar a falha de autenticação.", result);
            }

            database.Entry(user).State = EntityState.Detached;
        }

        throw new InvalidOperationException(
            "Não foi possível registrar a falha de autenticação após tentativas concorrentes.");
    }

    public async Task<bool> ResetFailuresAsync(Guid userId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaximumConcurrencyRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null || await userManager.IsLockedOutAsync(user))
            {
                return false;
            }

            var failedAttempts = await userManager.GetAccessFailedCountAsync(user);
            var result = failedAttempts == 0
                ? await userManager.UpdateAsync(user)
                : await userManager.ResetAccessFailedCountAsync(user);

            if (result.Succeeded)
            {
                return true;
            }

            if (!IsConcurrencyFailure(result))
            {
                ThrowIdentityFailure("Não foi possível concluir a autenticação.", result);
            }

            database.Entry(user).State = EntityState.Detached;
        }

        throw new InvalidOperationException(
            "Não foi possível concluir a autenticação após tentativas concorrentes.");
    }

    internal static bool IsConcurrencyFailure(IdentityResult result)
    {
        return !result.Succeeded &&
            result.Errors.Any() &&
            result.Errors.All(error =>
                string.Equals(error.Code, ConcurrencyFailureCode, StringComparison.Ordinal));
    }

    private static void ThrowIdentityFailure(string message, IdentityResult result)
    {
        var codes = string.Join(", ", result.Errors.Select(error => error.Code));
        throw new InvalidOperationException($"{message} Códigos: {codes}.");
    }
}
