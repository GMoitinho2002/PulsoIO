using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PulsoIO.Modules.Identity.Domain;
using PulsoIO.BuildingBlocks.Tenancy;

namespace PulsoIO.Modules.Identity.Authentication;

internal static class JwtBearerSetup
{
    public static void Configure(JwtBearerOptions bearerOptions, JwtOptions jwtOptions)
    {
        bearerOptions.MapInboundClaims = false;
        bearerOptions.SaveToken = false;
        bearerOptions.IncludeErrorDetails = false;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            RequireSignedTokens = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidTypes = ["JWT"],
            NameClaimType = IdentityConstants.NameClaim,
            RoleClaimType = IdentityConstants.RoleClaim
        };
        bearerOptions.Events = new JwtBearerEvents
        {
            OnTokenValidated = ValidateSecurityStampAsync
        };
    }

    private static async Task ValidateSecurityStampAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var subject = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var tokenSecurityStamp = principal?.FindFirst(IdentityConstants.SecurityStampClaim)?.Value;

        if (!Guid.TryParse(subject, out var userId) || string.IsNullOrWhiteSpace(tokenSecurityStamp))
        {
            context.Fail("Token inválido.");
            return;
        }

        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
        var clientDirectory = context.HttpContext.RequestServices.GetRequiredService<IClientDirectory>();
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null ||
            !AuthenticationEligibility.IsAllowed(user) ||
            !ClientClaimsMatch(
                user.ClientId,
                principal?.FindFirst(TenantClaimTypes.ClientId)?.Value) ||
            !await ClientAccessEligibility.IsAllowedAsync(
                user,
                clientDirectory,
                context.HttpContext.RequestAborted) ||
            await userManager.IsLockedOutAsync(user) ||
            !SecurityStampsMatch(user.SecurityStamp, tokenSecurityStamp))
        {
            context.Fail("Token inválido.");
        }
    }

    internal static bool SecurityStampsMatch(string? current, string? supplied)
    {
        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        var currentBytes = Encoding.UTF8.GetBytes(current);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);

        return currentBytes.Length == suppliedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(currentBytes, suppliedBytes);
    }

    internal static bool ClientClaimsMatch(Guid? currentClientId, string? suppliedClientId)
    {
        if (currentClientId is null)
        {
            return string.IsNullOrWhiteSpace(suppliedClientId);
        }

        return Guid.TryParse(suppliedClientId, out var parsedClientId) &&
            parsedClientId == currentClientId;
    }
}
