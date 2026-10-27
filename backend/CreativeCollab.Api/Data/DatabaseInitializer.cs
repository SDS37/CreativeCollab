using CreativeCollab.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CreativeCollab.Api.Data;

public static class DatabaseInitializer
{
    /// <summary>Applies migrations, then seeds a few Creatives if there are none. Development only.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CreativeDbContext>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        await db.Database.MigrateAsync();

        if (await db.Creatives.AnyAsync())
            return;

        var now = time.GetUtcNow();
        db.Creatives.AddRange(
            Seed("Spring sale banner", "Spring into savings", "Up to 40% off seasonal favourites.", "Shop now", now.AddMinutes(-2)),
            Seed("Newsletter signup", "Never miss a deal", "Join the list for early access to offers.", "Subscribe", now.AddMinutes(-1)),
            Seed("App launch teaser", "Our app is here", "Order faster and track every delivery.", "Get the app", now));
        await db.SaveChangesAsync();
    }

    // Seeded Creatives have no Display name: UpdatedBy is empty.
    private static Creative Seed(string name, string headline, string body, string cta, DateTimeOffset updatedAt) =>
        new(Guid.NewGuid(), name, headline, body, cta, ImageUrl: null, Version: 1, updatedAt, UpdatedBy: "");
}
