using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using CreativeCollab.Api.Auth;
using CreativeCollab.Api.Data;
using CreativeCollab.Api.Services;

namespace CreativeCollab.Api.Tests;

/// <summary>
/// The API on a private in-memory SQLite database with the real migrations applied.
/// Runs as "Testing", so the Development-only seeder does not add Creatives.
/// </summary>
public sealed class ApiFactory(
    Action<IServiceCollection>? configureServices = null,
    Action<IWebHostBuilder>? configureWebHost = null,
    params IInterceptor[] interceptors) : WebApplicationFactory<Program>
{
    /// <summary>Hosting environment. "Testing" by default; "Development" also runs the migrate-and-seed step.</summary>
    public string Environment { get; init; } = "Testing";

    /// <summary>The network as the image check sees it. Defaults to serving a PNG for every URL.</summary>
    public StubHttpMessageHandler Images { get; init; } = StubHttpMessageHandler.Responding(HttpStatusCode.OK, "image/png");

    /// <summary>Host name resolution as the SSRF guard sees it. Unknown names resolve to a public address.</summary>
    public StubHostAddressResolver Resolver { get; } = new();

    // In-memory SQLite lives only as long as its connection, so one connection is held open for the host.
    private readonly SqliteConnection _connection = OpenConnection();

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CreativeDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CreativeDbContext>>();
            services.AddDbContext<CreativeDbContext>(o => o.UseSqlite(_connection).AddInterceptors(interceptors));
            // The image check talks to the stubbed network, and host names resolve through the stub resolver.
            services.AddHttpClient<IImageUrlValidator, ImageUrlValidator>()
                .ConfigurePrimaryHttpMessageHandler(() => Images);
            services.AddSingleton<IHostAddressResolver>(Resolver);
            // Every test host shares one client address, so the auth limit is lifted unless a test sets it.
            services.Configure<AuthRateLimitOptions>(o => o.PermitLimit = 10_000);
            configureServices?.Invoke(services);
        });
        configureWebHost?.Invoke(builder);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CreativeDbContext>().Database.Migrate();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

public sealed class StubHostAddressResolver : IHostAddressResolver
{
    private static readonly IPAddress Public = IPAddress.Parse("93.184.216.34");

    private readonly Dictionary<string, IPAddress[]> _hosts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["localhost"] = [IPAddress.Loopback, IPAddress.IPv6Loopback],
    };

    public StubHostAddressResolver Map(string host, params string[] addresses)
    {
        _hosts[host] = [.. addresses.Select(IPAddress.Parse)];
        return this;
    }

    public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IPAddress>>(_hosts.TryGetValue(host, out var found) ? found : [Public]);
}
