using Microsoft.AspNetCore.Hosting;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CreativeCollab.Api.Tests;

public sealed class PersistenceTests : IDisposable
{
    private const string Email = "ana@example.com";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"creativecollab-{Guid.NewGuid():N}.db");

    /// <summary>A Development host on a real database file, as <c>dotnet run</c> would start.</summary>
    private WebApplicationFactory<Program> StartHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseEnvironment("Development")
            .UseSetting("ConnectionStrings:Creatives", $"Data Source={_dbPath};Pooling=False"));

    public void Dispose()
    {
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            if (File.Exists(path)) File.Delete(path);
    }

    [Fact]
    public async Task A_created_creative_survives_a_restart()
    {
        Guid id;
        await using (var first = StartHost())
        {
            var response = await (await first.CreateClient().AuthenticateAsync(Email, displayName: "Ana")).PostAsJsonAsync("/api/creatives",
                new { name = "Survivor", headline = "h", body = "b", cta = "c" });
            id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        await using var second = StartHost();
        var fetched = await (await second.CreateClient().AuthenticateAsync(Email, register: false)).GetFromJsonAsync<JsonElement>($"/api/creatives/{id}");

        Assert.Equal("Survivor", fetched.GetProperty("name").GetString());
        Assert.Equal("Ana", fetched.GetProperty("updatedBy").GetString());
    }

    [Fact]
    public async Task An_empty_database_is_seeded_with_creatives_that_have_no_updated_by()
    {
        await using var host = StartHost();

        var list = await (await host.CreateClient().AuthenticateAsync(Email)).GetFromJsonAsync<JsonElement>("/api/creatives");

        Assert.True(list.GetArrayLength() > 0);
        foreach (var creative in list.EnumerateArray())
        {
            Assert.Equal("", creative.GetProperty("updatedBy").GetString());
            Assert.Equal(1, creative.GetProperty("version").GetInt32());
        }
    }

    [Fact]
    public async Task A_database_that_already_has_creatives_is_not_seeded_again()
    {
        int seeded;
        await using (var first = StartHost())
        {
            var list = await (await first.CreateClient().AuthenticateAsync(Email)).GetFromJsonAsync<JsonElement>("/api/creatives");
            seeded = list.GetArrayLength();
        }

        await using var second = StartHost();
        var after = await (await second.CreateClient().AuthenticateAsync(Email, register: false)).GetFromJsonAsync<JsonElement>("/api/creatives");

        Assert.Equal(seeded, after.GetArrayLength());
    }
}
