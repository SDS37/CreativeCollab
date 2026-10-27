using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using CreativeCollab.Api.Data;

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
