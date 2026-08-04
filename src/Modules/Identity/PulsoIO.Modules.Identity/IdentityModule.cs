using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PulsoIO.Modules.Identity.Authentication;
using PulsoIO.Modules.Identity.Domain;
using PulsoIO.Modules.Identity.Infrastructure;
using IdentityConstants = PulsoIO.Modules.Identity.Authentication.IdentityConstants;

namespace PulsoIO.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration
            .GetRequiredSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();
        JwtOptions.Validate(jwtOptions);

        services.AddSingleton<IOptions<JwtOptions>>(Options.Create(jwtOptions));
        services.Configure<InitialAdminOptions>(
            configuration.GetSection(InitialAdminOptions.SectionName));
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Database"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));

        services
            .AddIdentityCore<User>(options =>
            {
                ConfigureIdentityOptions(options);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddPasswordValidator<SpecialCharacterPasswordValidator>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => JwtBearerSetup.Configure(options, jwtOptions));
        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                IdentityConstants.AdministratorPolicy,
                policy => policy.RequireRole(IdentityConstants.AdministratorRole));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = static (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };
            options.AddPolicy(
                IdentityConstants.LoginRateLimitPolicy,
                httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    GetRemoteAddress(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 10,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });

        services.AddScoped<TokenService>();
        services.AddScoped<LoginAttemptService>();
        services.AddScoped<RefreshTokenCookieService>();
        services.AddScoped<AuthRequestGuard>();
        services.AddScoped<UserAdministrationService>();
        services.AddScoped<UserProfileService>();
        services.AddHostedService<InitialAdminBootstrapper>();

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder endpoints)
    {
        return IdentityEndpoints.Map(endpoints);
    }

    internal static void ConfigureIdentityOptions(IdentityOptions options)
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 6;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    }

    private static string GetRemoteAddress(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();
    }
}
