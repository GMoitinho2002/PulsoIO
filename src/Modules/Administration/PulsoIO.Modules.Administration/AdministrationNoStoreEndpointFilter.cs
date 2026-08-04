using Microsoft.AspNetCore.Http;

namespace PulsoIO.Modules.Administration;

internal sealed class AdministrationNoStoreEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.HttpContext.Response.Headers.Pragma = "no-cache";
        return await next(context);
    }
}
