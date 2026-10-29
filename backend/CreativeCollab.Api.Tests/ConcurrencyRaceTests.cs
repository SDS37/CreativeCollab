using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CreativeCollab.Api.Tests;

public sealed class ConcurrencyRaceTests
{
    /// <summary>
    /// Lets one UPDATE through, then, just before the next save is written, a rival save commits to the
    /// same row. That is the window the service's Version pre-check cannot see.
    /// </summary>
    private sealed class RivalSaveInterceptor : SaveChangesInterceptor
    {
        public bool Armed { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            var db = eventData.Context!;
            var updating = db.ChangeTracker.Entries().Any(e => e.State == EntityState.Modified);
            if (Armed && updating)
            {
                Armed = false;
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE Creatives SET Version = Version + 1, Name = 'Ana got there first', UpdatedBy = 'Ana'", ct);
            }
            return result;
        }
    }

    [Fact]
    public async Task A_save_that_loses_a_race_at_the_database_returns_409_with_the_winners_creative()
    {
        var rival = new RivalSaveInterceptor();
        using var host = new ApiFactory(interceptors: rival);
        var client = await host.SignedInAsync();
        var created = await (await client.PostAsJsonAsync("/api/creatives",
            new { name = "Original", headline = "h", body = "b", cta = "c" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        rival.Armed = true;

        var response = await client.PutAsJsonAsync($"/api/creatives/{id}",
            new { name = "Bo's edit", headline = "h", body = "b", cta = "c", version = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var current = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("current");
        Assert.Equal(2, current.GetProperty("version").GetInt32());
        Assert.Equal("Ana got there first", current.GetProperty("name").GetString());
        var stored = await client.GetFromJsonAsync<JsonElement>($"/api/creatives/{id}");
        Assert.Equal("Ana got there first", stored.GetProperty("name").GetString());
        Assert.Equal(2, stored.GetProperty("version").GetInt32());
    }
}
