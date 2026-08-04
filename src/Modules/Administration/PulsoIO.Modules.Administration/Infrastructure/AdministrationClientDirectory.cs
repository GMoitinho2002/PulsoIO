using Microsoft.EntityFrameworkCore;
using PulsoIO.BuildingBlocks.Tenancy;

namespace PulsoIO.Modules.Administration.Infrastructure;

internal sealed class AdministrationClientDirectory(AdministrationDbContext database)
    : IClientDirectory
{
    public Task<bool> ExistsActiveAsync(Guid clientId, CancellationToken cancellationToken)
    {
        if (clientId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        return database.Clients
            .AsNoTracking()
            .AnyAsync(client => client.Id == clientId && client.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> clientIds,
        CancellationToken cancellationToken)
    {
        if (clientIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var distinctIds = clientIds
            .Where(clientId => clientId != Guid.Empty)
            .Distinct()
            .ToArray();

        return await database.Clients
            .AsNoTracking()
            .Where(client => distinctIds.Contains(client.Id))
            .ToDictionaryAsync(client => client.Id, client => client.Name, cancellationToken);
    }
}
