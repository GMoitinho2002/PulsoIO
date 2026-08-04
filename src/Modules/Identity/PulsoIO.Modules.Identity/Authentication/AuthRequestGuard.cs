using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace PulsoIO.Modules.Identity.Authentication;

internal sealed class AuthRequestGuard(IConfiguration configuration)
{
    private readonly string _allowedOrigin =
        configuration["FrontendUrl"] ?? "http://localhost:4200";

    public bool IsValid(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(IdentityConstants.CsrfHeaderName, out var csrfHeader) ||
            csrfHeader.Count != 1 ||
            !string.Equals(csrfHeader[0], IdentityConstants.CsrfHeaderValue, StringComparison.Ordinal))
        {
            return false;
        }

        if (!request.Headers.TryGetValue("Origin", out var originHeader))
        {
            return true;
        }

        return originHeader.Count == 1 && AreSameOrigin(originHeader[0], _allowedOrigin);
    }

    private static bool AreSameOrigin(string? suppliedOrigin, string configuredOrigin)
    {
        return Uri.TryCreate(suppliedOrigin, UriKind.Absolute, out var supplied) &&
            Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var configured) &&
            string.Equals(supplied.Scheme, configured.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(supplied.Host, configured.Host, StringComparison.OrdinalIgnoreCase) &&
            supplied.Port == configured.Port;
    }
}
