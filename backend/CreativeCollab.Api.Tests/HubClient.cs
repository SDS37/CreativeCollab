using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace CreativeCollab.Api.Tests;

/// <summary>A Collaborator's real-time connection to the hub, with every server event queued in order.</summary>
public sealed class HubClient : IAsyncDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly HubConnection _connection;
    private readonly Channel<string> _joined = Channel.CreateUnbounded<string>();
    private readonly Channel<string> _left = Channel.CreateUnbounded<string>();
    private readonly Channel<JsonElement> _updated = Channel.CreateUnbounded<JsonElement>();

    private HubClient(ApiFactory host, string? origin)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(host.Server.BaseAddress, "/hubs/creatives"), options =>
            {
                options.HttpMessageHandlerFactory = _ => host.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (origin is not null) options.Headers["Origin"] = origin;
            })
            .Build();
        _connection.On<string>("UserJoined", name => _joined.Writer.TryWrite(name));
        _connection.On<string>("UserLeft", name => _left.Writer.TryWrite(name));
        _connection.On<JsonElement>("CreativeUpdated", creative => _updated.Writer.TryWrite(creative));
    }

    public static async Task<HubClient> ConnectAsync(ApiFactory host, string? origin = null)
    {
        var client = new HubClient(host, origin);
        await client._connection.StartAsync();
        return client;
    }

    public async Task<string[]> JoinAsync(Guid creativeId, string userName) =>
        await _connection.InvokeAsync<string[]>("JoinCreative", creativeId, userName);

    public Task LeaveAsync(Guid creativeId) => _connection.InvokeAsync("LeaveCreative", creativeId);

    public Task StopAsync() => _connection.StopAsync();

    public Task<string> NextUserJoinedAsync() => NextAsync(_joined);
    public Task<string> NextUserLeftAsync() => NextAsync(_left);
    public Task<JsonElement> NextCreativeUpdatedAsync() => NextAsync(_updated);

    private static async Task<T> NextAsync<T>(Channel<T> channel)
    {
        using var timeout = new CancellationTokenSource(Patience);
        return await channel.Reader.ReadAsync(timeout.Token);
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
