namespace CreativeCollab.Api.Models;

public sealed record UpdateCreativeRequest(
    string? Name,
    string? Headline,
    string? Body,
    string? Cta,
    string? ImageUrl,
    string? DisplayName,
    int? Version) : ICreativeFields;
