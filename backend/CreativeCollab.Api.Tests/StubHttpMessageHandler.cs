using System.Net;
namespace CreativeCollab.Api.Tests;

/// <summary>Stands in for the network: answers every outbound request with <paramref name="respond"/> and records it.</summary>
public sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests
    {
        get { lock (_requests) return [.. _requests]; }
    }

    public static StubHttpMessageHandler Responding(HttpStatusCode status, string? contentType = null) =>
        new((_, _) => Task.FromResult(Response(status, contentType)));

    public static HttpResponseMessage Response(HttpStatusCode status, string? contentType = null)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent([]) };
        if (contentType is not null)
            response.Content.Headers.ContentType = new(contentType);
        return response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        lock (_requests) _requests.Add(request);
        return respond(request, ct);
    }
}
