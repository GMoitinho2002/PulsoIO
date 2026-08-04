using PulsoIO.BuildingBlocks.Tenancy;
using PulsoIO.Modules.Identity.Domain;

namespace PulsoIO.Modules.Identity.Authentication;

internal static class ClientAccessEligibility
{
    public static Task<bool> IsAllowedAsync(
        User user,
        IClientDirectory clientDirectory,
        CancellationToken cancellationToken)
    {
        return user.ClientId is not Guid clientId
            ? Task.FromResult(true)
            : clientDirectory.ExistsActiveAsync(clientId, cancellationToken);
    }
}
