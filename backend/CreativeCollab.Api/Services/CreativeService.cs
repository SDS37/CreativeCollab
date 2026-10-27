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
}
