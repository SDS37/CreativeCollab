using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace CreativeCollab.Api.Tests;

public sealed class ImageUrlTests
{
    private const string Url = "https://cdn.example.com/banner.png";

    private static object Create(string? imageUrl) =>
        new { name = "n", headline = "h", body = "b", cta = "c", imageUrl, displayName = "Ana" };

    private static async Task<JsonElement> ErrorsAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

    [Fact]
    public async Task Create_rejects_an_image_url_that_answers_with_a_non_image_content_type()
    {
        using var host = new ApiFactory { Images = StubHttpMessageHandler.Responding(HttpStatusCode.OK, "text/html") };

        var response = await (await host.SignedInAsync()).PostAsJsonAsync("/api/creatives", Create(Url));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorsAsync(response)).TryGetProperty("imageUrl", out _));
    }

    [Fact]
    public async Task Create_accepts_a_reachable_image_with_a_single_head_request()
    {
        var images = StubHttpMessageHandler.Responding(HttpStatusCode.OK, "image/jpeg");
        using var host = new ApiFactory { Images = images };

        var response = await (await host.SignedInAsync()).PostAsJsonAsync("/api/creatives", Create(Url));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var request = Assert.Single(images.Requests);
        Assert.Equal(HttpMethod.Head, request.Method);
        Assert.Equal(Url, request.RequestUri!.ToString());
    }

    [Fact]
    public async Task Create_falls_back_to_a_ranged_get_when_head_is_not_allowed()
    {
        var images = new StubHttpMessageHandler((request, _) => Task.FromResult(request.Method == HttpMethod.Head
            ? StubHttpMessageHandler.Response(HttpStatusCode.MethodNotAllowed)
            : StubHttpMessageHandler.Response(HttpStatusCode.PartialContent, "image/png")));
        using var host = new ApiFactory { Images = images };

        var response = await (await host.SignedInAsync()).PostAsJsonAsync("/api/creatives", Create(Url));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([HttpMethod.Head, HttpMethod.Get], images.Requests.Select(r => r.Method));
        var range = images.Requests[1].Headers.Range!.Ranges.Single();
        Assert.Equal((0L, 0L), (range.From!.Value, range.To!.Value));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.MovedPermanently)]
    public async Task Create_rejects_an_image_url_that_does_not_answer_with_success(HttpStatusCode status)
    {
        using var host = new ApiFactory { Images = StubHttpMessageHandler.Responding(status) };

        var response = await (await host.SignedInAsync()).PostAsJsonAsync("/api/creatives", Create(Url));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorsAsync(response)).TryGetProperty("imageUrl", out _));
    }

    [Fact]
    public async Task Create_rejects_an_image_url_that_cannot_be_reached_with_a_400_not_a_500()
    {
        var images = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("connection refused"));
        using var host = new ApiFactory { Images = images };

        var response = await (await host.SignedInAsync()).PostAsJsonAsync("/api/creatives", Create(Url));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorsAsync(response)).TryGetProperty("imageUrl", out _));
    }

    [Fact]
    public async Task Create_rejects_an_image_url_that_does_not_respond_within_the_timeout()
    {
        var images = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return StubHttpMessageHandler.Response(HttpStatusCode.OK, "image/png");
        });
        using var host = new ApiFactory(configureWebHost: b => b.UseSetting("ImageValidation:Timeout", "00:00:00.100"))
        {
            Images = images,
        };

        var response = await (await host.SignedInAsync()).PostAsJsonAsync("/api/creatives", Create(Url));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorsAsync(response)).TryGetProperty("imageUrl", out _));
    }

    [Theory]
    [InlineData("http://127.0.0.1/a.png")]
    [InlineData("http://127.1.2.3/a.png")]
    [InlineData("http://localhost/a.png")]
    [InlineData("http://[::1]/a.png")]
    [InlineData("http://10.0.0.5/a.png")]
    [InlineData("http://172.16.0.1/a.png")]
    [InlineData("http://172.31.255.255/a.png")]
    [InlineData("http://192.168.1.1/a.png")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://0.0.0.0/a.png")]
    [InlineData("http://100.64.0.1/a.png")]
    [InlineData("http://224.0.0.1/a.png")]
    [InlineData("http://255.255.255.255/a.png")]
    [InlineData("http://[fe80::1]/a.png")]
    [InlineData("http://[fd00::1]/a.png")]
    [InlineData("http://[::ffff:10.0.0.1]/a.png")]
    [InlineData("http://[::ffff:127.0.0.1]/a.png")]
    [InlineData("http://[64:ff9b::a00:1]/a.png")]
    [InlineData("http://[2002:7f00:1::]/a.png")]
    [InlineData("http://internal.example.com/a.png")]
    [InlineData("http://mixed.example.com/a.png")]
    public async Task Create_rejects_a_non_public_address_without_making_a_request(string imageUrl)
    {
        var images = StubHttpMessageHandler.Responding(HttpStatusCode.OK, "image/png");
        using var host = new ApiFactory { Images = images };
        host.Resolver.Map("internal.example.com", "10.1.2.3").Map("mixed.example.com", "93.184.216.34", "192.168.0.9");

        var response = await (await host.SignedInAsync()).PostAsJsonAsync("/api/creatives", Create(imageUrl));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorsAsync(response)).TryGetProperty("imageUrl", out _));
        Assert.Empty(images.Requests);
    }

    [Theory]
    [InlineData("http://93.184.216.34/a.png")]
    [InlineData("http://172.32.0.1/a.png")]
    [InlineData("http://[2606:2800:220:1:248:1893:25c8:1946]/a.png")]
    [InlineData("http://[::ffff:93.184.216.34]/a.png")]
    public async Task Create_accepts_a_public_address(string imageUrl)
    {
        using var host = new ApiFactory();

        var response = await (await host.SignedInAsync()).PostAsJsonAsync("/api/creatives", Create(imageUrl));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_a_host_that_does_not_resolve()
    {
        var images = StubHttpMessageHandler.Responding(HttpStatusCode.OK, "image/png");
        using var host = new ApiFactory { Images = images };
        host.Resolver.Map("nowhere.example.com");

        var response = await (await host.SignedInAsync()).PostAsJsonAsync("/api/creatives", Create("https://nowhere.example.com/a.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(images.Requests);
    }

    [Fact]
    public async Task Create_without_an_image_url_makes_no_request()
    {
        var images = StubHttpMessageHandler.Responding(HttpStatusCode.OK, "image/png");
        using var host = new ApiFactory { Images = images };

        var response = await (await host.SignedInAsync()).PostAsJsonAsync("/api/creatives", Create(null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(images.Requests);
    }

    private static object Update(string? imageUrl, int version, string name = "n") =>
        new { name, headline = "h", body = "b", cta = "c", imageUrl, displayName = "Bo", version };

    private static async Task<Guid> CreateWithImageAsync(HttpClient client, string? imageUrl)
    {
        var created = await (await client.PostAsJsonAsync("/api/creatives", Create(imageUrl)))
            .Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Update_does_not_recheck_an_image_url_that_did_not_change()
    {
        var images = StubHttpMessageHandler.Responding(HttpStatusCode.OK, "image/png");
        using var host = new ApiFactory { Images = images };
        var client = await host.SignedInAsync();
        var id = await CreateWithImageAsync(client, Url);
        var callsAfterCreate = images.Requests.Count;

        var response = await client.PutAsJsonAsync($"/api/creatives/{id}", Update(Url, 1, name: "renamed"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(callsAfterCreate, images.Requests.Count);
    }

    [Fact]
    public async Task Update_checks_a_changed_image_url_and_rejects_a_non_image_leaving_the_creative_untouched()
    {
        var images = new StubHttpMessageHandler((request, _) => Task.FromResult(
            StubHttpMessageHandler.Response(HttpStatusCode.OK, request.RequestUri!.AbsolutePath.EndsWith(".png") ? "image/png" : "text/html")));
        using var host = new ApiFactory { Images = images };
        var client = await host.SignedInAsync();
        var id = await CreateWithImageAsync(client, Url);

        var response = await client.PutAsJsonAsync($"/api/creatives/{id}", Update("https://cdn.example.com/page.html", 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ErrorsAsync(response)).TryGetProperty("imageUrl", out _));
        var stored = await client.GetFromJsonAsync<JsonElement>($"/api/creatives/{id}");
        Assert.Equal(1, stored.GetProperty("version").GetInt32());
        Assert.Equal(Url, stored.GetProperty("imageUrl").GetString());
    }

    [Fact]
    public async Task Update_accepts_a_changed_reachable_image_url()
    {
        var images = StubHttpMessageHandler.Responding(HttpStatusCode.OK, "image/webp");
        using var host = new ApiFactory { Images = images };
        var client = await host.SignedInAsync();
        var id = await CreateWithImageAsync(client, null);

        var response = await client.PutAsJsonAsync($"/api/creatives/{id}", Update("https://cdn.example.com/new.webp", 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(images.Requests);
    }

    [Fact]
    public async Task A_stale_update_is_a_conflict_without_an_outbound_call()
    {
        var images = StubHttpMessageHandler.Responding(HttpStatusCode.OK, "image/png");
        using var host = new ApiFactory { Images = images };
        var client = await host.SignedInAsync();
        var id = await CreateWithImageAsync(client, null);
        await client.PutAsJsonAsync($"/api/creatives/{id}", Update(null, 1));

        var response = await client.PutAsJsonAsync($"/api/creatives/{id}", Update("https://cdn.example.com/new.png", 1));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(images.Requests);
    }
}
