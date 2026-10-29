using CreativeCollab.Api.Auth;

namespace CreativeCollab.Api.Endpoints;

public static class AdminEndpoints
{
    public sealed record ChangeRoleRequest(string? Role);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization(Policies.CanAdminister);

        group.MapPut("/accounts/{id}/role", async (string id, ChangeRoleRequest request, RoleService roles) =>
        {
            if (AccountRole.Parse(request.Role) is not { } role)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = [AccountRole.Expected] });

            return await roles.ChangeRoleAsync(id, role) switch
            {
                RoleChange.Changed => Results.NoContent(),
                RoleChange.AccountNotFound => Results.NotFound(),
                RoleChange.WouldRemoveLastAdmin => Results.Problem(title: "Conflict",
                    detail: "This is the last Admin. Make another account an Admin before changing this role.",
                    statusCode: StatusCodes.Status409Conflict),
                _ => throw new InvalidOperationException("Unhandled role change result."),
            };
        });

        return app;
    }
}
