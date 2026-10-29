using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CreativeCollab.Api.Tests;

public sealed class AuthTests : IDisposable
{
    private readonly List<ApiFactory> _hosts = [];

    private HttpClient NewClient()
    {
        var host = new ApiFactory();
        _hosts.Add(host);
        return host.CreateClient();
    }

    public void Dispose() => _hosts.ForEach(h => h.Dispose());

    [Fact]
    public async Task A_registered_collaborator_signs_in_as_an_editor_under_their_display_name()
    {
        var client = NewClient();

        var session = await client.SignInAsync("ana@example.com", displayName: "Ana Lopez");

        var me = await client.MeAsync(session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var claims = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ana Lopez", claims.GetProperty("name").GetString());
        Assert.Equal("Editor", claims.GetProperty("role").GetString());
        Assert.False(string.IsNullOrEmpty(claims.GetProperty("sub").GetString()));
        Assert.False(string.IsNullOrEmpty(claims.GetProperty("jti").GetString()));
    }

    [Fact]
    public async Task A_request_without_a_token_is_refused_with_401()
    {
        var response = await NewClient().MeAsync(accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
