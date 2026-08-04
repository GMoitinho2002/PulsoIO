using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using PulsoIO.BuildingBlocks.Tenancy;
using PulsoIO.Modules.Identity.Authentication;
using PulsoIO.Modules.Identity.Domain;
using PulsoIO.Modules.Identity.Infrastructure;
using IdentityConstants = PulsoIO.Modules.Identity.Authentication.IdentityConstants;

namespace PulsoIO.Modules.Identity;

internal static class IdentityEndpoints
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/identity")
            .WithTags("Identity");

        var authGroup = group
            .MapGroup("/auth")
            .AddEndpointFilter<NoStoreEndpointFilter>();

        authGroup.MapPost("/login", LoginAsync)
            .RequireRateLimiting(IdentityConstants.LoginRateLimitPolicy)
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Autentica um usuário.")
            .WithDescription(
                $"Exige o cabeçalho {IdentityConstants.CsrfHeaderName}: {IdentityConstants.CsrfHeaderValue}.")
            .Produces<AuthSessionResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        authGroup.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithName("RefreshSession")
            .WithSummary("Renova a sessão com rotação do refresh token.")
            .WithDescription(
                $"Exige o cabeçalho {IdentityConstants.CsrfHeaderName}: {IdentityConstants.CsrfHeaderValue}.")
            .Produces<AuthSessionResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        authGroup.MapPost("/logout", LogoutAsync)
            .AllowAnonymous()
            .WithName("Logout")
            .WithSummary("Revoga o refresh token da sessão.")
            .WithDescription(
                $"Exige o cabeçalho {IdentityConstants.CsrfHeaderName}: {IdentityConstants.CsrfHeaderValue}.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        authGroup.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Obtém o usuário autenticado.")
            .Produces<AuthUserResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/users", ListUsersAsync)
            .AddEndpointFilter<NoStoreEndpointFilter>()
            .RequireAuthorization(IdentityConstants.AdministratorPolicy)
            .WithName("ListUsers")
            .WithSummary("Lista os usuários cadastrados.")
            .Produces<IReadOnlyCollection<UserSummaryResponse>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/users", CreateUserAsync)
            .AddEndpointFilter<NoStoreEndpointFilter>()
            .RequireAuthorization(IdentityConstants.AdministratorPolicy)
            .WithName("CreateUser")
            .WithSummary("Cadastra um usuário.")
            .Produces<UserSummaryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPut("/users/{userId:guid}/status", UpdateUserStatusAsync)
            .AddEndpointFilter<NoStoreEndpointFilter>()
            .RequireAuthorization(IdentityConstants.AdministratorPolicy)
            .WithName("UpdateUserStatus")
            .WithSummary("Ativa ou desativa um usuário.")
            .Produces<UserSummaryResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        var profileGroup = group
            .MapGroup("/profile")
            .AddEndpointFilter<NoStoreEndpointFilter>()
            .RequireAuthorization();

        profileGroup.MapGet("/photo", GetProfilePhotoAsync)
            .WithName("GetProfilePhoto")
            .WithSummary("Obtém a foto do perfil autenticado.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        profileGroup.MapPut("/photo", UpdateProfilePhotoAsync)
            .WithName("UpdateProfilePhoto")
            .WithSummary("Atualiza a foto do perfil autenticado.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status413PayloadTooLarge);

        profileGroup.MapDelete("/photo", DeleteProfilePhotoAsync)
            .WithName("DeleteProfilePhoto")
            .WithSummary("Remove a foto do perfil autenticado.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

        profileGroup.MapPut("/email", UpdateProfileEmailAsync)
            .RequireRateLimiting(IdentityConstants.LoginRateLimitPolicy)
            .WithName("UpdateProfileEmail")
            .WithSummary("Altera o e-mail de acesso e encerra todas as sessões.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);

        profileGroup.MapPut("/password", UpdateProfilePasswordAsync)
            .RequireRateLimiting(IdentityConstants.LoginRateLimitPolicy)
            .WithName("UpdateProfilePassword")
            .WithSummary("Altera a senha e encerra todas as sessões.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<User> userManager,
        IdentityDbContext database,
        TokenService tokenService,
        LoginAttemptService loginAttemptService,
        IClientDirectory clientDirectory,
        RefreshTokenCookieService cookieService,
        AuthRequestGuard requestGuard,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!requestGuard.IsValid(httpContext.Request))
        {
            return InvalidCsrfRequest();
        }

        var validationErrors = ValidateLogin(request);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            PasswordTimingEqualizer.Verify(request.Password);
            return Results.Unauthorized();
        }

        if (!AuthenticationEligibility.IsAllowed(user))
        {
            PasswordTimingEqualizer.Verify(request.Password);
            return Results.Unauthorized();
        }

        if (!await ClientAccessEligibility.IsAllowedAsync(user, clientDirectory, cancellationToken))
        {
            PasswordTimingEqualizer.Verify(request.Password);
            return Results.Unauthorized();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            PasswordTimingEqualizer.Verify(request.Password);
            return Results.Unauthorized();
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await loginAttemptService.RecordFailureAsync(user.Id, cancellationToken);
            return Results.Unauthorized();
        }

        if (!await loginAttemptService.ResetFailuresAsync(user.Id, cancellationToken))
        {
            return Results.Unauthorized();
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        var accessToken = tokenService.IssueAccessToken(user, roles);
        var refreshToken = tokenService.IssueRefreshToken(user);

        database.RefreshTokens.Add(refreshToken.Entity);
        await database.SaveChangesAsync(cancellationToken);
        cookieService.Append(httpContext.Response, refreshToken);

        var clientName = await GetClientNameAsync(user, clientDirectory, cancellationToken);
        return Results.Ok(ToSession(user, roles, accessToken, clientName));
    }

    private static async Task<IResult> RefreshAsync(
        UserManager<User> userManager,
        IdentityDbContext database,
        TokenService tokenService,
        RefreshTokenCookieService cookieService,
        AuthRequestGuard requestGuard,
        IClientDirectory clientDirectory,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!requestGuard.IsValid(httpContext.Request))
        {
            return InvalidCsrfRequest();
        }

        if (!TryGetRefreshToken(httpContext.Request, out var suppliedToken))
        {
            cookieService.Delete(httpContext.Response);
            return Results.Unauthorized();
        }

        var tokenHash = TokenService.HashRefreshToken(suppliedToken);
        var now = timeProvider.GetUtcNow();

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var currentToken = await database.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (currentToken is null)
        {
            cookieService.Delete(httpContext.Response);
            return Results.Unauthorized();
        }

        if (currentToken.RevokedAtUtc is not null || currentToken.ExpiresAtUtc <= now)
        {
            await RevokeFamilyAsync(database, currentToken.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            cookieService.Delete(httpContext.Response);
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(currentToken.UserId.ToString());
        if (user is null ||
            !AuthenticationEligibility.IsAllowed(user) ||
            !await ClientAccessEligibility.IsAllowedAsync(user, clientDirectory, cancellationToken) ||
            await userManager.IsLockedOutAsync(user) ||
            !JwtBearerSetup.SecurityStampsMatch(user.SecurityStamp, currentToken.SecurityStamp))
        {
            await RevokeFamilyAsync(database, currentToken.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            cookieService.Delete(httpContext.Response);
            return Results.Unauthorized();
        }

        var replacement = tokenService.IssueRefreshToken(
            user,
            currentToken.FamilyId,
            currentToken.ExpiresAtUtc);
        var affectedRows = await database.RefreshTokens
            .Where(token =>
                token.Id == currentToken.Id &&
                token.RevokedAtUtc == null &&
                token.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAtUtc, now)
                    .SetProperty(token => token.ReplacedByTokenHash, replacement.Entity.TokenHash),
                cancellationToken);

        if (affectedRows != 1)
        {
            await RevokeFamilyAsync(database, currentToken.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            cookieService.Delete(httpContext.Response);
            return Results.Unauthorized();
        }

        database.RefreshTokens.Add(replacement.Entity);
        await database.SaveChangesAsync(cancellationToken);

        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        var accessToken = tokenService.IssueAccessToken(user, roles);

        await transaction.CommitAsync(cancellationToken);
        cookieService.Append(httpContext.Response, replacement);

        var clientName = await GetClientNameAsync(user, clientDirectory, cancellationToken);
        return Results.Ok(ToSession(user, roles, accessToken, clientName));
    }

    private static async Task<IResult> LogoutAsync(
        IdentityDbContext database,
        RefreshTokenCookieService cookieService,
        AuthRequestGuard requestGuard,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!requestGuard.IsValid(httpContext.Request))
        {
            return InvalidCsrfRequest();
        }

        if (!TryGetRefreshToken(httpContext.Request, out var suppliedToken))
        {
            cookieService.Delete(httpContext.Response);
            return Results.NoContent();
        }

        var tokenHash = TokenService.HashRefreshToken(suppliedToken);
        var now = timeProvider.GetUtcNow();

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var currentToken = await database.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (currentToken is not null)
        {
            var affectedRows = await database.RefreshTokens
                .Where(token => token.Id == currentToken.Id && token.RevokedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(token => token.RevokedAtUtc, now),
                    cancellationToken);

            if (affectedRows != 1)
            {
                await RevokeFamilyAsync(database, currentToken.FamilyId, now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        cookieService.Delete(httpContext.Response);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        UserManager<User> userManager,
        IClientDirectory clientDirectory,
        HttpContext httpContext)
    {
        var subject = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null ||
            !AuthenticationEligibility.IsAllowed(user) ||
            !await ClientAccessEligibility.IsAllowedAsync(
                user,
                clientDirectory,
                httpContext.RequestAborted))
        {
            return Results.Unauthorized();
        }

        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        var clientName = await GetClientNameAsync(
            user,
            clientDirectory,
            httpContext.RequestAborted);
        return Results.Ok(ToAuthUser(user, roles, clientName));
    }

    private static async Task<IResult> ListUsersAsync(
        IdentityDbContext database,
        IClientDirectory clientDirectory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var clientScope = GetClientScope(httpContext.User);
        var query = database.Users.AsNoTracking();
        if (clientScope is Guid clientId)
        {
            query = query.Where(user => user.ClientId == clientId);
        }

        var users = await query
            .AsNoTracking()
            .OrderBy(user => user.Name)
            .Select(user => new
            {
                user.Id,
                user.Name,
                Email = user.Email ?? string.Empty,
                user.IsActive,
                user.ClientId,
                HasProfilePhoto = user.ProfilePhoto != null
            })
            .ToArrayAsync(cancellationToken);

        var clientIds = users
            .Where(user => user.ClientId.HasValue)
            .Select(user => user.ClientId!.Value)
            .Distinct()
            .ToArray();
        var clientNames = await clientDirectory.GetNamesAsync(clientIds, cancellationToken);

        return Results.Ok(users.Select(user => new UserSummaryResponse(
            user.Id,
            user.Name,
            user.Email,
            user.IsActive,
            user.ClientId,
            user.ClientId is Guid clientId ? clientNames.GetValueOrDefault(clientId) : null,
            user.ClientId is null,
            user.HasProfilePhoto)).ToArray());
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        UserManager<User> userManager,
        IClientDirectory clientDirectory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = ValidateNewUser(request);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var assignment = await ValidateClientAssignmentAsync(
            httpContext.User,
            request.ClientId,
            clientDirectory,
            cancellationToken);
        if (assignment is ClientAssignmentValidation.OutsideScope)
        {
            return Results.Forbid();
        }

        if (assignment is ClientAssignmentValidation.InactiveOrMissing)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.ClientId)] = ["Selecione um cliente ativo."]
            });
        }

        var user = new User(request.Name, request.Email, request.IsActive, request.ClientId)
        {
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return Results.ValidationProblem(ToValidationErrors(result));
        }

        var clientName = await GetClientNameAsync(user, clientDirectory, cancellationToken);
        var response = ToUserSummary(user, clientName);
        return Results.Created($"/api/identity/users/{user.Id}", response);
    }

    private static async Task<IResult> UpdateUserStatusAsync(
        Guid userId,
        UpdateUserStatusRequest request,
        UserAdministrationService administrationService,
        IdentityDbContext database,
        IClientDirectory clientDirectory,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var subject = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!Guid.TryParse(subject, out _))
        {
            return Results.Unauthorized();
        }

        if (IsSelfDeactivation(userId, request.IsActive, httpContext.User))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Operação de status não permitida.",
                detail: "Não é permitido desativar a própria conta.");
        }

        var targetClientId = await database.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.ClientId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!CanManageClient(httpContext.User, targetClientId))
        {
            return Results.NotFound();
        }

        var result = await administrationService.SetActiveStatusAsync(
            userId,
            request.IsActive,
            cancellationToken);
        if (result.HasConflict)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Operação de status não permitida.",
                detail: GetStatusConflictDetail(result.ConflictKind));
        }

        if (!result.WasFound)
        {
            return Results.NotFound();
        }

        if (!result.IdentityResult.Succeeded)
        {
            return Results.ValidationProblem(ToValidationErrors(result.IdentityResult));
        }

        var clientName = await GetClientNameAsync(result.User!, clientDirectory, cancellationToken);
        return Results.Ok(ToUserSummary(result.User!, clientName));
    }

    private static async Task<IResult> GetProfilePhotoAsync(
        UserManager<User> userManager,
        HttpContext httpContext)
    {
        if (!TryGetAuthenticatedUserId(httpContext.User, out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user?.ProfilePhoto is not { Length: > 0 } photo ||
            string.IsNullOrWhiteSpace(user.ProfilePhotoContentType))
        {
            return Results.NotFound();
        }

        return Results.File(photo, user.ProfilePhotoContentType);
    }

    private static async Task<IResult> UpdateProfilePhotoAsync(
        UserProfileService profileService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(httpContext.User, out var userId))
        {
            return Results.Unauthorized();
        }

        var contentType = httpContext.Request.ContentType?.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (httpContext.Request.ContentLength is > ProfilePhotoValidator.MaximumSizeBytes)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "A foto excede o limite permitido.");
        }

        var content = await ReadBodyAsync(
            httpContext.Request.Body,
            ProfilePhotoValidator.MaximumSizeBytes + 1,
            cancellationToken);
        if (content.Length > ProfilePhotoValidator.MaximumSizeBytes)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "A foto excede o limite permitido.");
        }

        if (!ProfilePhotoValidator.IsSupported(contentType, content))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["photo"] = ["Envie uma imagem JPEG, PNG ou WebP válida."]
            });
        }

        var result = await profileService.SetPhotoAsync(userId, content, contentType!);
        if (result is null)
        {
            return Results.Unauthorized();
        }

        return result.Succeeded
            ? Results.NoContent()
            : Results.ValidationProblem(ToValidationErrors(result));
    }

    private static async Task<IResult> DeleteProfilePhotoAsync(
        UserProfileService profileService,
        HttpContext httpContext)
    {
        if (!TryGetAuthenticatedUserId(httpContext.User, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await profileService.RemovePhotoAsync(userId);
        if (result is null)
        {
            return Results.Unauthorized();
        }

        return result.Succeeded
            ? Results.NoContent()
            : Results.ValidationProblem(ToValidationErrors(result));
    }

    private static async Task<IResult> UpdateProfileEmailAsync(
        UpdateProfileEmailRequest request,
        UserProfileService profileService,
        RefreshTokenCookieService cookieService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(httpContext.User, out var userId))
        {
            return Results.Unauthorized();
        }

        var errors = ValidateEmailUpdate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await profileService.UpdateEmailAsync(
            userId,
            request.Email,
            request.CurrentPassword,
            cancellationToken);
        if (result.HasConflict)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "O perfil foi alterado por outra requisição.",
                detail: "Atualize os dados e tente novamente.");
        }

        if (!result.WasFound)
        {
            return Results.Unauthorized();
        }

        if (!result.IdentityResult.Succeeded)
        {
            return Results.ValidationProblem(ToValidationErrors(result.IdentityResult));
        }

        cookieService.Delete(httpContext.Response);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateProfilePasswordAsync(
        UpdateProfilePasswordRequest request,
        UserProfileService profileService,
        RefreshTokenCookieService cookieService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(httpContext.User, out var userId))
        {
            return Results.Unauthorized();
        }

        var errors = ValidatePasswordUpdate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await profileService.UpdatePasswordAsync(
            userId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);
        if (result.HasConflict)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "O perfil foi alterado por outra requisição.",
                detail: "Atualize os dados e tente novamente.");
        }

        if (!result.WasFound)
        {
            return Results.Unauthorized();
        }

        if (!result.IdentityResult.Succeeded)
        {
            return Results.ValidationProblem(ToValidationErrors(result.IdentityResult));
        }

        cookieService.Delete(httpContext.Response);
        return Results.NoContent();
    }

    private static Dictionary<string, string[]> ValidateLogin(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email) ||
            request.Email.Length > 320 ||
            !new EmailAddressAttribute().IsValid(request.Email))
        {
            errors[nameof(request.Email)] = ["Informe um e-mail válido."];
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 128)
        {
            errors[nameof(request.Password)] = ["Informe uma senha válida."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateNewUser(CreateUserRequest request)
    {
        var errors = ValidateLogin(new LoginRequest(request.Email, request.Password));

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 150)
        {
            errors[nameof(request.Name)] = ["O nome deve conter entre 2 e 150 caracteres."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateEmailUpdate(UpdateProfileEmailRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email) ||
            request.Email.Length > 320 ||
            !new EmailAddressAttribute().IsValid(request.Email))
        {
            errors[nameof(request.Email)] = ["Informe um e-mail válido."];
        }

        if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 128)
        {
            errors[nameof(request.CurrentPassword)] = ["Informe a senha atual."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidatePasswordUpdate(
        UpdateProfilePasswordRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 128)
        {
            errors[nameof(request.CurrentPassword)] = ["Informe a senha atual."];
        }

        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length > 128)
        {
            errors[nameof(request.NewPassword)] = ["Informe uma nova senha válida."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ToValidationErrors(IdentityResult result)
    {
        return result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray());
    }

    private static AuthSessionResponse ToSession(
        User user,
        IReadOnlyCollection<string> roles,
        IssuedAccessToken accessToken,
        string? clientName)
    {
        return new AuthSessionResponse(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            ToAuthUser(user, roles, clientName));
    }

    private static AuthUserResponse ToAuthUser(
        User user,
        IReadOnlyCollection<string> roles,
        string? clientName)
    {
        return new AuthUserResponse(
            user.Id,
            user.Name,
            user.Email ?? string.Empty,
            roles,
            user.ClientId,
            clientName,
            user.ClientId is null,
            user.ProfilePhoto is { Length: > 0 });
    }

    private static UserSummaryResponse ToUserSummary(User user, string? clientName)
    {
        return new UserSummaryResponse(
            user.Id,
            user.Name,
            user.Email ?? string.Empty,
            user.IsActive,
            user.ClientId,
            clientName,
            user.ClientId is null,
            user.ProfilePhoto is { Length: > 0 });
    }

    private static async Task<string?> GetClientNameAsync(
        User user,
        IClientDirectory clientDirectory,
        CancellationToken cancellationToken)
    {
        if (user.ClientId is not Guid clientId)
        {
            return null;
        }

        var names = await clientDirectory.GetNamesAsync([clientId], cancellationToken);
        return names.GetValueOrDefault(clientId);
    }

    private static Guid? GetClientScope(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(TenantClaimTypes.ClientId)?.Value;
        return Guid.TryParse(value, out var clientId) ? clientId : null;
    }

    internal static bool CanManageClient(ClaimsPrincipal principal, Guid? targetClientId)
    {
        var claim = principal.FindFirst(TenantClaimTypes.ClientId);
        if (claim is null)
        {
            return true;
        }

        return Guid.TryParse(claim.Value, out var clientId) && clientId == targetClientId;
    }

    internal static async Task<ClientAssignmentValidation> ValidateClientAssignmentAsync(
        ClaimsPrincipal principal,
        Guid? targetClientId,
        IClientDirectory clientDirectory,
        CancellationToken cancellationToken)
    {
        if (!CanManageClient(principal, targetClientId))
        {
            return ClientAssignmentValidation.OutsideScope;
        }

        if (targetClientId is Guid clientId &&
            !await clientDirectory.ExistsActiveAsync(clientId, cancellationToken))
        {
            return ClientAssignmentValidation.InactiveOrMissing;
        }

        return ClientAssignmentValidation.Valid;
    }

    private static bool TryGetAuthenticatedUserId(
        ClaimsPrincipal principal,
        out Guid userId)
    {
        return Guid.TryParse(
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            out userId);
    }

    private static async Task<byte[]> ReadBodyAsync(
        Stream body,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var destination = new MemoryStream(Math.Min(maximumBytes, 64 * 1024));
        var buffer = new byte[64 * 1024];
        while (destination.Length < maximumBytes)
        {
            var remaining = maximumBytes - (int)destination.Length;
            var read = await body.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return destination.ToArray();
    }

    internal static bool IsSelfDeactivation(
        Guid targetUserId,
        bool isActive,
        ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return !isActive && Guid.TryParse(subject, out var authenticatedUserId) &&
            authenticatedUserId == targetUserId;
    }

    internal static string GetStatusConflictDetail(UserStatusConflictKind conflictKind)
    {
        return conflictKind is UserStatusConflictKind.ConcurrentUpdate
            ? "O status foi alterado por outra requisição. Atualize os dados e tente novamente."
            : "É necessário manter pelo menos um administrador ativo.";
    }

    private static bool TryGetRefreshToken(HttpRequest request, out string value)
    {
        if (request.Cookies.TryGetValue(IdentityConstants.RefreshTokenCookieName, out var token) &&
            !string.IsNullOrWhiteSpace(token) &&
            token.Length <= 256)
        {
            value = token;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static Task<int> RevokeFamilyAsync(
        IdentityDbContext database,
        Guid familyId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken)
    {
        return database.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAtUtc, revokedAtUtc),
                cancellationToken);
    }

    private static IResult InvalidCsrfRequest()
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [IdentityConstants.CsrfHeaderName] = ["Requisição de sessão inválida."]
        });
    }
}

internal enum ClientAssignmentValidation
{
    Valid,
    OutsideScope,
    InactiveOrMissing
}
