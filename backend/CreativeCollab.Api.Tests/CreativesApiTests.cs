using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CreativeCollab.Api.Tests;

public class CreativesApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient NewClient() => factory.WithWebHostBuilder(_ => { }).CreateClient();

    [Fact]
    public async Task List_returns_200_with_empty_array_when_no_creatives_exist()
    {
        var client = NewClient();

        var response = await client.GetAsync("/api/creatives");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetArrayLength());
    }

    private static object ValidCreative(string name = "Spring sale", string displayName = "Ana") => new
    {
        name,
        headline = "Save big",
        body = "Everything must go this weekend.",
        cta = "Shop now",
        imageUrl = (string?)null,
        displayName,
    };

    [Fact]
    public async Task Create_returns_201_with_version_1_and_records_display_name()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/creatives", ValidCreative());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.Equal("Spring sale", body.GetProperty("name").GetString());
        Assert.Equal("Ana", body.GetProperty("updatedBy").GetString());
    }

    [Fact]
    public async Task Get_returns_the_created_creative()
    {
        var client = NewClient();
        var created = await (await client.PostAsJsonAsync("/api/creatives", ValidCreative()))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var response = await client.GetAsync($"/api/creatives/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal("Save big", body.GetProperty("headline").GetString());
    }

    [Fact]
    public async Task Get_returns_404_for_a_missing_id()
    {
        var client = NewClient();

        var response = await client.GetAsync($"/api/creatives/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_is_sorted_by_most_recently_updated_first()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var client = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.AddSingleton<TimeProvider>(time))).CreateClient();

        await client.PostAsJsonAsync("/api/creatives", ValidCreative("older"));
        time.Advance(TimeSpan.FromMinutes(5));
        await client.PostAsJsonAsync("/api/creatives", ValidCreative("newer"));

        var list = await client.GetFromJsonAsync<JsonElement>("/api/creatives");

        Assert.Equal(
            ["newer", "older"],
            list.EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToArray());
    }

    [Fact]
    public async Task Create_returns_400_with_details_for_each_invalid_field()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/creatives", new
        {
            name = new string('n', 101),
            headline = new string('h', 41),
            body = new string('b', 201),
            cta = new string('c', 21),
            imageUrl = "not-a-url",
            displayName = "Ana",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors");
        foreach (var field in new[] { "name", "headline", "body", "cta", "imageUrl" })
            Assert.True(errors.TryGetProperty(field, out _), $"expected a validation error for {field}");
    }

    [Theory]
    [InlineData("name")]
    [InlineData("headline")]
    [InlineData("body")]
    [InlineData("cta")]
    [InlineData("displayName")]
    public async Task Create_returns_400_when_a_required_field_is_missing(string field)
    {
        var client = NewClient();
        var payload = JsonSerializer.SerializeToNode(ValidCreative())!.AsObject();
        payload.Remove(field);

        var response = await client.PostAsJsonAsync("/api/creatives", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Theory]
    [InlineData("ftp://example.com/a.png")]
    [InlineData("/relative/a.png")]
    public async Task Create_rejects_image_urls_that_are_not_absolute_http_or_https(string imageUrl)
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/creatives",
            new { name = "n", headline = "h", body = "b", cta = "c", imageUrl, displayName = "Ana" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_accepts_a_valid_https_image_url()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/creatives",
            new { name = "n", headline = "h", body = "b", cta = "c", imageUrl = "https://example.com/a.png", displayName = "Ana" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_stores_an_empty_image_url_as_absent()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/creatives",
            new { name = "n", headline = "h", body = "b", cta = "c", imageUrl = "", displayName = "Ana" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("imageUrl").ValueKind);
    }
}
