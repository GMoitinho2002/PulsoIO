using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PulsoIO.Modules.Administration.Infrastructure;
using Xunit;

namespace PulsoIO.Modules.Administration.Tests;

public sealed class AdministrationEndpointContractTests
{
    [Fact]
    public void MapsEveryAdministrativeRouteWithAdminPolicy()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<AdministrationDbContext>(_ => null!);
        builder.Services.AddSingleton(TimeProvider.System);
        var app = builder.Build();

        AdministrationEndpoints.Map(app);

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
        var patterns = routes
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(8, routes.Length);
        Assert.Contains("/api/administration/clients/", patterns);
        Assert.Contains("/api/administration/clients/{clientId:guid}", patterns);
        Assert.Contains(
            "/api/administration/clients/{clientId:guid}/environments",
            patterns);
        Assert.Contains(
            "/api/administration/clients/{clientId:guid}/environments/{environmentId:guid}",
            patterns);
        Assert.Contains(
            "/api/administration/clients/{clientId:guid}/integrations",
            patterns);
        Assert.Contains(
            "/api/administration/clients/{clientId:guid}/integrations/{integrationId:guid}",
            patterns);
        Assert.All(routes, route => Assert.Contains(
            route.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            authorization => authorization.Policy == "Admin"));
    }
}
