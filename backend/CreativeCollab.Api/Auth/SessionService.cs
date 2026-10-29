using CreativeCollab.Api.Data;
using CreativeCollab.Api.Endpoints;
using Microsoft.AspNetCore.Identity;

namespace CreativeCollab.Api.Auth;

/// <summary>Starts a session: an access token for the body and a refresh token in a scoped HttpOnly cookie.</summary>
public sealed class SessionService(CreativeDbContext db, TokenService tokens, UserManager<AppUser> users, TimeProvider time)
{
    public const string RefreshCookieName = "refresh_token";

    public async Task<AccessTokenResponse> StartAsync(AppUser user, HttpContext http)
    {
        var (token, hash) = TokenService.NewRefreshToken();
        var expires = time.GetUtcNow() + tokens.RefreshTokenLifetime;
        db.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, FamilyId = Guid.NewGuid(), TokenHash = hash, ExpiresAt = expires });
        await db.SaveChangesAsync(http.RequestAborted);

        SetCookie(http, token, expires);
        var access = tokens.CreateAccessToken(user, await users.GetRolesAsync(user));
        return new AccessTokenResponse(access, (int)tokens.AccessTokenLifetime.TotalSeconds);
    }

    private static void SetCookie(HttpContext http, string token, DateTimeOffset expires) =>
        http.Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = AuthEndpoints.BasePath,
            Expires = expires,
            IsEssential = true,
        });
}
