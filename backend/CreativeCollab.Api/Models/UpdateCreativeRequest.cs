namespace CreativeCollab.Api.Models;

public sealed record UpdateCreativeRequest(
    string? Name,
    string? Headline,
    string? Body,
    string? Cta,
    string? ImageUrl,
    int? Version) : ICreativeFields;
