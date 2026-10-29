using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CreativeCollab.Api.Tests;

public sealed class CreativesApiTests : IDisposable
{
    private readonly List<ApiFactory> _hosts = [];

    private async Task<HttpClient> NewClientAsync(Action<IServiceCollection>? configure = null)
    {
        var host = new ApiFactory(configure);
        _hosts.Add(host);
        return await host.SignedInAsync();
    }

    public void Dispose() => _hosts.ForEach(h => h.Dispose());

    [Fact]
    public async Task List_returns_200_with_empty_array_when_no_creatives_exist()
    {
        var client = await NewClientAsync();

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
        var client = await NewClientAsync();

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
        var client = await NewClientAsync();
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
        var client = await NewClientAsync();

        var response = await client.GetAsync($"/api/creatives/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_is_sorted_by_most_recently_updated_first()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var client = await NewClientAsync(s => s.AddSingleton<TimeProvider>(time));

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
        var client = await NewClientAsync();

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
        var client = await NewClientAsync();
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
        var client = await NewClientAsync();

        var response = await client.PostAsJsonAsync("/api/creatives",
            new { name = "n", headline = "h", body = "b", cta = "c", imageUrl, displayName = "Ana" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_accepts_a_valid_https_image_url()
    {
        var client = await NewClientAsync();

        var response = await client.PostAsJsonAsync("/api/creatives",
            new { name = "n", headline = "h", body = "b", cta = "c", imageUrl = "https://example.com/a.png", displayName = "Ana" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_stores_an_empty_image_url_as_absent()
    {
        var client = await NewClientAsync();

        var response = await client.PostAsJsonAsync("/api/creatives",
            new { name = "n", headline = "h", body = "b", cta = "c", imageUrl = "", displayName = "Ana" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("imageUrl").ValueKind);
    }

    private static object ValidUpdate(int? version, string name = "Spring sale v2", string displayName = "Bo") => new
    {
        name,
        headline = "Save more",
        body = "Everything must go.",
        cta = "Buy",
        imageUrl = (string?)null,
        displayName,
        version,
    };

    private static async Task<JsonElement> CreateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/creatives", ValidCreative());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Update_with_the_current_version_saves_and_increments_the_version()
    {
        var client = await NewClientAsync();
        var created = await CreateAsync(client);
        var id = created.GetProperty("id").GetGuid();

        var response = await client.PutAsJsonAsync($"/api/creatives/{id}", ValidUpdate(1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("version").GetInt32());
        Assert.Equal("Spring sale v2", body.GetProperty("name").GetString());
        Assert.Equal("Bo", body.GetProperty("updatedBy").GetString());
        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/creatives/{id}");
        Assert.Equal(2, fetched.GetProperty("version").GetInt32());
        Assert.Equal("Save more", fetched.GetProperty("headline").GetString());
    }

    [Fact]
    public async Task Update_with_a_stale_version_returns_409_with_the_latest_creative_in_current()
    {
        var client = await NewClientAsync();
        var id = (await CreateAsync(client)).GetProperty("id").GetGuid();
        await client.PutAsJsonAsync($"/api/creatives/{id}", ValidUpdate(1, name: "Ana's winner", displayName: "Ana"));

        var response = await client.PutAsJsonAsync($"/api/creatives/{id}", ValidUpdate(1, name: "Bo's loser"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        var current = problem.GetProperty("current");
        Assert.Equal(2, current.GetProperty("version").GetInt32());
        Assert.Equal("Ana's winner", current.GetProperty("name").GetString());
        Assert.Equal("Ana", current.GetProperty("updatedBy").GetString());
        var stored = await client.GetFromJsonAsync<JsonElement>($"/api/creatives/{id}");
        Assert.Equal("Ana's winner", stored.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_returns_404_for_a_missing_id()
    {
        var client = await NewClientAsync();

        var response = await client.PutAsJsonAsync($"/api/creatives/{Guid.NewGuid()}", ValidUpdate(1));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_returns_400_with_details_for_each_invalid_field()
    {
        var client = await NewClientAsync();
        var id = (await CreateAsync(client)).GetProperty("id").GetGuid();

        var response = await client.PutAsJsonAsync($"/api/creatives/{id}", new
        {
            name = new string('n', 101),
            headline = "",
            body = new string('b', 201),
            cta = new string('c', 21),
            imageUrl = "ftp://x/a.png",
            displayName = "",
            version = 1,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        foreach (var field in new[] { "name", "headline", "body", "cta", "imageUrl", "displayName" })
            Assert.True(errors.TryGetProperty(field, out _), $"expected a validation error for {field}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Update_returns_400_when_the_version_is_missing_or_below_1(int? version)
    {
        var client = await NewClientAsync();
        var id = (await CreateAsync(client)).GetProperty("id").GetGuid();

        var response = await client.PutAsJsonAsync($"/api/creatives/{id}", ValidUpdate(version));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("version", out _));
    }

    [Fact]
    public async Task Update_sets_updated_at_from_the_clock()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var client = await NewClientAsync(s => s.AddSingleton<TimeProvider>(time));
        var id = (await CreateAsync(client)).GetProperty("id").GetGuid();
        time.Advance(TimeSpan.FromHours(1));
        client = await _hosts.Last().SignedInAsync(); // the first token has expired by now

        var body = await (await client.PutAsJsonAsync($"/api/creatives/{id}", ValidUpdate(1)))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(DateTimeOffset.Parse("2026-01-01T01:00:00Z"), body.GetProperty("updatedAt").GetDateTimeOffset());
    }
}
