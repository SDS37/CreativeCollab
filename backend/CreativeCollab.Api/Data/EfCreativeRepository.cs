using CreativeCollab.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CreativeCollab.Api.Data;

public sealed class EfCreativeRepository(CreativeDbContext db) : ICreativeRepository
{
    public async Task<IReadOnlyList<Creative>> ListAsync(CancellationToken ct) =>
        await db.Creatives.AsNoTracking()
            .OrderByDescending(c => c.UpdatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

    public Task<Creative?> GetAsync(Guid id, CancellationToken ct) =>
        db.Creatives.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddAsync(Creative creative, CancellationToken ct)
    {
        db.Creatives.Add(creative);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> TryUpdateAsync(Creative updated, int expectedVersion, CancellationToken ct)
    {
        var tracked = await db.Creatives.FirstOrDefaultAsync(c => c.Id == updated.Id, ct);
        if (tracked is null)
            return false;

        var entry = db.Entry(tracked);
        entry.CurrentValues.SetValues(updated);
        // The UPDATE's WHERE clause uses the original value, so pin it to what the caller loaded.
        entry.Property(c => c.Version).OriginalValue = expectedVersion;

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
