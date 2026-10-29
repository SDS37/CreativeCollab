using System.Security.Claims;
using CreativeCollab.Api.Auth;
using CreativeCollab.Api.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CreativeCollab.Api.Endpoints;

public static class AuthEndpoints
{
    public const string BasePath = "/api/auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(BasePath);

        group.MapPost("/register", async (RegisterRequest request, UserManager<AppUser> users) =>
        {
            // Display names are public, so saying one is taken is fine (unlike an email).
            var normalized = DisplayNames.Normalize(request.DisplayName!);
            if (await users.Users.AnyAsync(u => u.NormalizedDisplayName == normalized))
                return Results.ValidationProblem(DisplayNameTaken);

            var user = new AppUser { UserName = request.Email, Email = request.Email, DisplayName = request.DisplayName! };
            IdentityResult created;
            try
            {
                created = await users.CreateAsync(user, request.Password!);
            }
            catch (DbUpdateException)
            {
                // Lost a race for the same name (the unique index caught it); anything else is a real failure.
                if (!await users.Users.AnyAsync(u => u.NormalizedDisplayName == normalized)) throw;
                return Results.ValidationProblem(DisplayNameTaken);
            }
            if (!created.Succeeded)
                return Results.ValidationProblem(ToProblems(created));

            await users.AddToRoleAsync(user, AccountRoles.Default);
            return Results.Created($"{BasePath}/me", new { id = user.Id, email = user.Email, displayName = user.DisplayName });
        }).AddEndpointFilter<ValidationFilter<RegisterRequest>>().RequireRateLimiting(AuthRateLimitOptions.PolicyName);

        group.MapPost("/login", async (LoginRequest request, UserManager<AppUser> users, SessionService sessions, IPasswordHasher<AppUser> hasher, HttpContext http) =>
        {
            var user = await users.FindByEmailAsync(request.Email ?? "");
            if (user is not null && await users.IsLockedOutAsync(user))
                return Results.Problem(title: "Locked", detail: "This account is locked after too many failed sign-ins. Please try again in 15 minutes.",
                    statusCode: StatusCodes.Status423Locked);

            // An unknown email still pays for one password hash check, so response time does not reveal which emails have accounts.
            if (user is null)
                hasher.VerifyHashedPassword(new AppUser(), DummyHash(hasher), request.Password ?? "");

            if (user is null || !await users.CheckPasswordAsync(user, request.Password ?? ""))
            {
                if (user is not null) await users.AccessFailedAsync(user);
                return Results.Problem(title: "Unauthorized", detail: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);
            }

            await users.ResetAccessFailedCountAsync(user);
            return Results.Ok(await sessions.StartAsync(user, http));
        }).RequireRateLimiting(AuthRateLimitOptions.PolicyName);

        group.MapPost("/refresh", async (SessionService sessions, HttpContext http) =>
            await sessions.RefreshAsync(http) is { } renewed
                ? Results.Ok(renewed)
                : Results.Problem(title: "Unauthorized", detail: "Please sign in again.", statusCode: StatusCodes.Status401Unauthorized))
            .RequireRateLimiting(AuthRateLimitOptions.PolicyName);

        group.MapPost("/logout", async (SessionService sessions, HttpContext http) =>
        {
            await sessions.EndAsync(http);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("/me", (ClaimsPrincipal principal) => Results.Ok(new
        {
            sub = principal.FindFirstValue("sub"),
            name = principal.FindFirstValue("name"),
            role = principal.FindFirstValue("role"),
            jti = principal.FindFirstValue("jti"),
        })).RequireAuthorization();

        return app;
    }

    private static readonly Dictionary<string, string[]> DisplayNameTaken = new()
    {
        ["displayName"] = ["That name is taken. Please choose another."],
    };

    private static string? _dummyHash;

    private static string DummyHash(IPasswordHasher<AppUser> hasher) =>
        _dummyHash ??= hasher.HashPassword(new AppUser(), Guid.NewGuid().ToString("N"));

    public const string GenericRegistrationMessage = "Registration could not be completed with these details. Check the details and try again, or sign in.";

    /// <summary>
    /// A duplicate email or user name gets one generic message, so registering cannot be used to learn which emails have accounts.
    /// (A successful registration still differs from a failed one: without email verification that cannot be closed. ADR 0003.)
    /// </summary>
    private static Dictionary<string, string[]> ToProblems(IdentityResult result)
    {
        var problems = new Dictionary<string, List<string>>();
        foreach (var error in result.Errors)
        {
            var (field, message) = error.Code switch
            {
                nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName) => ("email", GenericRegistrationMessage),
                var code when code.Contains("Password", StringComparison.Ordinal) => ("password", error.Description),
                _ => ("email", error.Description),
            };
            if (!problems.TryGetValue(field, out var list)) problems[field] = list = [];
            if (!list.Contains(message)) list.Add(message);
        }
        return problems.ToDictionary(p => p.Key, p => p.Value.ToArray());
    }
}
