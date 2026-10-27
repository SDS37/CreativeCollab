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

        return app;
    }
}
