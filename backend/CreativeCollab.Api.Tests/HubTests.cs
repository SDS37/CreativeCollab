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

    /// <summary>A new Editor account with this name, connected with its real token.</summary>
    private async Task<HubClient> ConnectAsync(string name) => await ConnectAsync(await _host.NewAccountAsync(name: name));

    private async Task<HubClient> ConnectAsync(Account account)
    {
        var client = await HubClient.ConnectAsync(_host, account.AccessToken);
        _clients.Add(client);
        return client;
    }

    private static readonly Guid Creative = Guid.NewGuid();

    [Fact]
    public async Task Joining_returns_who_is_present_and_announces_the_arrival_to_the_others()
    {
        var ana = await ConnectAsync("Ana");
        var bo = await ConnectAsync("Bo");

        var firstView = await ana.JoinAsync(Creative);
        var secondView = await bo.JoinAsync(Creative);

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
        var ana = await ConnectAsync("Ana");
        var boAccount = await _host.NewAccountAsync(name: "Bo");
        var bo = await ConnectAsync(boAccount);
        await ana.JoinAsync(id);
        await bo.JoinAsync(id);

        var response = await boAccount.Client.PutAsJsonAsync($"/api/creatives/{id}", Edit(1));
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
        var ana = await ConnectAsync("Ana");
        var bo = await ConnectAsync("Bo");
        await ana.JoinAsync(Creative);
        await bo.JoinAsync(Creative);

        await bo.LeaveAsync(Creative);

        Assert.Equal("Bo", await ana.NextUserLeftAsync());
        var cy = await ConnectAsync("Cy");
        Assert.Equal(["Ana", "Cy"], (await cy.JoinAsync(Creative)).Order());
    }

    [Fact]
    public async Task Leaving_a_creative_that_was_never_joined_announces_nothing()
    {
        var ana = await ConnectAsync("Ana");
        var bo = await ConnectAsync("Bo");
        await ana.JoinAsync(Creative);

        await bo.LeaveAsync(Creative);
        await ana.LeaveAsync(Creative);
        var cy = await ConnectAsync("Cy");
        await cy.JoinAsync(Creative);
        await bo.JoinAsync(Creative);

        // Bo's join is the first thing Cy hears: the earlier no-op leave produced no UserLeft.
        Assert.Equal("Bo", await cy.NextUserJoinedAsync());
    }

    [Fact]
    public async Task A_dropped_connection_is_removed_from_presence_and_announced_as_left()
    {
        var ana = await ConnectAsync("Ana");
        var bo = await ConnectAsync("Bo");
        await ana.JoinAsync(Creative);
        await bo.JoinAsync(Creative);

        await bo.StopAsync();

        Assert.Equal("Bo", await ana.NextUserLeftAsync());
        var cy = await ConnectAsync("Cy");
        Assert.Equal(["Ana", "Cy"], (await cy.JoinAsync(Creative)).Order());
    }

    [Fact]
    public async Task One_collaborator_in_two_connections_appears_twice_and_leaves_once_per_connection()
    {
        var ana = await _host.NewAccountAsync(name: "Ana");
        var anaTab1 = await ConnectAsync(ana);
        var anaTab2 = await ConnectAsync(ana);
        var bo = await ConnectAsync("Bo");
        await anaTab1.JoinAsync(Creative);
        await anaTab2.JoinAsync(Creative);

        var present = await bo.JoinAsync(Creative);
        await anaTab1.StopAsync();

        Assert.Equal(["Ana", "Ana", "Bo"], present.Order());
        Assert.Equal("Ana", await bo.NextUserLeftAsync());
        var cy = await ConnectAsync("Cy");
        Assert.Equal(["Ana", "Bo", "Cy"], (await cy.JoinAsync(Creative)).Order());
    }

    [Fact]
    public async Task Joining_the_same_creative_twice_on_one_connection_counts_once()
    {
        var ana = await ConnectAsync("Ana");
        var bo = await ConnectAsync("Bo");
        await ana.JoinAsync(Creative);
        await bo.JoinAsync(Creative);

        var again = await bo.JoinAsync(Creative);

        Assert.Equal(["Ana", "Bo"], again.Order());
    }

    [Fact]
    public async Task Presence_and_updates_stay_within_each_creatives_group()
    {
        var http = await _host.SignedInAsync();
        var first = await CreateCreativeAsync(http);
        var second = await CreateCreativeAsync(http);
        var ana = await ConnectAsync("Ana");
        var bo = await ConnectAsync("Bo");

        Assert.Equal(["Ana"], await ana.JoinAsync(first));
        Assert.Equal(["Bo"], await bo.JoinAsync(second));
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
        var ana = await ConnectAsync("Ana");
        await ana.JoinAsync(id);

        await http.PutAsJsonAsync($"/api/creatives/{id}", Edit(1, headline: "First"));
        var stale = await http.PutAsJsonAsync($"/api/creatives/{id}", Edit(1, headline: "Stale"));
        var invalid = await http.PutAsJsonAsync($"/api/creatives/{id}", Edit(0, headline: "Invalid"));
        await http.PutAsJsonAsync($"/api/creatives/{id}", Edit(2, headline: "Third"));

        Assert.Equal(System.Net.HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("First", (await ana.NextCreativeUpdatedAsync()).GetProperty("headline").GetString());
        Assert.Equal("Third", (await ana.NextCreativeUpdatedAsync()).GetProperty("headline").GetString());
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

    [Fact]
    public async Task Presence_shows_the_name_on_the_account_not_one_the_client_chose()
    {
        var ana = await ConnectAsync("Ana Lopez");
        var bo = await ConnectAsync("Bo Chen");

        Assert.Equal(["Ana Lopez"], await ana.JoinAsync(Creative));
        Assert.Equal(["Ana Lopez", "Bo Chen"], (await bo.JoinAsync(Creative)).Order());
        Assert.Equal("Bo Chen", await ana.NextUserJoinedAsync());
    }

    [Fact]
    public async Task Connecting_without_a_token_is_refused_with_401()
    {
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => HubClient.ConnectAsync(_host, accessToken: null));

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task Connecting_with_a_token_that_has_no_role_is_refused_with_403()
    {
        var nobody = await _host.NewAccountWithoutRoleAsync();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => HubClient.ConnectAsync(_host, nobody.AccessToken));

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task A_viewer_can_join_and_sees_live_updates()
    {
        var editor = await _host.SignedInAsync();
        var id = await CreateCreativeAsync(editor);
        var viewer = await ConnectAsync(await _host.NewAccountAsync("Viewer", "Vi"));
        await viewer.JoinAsync(id);

        await editor.PutAsJsonAsync($"/api/creatives/{id}", Edit(1));

        Assert.Equal(2, (await viewer.NextCreativeUpdatedAsync()).GetProperty("version").GetInt32());
    }

    private static HttpRequestMessage Negotiate(string path) => new(HttpMethod.Post, path);

    [Fact]
    public async Task The_hub_accepts_the_token_as_an_access_token_query_value()
    {
        var token = (await _host.NewAccountAsync()).AccessToken;
        var anonymous = _host.CreateClient();

        var withQuery = await anonymous.SendAsync(Negotiate($"/hubs/creatives/negotiate?negotiateVersion=1&access_token={token}"));
        var without = await anonymous.SendAsync(Negotiate("/hubs/creatives/negotiate?negotiateVersion=1"));
        var junk = await anonymous.SendAsync(Negotiate("/hubs/creatives/negotiate?negotiateVersion=1&access_token=not-a-token"));

        Assert.Equal(System.Net.HttpStatusCode.OK, withQuery.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, without.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, junk.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/creatives")]
    [InlineData("GET", "/api/auth/me")]
    public async Task Outside_the_hub_paths_the_query_string_token_is_ignored(string method, string path)
    {
        var token = (await _host.NewAccountAsync()).AccessToken;

        var response = await _host.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), $"{path}?access_token={token}"));

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
