using CreativeCollab.Api.Auth;

namespace CreativeCollab.Api.Endpoints;

/// <summary>The refresh token's cookie: HttpOnly, Secure, SameSite=Strict and scoped to the auth path. HTTP details only; the token's rules live in SessionService.</summary>
internal static class RefreshCookie
{
    public const string Name = "refresh_token";

    public static string? Read(HttpContext http) =>
        http.Request.Cookies.TryGetValue(Name, out var token) ? token : null;

    public static void Write(HttpContext http, IssuedSession session) =>
        http.Response.Cookies.Append(Name, session.RefreshToken, Options(session.RefreshExpires));

    public static void Clear(HttpContext http) =>
        http.Response.Cookies.Delete(Name, Options(expires: null));

    private static CookieOptions Options(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = AuthEndpoints.BasePath,
        Expires = expires,
        IsEssential = true,
    };
}
