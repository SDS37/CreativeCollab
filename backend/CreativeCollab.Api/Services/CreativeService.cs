using CreativeCollab.Api.Data;
using CreativeCollab.Api.Models;

namespace CreativeCollab.Api.Services;

public sealed class CreativeService(ICreativeRepository repository, TimeProvider time)
{
    public Task<IReadOnlyList<Creative>> ListAsync(CancellationToken ct) => repository.ListAsync(ct);

    public Task<Creative?> GetAsync(Guid id, CancellationToken ct) => repository.GetAsync(id, ct);

    public async Task<Creative> CreateAsync(CreateCreativeRequest request, CancellationToken ct)
    {
        var creative = new Creative(
            Id: Guid.NewGuid(),
            Name: request.Name!,
            Headline: request.Headline!,
            Body: request.Body!,
            Cta: request.Cta!,
            ImageUrl: string.IsNullOrEmpty(request.ImageUrl) ? null : request.ImageUrl,
            Version: 1,
            UpdatedAt: time.GetUtcNow(),
            UpdatedBy: request.DisplayName!);

        await repository.AddAsync(creative, ct);
        return creative;
    }

    public async Task<UpdateResult> UpdateAsync(Guid id, UpdateCreativeRequest request, CancellationToken ct)
    {
        var existing = await repository.GetAsync(id, ct);
        if (existing is null)
            return new UpdateResult.NotFound();
        if (existing.Version != request.Version)
            return new UpdateResult.Conflict(existing);

        var updated = existing with
        {
            Name = request.Name!,
            Headline = request.Headline!,
            Body = request.Body!,
            Cta = request.Cta!,
            ImageUrl = string.IsNullOrEmpty(request.ImageUrl) ? null : request.ImageUrl,
            Version = existing.Version + 1,
            UpdatedAt = time.GetUtcNow(),
            UpdatedBy = request.DisplayName!,
        };

        if (await repository.TryUpdateAsync(updated, existing.Version, ct))
            return new UpdateResult.Updated(updated);

        // Lost a race with another save: report the latest copy.
        return await repository.GetAsync(id, ct) is { } current
            ? new UpdateResult.Conflict(current)
            : new UpdateResult.NotFound();
    }
}
