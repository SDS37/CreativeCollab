using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CreativeCollab.Api.Tests;

public sealed class RoleAdminTests : IDisposable
{
    private readonly ApiFactory _host = new();

    public void Dispose() => _host.Dispose();

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private static Task<HttpResponseMessage> ChangeRoleAsync(HttpClient client, string? userId, string role, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/users/{userId}/role") { Content = JsonContent.Create(new { role }) };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var registered = await client.RegisterAsync(email);
        return (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task<string> AdminTokenAsync(HttpClient client) =>
        (await AuthClient.ReadSessionAsync(await client.LoginAsync(ApiFactory.AdminEmail))).AccessToken;

    private static async Task<string?> RoleOfAsync(HttpClient client, string token) =>
        (await (await client.MeAsync(token)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString();

    [Fact]
    public async Task Changing_a_role_without_signing_in_is_401()
    {
        var client = _host.CreateClient();
        var id = await RegisterAsync(client, NewEmail());

        Assert.Equal(HttpStatusCode.Unauthorized, (await ChangeRoleAsync(client, id, "Viewer", null)).StatusCode);
    }

    [Fact]
    public async Task Changing_a_role_as_an_editor_is_403()
    {
        var client = _host.CreateClient();
        var editor = await client.SignInAsync(NewEmail());
        var id = await RegisterAsync(client, NewEmail());

        Assert.Equal(HttpStatusCode.Forbidden, (await ChangeRoleAsync(client, id, "Viewer", editor.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task An_admin_changes_a_role_and_it_applies_after_the_next_token_refresh()
    {
        var client = _host.CreateClient();
        var email = NewEmail();
        var id = await RegisterAsync(client, email);
        var session = await AuthClient.ReadSessionAsync(await client.LoginAsync(email));

        var changed = await ChangeRoleAsync(client, id, "Viewer", await AdminTokenAsync(client));

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal("Editor", await RoleOfAsync(client, session.AccessToken));
        var renewed = await AuthClient.ReadSessionAsync(await client.RefreshAsync(session.RefreshCookie));
        Assert.Equal("Viewer", await RoleOfAsync(client, renewed.AccessToken));
    }

    [Fact]
    public async Task An_unknown_role_is_400_and_an_unknown_account_is_404()
    {
        var client = _host.CreateClient();
        var id = await RegisterAsync(client, NewEmail());
        var admin = await AdminTokenAsync(client);

        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeRoleAsync(client, id, "Overlord", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ChangeRoleAsync(client, Guid.NewGuid().ToString(), "Viewer", admin)).StatusCode);
    }
}
