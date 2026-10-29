using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CreativeCollab.Api.Tests;

/// <summary>Test configuration that must exist before any host starts: the signing key is never committed in appsettings.</summary>
internal static class TestSigningKey
{
    public const string Value = "test-only-signing-key-0123456789-abcdefghijkl";

    [ModuleInitializer]
    internal static void Supply() => Environment.SetEnvironmentVariable("Jwt__Key", Value);
}

/// <summary>What a Collaborator holds after signing in: the access token and the raw refresh cookie.</summary>
internal sealed record Session(string AccessToken, string RefreshCookie, string SetCookieHeader);

/// <summary>Drives the auth endpoints the way a browser would, over the real HTTP surface.</summary>
internal static class AuthClient
{
    public const string Password = "correct horse battery";
    public const string CookieName = "refresh_token";

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, string email, string password = Password, string displayName = "Ana") =>
        client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName });

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    public static async Task<Session> SignInAsync(this HttpClient client, string email, string password = Password, string displayName = "Ana")
    {
        var registered = await client.RegisterAsync(email, password, displayName);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var login = await client.LoginAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return await ReadSessionAsync(login);
    }

    public static async Task<Session> ReadSessionAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(CookieName + "="));
        var value = setCookie.Split(';')[0][(CookieName.Length + 1)..];
        return new Session(body.GetProperty("accessToken").GetString()!, value, setCookie);
    }

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string? cookie) => PostWithCookie(client, "/api/auth/refresh", cookie);

    public static Task<HttpResponseMessage> LogoutAsync(this HttpClient client, string? cookie) => PostWithCookie(client, "/api/auth/logout", cookie);

    private static Task<HttpResponseMessage> PostWithCookie(HttpClient client, string url, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (cookie is not null) request.Headers.Add("Cookie", $"{CookieName}={cookie}");
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> MeAsync(this HttpClient client, string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        if (accessToken is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }
}
