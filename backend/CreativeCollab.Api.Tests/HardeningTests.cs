using System.Net;
using CreativeCollab.Api.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace CreativeCollab.Api.Tests;

public sealed class HardeningTests : IDisposable
{
    private readonly List<ApiFactory> _hosts = [];
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    private ApiFactory NewHost(Action<IServiceCollection>? services = null, Action<IWebHostBuilder>? web = null)
    {
        var host = new ApiFactory(s =>
        {
            s.AddSingleton<TimeProvider>(_time);
            services?.Invoke(s);
        }, web);
        _hosts.Add(host);
        return host;
    }

    public void Dispose() => _hosts.ForEach(h => h.Dispose());

    // Clock skew

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("00:00:30")]
    public void A_clock_skew_up_to_30_seconds_starts(string skew)
    {
        var host = NewHost(web: b => b.UseSetting("Jwt:ClockSkew", skew));

        host.CreateClient(); // starting the host validates the options
    }

    [Theory]
    [InlineData("00:00:31")]
    [InlineData("00:05:00")]
    [InlineData("-00:00:01")]
    public void A_clock_skew_over_30_seconds_or_negative_stops_the_host_from_starting(string skew)
    {
        var host = NewHost(web: b => b.UseSetting("Jwt:ClockSkew", skew));

        var failure = Assert.ThrowsAny<Exception>(() => host.CreateClient());

        Assert.Contains("ClockSkew", failure.ToString());
    }

    // Protected by default

    /// <summary>Adds a route that says nothing about authorization, as a future endpoint might.</summary>
    private sealed class UnlistedRouteFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            if (app.Properties.TryGetValue("__EndpointRouteBuilder", out var builder) && builder is IEndpointRouteBuilder routes)
                routes.MapGet("/api/unlisted", () => "secret");
        };
    }

    [Fact]
    public async Task A_route_nobody_listed_is_401_anonymously_and_200_when_signed_in()
    {
        var host = NewHost(s => s.AddTransient<IStartupFilter, UnlistedRouteFilter>());
        var anonymous = host.CreateClient();
        var editor = await host.SignedInAsync();

        var refused = await anonymous.GetAsync("/api/unlisted");
        var allowed = await editor.GetAsync("/api/unlisted");

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    // Token table housekeeping

    private static async Task<int> RefreshTokenCountAsync(ApiFactory host)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CreativeDbContext>().RefreshTokens.CountAsync();
    }

    [Fact]
    public async Task Long_expired_and_long_revoked_refresh_tokens_are_purged_when_someone_signs_in()
    {
        var host = NewHost();
        var client = host.CreateClient();
        var expiring = await client.RegisterAndSignInAsync("ana@example.com");
        var revoked = await client.RegisterAndSignInAsync("bo@example.com");
        await client.LogoutAsync(revoked.RefreshCookie);
        Assert.Equal(2, await RefreshTokenCountAsync(host));

        _time.Advance(TimeSpan.FromDays(14 + 8)); // past expiry and the retention period
        await client.AuthenticateAsync("cy@example.com");

        Assert.Equal(1, await RefreshTokenCountAsync(host)); // only Cy's new token is left
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshAsync(expiring.RefreshCookie)).StatusCode);
    }

    [Fact]
    public async Task Recently_revoked_or_expired_tokens_are_kept_for_now()
    {
        var host = NewHost();
        var client = host.CreateClient();
        await client.RegisterAndSignInAsync("ana@example.com");
        var revoked = await client.RegisterAndSignInAsync("bo@example.com");
        await client.LogoutAsync(revoked.RefreshCookie);

        _time.Advance(TimeSpan.FromDays(1));
        await client.AuthenticateAsync("cy@example.com");

        Assert.Equal(3, await RefreshTokenCountAsync(host));
    }

    [Fact]
    public async Task Old_tokens_are_also_purged_when_a_session_refreshes()
    {
        var host = NewHost();
        var client = host.CreateClient();
        await client.RegisterAndSignInAsync("ana@example.com"); // expires on day 14
        _time.Advance(TimeSpan.FromDays(20));
        var bo = await client.RegisterAndSignInAsync("bo@example.com"); // Ana's expired 6 days ago: inside retention, kept
        Assert.Equal(2, await RefreshTokenCountAsync(host));
        _time.Advance(TimeSpan.FromDays(10)); // Ana's expired 16 days ago, Bo's token is still valid

        var renewed = await client.RefreshAsync(bo.RefreshCookie);

        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        Assert.Equal(2, await RefreshTokenCountAsync(host)); // Bo's used token and his new one; Ana's is gone
    }
}
