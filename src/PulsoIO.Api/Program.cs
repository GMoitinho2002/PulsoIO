using PulsoIO.Modules.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Pulso I/O API";
        document.Info.Version = "v1";
        document.Info.Description = "API de monitoramento, diagnóstico e rastreabilidade de integrações.";

        return Task.CompletedTask;
    });
    options.AddDocumentTransformer(OpenApiSecurityTransformers.AddBearerSchemeAsync);
    options.AddOperationTransformer(OpenApiSecurityTransformers.AddBearerRequirementAsync);
    options.AddOperationTransformer(OpenApiSecurityTransformers.AddCsrfHeaderAsync);
});
builder.Services.AddIdentityModule(builder.Configuration);
PulsoIO.Modules.Administration.AdministrationModule.AddAdministrationModule(
    builder.Services,
    builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "Pulso I/O API";
        options.SwaggerEndpoint("/openapi/v1.json", "Pulso I/O API v1");
    });
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("healthy")))
    .WithName("GetHealth")
    .WithSummary("Obtém o estado da API.")
    .WithDescription("Confirma que o processo da API está disponível.")
    .WithTags("Health");

app.MapIdentityModule();
PulsoIO.Modules.Administration.AdministrationModule.MapAdministrationModule(app);

app.Run();

internal sealed record HealthResponse(string Status);
