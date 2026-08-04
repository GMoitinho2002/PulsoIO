namespace PulsoIO.BuildingBlocks.Tenancy;

public interface IClientDirectory
{
    Task<bool> ExistsActiveAsync(Guid clientId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> clientIds,
        CancellationToken cancellationToken);
}
