using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PulsoIO.Modules.Identity.Authentication;
using PulsoIO.Modules.Identity.Domain;
using IdentityConstants = PulsoIO.Modules.Identity.Authentication.IdentityConstants;

namespace PulsoIO.Modules.Identity.Infrastructure;

internal sealed class InitialAdminBootstrapper(
    IServiceScopeFactory scopeFactory,
    IOptions<InitialAdminOptions> options,
    ILogger<InitialAdminBootstrapper> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configuredAdmin = options.Value;
        var hasAnyValue =
            !string.IsNullOrWhiteSpace(configuredAdmin.Name) ||
            !string.IsNullOrWhiteSpace(configuredAdmin.Email) ||
            !string.IsNullOrWhiteSpace(configuredAdmin.Password);

        if (!hasAnyValue)
        {
            logger.LogWarning(
                "O administrador inicial não foi configurado em {ConfigurationSection}; o bootstrap foi ignorado.",
                InitialAdminOptions.SectionName);
            return;
        }

        ValidateCompleteConfiguration(configuredAdmin);

        await using var scope = scopeFactory.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        await EnsureAdministratorRoleAsync(roleManager);

        var user = await userManager.FindByEmailAsync(configuredAdmin.Email);
        if (user is null)
        {
            var existingAdministrators = await userManager.GetUsersInRoleAsync(
                IdentityConstants.AdministratorRole);
            if (existingAdministrators.Any(candidate => candidate.IsActive))
            {
                logger.LogWarning(
                    "O e-mail configurado para o bootstrap não corresponde ao administrador ativo existente; a criação foi ignorada para evitar uma conta duplicada.");
                return;
            }

            user = new User(configuredAdmin.Name, configuredAdmin.Email)
            {
                EmailConfirmed = true
            };

            EnsureSucceeded(
                await userManager.CreateAsync(user, configuredAdmin.Password),
                "Não foi possível criar o administrador inicial.");
        }
        else
        {
            user.Rename(configuredAdmin.Name);
            user.EmailConfirmed = true;
            user.LockoutEnabled = true;
            EnsureSucceeded(
                await userManager.UpdateAsync(user),
                "Não foi possível atualizar o administrador inicial.");

            if (!await userManager.HasPasswordAsync(user))
            {
                EnsureSucceeded(
                    await userManager.AddPasswordAsync(user, configuredAdmin.Password),
                    "Não foi possível definir a senha do administrador inicial.");
            }
        }

        if (!await userManager.IsInRoleAsync(user, IdentityConstants.AdministratorRole))
        {
            EnsureSucceeded(
                await userManager.AddToRoleAsync(user, IdentityConstants.AdministratorRole),
                "Não foi possível atribuir o papel de administrador.");
            EnsureSucceeded(
                await userManager.UpdateSecurityStampAsync(user),
                "Não foi possível renovar a identidade do administrador.");
        }

        logger.LogInformation("Administrador inicial garantido com o identificador {UserId}.", user.Id);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static void ValidateCompleteConfiguration(InitialAdminOptions options)
    {
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Name))
        {
            missingFields.Add(nameof(options.Name));
        }

        if (string.IsNullOrWhiteSpace(options.Email))
        {
            missingFields.Add(nameof(options.Email));
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            missingFields.Add(nameof(options.Password));
        }

        if (missingFields.Count > 0)
        {
            throw new InvalidOperationException(
                $"Configuração incompleta em {InitialAdminOptions.SectionName}: {string.Join(", ", missingFields)}.");
        }
    }

    private static async Task EnsureAdministratorRoleAsync(
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        if (await roleManager.RoleExistsAsync(IdentityConstants.AdministratorRole))
        {
            return;
        }

        EnsureSucceeded(
            await roleManager.CreateAsync(new IdentityRole<Guid>(IdentityConstants.AdministratorRole)
            {
                Id = Guid.NewGuid()
            }),
            "Não foi possível criar o papel de administrador.");
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errorCodes = string.Join(", ", result.Errors.Select(error => error.Code));
        throw new InvalidOperationException($"{message} Códigos: {errorCodes}.");
    }
}
