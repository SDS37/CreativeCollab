using System.Collections.Concurrent;

namespace CreativeCollab.Api.Hubs;

/// <summary>
/// Presence: which Display names have which Creative open, one entry per hub connection.
/// In memory and per process (ADR 0002). It is a singleton, so it depends on nothing scoped.
/// </summary>
public sealed class PresenceTracker
{
    // connection id -> (creative id -> Display name that connection joined with)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, string>> _connections = new();

    /// <summary>Returns false when this connection was already present on the Creative.</summary>
    public bool Join(string connectionId, Guid creativeId, string userName) =>
        _connections.GetOrAdd(connectionId, _ => new()).TryAdd(creativeId, userName);

    /// <summary>Returns the Display name the connection had on the Creative, or null if it was not present.</summary>
    public string? Leave(string connectionId, Guid creativeId) =>
        _connections.TryGetValue(connectionId, out var joined) && joined.TryRemove(creativeId, out var name) ? name : null;

    /// <summary>Forgets the connection and returns every Creative it was present on.</summary>
    public IReadOnlyList<(Guid CreativeId, string UserName)> Disconnect(string connectionId) =>
        _connections.TryRemove(connectionId, out var joined)
            ? [.. joined.Select(j => (j.Key, j.Value))]
            : [];

    /// <summary>One Display name per connection present on the Creative, so a name can repeat.</summary>
    public IReadOnlyList<string> Present(Guid creativeId) =>
        [.. _connections.Values
            .Select(joined => joined.TryGetValue(creativeId, out var name) ? name : null)
            .OfType<string>()
            .Order(StringComparer.Ordinal)];
}
