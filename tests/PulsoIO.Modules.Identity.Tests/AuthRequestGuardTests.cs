using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PulsoIO.Modules.Identity.Authentication;
using Xunit;

namespace PulsoIO.Modules.Identity.Tests;

public sealed class AuthRequestGuardTests
{
    [Fact]
    public void AcceptsRequiredHeaderFromConfiguredOrigin()
    {
        var request = CreateRequest("http://localhost:4200", includeCsrfHeader: true);

        Assert.True(CreateGuard().IsValid(request));
    }

    [Fact]
    public void RejectsMissingCustomHeader()
    {
        var request = CreateRequest("http://localhost:4200", includeCsrfHeader: false);

        Assert.False(CreateGuard().IsValid(request));
    }

    [Fact]
    public void RejectsDifferentOriginEvenWithCustomHeader()
    {
        var request = CreateRequest("https://attacker.example", includeCsrfHeader: true);

        Assert.False(CreateGuard().IsValid(request));
    }

    private static AuthRequestGuard CreateGuard()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FrontendUrl"] = "http://localhost:4200"
            })
            .Build();

        return new AuthRequestGuard(configuration);
    }

    private static HttpRequest CreateRequest(string origin, bool includeCsrfHeader)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Origin = origin;

        if (includeCsrfHeader)
        {
            request.Headers["X-Pulso-CSRF"] = "1";
        }

        return request;
    }
}
