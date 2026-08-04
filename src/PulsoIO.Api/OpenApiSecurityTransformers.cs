using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

internal static class OpenApiSecurityTransformers
{
    private const string BearerSchemeName = "Bearer";
    private const string CsrfHeaderName = "X-Pulso-CSRF";
    private static readonly HashSet<string> CsrfProtectedOperationIds =
        new(["Login", "RefreshSession", "Logout"], StringComparer.Ordinal);

    public static Task AddBearerSchemeAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes[BearerSchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Informe somente o access token JWT, sem o prefixo Bearer."
        };

        return Task.CompletedTask;
    }

    public static Task AddBearerRequirementAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any();
        var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();

        if (!requiresAuthorization || allowsAnonymous)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSchemeName, context.Document)] = []
        });

        return Task.CompletedTask;
    }

    public static Task AddCsrfHeaderAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.OperationId is null ||
            !CsrfProtectedOperationIds.Contains(operation.OperationId))
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = CsrfHeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Proteção CSRF. O valor obrigatório é 1.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String
            }
        });

        return Task.CompletedTask;
    }
}
