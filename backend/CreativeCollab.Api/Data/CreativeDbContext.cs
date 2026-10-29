using CreativeCollab.Api.Auth;
using CreativeCollab.Api.Models;
using CreativeCollab.Api.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CreativeCollab.Api.Data;

public sealed class CreativeDbContext(DbContextOptions<CreativeDbContext> options)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<Creative> Creatives => Set<Creative>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(e =>
        {
            e.Property(u => u.DisplayName).IsRequired().HasMaxLength(AppUser.DisplayNameMaxLength);
            e.Property(u => u.NormalizedDisplayName).IsRequired().HasMaxLength(AppUser.DisplayNameMaxLength * 2);
            e.HasIndex(u => u.NormalizedDisplayName).IsUnique();
        });

        // Roles are part of the schema, so every database has them from the first migration.
        modelBuilder.Entity<IdentityRole>().HasData(AccountRoles.Names.Select(name => new IdentityRole
        {
            Id = $"role-{name.ToLowerInvariant()}",
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ConcurrencyStamp = $"role-{name.ToLowerInvariant()}-v1",
        }));

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => t.FamilyId);
            e.Property(t => t.TokenHash).IsRequired();
            e.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            // Stored as UTC ticks, like Creative.UpdatedAt.
            e.Property(t => t.ExpiresAt).HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));
            e.Property(t => t.UsedAt).HasConversion(
                v => v == null ? (long?)null : v.Value.UtcTicks,
                v => v == null ? null : new DateTimeOffset(v.Value, TimeSpan.Zero));
            e.Property(t => t.RevokedAt).HasConversion(
                v => v == null ? (long?)null : v.Value.UtcTicks,
                v => v == null ? null : new DateTimeOffset(v.Value, TimeSpan.Zero));
        });

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
