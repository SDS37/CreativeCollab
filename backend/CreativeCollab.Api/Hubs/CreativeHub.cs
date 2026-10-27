using Microsoft.AspNetCore.SignalR;

namespace CreativeCollab.Api.Hubs;

/// <summary>
/// Real-time channel for one Creative at a time per join. It tracks Presence and relays announcements;
/// saving a Creative is the service's job, never the hub's.
/// </summary>
public sealed class CreativeHub(PresenceTracker presence) : Hub
{
    public static class Events
    {
        public const string CreativeUpdated = "CreativeUpdated";
        public const string UserJoined = "UserJoined";
        public const string UserLeft = "UserLeft";
    }

    public static string GroupName(Guid creativeId) => $"creative-{creativeId}";

    /// <summary>Joins the Creative's group and returns who is present now, including the caller.</summary>
    public async Task<string[]> JoinCreative(Guid creativeId, string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new HubException("A display name is required.");

        var group = GroupName(creativeId);
        var added = presence.Join(Context.ConnectionId, creativeId, userName);
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        if (added)
            await Clients.OthersInGroup(group).SendAsync(Events.UserJoined, userName);

        return [.. presence.Present(creativeId)];
    }

    public async Task LeaveCreative(Guid creativeId)
    {
        var group = GroupName(creativeId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        if (presence.Leave(Context.ConnectionId, creativeId) is { } userName)
            await Clients.OthersInGroup(group).SendAsync(Events.UserLeft, userName);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var (creativeId, userName) in presence.Disconnect(Context.ConnectionId))
            await Clients.OthersInGroup(GroupName(creativeId)).SendAsync(Events.UserLeft, userName);

        await base.OnDisconnectedAsync(exception);
    }
}
