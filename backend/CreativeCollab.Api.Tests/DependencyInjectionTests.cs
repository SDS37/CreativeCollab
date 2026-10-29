using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CreativeCollab.Api.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public async Task The_container_has_no_scoped_service_captured_by_a_singleton()
    {
        // Scope and build validation make the host fail to start if, say, the presence tracker took a scoped dependency.
        using var host = new ApiFactory(configureWebHost: b => b.UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = true;
            o.ValidateOnBuild = true;
        }));

        var response = await (await host.SignedInAsync()).GetAsync("/api/creatives");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
