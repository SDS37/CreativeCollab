using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CreativeCollab.Api.Tests;

public sealed class RefreshTests : IDisposable
{
    private readonly List<ApiFactory> _hosts = [];
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    private HttpClient NewClient()
    {
        var host = new ApiFactory(s => s.AddSingleton<TimeProvider>(_time));
        _hosts.Add(host);
        return host.CreateClient();
    }

    public void Dispose() => _hosts.ForEach(h => h.Dispose());

    [Fact]
    public async Task The_refresh_cookie_is_http_only_secure_same_site_strict_and_scoped_to_the_auth_path()
    {
        var session = await NewClient().RegisterAndSignInAsync("ana@example.com");

        var cookie = session.SetCookieHeader.ToLowerInvariant();

        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/auth", cookie);
        Assert.DoesNotContain(session.RefreshCookie, session.AccessToken);
    }

    [Fact]
    public async Task Refreshing_returns_a_working_access_token_and_a_different_refresh_token()
    {
        var client = NewClient();
        var first = await client.RegisterAndSignInAsync("ana@example.com", displayName: "Ana Lopez");

        var response = await client.RefreshAsync(first.RefreshCookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = await AuthClient.ReadSessionAsync(response);
        Assert.NotEqual(first.RefreshCookie, second.RefreshCookie);
        Assert.Equal(HttpStatusCode.OK, (await client.ListCreativesAsync(second.AccessToken)).StatusCode);
        Assert.Equal("Ana Lopez", AuthClient.ClaimsOf(second.AccessToken).GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_rotated_refresh_token_can_be_followed_through_the_chain()
    {
        var client = NewClient();
        var session = await client.RegisterAndSignInAsync("ana@example.com");

        for (var i = 0; i < 3; i++)
        {
            var response = await client.RefreshAsync(session.RefreshCookie);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            session = await AuthClient.ReadSessionAsync(response);
        }
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_is_refused_and_revokes_the_whole_family()
    {
        var client = NewClient();
        var first = await client.RegisterAndSignInAsync("ana@example.com");
        var second = await AuthClient.ReadSessionAsync(await client.RefreshAsync(first.RefreshCookie));

        var reuse = await client.RefreshAsync(first.RefreshCookie);
        var latest = await client.RefreshAsync(second.RefreshCookie);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, latest.StatusCode);
    }

    [Fact]
    public async Task Reuse_by_one_account_does_not_revoke_another_accounts_session()
    {
        var client = NewClient();
        var ana = await client.RegisterAndSignInAsync("ana@example.com");
        var bo = await client.RegisterAndSignInAsync("bo@example.com", displayName: "Bo");
        await client.RefreshAsync(ana.RefreshCookie);
        await client.RefreshAsync(ana.RefreshCookie);

        var response = await client.RefreshAsync(bo.RefreshCookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task A_missing_or_unknown_refresh_token_is_refused_with_401(string? cookie)
    {
        var response = await NewClient().RefreshAsync(cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_expired_refresh_token_is_refused()
    {
        var client = NewClient();
        var session = await client.RegisterAndSignInAsync("ana@example.com");

        _time.Advance(TimeSpan.FromDays(15));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshAsync(session.RefreshCookie)).StatusCode);
    }

    [Fact]
    public async Task Signing_out_revokes_the_session_on_the_server_so_a_copied_cookie_cannot_revive_it()
    {
        var client = NewClient();
        var session = await client.RegisterAndSignInAsync("ana@example.com");

        var logout = await client.LogoutAsync(session.RefreshCookie, session.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshAsync(session.RefreshCookie)).StatusCode);
    }

    [Fact]
    public async Task Signing_out_without_a_session_is_harmless()
    {
        var session = await NewClient().RegisterAndSignInAsync("ana@example.com");

        var logout = await NewClient().LogoutAsync(cookie: null, session.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task Signing_out_works_from_the_refresh_cookie_alone_with_no_access_token()
    {
        var client = NewClient();
        var session = await client.RegisterAndSignInAsync("ana@example.com");

        var logout = await client.LogoutAsync(session.RefreshCookie);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshAsync(session.RefreshCookie)).StatusCode);
    }

    [Fact]
    public async Task Signing_out_with_an_expired_access_token_still_revokes_the_session()
    {
        var client = NewClient();
        var session = await client.RegisterAndSignInAsync("ana@example.com");
        _time.Advance(TimeSpan.FromHours(1)); // the access token (15 minutes) has expired

        var logout = await client.LogoutAsync(session.RefreshCookie, session.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshAsync(session.RefreshCookie)).StatusCode);
    }

    [Fact]
    public async Task Signing_out_revokes_the_whole_family_including_a_rotated_token()
    {
        var client = NewClient();
        var first = await client.RegisterAndSignInAsync("ana@example.com");
        var second = await AuthClient.ReadSessionAsync(await client.RefreshAsync(first.RefreshCookie));

        await client.LogoutAsync(second.RefreshCookie);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.RefreshAsync(second.RefreshCookie)).StatusCode);
    }

    [Fact]
    public async Task Signing_out_always_answers_204_and_clears_the_cookie_even_for_a_junk_or_missing_cookie()
    {
        var client = NewClient();

        var junk = await client.LogoutAsync("not-a-real-token");
        var none = await client.LogoutAsync(cookie: null);

        foreach (var response in new[] { junk, none })
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var cleared = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(AuthClient.CookieName + "=")).ToLowerInvariant();
            Assert.Contains("expires=thu, 01 jan 1970", cleared);
        }
    }
}
