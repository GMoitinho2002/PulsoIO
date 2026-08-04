namespace PulsoIO.Modules.Administration;

public sealed record CreateClientRequest(string Name, bool IsActive);

public sealed record UpdateClientRequest(string Name, bool IsActive);

public sealed record CreateEnvironmentRequest(string Name, string Kind, bool IsActive);

public sealed record UpdateEnvironmentRequest(string Name, string Kind, bool IsActive);

public sealed record CreateIntegrationRequest(
    Guid EnvironmentId,
    string Name,
    string Direction,
    string SourceSystem,
    string TargetSystem,
    string? HttpMethod,
    string? EndpointPattern,
    bool IsActive);

public sealed record UpdateIntegrationRequest(
    Guid EnvironmentId,
    string Name,
    string Direction,
    string SourceSystem,
    string TargetSystem,
    string? HttpMethod,
    string? EndpointPattern,
    bool IsActive);

public sealed record ClientListItemResponse(
    Guid Id,
    string Name,
    bool IsActive,
    int EnvironmentCount,
    int IntegrationCount);

public sealed record ClientDetailResponse(
    Guid Id,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyCollection<ClientEnvironmentResponse> Environments,
    IReadOnlyCollection<IntegrationResponse> Integrations);

public sealed record ClientEnvironmentResponse(
    Guid Id,
    Guid ClientId,
    string Name,
    string Kind,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record IntegrationResponse(
    Guid Id,
    Guid ClientId,
    Guid EnvironmentId,
    string Name,
    string Direction,
    string SourceSystem,
    string TargetSystem,
    string? HttpMethod,
    string? EndpointPattern,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
