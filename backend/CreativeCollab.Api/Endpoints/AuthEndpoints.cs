using CreativeCollab.Api.Auth;
using CreativeCollab.Api.Validation;

namespace CreativeCollab.Api.Endpoints;

public static class AuthEndpoints
{
    public const string BasePath = "/api/auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(BasePath);

        group.MapPost("/register", async (RegisterRequest request, AccountService accounts) =>
            await accounts.RegisterAsync(request) switch
            {
                RegisterResult.Registered r => Results.Json(new { id = r.Account.Id, email = r.Account.Email, displayName = r.Account.DisplayName }, statusCode: StatusCodes.Status201Created),
                RegisterResult.Invalid i => Results.ValidationProblem(i.Problems),
                _ => throw new InvalidOperationException("Unhandled register result."),
            }).AddEndpointFilter<ValidationFilter<RegisterRequest>>().RequireRateLimiting(AuthRateLimitOptions.PolicyName).AllowAnonymous();

        group.MapPost("/login", async (LoginRequest request, AccountService accounts, SessionService sessions, HttpContext http) =>
        {
            switch (await accounts.SignInAsync(request))
            {
                case SignInResult.SignedIn s:
                    var session = await sessions.StartAsync(s.Account, http.RequestAborted);
                    RefreshCookie.Write(http, session);
                    return Results.Ok(session.Access);
                case SignInResult.LockedOut:
                    return Results.Problem(title: "Locked", detail: "This account is locked after too many failed sign-ins. Please try again in 15 minutes.",
                        statusCode: StatusCodes.Status423Locked);
                default:
                    return Results.Problem(title: "Unauthorized", detail: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);
            }
        }).RequireRateLimiting(AuthRateLimitOptions.PolicyName).AllowAnonymous();

        group.MapPost("/refresh", async (SessionService sessions, HttpContext http) =>
        {
            if (await sessions.RefreshAsync(RefreshCookie.Read(http), http.RequestAborted) is not { } renewed)
            {
                RefreshCookie.Clear(http);
                return Results.Problem(title: "Unauthorized", detail: "Please sign in again.", statusCode: StatusCodes.Status401Unauthorized);
            }
            RefreshCookie.Write(http, renewed);
            return Results.Ok(renewed.Access);
        }).RequireRateLimiting(AuthRateLimitOptions.PolicyName).AllowAnonymous();

        group.MapPost("/logout", async (SessionService sessions, HttpContext http) =>
        {
            // The refresh cookie identifies the session, so signing out works with an expired or missing access token.
            await sessions.EndAsync(RefreshCookie.Read(http), http.RequestAborted);
            RefreshCookie.Clear(http);
            return Results.NoContent();
        }).AllowAnonymous();

        return app;
    }
}
