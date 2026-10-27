namespace CreativeCollab.Api.Models;

public abstract record UpdateResult
{
    private UpdateResult() { }

    public sealed record Updated(Creative Creative) : UpdateResult;
    public sealed record InvalidImage(string Message) : UpdateResult;
    public sealed record NotFound : UpdateResult;
    /// <summary>The supplied Version is stale. Carries the latest Creative.</summary>
    public sealed record Conflict(Creative Current) : UpdateResult;
}
