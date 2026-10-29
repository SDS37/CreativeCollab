using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CreativeCollab.Api.Tests;

public sealed class TokenValidationTests : IDisposable
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
    public async Task A_token_is_accepted_inside_its_lifetime_and_refused_once_it_and_the_skew_have_passed()
    {
        var client = NewClient();
        var session = await client.SignInAsync("ana@example.com");

        _time.Advance(TimeSpan.FromMinutes(14));
        Assert.Equal(HttpStatusCode.OK, (await client.ListCreativesAsync(session.AccessToken)).StatusCode);

        _time.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(29)); // inside the 30 second skew
        Assert.Equal(HttpStatusCode.OK, (await client.ListCreativesAsync(session.AccessToken)).StatusCode);

        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.ListCreativesAsync(session.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task A_tampered_token_is_refused()
    {
        var client = NewClient();
        var session = await client.SignInAsync("ana@example.com");
        var parts = session.AccessToken.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1])).Replace("\"Editor\"", "\"Admin\"");
        var forged = $"{parts[0]}.{Base64UrlEncoder.Encode(payload)}.{parts[2]}";

        Assert.NotEqual(session.AccessToken, forged);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.ListCreativesAsync(forged)).StatusCode);
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_refused()
    {
        var token = Forge(key: "another-signing-key-0123456789-abcdefghijk");

        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().ListCreativesAsync(token)).StatusCode);
    }

    [Fact]
    public async Task A_token_signed_with_another_algorithm_is_refused_even_with_the_right_key()
    {
        var token = Forge(algorithm: SecurityAlgorithms.HmacSha384);

        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().ListCreativesAsync(token)).StatusCode);
    }

    [Fact]
    public async Task An_unsigned_token_is_refused()
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncoder.Encode($$"""{"sub":"x","name":"Mallory","role":"Admin","iss":"creativecollab-api","aud":"creativecollab-web","exp":{{_time.GetUtcNow().AddMinutes(5).ToUnixTimeSeconds()}}}""");

        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().ListCreativesAsync($"{header}.{payload}.")).StatusCode);
    }

    [Fact]
    public async Task A_token_for_another_issuer_or_audience_is_refused()
    {
        var client = NewClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.ListCreativesAsync(Forge(issuer: "someone-else"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.ListCreativesAsync(Forge(audience: "another-app"))).StatusCode);
    }

    [Fact]
    public async Task A_correctly_signed_forged_token_control_is_accepted()
    {
        // Guards the helper: with every field right, the forged token passes, so the refusals above are about one field each.
        Assert.Equal(HttpStatusCode.OK, (await NewClient().ListCreativesAsync(Forge())).StatusCode);
    }

    private string Forge(string key = TestSigningKey.Value, string algorithm = SecurityAlgorithms.HmacSha256,
        string issuer = "creativecollab-api", string audience = "creativecollab-web")
    {
        var now = _time.GetUtcNow().UtcDateTime;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            NotBefore = now,
            Expires = now.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), algorithm),
            Claims = new Dictionary<string, object> { ["sub"] = "x", ["name"] = "Mallory", ["role"] = "Admin", ["jti"] = "j" },
        });
    }
}
