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
            Results.Ok(await service.ListAsync(ct)));

        group.MapGet("/{id:guid}", async (Guid id, CreativeService service, CancellationToken ct) =>
            await service.GetAsync(id, ct) is { } creative ? Results.Ok(creative) : Results.NotFound());

        group.MapPost("/", async (CreateCreativeRequest request, CreativeService service, CancellationToken ct) =>
        {
            var creative = await service.CreateAsync(request, ct);
            return Results.Created($"/api/creatives/{creative.Id}", creative);
        }).AddEndpointFilter<ValidationFilter<CreateCreativeRequest>>();

        group.MapPut("/{id:guid}", async (Guid id, UpdateCreativeRequest request, CreativeService service, CancellationToken ct) =>
            await service.UpdateAsync(id, request, ct) switch
            {
                UpdateResult.Updated u => Results.Ok(u.Creative),
                UpdateResult.Conflict c => ConflictResult(c.Current),
                _ => Results.NotFound(),
            }).AddEndpointFilter<ValidationFilter<UpdateCreativeRequest>>();

        return app;
    }

    private static IResult ConflictResult(Creative current) =>
        Results.Problem(
            title: "Conflict",
            detail: "The Creative was saved by someone else since you loaded it.",
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?> { ["current"] = current });
}
