using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CreativeCollab.Api.Data;
using CreativeCollab.Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CreativeCollab.Api.Tests;

public sealed class ErrorHandlingTests
{
    private const string Secret = "Server=prod-db;Password=hunter2";

    private sealed class ExplodingRepository : ICreativeRepository
    {
        public Task<IReadOnlyList<Creative>> ListAsync(CancellationToken ct) =>
            throw new InvalidOperationException(Secret);
        public Task<Creative?> GetAsync(Guid id, CancellationToken ct) => throw new InvalidOperationException(Secret);
        public Task AddAsync(Creative creative, CancellationToken ct) => throw new InvalidOperationException(Secret);
        public Task<bool> TryUpdateAsync(Creative updated, int expectedVersion, CancellationToken ct) =>
            throw new InvalidOperationException(Secret);
    }

    [Fact]
    public async Task An_unhandled_exception_returns_a_500_problem_without_internal_detail()
    {
        using var host = new ApiFactory(s => s.AddScoped<ICreativeRepository, ExplodingRepository>())
        {
            Environment = "Development",
        };

        var response = await host.CreateClient().GetAsync("/api/creatives");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var raw = await response.Content.ReadAsStringAsync();
        var problem = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.DoesNotContain(Secret, raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        Assert.DoesNotContain("ExplodingRepository", raw);
        Assert.DoesNotContain(" at ", raw);
        Assert.False(problem.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task A_malformed_request_body_is_still_a_400_not_a_500()
    {
        using var host = new ApiFactory { Environment = "Development" };

        var response = await host.CreateClient().PostAsync("/api/creatives",
            new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
