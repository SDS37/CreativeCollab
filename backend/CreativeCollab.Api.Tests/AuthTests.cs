using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CreativeCollab.Api.Auth;
using Microsoft.Extensions.DependencyInjection;

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
    public async Task A_wrong_password_and_an_unknown_email_get_the_same_generic_401()
    {
        var client = NewClient();
        await client.RegisterAsync("ana@example.com");

        var wrongPassword = await client.LoginAsync("ana@example.com", "not the right password");
        var unknownEmail = await client.LoginAsync("nobody@example.com");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var wrongBody = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var unknownBody = await unknownEmail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(wrongBody.GetProperty("detail").GetString(), unknownBody.GetProperty("detail").GetString());
        Assert.Equal(wrongBody.GetProperty("title").GetString(), unknownBody.GetProperty("title").GetString());
        Assert.False(wrongPassword.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Five_failed_sign_ins_lock_the_account_even_for_the_right_password()
    {
        // The 15 minute expiry is not asserted: Identity reads the system clock, not the injected TimeProvider.
        var client = NewClient();
        await client.RegisterAsync("ana@example.com");
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync("ana@example.com", "wrong password here")).StatusCode);

        var locked = await client.LoginAsync("ana@example.com");

        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);
        Assert.Contains("try again", await locked.Content.ReadAsStringAsync());
        Assert.False(locked.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Fewer_than_five_failures_followed_by_the_right_password_do_not_lock_the_account()
    {
        var client = NewClient();
        await client.RegisterAsync("ana@example.com");
        for (var i = 0; i < 4; i++) await client.LoginAsync("ana@example.com", "wrong password here");
        Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync("ana@example.com")).StatusCode);

        for (var i = 0; i < 4; i++) await client.LoginAsync("ana@example.com", "wrong password here");

        Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync("ana@example.com")).StatusCode);
    }

    [Fact]
    public async Task A_password_of_11_characters_is_refused_and_a_12_character_passphrase_with_no_symbols_is_accepted()
    {
        var client = NewClient();

        var tooShort = await client.RegisterAsync("short@example.com", "abcdefghijk");
        var passphrase = await client.RegisterAsync("long@example.com", "abcdefghijkl");

        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        var problem = await tooShort.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("password", out _));
        Assert.Equal(HttpStatusCode.Created, passphrase.StatusCode);
    }

    [Fact]
    public async Task Registering_with_a_taken_or_invalid_email_or_without_a_display_name_is_a_400()
    {
        var client = NewClient();
        await client.RegisterAsync("ana@example.com");

        var taken = await client.RegisterAsync("ana@example.com");
        var invalid = await client.RegisterAsync("not-an-email");
        var noName = await client.RegisterAsync("bo@example.com", displayName: "");

        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.True((await taken.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("email", out _));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
    }

    [Fact]
    public async Task Register_login_and_refresh_are_rate_limited_each_on_its_own()
    {
        var host = new ApiFactory(s => s.Configure<AuthRateLimitOptions>(o => o.PermitLimit = 3));
        _hosts.Add(host);
        var client = host.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.LoginAsync("ana@example.com", "wrong password here")).StatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.RegisterAsync($"user{i}@example.com")).StatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.RefreshAsync("nope")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.LoginAsync("ana@example.com", "wrong password here")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.RegisterAsync("late@example.com")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.RefreshAsync("nope")).StatusCode);
    }

    [Fact]
    public async Task A_request_without_a_token_is_refused_with_401()
    {
        var response = await NewClient().MeAsync(accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
