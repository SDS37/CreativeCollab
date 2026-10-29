namespace CreativeCollab.Api.Models;

public sealed record CreateCreativeRequest(
    string? Name,
    string? Headline,
    string? Body,
    string? Cta,
    string? ImageUrl) : ICreativeFields;
