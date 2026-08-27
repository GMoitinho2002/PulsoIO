using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PulsoIO.BuildingBlocks.Tenancy;
using PulsoIO.Modules.Administration.Domain;
using PulsoIO.Modules.Administration.Infrastructure;

namespace PulsoIO.Modules.Administration;

internal static class AdministrationEndpoints
{
    private const string AdministratorPolicy = "Admin";

    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/administration/overview", GetOverviewAsync)
            .WithName("GetAdministrationOverview")
            .WithSummary("Obtém os totais administrativos acessíveis ao usuário.")
            .WithTags("Administration overview")
            .AddEndpointFilter<AdministrationNoStoreEndpointFilter>()
            .RequireAuthorization()
            .Produces<AdministrationOverviewResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        var clients = endpoints
            .MapGroup("/api/administration/clients")
            .WithTags("Client administration")
            .AddEndpointFilter<AdministrationNoStoreEndpointFilter>()
            .RequireAuthorization(AdministratorPolicy);

        clients.MapGet("/", ListClientsAsync)
            .WithName("ListClients")
            .WithSummary("Lista os clientes acessíveis pelo administrador.")
            .Produces<IReadOnlyCollection<ClientListItemResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        clients.MapPost("/", CreateClientAsync)
            .WithName("CreateClient")
            .WithSummary("Cadastra um cliente.")
            .Produces<ClientDetailResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        clients.MapGet("/{clientId:guid}", GetClientAsync)
            .WithName("GetClient")
            .WithSummary("Obtém um cliente, seus ambientes e suas integrações.")
            .Produces<ClientDetailResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        clients.MapPut("/{clientId:guid}", UpdateClientAsync)
            .WithName("UpdateClient")
            .WithSummary("Atualiza os dados e o status de um cliente.")
            .Produces<ClientDetailResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        clients.MapPost("/{clientId:guid}/environments", CreateEnvironmentAsync)
            .WithName("CreateClientEnvironment")
            .WithSummary("Cadastra um ambiente do cliente.")
            .Produces<ClientEnvironmentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        clients.MapPut("/{clientId:guid}/environments/{environmentId:guid}", UpdateEnvironmentAsync)
            .WithName("UpdateClientEnvironment")
            .WithSummary("Atualiza um ambiente do cliente.")
            .Produces<ClientEnvironmentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        clients.MapPost("/{clientId:guid}/integrations", CreateIntegrationAsync)
            .WithName("CreateClientIntegration")
            .WithSummary("Cadastra uma integração em um ambiente do cliente.")
            .Produces<IntegrationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        clients.MapPut("/{clientId:guid}/integrations/{integrationId:guid}", UpdateIntegrationAsync)
            .WithName("UpdateClientIntegration")
            .WithSummary("Atualiza uma integração do cliente.")
            .Produces<IntegrationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetOverviewAsync(
        AdministrationDbContext database,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var scope = TenantScope.From(principal);
        if (!scope.IsValid)
        {
            return Results.Forbid();
        }

        var clients = database.Clients.AsNoTracking();
        var environments = database.Environments.AsNoTracking();
        var integrations = database.Integrations.AsNoTracking();
        if (scope.ClientId is { } clientId)
        {
            clients = clients.Where(client => client.Id == clientId);
            environments = environments.Where(environment => environment.ClientId == clientId);
            integrations = integrations.Where(integration => integration.ClientId == clientId);
        }

        var response = new AdministrationOverviewResponse(
            await clients.CountAsync(cancellationToken),
            await clients.CountAsync(client => client.IsActive, cancellationToken),
            await environments.CountAsync(cancellationToken),
            await environments.CountAsync(environment => environment.IsActive, cancellationToken),
            await integrations.CountAsync(cancellationToken),
            await integrations.CountAsync(integration => integration.IsActive, cancellationToken));

        return Results.Ok(response);
    }

    private static async Task<IResult> ListClientsAsync(
        AdministrationDbContext database,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var scope = TenantScope.From(principal);
        if (!scope.IsValid)
        {
            return Results.Forbid();
        }

        var query = database.Clients.AsNoTracking();
        if (scope.ClientId is { } clientId)
        {
            query = query.Where(client => client.Id == clientId);
        }

        var clients = await query
            .OrderBy(client => client.Name)
            .Select(client => new ClientListItemResponse(
                client.Id,
                client.Name,
                client.IsActive,
                database.Environments.Count(environment => environment.ClientId == client.Id),
                database.Integrations.Count(integration => integration.ClientId == client.Id)))
            .ToArrayAsync(cancellationToken);

        return Results.Ok(clients);
    }

