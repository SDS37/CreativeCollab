using CreativeCollab.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CreativeCollab.Api.Auth;

/// <summary>What a started or renewed Session hands back: the access token, and the refresh token the caller keeps until <paramref name="RefreshExpires"/>.</summary>
public sealed record IssuedSession(AccessTokenResponse Access, string RefreshToken, DateTimeOffset RefreshExpires);

/// <summary>
/// Sessions: an access token and a refresh token. How the refresh token travels (a scoped HttpOnly cookie) is the endpoint layer's business.
/// A refresh token is single use. Presenting one that was already used or revoked revokes its whole family.
/// </summary>
public sealed class SessionService(CreativeDbContext db, TokenService tokens, UserManager<AppUser> users, TimeProvider time)
{
    /// <summary>Rows this long past expiry, or this long since revocation, are deleted. Opportunistic: it runs on sign-in and refresh.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public async Task<IssuedSession> StartAsync(AppUser user, CancellationToken ct)
    {
        await PurgeAsync(ct);
        return await IssueAsync(user, familyId: Guid.NewGuid(), ct);
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        var cutoff = time.GetUtcNow() - Retention;
        await db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff || (t.RevokedAt != null && t.RevokedAt < cutoff))
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>Rotates the presented refresh token. Null means the caller must sign in again.</summary>
    public async Task<IssuedSession?> RefreshAsync(string? refreshToken, CancellationToken ct)
    {
        await PurgeAsync(ct);
        var presented = await FindAsync(refreshToken, ct);
        if (presented is null)
            return null;

        if (presented.UsedAt is not null || presented.RevokedAt is not null)
        {
            await RevokeFamilyAsync(presented.FamilyId, ct);
            return null;
        }

        var now = time.GetUtcNow();
        if (presented.ExpiresAt <= now)
            return null;

        // The compare-and-set makes rotation single use even when two requests race.
        var rotated = await db.RefreshTokens
            .Where(t => t.Id == presented.Id && t.UsedAt == null && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct);
        if (rotated == 0)
        {
            await RevokeFamilyAsync(presented.FamilyId, ct);
            return null;
        }

        var user = await users.FindByIdAsync(presented.UserId);
        if (user is null || await users.IsLockedOutAsync(user))
        {
            await RevokeFamilyAsync(presented.FamilyId, ct);
            return null;
        }

        return await IssueAsync(user, presented.FamilyId, ct);
    }

    /// <summary>Revokes the Session the presented refresh token belongs to, if any.</summary>
    public async Task EndAsync(string? refreshToken, CancellationToken ct)
    {
        if (await FindAsync(refreshToken, ct) is { } presented)
            await RevokeFamilyAsync(presented.FamilyId, ct);
    }

    private async Task<IssuedSession> IssueAsync(AppUser user, Guid familyId, CancellationToken ct)
    {
        var (token, hash) = TokenService.NewRefreshToken();
        var expires = time.GetUtcNow() + tokens.RefreshTokenLifetime;
        db.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, FamilyId = familyId, TokenHash = hash, ExpiresAt = expires });
        await db.SaveChangesAsync(ct);

        // Roles are read now, so a role change takes effect the next time the session renews.
        var access = tokens.CreateAccessToken(user, await users.GetRolesAsync(user));
        return new IssuedSession(new AccessTokenResponse(access, (int)tokens.AccessTokenLifetime.TotalSeconds), token, expires);
    }

    private async Task<RefreshToken?> FindAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return null;
        var hash = TokenService.Hash(refreshToken);
        return await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }
}
