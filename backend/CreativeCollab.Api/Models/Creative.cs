namespace CreativeCollab.Api.Models;

public sealed record Creative(
    Guid Id,
    string Name,
    string Headline,
    string Body,
    string Cta,
    string? ImageUrl,
    int Version,
    DateTimeOffset UpdatedAt,
    string UpdatedBy);
