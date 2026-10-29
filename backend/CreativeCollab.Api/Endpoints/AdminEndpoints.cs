using CreativeCollab.Api.Auth;
using Microsoft.AspNetCore.Identity;

namespace CreativeCollab.Api.Endpoints;

public static class AdminEndpoints
{
    public sealed record ChangeRoleRequest(string? Role);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization(Policies.CanAdminister);

        // The new role reaches the account's token at its next refresh (SessionService reads roles then).
        group.MapPut("/users/{id}/role", async (string id, ChangeRoleRequest request, UserManager<AppUser> users) =>
        {
            if (request.Role is null || !AccountRoles.Names.Contains(request.Role))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["role"] = [$"Role must be one of: {string.Join(", ", AccountRoles.Names)}."],
                });

            if (await users.FindByIdAsync(id) is not { } user)
                return Results.NotFound();

            var current = await users.GetRolesAsync(user);
            if (!current.Contains(request.Role))
            {
                await users.RemoveFromRolesAsync(user, current);
                await users.AddToRoleAsync(user, request.Role);
            }
            return Results.NoContent();
        });

        return app;
    }
}
