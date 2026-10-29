using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CreativeCollab.Api.Tests;

/// <summary>A Display name is public, unique and plain, so nobody can pass as another account in updatedBy or Presence.</summary>
public sealed class DisplayNameTests : IDisposable
{
    private readonly ApiFactory _host = new();

    public void Dispose() => _host.Dispose();

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private static async Task<JsonElement> ErrorsAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

    [Theory]
    [InlineData("Ana")]
    [InlineData("Al")]
    [InlineData("O'Brien-Smith Jr.")]
    [InlineData("ana_lopez 2")]
    [InlineData("José Núñez")]
    [InlineData("12")]
    public async Task A_plain_name_is_accepted(string name)
    {
        var response = await _host.CreateClient().RegisterAsync(NewEmail(), displayName: name);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_forty_character_name_is_accepted_and_forty_one_is_not()
    {
        var client = _host.CreateClient();

        Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(NewEmail(), displayName: new string('a', 40))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.RegisterAsync(NewEmail(), displayName: new string('b', 41))).StatusCode);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("")]
    [InlineData(" Ana")]
    [InlineData("Ana ")]
    [InlineData("Ana  Lopez")]
    [InlineData("Ana\tLopez")]
    [InlineData("Ana<b>")]
    [InlineData("Ana@Corp")]
    [InlineData("Ana​Lopez")]
    [InlineData("Ana’s")]
    [InlineData("Ana Lopez")]
    [InlineData("Ana 🙂")]
    [InlineData("Ana\nLopez")]
    public async Task A_name_with_other_characters_or_odd_spacing_is_a_400_on_displayName(string name)
    {
        var response = await _host.CreateClient().RegisterAsync(NewEmail(), displayName: name);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorsAsync(response)).TryGetProperty("displayName", out _));
    }

    [Theory]
    [InlineData("Ana Lopez")]
    [InlineData("ana lopez")]
    [InlineData("ANA LOPEZ")]
    [InlineData("Ａna Lopez")] // full-width A normalizes to A
    public async Task A_taken_name_is_a_400_on_displayName_whatever_its_case(string second)
    {
        var client = _host.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(NewEmail(), displayName: "Ana Lopez")).StatusCode);
        var email = NewEmail();

        var response = await client.RegisterAsync(email, displayName: second);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorsAsync(response)).TryGetProperty("displayName", out _));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(email)).StatusCode); // no account was made
    }

    [Fact]
    public async Task Two_accounts_with_different_names_still_register()
    {
        var client = _host.CreateClient();

        Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(NewEmail(), displayName: "Ana Lopez")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(NewEmail(), displayName: "Ana Lopez 2")).StatusCode);
    }
}
