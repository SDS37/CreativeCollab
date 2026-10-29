using System.Security.Claims;
using CreativeCollab.Api.Auth;
using CreativeCollab.Api.Models;
using CreativeCollab.Api.Services;
using CreativeCollab.Api.Validation;

namespace CreativeCollab.Api.Endpoints;

public static class CreativeEndpoints
{
    public static IEndpointRouteBuilder MapCreativeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/creatives");

        group.MapGet("/", async (CreativeService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct))).RequireAuthorization(Policies.CanRead);

        group.MapGet("/{id:guid}", async (Guid id, CreativeService service, CancellationToken ct) =>
            await service.GetAsync(id, ct) is { } creative ? Results.Ok(creative) : Results.NotFound()).RequireAuthorization(Policies.CanRead);

        group.MapPost("/", async (CreateCreativeRequest request, ClaimsPrincipal user, CreativeService service, CancellationToken ct) =>
        {
            return await service.CreateAsync(request, AccountName(user), ct) switch
            {
                CreateResult.Created c => Results.Created($"/api/creatives/{c.Creative.Id}", c.Creative),
                CreateResult.InvalidImage i => ImageProblem(i.Message),
                _ => throw new InvalidOperationException("Unhandled create result."),
            };
        }).AddEndpointFilter<ValidationFilter<CreateCreativeRequest>>().RequireAuthorization(Policies.CanEdit);

        group.MapPut("/{id:guid}", async (Guid id, UpdateCreativeRequest request, ClaimsPrincipal user, CreativeService service, CancellationToken ct) =>
            await service.UpdateAsync(id, request, AccountName(user), ct) switch
            {
                UpdateResult.Updated u => Results.Ok(u.Creative),
                UpdateResult.Conflict c => ConflictResult(c.Current),
                UpdateResult.InvalidImage i => ImageProblem(i.Message),
                _ => Results.NotFound(),
            }).AddEndpointFilter<ValidationFilter<UpdateCreativeRequest>>().RequireAuthorization(Policies.CanEdit);

        return app;
    }

    /// <summary>Who is acting, attested by the signed token. Never taken from the request body.</summary>
    private static string AccountName(ClaimsPrincipal user) =>
        AccountClaims.NameOf(user) ?? throw new InvalidOperationException("An authorized token always carries a name.");

    private static IResult ImageProblem(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["imageUrl"] = [message] });

    private static IResult ConflictResult(Creative current) =>
        Results.Problem(
            title: "Conflict",
            detail: "The Creative was saved by someone else since you loaded it.",
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?> { ["current"] = current });
}
