using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CreativeCollab.Api.Tests;

public sealed class EndpointEnforcementTests : IDisposable
{
    private readonly ApiFactory _host = new();

    public void Dispose() => _host.Dispose();

    private static object Body(int? version = null) =>
        new { name = "n", headline = "h", body = "b", cta = "c", version };

    private async Task<Guid> ExistingCreativeAsync()
    {
        var editor = await _host.SignedInAsync();
        var created = await (await editor.PostAsJsonAsync("/api/creatives", Body())).Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    /// <summary>Every Creative endpoint with the least role that may call it.</summary>
    public static TheoryData<string, string, string> Endpoints => new()
    {
        { "GET", "/api/creatives", "Viewer" },
        { "GET", "/api/creatives/{id}", "Viewer" },
        { "POST", "/api/creatives", "Editor" },
        { "PUT", "/api/creatives/{id}", "Editor" },
    };

    private static HttpRequestMessage Request(string method, string path, Guid id) =>
        new(new HttpMethod(method), path.Replace("{id}", id.ToString()))
        {
            Content = method is "POST" or "PUT" ? JsonContent.Create(Body(version: 1)) : null,
        };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Anonymous_callers_get_401(string method, string path, string _)
    {
        var id = await ExistingCreativeAsync();

        var response = await _host.CreateClient().SendAsync(Request(method, path, id));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task A_valid_token_without_any_role_gets_403(string method, string path, string _)
    {
        var id = await ExistingCreativeAsync();
        var nobody = await _host.NewAccountWithoutRoleAsync();

        var response = await nobody.Client.SendAsync(Request(method, path, id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/creatives")]
    [InlineData("PUT", "/api/creatives/{id}")]
    public async Task A_viewer_cannot_create_or_update_and_gets_403(string method, string path)
    {
        var id = await ExistingCreativeAsync();
        var viewer = await _host.SignedInAsync("Viewer");

        var response = await viewer.SendAsync(Request(method, path, id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_viewer_can_read_the_list_and_one_creative()
    {
        var id = await ExistingCreativeAsync();
        var viewer = await _host.SignedInAsync("Viewer");

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/creatives")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/creatives/{id}")).StatusCode);
    }

    [Fact]
    public async Task An_admin_can_create_and_update()
    {
        var admin = await _host.SignedInAsync("Admin");

        var created = await admin.PostAsJsonAsync("/api/creatives", Body());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var updated = await admin.PutAsJsonAsync($"/api/creatives/{id}", Body(version: 1));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    }

    [Fact]
    public async Task Logout_needs_no_token_and_no_role()
    {
        var anonymous = _host.CreateClient();
        var nobody = await _host.NewAccountWithoutRoleAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/creatives")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await nobody.Client.PostAsync("/api/auth/logout", null)).StatusCode);
    }

    [Fact]
    public async Task Register_sign_in_refresh_and_logout_stay_open_to_anonymous_callers()
    {
        var client = _host.CreateClient();

        Assert.NotEqual(HttpStatusCode.Unauthorized, (await client.RegisterAsync("open@example.com")).StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.SignInAsync("open@example.com")).StatusCode);
        // No cookie: the refusal is the refresh endpoint's own generic 401, not an authentication wall.
        var refresh = await client.RefreshAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal("Please sign in again.", (await refresh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }
}