    private static async Task<IResult> CreateClientAsync(
        CreateClientRequest request,
        AdministrationDbContext database,
        TimeProvider timeProvider,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var scope = TenantScope.From(principal);
        if (!scope.IsValid || scope.ClientId is not null)
        {
            return Results.Forbid();
        }

        var errors = ValidateClient(request.Name);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var client = new Client(request.Name, request.IsActive, timeProvider.GetUtcNow());
        database.Clients.Add(client);

        var failure = await SaveAsync(database, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = ToClientDetail(client, [], []);
        return Results.Created($"/api/administration/clients/{client.Id}", response);
    }

    private static async Task<IResult> GetClientAsync(
        Guid clientId,
        AdministrationDbContext database,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var access = EnsureAccess(principal, clientId);
        if (access is not null)
        {
            return access;
        }

        var response = await LoadClientDetailAsync(database, clientId, cancellationToken);
        return response is null ? Results.NotFound() : Results.Ok(response);
    }

    private static async Task<IResult> UpdateClientAsync(
        Guid clientId,
        UpdateClientRequest request,
        AdministrationDbContext database,
        TimeProvider timeProvider,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var access = EnsureAccess(principal, clientId);
        if (access is not null)
        {
            return access;
        }

        var errors = ValidateClient(request.Name);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var client = await database.Clients
            .SingleOrDefaultAsync(candidate => candidate.Id == clientId, cancellationToken);
        if (client is null)
        {
            return Results.NotFound();
        }

        client.Update(request.Name, request.IsActive, timeProvider.GetUtcNow());
        var failure = await SaveAsync(database, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await LoadClientDetailAsync(database, clientId, cancellationToken);
        return Results.Ok(response!);
    }

    private static async Task<IResult> CreateEnvironmentAsync(
        Guid clientId,
        CreateEnvironmentRequest request,
        AdministrationDbContext database,
        TimeProvider timeProvider,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var access = EnsureAccess(principal, clientId);
        if (access is not null)
        {
            return access;
        }

        var errors = ValidateEnvironment(request.Name, request.Kind, out var kind);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (!await database.Clients.AnyAsync(client => client.Id == clientId, cancellationToken))
        {
            return Results.NotFound();
        }

        var environment = new ClientEnvironment(
            clientId,
            request.Name,
            kind,
            request.IsActive,
            timeProvider.GetUtcNow());
        database.Environments.Add(environment);

        var failure = await SaveAsync(database, cancellationToken);
        return failure ?? Results.Created(
            $"/api/administration/clients/{clientId}/environments/{environment.Id}",
            ToEnvironmentResponse(environment));
    }

    private static async Task<IResult> UpdateEnvironmentAsync(
        Guid clientId,
        Guid environmentId,
        UpdateEnvironmentRequest request,
        AdministrationDbContext database,
        TimeProvider timeProvider,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var access = EnsureAccess(principal, clientId);
        if (access is not null)
        {
            return access;
        }

        var errors = ValidateEnvironment(request.Name, request.Kind, out var kind);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var environment = await database.Environments.SingleOrDefaultAsync(
            candidate => candidate.Id == environmentId && candidate.ClientId == clientId,
            cancellationToken);
        if (environment is null)
        {
            return Results.NotFound();
        }

        environment.Update(request.Name, kind, request.IsActive, timeProvider.GetUtcNow());
        var failure = await SaveAsync(database, cancellationToken);
        return failure ?? Results.Ok(ToEnvironmentResponse(environment));
    }

    private static async Task<IResult> CreateIntegrationAsync(
        Guid clientId,
        CreateIntegrationRequest request,
        AdministrationDbContext database,
        TimeProvider timeProvider,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var access = EnsureAccess(principal, clientId);
        if (access is not null)
        {
            return access;
        }

        var errors = ValidateIntegration(request, out var direction);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (!await EnvironmentBelongsToClientAsync(
                database,
                request.EnvironmentId,
                clientId,
                cancellationToken))
        {
            return Results.NotFound();
        }

        var integration = new Integration(
            clientId,
            request.EnvironmentId,
            request.Name,
            direction,
            request.SourceSystem,
            request.TargetSystem,
            request.HttpMethod,
            request.EndpointPattern,
            request.IsActive,
            timeProvider.GetUtcNow());
        database.Integrations.Add(integration);

        var failure = await SaveAsync(database, cancellationToken);
        return failure ?? Results.Created(
            $"/api/administration/clients/{clientId}/integrations/{integration.Id}",
            ToIntegrationResponse(integration));
    }

    private static async Task<IResult> UpdateIntegrationAsync(
        Guid clientId,
        Guid integrationId,
        UpdateIntegrationRequest request,
        AdministrationDbContext database,
        TimeProvider timeProvider,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var access = EnsureAccess(principal, clientId);
        if (access is not null)
        {
            return access;
        }

        var errors = ValidateIntegration(request, out var direction);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (!await EnvironmentBelongsToClientAsync(
                database,
                request.EnvironmentId,
                clientId,
                cancellationToken))
        {
            return Results.NotFound();
        }

        var integration = await database.Integrations.SingleOrDefaultAsync(
            candidate => candidate.Id == integrationId && candidate.ClientId == clientId,
            cancellationToken);
        if (integration is null)
        {
            return Results.NotFound();
        }

        integration.Update(
            request.EnvironmentId,
            request.Name,
            direction,
            request.SourceSystem,
            request.TargetSystem,
            request.HttpMethod,
            request.EndpointPattern,
            request.IsActive,
            timeProvider.GetUtcNow());

        var failure = await SaveAsync(database, cancellationToken);
        return failure ?? Results.Ok(ToIntegrationResponse(integration));
    }

    private static Task<bool> EnvironmentBelongsToClientAsync(
        AdministrationDbContext database,
        Guid environmentId,
        Guid clientId,
        CancellationToken cancellationToken)
    {
        return database.Environments.AsNoTracking().AnyAsync(
            environment => environment.Id == environmentId && environment.ClientId == clientId,
            cancellationToken);
    }

    private static async Task<ClientDetailResponse?> LoadClientDetailAsync(
        AdministrationDbContext database,
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var client = await database.Clients
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == clientId, cancellationToken);
        if (client is null)
        {
            return null;
        }

        var environmentEntities = await database.Environments
            .AsNoTracking()
            .Where(environment => environment.ClientId == clientId)
            .OrderBy(environment => environment.Name)
            .ToArrayAsync(cancellationToken);
        var environments = environmentEntities.Select(ToEnvironmentResponse).ToArray();
        var integrationEntities = await database.Integrations
            .AsNoTracking()
            .Where(integration => integration.ClientId == clientId)
            .OrderBy(integration => integration.Name)
            .ToArrayAsync(cancellationToken);
        var integrations = integrationEntities.Select(ToIntegrationResponse).ToArray();

        return ToClientDetail(client, environments, integrations);
    }

    private static Dictionary<string, string[]> ValidateClient(string name)
    {
        var errors = new Dictionary<string, string[]>();
        AddTextError(errors, nameof(name), name, 2, 150, "O nome do cliente");
        return errors;
    }

    private static Dictionary<string, string[]> ValidateEnvironment(
        string name,
        string kindValue,
        out EnvironmentKind kind)
    {
        var errors = new Dictionary<string, string[]>();
        AddTextError(errors, nameof(name), name, 2, 100, "O nome do ambiente");
        if (!TryParseNamedEnum(kindValue, out kind))
        {
            errors[nameof(kindValue)] =
                ["O tipo deve ser Production, Staging ou Development."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateIntegration(
        CreateIntegrationRequest request,
        out IntegrationDirection direction)
    {
        return ValidateIntegrationValues(
            request.EnvironmentId,
            request.Name,
            request.Direction,
            request.SourceSystem,
            request.TargetSystem,
            request.HttpMethod,
            request.EndpointPattern,
            out direction);
    }

    private static Dictionary<string, string[]> ValidateIntegration(
        UpdateIntegrationRequest request,
        out IntegrationDirection direction)
    {
        return ValidateIntegrationValues(
            request.EnvironmentId,
            request.Name,
            request.Direction,
            request.SourceSystem,
            request.TargetSystem,
            request.HttpMethod,
            request.EndpointPattern,
            out direction);
    }

    private static Dictionary<string, string[]> ValidateIntegrationValues(
        Guid environmentId,
        string name,
        string directionValue,
        string sourceSystem,
        string targetSystem,
        string? httpMethod,
        string? endpointPattern,
        out IntegrationDirection direction)
    {
        var errors = new Dictionary<string, string[]>();
        if (environmentId == Guid.Empty)
        {
            errors[nameof(environmentId)] = ["Informe um ambiente válido."];
        }

        AddTextError(errors, nameof(name), name, 2, 150, "O nome da integração");
        AddTextError(errors, nameof(sourceSystem), sourceSystem, 1, 150, "O sistema de origem");
        AddTextError(errors, nameof(targetSystem), targetSystem, 1, 150, "O sistema de destino");
        if (!TryParseNamedEnum(directionValue, out direction))
        {
            errors[nameof(directionValue)] =
                ["A direção deve ser Inbound, Outbound ou Bidirectional."];
        }

        if (!string.IsNullOrWhiteSpace(httpMethod) &&
            (httpMethod.Trim().Length > 16 || !httpMethod.Trim().All(char.IsAsciiLetter)))
        {
            errors[nameof(httpMethod)] = ["Informe um método HTTP válido com até 16 letras."];
        }

        if (endpointPattern?.Trim().Length > 500)
        {
            errors[nameof(endpointPattern)] = ["O padrão do endpoint deve ter até 500 caracteres."];
        }

        return errors;
    }

    private static void AddTextError(
        IDictionary<string, string[]> errors,
        string key,
        string? value,
        int minimumLength,
        int maximumLength,
        string label)
    {
        var length = value?.Trim().Length ?? 0;
        if (length < minimumLength || length > maximumLength)
        {
            errors[key] = [$"{label} deve conter entre {minimumLength} e {maximumLength} caracteres."];
        }
    }

    private static bool TryParseNamedEnum<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value) &&
            Enum.GetNames<TEnum>().Any(name =>
                string.Equals(name, value.Trim(), StringComparison.OrdinalIgnoreCase)) &&
            Enum.TryParse(value.Trim(), ignoreCase: true, out result);
    }

    private static async Task<IResult?> SaveAsync(
        AdministrationDbContext database,
        CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Alteração concorrente.",
                detail: "Os dados foram alterados por outra requisição. Atualize e tente novamente.");
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            database.ChangeTracker.Clear();
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Cadastro duplicado.",
                detail: "Já existe um cadastro com esse nome no mesmo contexto.");
        }
    }

    internal static bool IsUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
    }

    private static IResult? EnsureAccess(ClaimsPrincipal principal, Guid requestedClientId)
    {
        var scope = TenantScope.From(principal);
        if (!scope.IsValid)
        {
            return Results.Forbid();
        }

        return scope.ClientId is { } scopedClientId && scopedClientId != requestedClientId
            ? Results.NotFound()
            : null;
    }

    private static ClientDetailResponse ToClientDetail(
        Client client,
        IReadOnlyCollection<ClientEnvironmentResponse> environments,
        IReadOnlyCollection<IntegrationResponse> integrations)
    {
        return new ClientDetailResponse(
            client.Id,
            client.Name,
            client.IsActive,
            client.CreatedAtUtc,
            client.UpdatedAtUtc,
            environments,
            integrations);
    }

    private static ClientEnvironmentResponse ToEnvironmentResponse(ClientEnvironment environment)
    {
        return new ClientEnvironmentResponse(
            environment.Id,
            environment.ClientId,
            environment.Name,
            environment.Kind.ToString(),
            environment.IsActive,
            environment.CreatedAtUtc,
            environment.UpdatedAtUtc);
    }

    private static IntegrationResponse ToIntegrationResponse(Integration integration)
    {
        return new IntegrationResponse(
            integration.Id,
            integration.ClientId,
            integration.EnvironmentId,
            integration.Name,
            integration.Direction.ToString(),
            integration.SourceSystem,
            integration.TargetSystem,
            integration.HttpMethod,
            integration.EndpointPattern,
            integration.IsActive,
            integration.CreatedAtUtc,
            integration.UpdatedAtUtc);
    }

    internal readonly record struct TenantScope(bool IsValid, Guid? ClientId)
    {
        public static TenantScope From(ClaimsPrincipal principal)
        {
            var values = principal.FindAll(TenantClaimTypes.ClientId)
                .Select(claim => claim.Value)
                .ToArray();
            if (values.Length == 0)
            {
                return new TenantScope(true, null);
            }

            return values.Length == 1 && Guid.TryParse(values[0], out var clientId) &&
                clientId != Guid.Empty
                ? new TenantScope(true, clientId)
                : new TenantScope(false, null);
        }
    }
}
