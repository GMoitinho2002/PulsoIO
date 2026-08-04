using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsoIO.BuildingBlocks.Tenancy;
using PulsoIO.Modules.Administration.Infrastructure;

namespace PulsoIO.Modules.Administration;

public static class AdministrationModule
{
    public static IServiceCollection AddAdministrationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AdministrationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Database"),
                npgsql => npgsql.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    "administration")));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IClientDirectory, AdministrationClientDirectory>();

        return services;
    }

    public static IEndpointRouteBuilder MapAdministrationModule(
        this IEndpointRouteBuilder endpoints)
    {
        return AdministrationEndpoints.Map(endpoints);
    }
}
