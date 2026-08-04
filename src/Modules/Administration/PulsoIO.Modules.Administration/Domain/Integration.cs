using PulsoIO.BuildingBlocks.Domain;

namespace PulsoIO.Modules.Administration.Domain;

public sealed class Integration : Entity
{
    private Integration()
    {
    }

    public Integration(
        Guid clientId,
        Guid environmentId,
        string name,
        IntegrationDirection direction,
        string sourceSystem,
        string targetSystem,
        string? httpMethod,
        string? endpointPattern,
        bool isActive,
        DateTimeOffset now)
    {
        if (clientId == Guid.Empty)
        {
            throw new ArgumentException("O cliente deve ser informado.", nameof(clientId));
        }

        if (environmentId == Guid.Empty)
        {
            throw new ArgumentException("O ambiente deve ser informado.", nameof(environmentId));
        }

        ClientId = clientId;
        EnvironmentId = environmentId;
        SetValues(name, direction, sourceSystem, targetSystem, httpMethod, endpointPattern, isActive);
        CreatedAtUtc = now.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid ClientId { get; private set; }

    public Guid EnvironmentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public IntegrationDirection Direction { get; private set; }

    public string SourceSystem { get; private set; } = string.Empty;

    public string TargetSystem { get; private set; } = string.Empty;

    public string? HttpMethod { get; private set; }

    public string? EndpointPattern { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    public ClientEnvironment Environment { get; private set; } = null!;

    public void Update(
        Guid environmentId,
        string name,
        IntegrationDirection direction,
        string sourceSystem,
        string targetSystem,
        string? httpMethod,
        string? endpointPattern,
        bool isActive,
        DateTimeOffset now)
    {
        if (environmentId == Guid.Empty)
        {
            throw new ArgumentException("O ambiente deve ser informado.", nameof(environmentId));
        }

        EnvironmentId = environmentId;
        SetValues(name, direction, sourceSystem, targetSystem, httpMethod, endpointPattern, isActive);
        UpdatedAtUtc = now.ToUniversalTime();
        ConcurrencyToken = Guid.NewGuid();
    }

    private void SetValues(
        string name,
        IntegrationDirection direction,
        string sourceSystem,
        string targetSystem,
        string? httpMethod,
        string? endpointPattern,
        bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSystem);

        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Direction = direction;
        SourceSystem = sourceSystem.Trim();
        TargetSystem = targetSystem.Trim();
        HttpMethod = string.IsNullOrWhiteSpace(httpMethod) ? null : httpMethod.Trim().ToUpperInvariant();
        EndpointPattern = string.IsNullOrWhiteSpace(endpointPattern) ? null : endpointPattern.Trim();
        IsActive = isActive;
    }
}
