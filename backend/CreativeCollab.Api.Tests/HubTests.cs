using System.Net.Http.Json;
using System.Text.Json;

namespace CreativeCollab.Api.Tests;

public sealed class HubTests : IAsyncDisposable
{
    private readonly ApiFactory _host = new();
    private readonly List<HubClient> _clients = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients) await client.DisposeAsync();
        _host.Dispose();
    }

    private async Task<HubClient> ConnectAsync(string? origin = null)
    {
        var client = await HubClient.ConnectAsync(_host, origin);
        _clients.Add(client);
        return client;
    }

    private static readonly Guid Creative = Guid.NewGuid();

    [Fact]
    public async Task Joining_returns_who_is_present_and_announces_the_arrival_to_the_others()
    {
        var ana = await ConnectAsync();
        var bo = await ConnectAsync();

        var firstView = await ana.JoinAsync(Creative, "Ana");
        var secondView = await bo.JoinAsync(Creative, "Bo");

        Assert.Equal(["Ana"], firstView);
        Assert.Equal(["Ana", "Bo"], secondView.Order());
        Assert.Equal("Bo", await ana.NextUserJoinedAsync());
    }

    private static object NewCreative() =>
        new { name = "n", headline = "h", body = "b", cta = "c" };

    private static object Edit(int version, string headline = "Edited") =>
        new { name = "n", headline, body = "b", cta = "c", version };

    private async Task<Guid> CreateCreativeAsync(HttpClient http)
    {
        var created = await (await http.PostAsJsonAsync("/api/creatives", NewCreative()))
            .Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task A_successful_update_broadcasts_the_full_creative_to_everyone_in_the_group_including_the_sender()
    {
        var http = await _host.SignedInAsync();
        var id = await CreateCreativeAsync(http);
        var ana = await ConnectAsync();
        var bo = await ConnectAsync();
        await ana.JoinAsync(id, "Ana");
        await bo.JoinAsync(id, "Bo");
        var editor = await _host.SignedInAsync(name: "Bo");

        var response = await editor.PutAsJsonAsync($"/api/creatives/{id}", Edit(1));
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var client in new[] { ana, bo })
        {
            var pushed = await client.NextCreativeUpdatedAsync();
            Assert.Equal(id, pushed.GetProperty("id").GetGuid());
            Assert.Equal(2, pushed.GetProperty("version").GetInt32());
            Assert.Equal("Edited", pushed.GetProperty("headline").GetString());
            Assert.Equal("Bo", pushed.GetProperty("updatedBy").GetString());
            Assert.Equal(saved.GetProperty("updatedAt").GetDateTimeOffset(), pushed.GetProperty("updatedAt").GetDateTimeOffset());
        }
    }

    [Fact]
    public async Task Leaving_announces_it_to_the_others_and_removes_the_collaborator_from_presence()
    {
        var ana = await ConnectAsync();
        var bo = await ConnectAsync();
        await ana.JoinAsync(Creative, "Ana");
        await bo.JoinAsync(Creative, "Bo");

        await bo.LeaveAsync(Creative);

        Assert.Equal("Bo", await ana.NextUserLeftAsync());
        var cy = await ConnectAsync();
        Assert.Equal(["Ana", "Cy"], (await cy.JoinAsync(Creative, "Cy")).Order());
    }

    [Fact]
    public async Task Leaving_a_creative_that_was_never_joined_announces_nothing()
    {
        var ana = await ConnectAsync();
        var bo = await ConnectAsync();
        await ana.JoinAsync(Creative, "Ana");

        await bo.LeaveAsync(Creative);
        await ana.LeaveAsync(Creative);
        var cy = await ConnectAsync();
        await cy.JoinAsync(Creative, "Cy");
        await bo.JoinAsync(Creative, "Bo");

        // Bo's join is the first thing Cy hears: the earlier no-op leave produced no UserLeft.
        Assert.Equal("Bo", await cy.NextUserJoinedAsync());
    }

    [Fact]
    public async Task A_dropped_connection_is_removed_from_presence_and_announced_as_left()
    {
        var ana = await ConnectAsync();
        var bo = await ConnectAsync();
        await ana.JoinAsync(Creative, "Ana");
        await bo.JoinAsync(Creative, "Bo");

        await bo.StopAsync();

        Assert.Equal("Bo", await ana.NextUserLeftAsync());
        var cy = await ConnectAsync();
        Assert.Equal(["Ana", "Cy"], (await cy.JoinAsync(Creative, "Cy")).Order());
    }

    [Fact]
    public async Task One_collaborator_in_two_connections_appears_twice_and_leaves_once_per_connection()
    {
        var anaTab1 = await ConnectAsync();
        var anaTab2 = await ConnectAsync();
        var bo = await ConnectAsync();
        await anaTab1.JoinAsync(Creative, "Ana");
        await anaTab2.JoinAsync(Creative, "Ana");

        var present = await bo.JoinAsync(Creative, "Bo");
        await anaTab1.StopAsync();

        Assert.Equal(["Ana", "Ana", "Bo"], present.Order());
        Assert.Equal("Ana", await bo.NextUserLeftAsync());
        var cy = await ConnectAsync();
        Assert.Equal(["Ana", "Bo", "Cy"], (await cy.JoinAsync(Creative, "Cy")).Order());
    }

    [Fact]
    public async Task Joining_the_same_creative_twice_on_one_connection_counts_once()
    {
        var ana = await ConnectAsync();
        var bo = await ConnectAsync();
        await ana.JoinAsync(Creative, "Ana");
        await bo.JoinAsync(Creative, "Bo");

        var again = await bo.JoinAsync(Creative, "Bo");

        Assert.Equal(["Ana", "Bo"], again.Order());
    }

    [Fact]
    public async Task Presence_and_updates_stay_within_each_creatives_group()
    {
        var http = await _host.SignedInAsync();
        var first = await CreateCreativeAsync(http);
        var second = await CreateCreativeAsync(http);
        var ana = await ConnectAsync();
        var bo = await ConnectAsync();

        Assert.Equal(["Ana"], await ana.JoinAsync(first, "Ana"));
        Assert.Equal(["Bo"], await bo.JoinAsync(second, "Bo"));
        await http.PutAsJsonAsync($"/api/creatives/{first}", Edit(1));
        await http.PutAsJsonAsync($"/api/creatives/{second}", Edit(1));

        Assert.Equal(first, (await ana.NextCreativeUpdatedAsync()).GetProperty("id").GetGuid());
        Assert.Equal(second, (await bo.NextCreativeUpdatedAsync()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_rejected_update_broadcasts_nothing()
    {
        var http = await _host.SignedInAsync();
        var id = await CreateCreativeAsync(http);
        var ana = await ConnectAsync();
        await ana.JoinAsync(id, "Ana");

        await http.PutAsJsonAsync($"/api/creatives/{id}", Edit(1, headline: "First"));
        var stale = await http.PutAsJsonAsync($"/api/creatives/{id}", Edit(1, headline: "Stale"));
        var invalid = await http.PutAsJsonAsync($"/api/creatives/{id}", Edit(0, headline: "Invalid"));
        await http.PutAsJsonAsync($"/api/creatives/{id}", Edit(2, headline: "Third"));

        Assert.Equal(System.Net.HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("First", (await ana.NextCreativeUpdatedAsync()).GetProperty("headline").GetString());
        Assert.Equal("Third", (await ana.NextCreativeUpdatedAsync()).GetProperty("headline").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Joining_without_a_display_name_is_refused(string userName)
    {
        var ana = await ConnectAsync();

        var error = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() => ana.JoinAsync(Creative, userName));

        Assert.Contains("display name", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_hub_negotiates_with_credentials_for_the_allowed_origin_only()
    {
        var http = await _host.SignedInAsync();

        HttpRequestMessage Negotiate(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/creatives/negotiate?negotiateVersion=1");
            request.Headers.Add("Origin", origin);
            return request;
        }

        var allowed = await http.SendAsync(Negotiate("http://localhost:4200"));
        var other = await http.SendAsync(Negotiate("http://evil.example"));

        Assert.Equal(["http://localhost:4200"], allowed.Headers.GetValues("Access-Control-Allow-Origin"));
        Assert.Equal(["true"], allowed.Headers.GetValues("Access-Control-Allow-Credentials"));
        Assert.False(other.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
