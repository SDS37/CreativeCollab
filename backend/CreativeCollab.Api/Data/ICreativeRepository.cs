using CreativeCollab.Api.Models;

namespace CreativeCollab.Api.Data;

public interface ICreativeRepository
{
    Task<IReadOnlyList<Creative>> ListAsync(CancellationToken ct);
    Task<Creative?> GetAsync(Guid id, CancellationToken ct);
    Task AddAsync(Creative creative, CancellationToken ct);
}
