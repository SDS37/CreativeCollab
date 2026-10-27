using CreativeCollab.Api.Models;

namespace CreativeCollab.Api.Data;

public interface ICreativeRepository
{
    Task<IReadOnlyList<Creative>> ListAsync(CancellationToken ct);
    Task<Creative?> GetAsync(Guid id, CancellationToken ct);
    Task AddAsync(Creative creative, CancellationToken ct);

    /// <summary>
    /// Replaces the stored Creative only if its Version still equals <paramref name="expectedVersion"/>.
    /// Returns false when it does not (or the Creative is gone), so the caller can report a Conflict.
    /// </summary>
    Task<bool> TryUpdateAsync(Creative updated, int expectedVersion, CancellationToken ct);
}
