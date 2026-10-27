using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace CreativeCollab.Api.Services;

/// <summary>
/// Checks that an image URL is public and serves an image. Registered as a typed client, so the
/// <see cref="HttpClient"/> comes from <see cref="IHttpClientFactory"/> and its handler is pooled.
/// The client must not follow redirects.
/// </summary>
/// <remarks>
/// The host is resolved here and again by the client when it connects, so DNS rebinding is not fully mitigated.
/// </remarks>
public sealed class ImageUrlValidator(
    HttpClient client,
    IHostAddressResolver resolver,
    IOptions<ImageValidationOptions> options) : IImageUrlValidator
{
    public async Task<ImageCheckResult> CheckAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return ImageCheckResult.Fail("The image URL must be an absolute http or https URL.");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(options.Value.Timeout);

        try
        {
            var addresses = await ResolveAsync(uri.DnsSafeHost, budget.Token);
            if (addresses.Count == 0)
                return ImageCheckResult.Fail("The image URL's host could not be resolved.");
            if (!addresses.All(PublicAddress.IsPublic))
                return ImageCheckResult.Fail("The image URL must point to a public address.");

            return await CheckImageAsync(uri, budget.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ImageCheckResult.Fail("The image URL did not respond in time.");
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException)
        {
            return ImageCheckResult.Fail("The image URL could not be reached.");
        }
    }

    private async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct) =>
        IPAddress.TryParse(host, out var literal) ? [literal] : await resolver.ResolveAsync(host, ct);

    private async Task<ImageCheckResult> CheckImageAsync(Uri uri, CancellationToken ct)
    {
        using var head = new HttpRequestMessage(HttpMethod.Head, uri);
        using var headResponse = await client.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, ct);
        if (headResponse.IsSuccessStatusCode)
            return Evaluate(headResponse);
        if (IsRedirect(headResponse.StatusCode))
            return RedirectFailure;

        // HEAD refused (405, 501, or a server that answers it badly): ask for the first byte only.
        using var get = new HttpRequestMessage(HttpMethod.Get, uri);
        get.Headers.Range = new RangeHeaderValue(0, 0);
        using var getResponse = await client.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, ct);
        if (getResponse.IsSuccessStatusCode)
            return Evaluate(getResponse);
        return IsRedirect(getResponse.StatusCode)
            ? RedirectFailure
            : ImageCheckResult.Fail($"The image URL answered with status {(int)getResponse.StatusCode}.");
    }

    private static ImageCheckResult Evaluate(HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
            ? ImageCheckResult.Ok
            : ImageCheckResult.Fail("The image URL does not point to an image.");

    private static bool IsRedirect(HttpStatusCode status) => (int)status is >= 300 and < 400;

    private static ImageCheckResult RedirectFailure { get; } =
        ImageCheckResult.Fail("The image URL redirects. Use the final image URL.");
}
