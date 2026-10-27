using CreativeCollab.Api.Models;
using CreativeCollab.Api.Validation;
using Microsoft.EntityFrameworkCore;

namespace CreativeCollab.Api.Data;

public sealed class CreativeDbContext(DbContextOptions<CreativeDbContext> options) : DbContext(options)
{
    public DbSet<Creative> Creatives => Set<Creative>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Creative>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(CreativeFieldsValidator<CreateCreativeRequest>.NameMaxLength);
            e.Property(c => c.Headline).IsRequired().HasMaxLength(CreativeFieldsValidator<CreateCreativeRequest>.HeadlineMaxLength);
            e.Property(c => c.Body).IsRequired().HasMaxLength(CreativeFieldsValidator<CreateCreativeRequest>.BodyMaxLength);
            e.Property(c => c.Cta).IsRequired().HasMaxLength(CreativeFieldsValidator<CreateCreativeRequest>.CtaMaxLength);
            e.Property(c => c.ImageUrl).HasMaxLength(CreativeFieldsValidator<CreateCreativeRequest>.ImageUrlMaxLength);
            e.Property(c => c.UpdatedBy).IsRequired();

            // SQLite has no rowversion, so the integer Version is the concurrency token (ADR 0001).
            e.Property(c => c.Version).IsConcurrencyToken();

            // Stored as UTC ticks so SQLite can sort by it.
            e.Property(c => c.UpdatedAt).HasConversion(
                v => v.UtcTicks,
                v => new DateTimeOffset(v, TimeSpan.Zero));
            e.HasIndex(c => c.UpdatedAt);
        });
    }
}
