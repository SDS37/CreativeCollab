using CreativeCollab.Api.Auth;
using CreativeCollab.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace CreativeCollab.Api.Endpoints;

public static class AdminEndpoints
{
    public sealed record ChangeRoleRequest(string? Role);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization(Policies.CanAdminister);

        // The new role reaches the account's token at its next refresh (SessionService reads roles then).
        group.MapPut("/users/{id}/role", async (string id, ChangeRoleRequest request, UserManager<AppUser> users, CreativeDbContext db) =>
        {
            if (request.Role is null || !AccountRoles.Names.Contains(request.Role))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["role"] = [$"Role must be one of: {string.Join(", ", AccountRoles.Names)}."],
                });

            if (await users.FindByIdAsync(id) is not { } user)
                return Results.NotFound();

            // One transaction: a failure part-way must not leave the account without a role, and the last-Admin
            // check and the change see the same data.
            await using var transaction = await db.Database.BeginTransactionAsync();
            var current = await users.GetRolesAsync(user);
            if (!current.Contains(request.Role))
            {
                if (current.Contains(AccountRoles.Admin) && (await users.GetUsersInRoleAsync(AccountRoles.Admin)).Count <= 1)
                    return Results.Problem(title: "Conflict", detail: "This is the last Admin. Make another account an Admin before changing this role.",
                        statusCode: StatusCodes.Status409Conflict);

                Throw(await users.RemoveFromRolesAsync(user, current));
                Throw(await users.AddToRoleAsync(user, request.Role));
            }
            await transaction.CommitAsync();
            return Results.NoContent();
        });

        return app;
    }

    private static void Throw(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Changing the role failed: " + string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
