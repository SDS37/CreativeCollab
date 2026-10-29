using System.Security.Claims;
using CreativeCollab.Api.Auth;
using CreativeCollab.Api.Validation;
using Microsoft.AspNetCore.Identity;

namespace CreativeCollab.Api.Endpoints;

public static class AuthEndpoints
{
    public const string BasePath = "/api/auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(BasePath);

        group.MapPost("/register", async (RegisterRequest request, UserManager<AppUser> users) =>
        {
            var user = new AppUser { UserName = request.Email, Email = request.Email, DisplayName = request.DisplayName!.Trim() };
            var created = await users.CreateAsync(user, request.Password!);
            if (!created.Succeeded)
                return Results.ValidationProblem(ToProblems(created));

            await users.AddToRoleAsync(user, AccountRoles.Default);
            return Results.Created($"{BasePath}/me", new { id = user.Id, email = user.Email, displayName = user.DisplayName });
        }).AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        group.MapPost("/login", async (LoginRequest request, UserManager<AppUser> users, SessionService sessions, HttpContext http) =>
        {
            var user = await users.FindByEmailAsync(request.Email ?? "");
            if (user is not null && await users.IsLockedOutAsync(user))
                return Results.Problem(title: "Locked", detail: "This account is locked after too many failed sign-ins. Please try again in 15 minutes.",
                    statusCode: StatusCodes.Status423Locked);

            if (user is null || !await users.CheckPasswordAsync(user, request.Password ?? ""))
            {
                if (user is not null) await users.AccessFailedAsync(user);
                return Results.Problem(title: "Unauthorized", detail: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);
            }

            await users.ResetAccessFailedCountAsync(user);
            return Results.Ok(await sessions.StartAsync(user, http));
        });

        group.MapPost("/refresh", async (SessionService sessions, HttpContext http) =>
            await sessions.RefreshAsync(http) is { } renewed
                ? Results.Ok(renewed)
                : Results.Problem(title: "Unauthorized", detail: "Please sign in again.", statusCode: StatusCodes.Status401Unauthorized));

        group.MapPost("/logout", async (SessionService sessions, HttpContext http) =>
        {
            await sessions.EndAsync(http);
            return Results.NoContent();
        });

        group.MapGet("/me", (ClaimsPrincipal principal) => Results.Ok(new
        {
            sub = principal.FindFirstValue("sub"),
            name = principal.FindFirstValue("name"),
            role = principal.FindFirstValue("role"),
            jti = principal.FindFirstValue("jti"),
        })).RequireAuthorization();

        return app;
    }

    private static Dictionary<string, string[]> ToProblems(IdentityResult result) =>
        result.Errors
            .GroupBy(e => e.Code.Contains("Password", StringComparison.Ordinal) ? "password" : "email")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}
