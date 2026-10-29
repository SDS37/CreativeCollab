namespace CreativeCollab.Api.Models;

/// <summary>The editable fields shared by create and update requests.</summary>
public interface ICreativeFields
{
    string? Name { get; }
    string? Headline { get; }
    string? Body { get; }
    string? Cta { get; }
    string? ImageUrl { get; }
}
