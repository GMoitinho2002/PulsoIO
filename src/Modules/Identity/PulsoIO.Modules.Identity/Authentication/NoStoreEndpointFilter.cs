using Microsoft.AspNetCore.Http;

namespace PulsoIO.Modules.Identity.Authentication;

internal sealed class NoStoreEndpointFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.HttpContext.Response.Headers.Pragma = "no-cache";

        return next(context);
    }
}
