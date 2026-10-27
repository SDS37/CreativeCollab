using System.Collections.Concurrent;
using CreativeCollab.Api.Models;

namespace CreativeCollab.Api.Data;

public sealed class InMemoryCreativeRepository : ICreativeRepository
{
    private readonly ConcurrentDictionary<Guid, Creative> _items = new();

    public Task<IReadOnlyList<Creative>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Creative>>(
            _items.Values.OrderByDescending(c => c.UpdatedAt).ToList());

    public Task<Creative?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.GetValueOrDefault(id));

    public Task AddAsync(Creative creative, CancellationToken ct)
    {
        _items[creative.Id] = creative;
        return Task.CompletedTask;
    }
}
