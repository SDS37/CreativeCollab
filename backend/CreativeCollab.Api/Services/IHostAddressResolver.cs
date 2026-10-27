using System.Net;

namespace CreativeCollab.Api.Services;

/// <summary>Resolves a host name to addresses. A seam so the SSRF guard can be exercised without real DNS.</summary>
public interface IHostAddressResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct);
}

public sealed class DnsHostAddressResolver : IHostAddressResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct) =>
        await Dns.GetHostAddressesAsync(host, ct);
}
