using CreativeCollab.Api.Data;
using CreativeCollab.Api.Endpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CreativeCollab.Api.Auth;

/// <summary>
/// Sessions: an access token for the response body and a refresh token in a scoped HttpOnly cookie.
/// A refresh token is single use. Presenting one that was already used or revoked revokes its whole family.
/// </summary>
public sealed class SessionService(CreativeDbContext db, TokenService tokens, UserManager<AppUser> users, TimeProvider time)
{
    public const string RefreshCookieName = "refresh_token";

    /// <summary>Rows this long past expiry, or this long since revocation, are deleted. Opportunistic: it runs on login and refresh.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public async Task<AccessTokenResponse> StartAsync(AppUser user, HttpContext http)
    {
        await PurgeAsync(http.RequestAborted);
        return await IssueAsync(user, familyId: Guid.NewGuid(), http);
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        var cutoff = time.GetUtcNow() - Retention;
        await db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff || (t.RevokedAt != null && t.RevokedAt < cutoff))
            .ExecuteDeleteAsync(ct);
    }

    /// <summary>Rotates the refresh token in the cookie. Null means the caller must sign in again.</summary>
    public async Task<AccessTokenResponse?> RefreshAsync(HttpContext http)
    {
        var ct = http.RequestAborted;
        await PurgeAsync(ct);
        var presented = await FindAsync(http, ct);
        if (presented is null)
            return Reject(http);

        if (presented.UsedAt is not null || presented.RevokedAt is not null)
        {
            await RevokeFamilyAsync(presented.FamilyId, ct);
            return Reject(http);
        }

        var now = time.GetUtcNow();
        if (presented.ExpiresAt <= now)
            return Reject(http);

        // The compare-and-set makes rotation single use even when two requests race.
        var rotated = await db.RefreshTokens
            .Where(t => t.Id == presented.Id && t.UsedAt == null && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct);
        if (rotated == 0)
        {
            await RevokeFamilyAsync(presented.FamilyId, ct);
            return Reject(http);
        }

        var user = await users.FindByIdAsync(presented.UserId);
        if (user is null || await users.IsLockedOutAsync(user))
        {
            await RevokeFamilyAsync(presented.FamilyId, ct);
            return Reject(http);
        }

        return await IssueAsync(user, presented.FamilyId, http);
    }

    /// <summary>Revokes the session the cookie belongs to, if any, and clears the cookie.</summary>
    public async Task EndAsync(HttpContext http)
    {
        if (await FindAsync(http, http.RequestAborted) is { } presented)
            await RevokeFamilyAsync(presented.FamilyId, http.RequestAborted);
        ClearCookie(http);
    }

    private async Task<AccessTokenResponse> IssueAsync(AppUser user, Guid familyId, HttpContext http)
    {
        var (token, hash) = TokenService.NewRefreshToken();
        var expires = time.GetUtcNow() + tokens.RefreshTokenLifetime;
        db.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, FamilyId = familyId, TokenHash = hash, ExpiresAt = expires });
        await db.SaveChangesAsync(http.RequestAborted);

        http.Response.Cookies.Append(RefreshCookieName, token, CookieOptions(expires));
        // Roles are read now, so a role change takes effect the next time the session renews.
        var access = tokens.CreateAccessToken(user, await users.GetRolesAsync(user));
        return new AccessTokenResponse(access, (int)tokens.AccessTokenLifetime.TotalSeconds);
    }

    private async Task<RefreshToken?> FindAsync(HttpContext http, CancellationToken ct)
    {
        if (!http.Request.Cookies.TryGetValue(RefreshCookieName, out var token) || string.IsNullOrEmpty(token))
            return null;
        var hash = TokenService.Hash(token);
        return await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private static AccessTokenResponse? Reject(HttpContext http)
    {
        ClearCookie(http);
        return null;
    }

    private static void ClearCookie(HttpContext http) =>
        http.Response.Cookies.Delete(RefreshCookieName, CookieOptions(expires: null));

    private static CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = AuthEndpoints.BasePath,
        Expires = expires,
        IsEssential = true,
    };
}
