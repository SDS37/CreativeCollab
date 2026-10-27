using CreativeCollab.Api.Data;
using CreativeCollab.Api.Hubs;
using CreativeCollab.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace CreativeCollab.Api.Services;

public sealed class CreativeService(
    ICreativeRepository repository,
    IImageUrlValidator imageValidator,
    IHubContext<CreativeHub> hub,
    TimeProvider time,
    ILogger<CreativeService> logger)
{
    public Task<IReadOnlyList<Creative>> ListAsync(CancellationToken ct) => repository.ListAsync(ct);

    public Task<Creative?> GetAsync(Guid id, CancellationToken ct) => repository.GetAsync(id, ct);

    public async Task<CreateResult> CreateAsync(CreateCreativeRequest request, CancellationToken ct)
    {
        var imageUrl = NormalizeImageUrl(request.ImageUrl);
        if (imageUrl is not null && await imageValidator.CheckAsync(imageUrl, ct) is { IsValid: false } bad)
            return new CreateResult.InvalidImage(bad.Message!);

        var creative = new Creative(
            Id: Guid.NewGuid(),
            Name: request.Name!,
            Headline: request.Headline!,
            Body: request.Body!,
            Cta: request.Cta!,
            ImageUrl: imageUrl,
            Version: 1,
            UpdatedAt: time.GetUtcNow(),
            UpdatedBy: request.DisplayName!);

        await repository.AddAsync(creative, ct);
        return new CreateResult.Created(creative);
    }

    public async Task<UpdateResult> UpdateAsync(Guid id, UpdateCreativeRequest request, CancellationToken ct)
    {
        var existing = await repository.GetAsync(id, ct);
        if (existing is null)
            return new UpdateResult.NotFound();
        if (existing.Version != request.Version)
            return new UpdateResult.Conflict(existing);

        // Only a new or changed image URL costs an outbound call.
        var imageUrl = NormalizeImageUrl(request.ImageUrl);
        if (imageUrl is not null && imageUrl != existing.ImageUrl
            && await imageValidator.CheckAsync(imageUrl, ct) is { IsValid: false } bad)
            return new UpdateResult.InvalidImage(bad.Message!);

        var updated = existing with
        {
            Name = request.Name!,
            Headline = request.Headline!,
            Body = request.Body!,
            Cta = request.Cta!,
            ImageUrl = imageUrl,
            Version = existing.Version + 1,
            UpdatedAt = time.GetUtcNow(),
            UpdatedBy = request.DisplayName!,
        };

        if (await repository.TryUpdateAsync(updated, existing.Version, ct))
        {
            await BroadcastUpdateAsync(updated);
            return new UpdateResult.Updated(updated);
        }

        // Lost a race with another save: report the latest copy.
        return await repository.GetAsync(id, ct) is { } current
            ? new UpdateResult.Conflict(current)
            : new UpdateResult.NotFound();
    }

    private static string? NormalizeImageUrl(string? imageUrl) => string.IsNullOrEmpty(imageUrl) ? null : imageUrl;

    /// <summary>
    /// Pushes the saved Creative to everyone who has it open, the sender included (their client ignores a
    /// Version it already has). The save is already committed, so a failed push is logged, not surfaced,
    /// and it is deliberately not tied to the request's cancellation.
    /// </summary>
    private async Task BroadcastUpdateAsync(Creative creative)
    {
        try
        {
            await hub.Clients.Group(CreativeHub.GroupName(creative.Id))
                .SendAsync(CreativeHub.Events.CreativeUpdated, creative, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Saved Creative {CreativeId} v{Version} but could not broadcast it", creative.Id, creative.Version);
        }
    }
}
