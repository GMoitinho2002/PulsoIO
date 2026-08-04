using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PulsoIO.Modules.Identity.Authentication;
using PulsoIO.Modules.Identity.Domain;
using PulsoIO.Modules.Identity.Infrastructure;
using Xunit;

namespace PulsoIO.Modules.Identity.Tests;

public sealed class IdentityPolicyTests
{
    [Fact]
    public void UserStatusDefaultsToActiveAndCanBeChangedExplicitly()
    {
        var user = new User("Operador", "operador@example.com");

        Assert.True(user.IsActive);
        Assert.False(user.SetActiveStatus(true));
        Assert.True(user.SetActiveStatus(false));
        Assert.False(user.IsActive);
        Assert.False(AuthenticationEligibility.IsAllowed(user));
        Assert.True(user.SetActiveStatus(true));
        Assert.True(AuthenticationEligibility.IsAllowed(user));

        var inactiveUser = new User("Inativo", "inativo@example.com", isActive: false);
        Assert.False(inactiveUser.IsActive);
    }

    [Fact]
    public void PasswordOptionsMatchTheAdministrationContract()
    {
        var options = new IdentityOptions();

        IdentityModule.ConfigureIdentityOptions(options);

        Assert.Equal(6, options.Password.RequiredLength);
        Assert.Equal(1, options.Password.RequiredUniqueChars);
        Assert.False(options.Password.RequireDigit);
        Assert.True(options.Password.RequireLowercase);
        Assert.True(options.Password.RequireUppercase);
        Assert.True(options.Password.RequireNonAlphanumeric);
    }

    [Theory]
    [InlineData("Abc de", false)]
    [InlineData("Abc\u0301de", false)]
    [InlineData("Abc_de", true)]
    [InlineData("Abc€de", true)]
    [InlineData("Abcde😀", true)]
    public async Task SpecialCharacterValidatorRejectsWhitespaceAsTheOnlySeparator(
        string password,
        bool expectedSuccess)
    {
        var validator = new SpecialCharacterPasswordValidator();
        var user = new User("Operador", "operador@example.com");

        var result = await validator.ValidateAsync(null!, user, password);

        Assert.Equal(expectedSuccess, result.Succeeded);
    }

    [Fact]
    public void SelfDeactivationUsesTheAuthenticatedSubject()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
            "test"));

        Assert.True(IdentityEndpoints.IsSelfDeactivation(userId, isActive: false, principal));
        Assert.False(IdentityEndpoints.IsSelfDeactivation(userId, isActive: true, principal));
        Assert.False(IdentityEndpoints.IsSelfDeactivation(Guid.NewGuid(), isActive: false, principal));
    }

    [Fact]
    public void StatusConflictDetailsDistinguishConcurrencyFromAdministratorInvariant()
    {
        var concurrencyDetail = IdentityEndpoints.GetStatusConflictDetail(
            UserStatusConflictKind.ConcurrentUpdate);
        var administratorDetail = IdentityEndpoints.GetStatusConflictDetail(
            UserStatusConflictKind.LastActiveAdministrator);

        Assert.Contains("outra requisição", concurrencyDetail, StringComparison.Ordinal);
        Assert.Contains("administrador ativo", administratorDetail, StringComparison.Ordinal);
        Assert.NotEqual(concurrencyDetail, administratorDetail);
    }
}
