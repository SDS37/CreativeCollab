using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CreativeCollab.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CreativeCollab.Api.Tests;

public sealed class IdentityBootstrapTests : IDisposable
{
    private const string AdminEmail = "admin@example.com";
    private const string AdminPassword = "an admin passphrase";
    private const string InitialCreate = "20261027135314_InitialCreate";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"creativecollab-{Guid.NewGuid():N}.db");
    private string ConnectionString => $"Data Source={_dbPath};Pooling=False";

    public void Dispose()
    {
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>A Development host on the database file, with the first Admin configured.</summary>
    private WebApplicationFactory<Program> StartHost(bool configureAdmin = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:Creatives", ConnectionString);
            if (!configureAdmin) return;
            b.UseSetting("Admin:Email", AdminEmail);
            b.UseSetting("Admin:Password", AdminPassword);
            b.UseSetting("Admin:DisplayName", "First Admin");
        });

    private static async Task<JsonElement> MeAsync(HttpClient client, string email, string password)
    {
        var login = await client.LoginAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await AuthClient.ReadSessionAsync(login);
        return await (await client.MeAsync(session.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task The_first_admin_comes_from_configuration_and_signs_in_as_admin()
    {
        await using var host = StartHost();

        var me = await MeAsync(host.CreateClient(), AdminEmail, AdminPassword);

        Assert.Equal("Admin", me.GetProperty("role").GetString());
        Assert.Equal("First Admin", me.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Restarting_with_the_same_configuration_keeps_one_admin_who_can_still_sign_in()
    {
        await using (var first = StartHost())
            await MeAsync(first.CreateClient(), AdminEmail, AdminPassword);

        await using var second = StartHost();

        var me = await MeAsync(second.CreateClient(), AdminEmail, AdminPassword);
        Assert.Equal("Admin", me.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Without_admin_configuration_nobody_is_an_admin_and_registration_still_gives_editors()
    {
        await using var host = StartHost(configureAdmin: false);
        var client = host.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(AdminEmail, AdminPassword)).StatusCode);
        await client.RegisterAsync("ana@example.com");
        Assert.Equal("Editor", (await MeAsync(client, "ana@example.com", AuthClient.Password)).GetProperty("role").GetString());
    }

    [Fact]
    public async Task A_database_from_before_accounts_upgrades_in_place_and_keeps_its_creatives()
    {
        var id = Guid.NewGuid();
        await CreateDatabaseAsItWasBeforeAccountsAsync(id);

        await using var host = StartHost();
        var client = host.CreateClient();

        var creative = await client.GetFromJsonAsync<JsonElement>($"/api/creatives/{id}");
        Assert.Equal("Old campaign", creative.GetProperty("name").GetString());
        Assert.Equal("Ana (typed name)", creative.GetProperty("updatedBy").GetString());
        Assert.Equal(7, creative.GetProperty("version").GetInt32());
        var list = await client.GetFromJsonAsync<JsonElement>("/api/creatives");
        Assert.Equal(1, list.GetArrayLength()); // not seeded: the table was not empty
        Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync("ana@example.com")).StatusCode);
    }

    private async Task CreateDatabaseAsItWasBeforeAccountsAsync(Guid id)
    {
        var options = new DbContextOptionsBuilder<CreativeDbContext>().UseSqlite(ConnectionString).Options;
        await using (var db = new CreativeDbContext(options))
            db.GetService<IMigrator>().Migrate(InitialCreate);

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO Creatives (Id, Name, Headline, Body, Cta, ImageUrl, Version, UpdatedAt, UpdatedBy)
            VALUES ($id, 'Old campaign', 'Headline', 'Body', 'Go', NULL, 7, 637000000000000000, 'Ana (typed name)')
            """;
        insert.Parameters.AddWithValue("$id", id.ToString().ToUpperInvariant());
        await insert.ExecuteNonQueryAsync();
    }
}
