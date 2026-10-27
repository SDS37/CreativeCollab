using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace CreativeCollab.Api.Tests;

public sealed class CorsTests
{
    private const string Angular = "http://localhost:4200";

    private static HttpRequestMessage ListFrom(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/creatives");
        request.Headers.Add("Origin", origin);
        return request;
    }

    [Fact]
    public async Task The_default_angular_origin_is_allowed_with_credentials()
    {
        using var host = new ApiFactory();

        var response = await host.CreateClient().SendAsync(ListFrom(Angular));

        Assert.Equal([Angular], response.Headers.GetValues("Access-Control-Allow-Origin"));
        Assert.Equal(["true"], response.Headers.GetValues("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task A_preflight_from_the_allowed_origin_is_answered_for_put()
    {
        using var host = new ApiFactory();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/creatives/" + Guid.NewGuid());
        request.Headers.Add("Origin", Angular);
        request.Headers.Add("Access-Control-Request-Method", "PUT");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await host.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([Angular], response.Headers.GetValues("Access-Control-Allow-Origin"));
        Assert.Contains("PUT", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
    }

    [Fact]
    public async Task A_disallowed_origin_gets_no_cors_headers()
    {
        using var host = new ApiFactory();

        var response = await host.CreateClient().SendAsync(ListFrom("http://evil.example"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Allowed_origins_come_from_configuration()
    {
        using var host = new ApiFactory(configureWebHost: b => b.UseSetting("Cors:AllowedOrigins:0", "https://creatives.example"));
        var client = host.CreateClient();

        var configured = await client.SendAsync(ListFrom("https://creatives.example"));
        var formerDefault = await client.SendAsync(ListFrom(Angular));

        Assert.Equal(["https://creatives.example"], configured.Headers.GetValues("Access-Control-Allow-Origin"));
        Assert.False(formerDefault.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
