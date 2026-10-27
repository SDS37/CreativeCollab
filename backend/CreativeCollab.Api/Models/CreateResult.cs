namespace CreativeCollab.Api.Models;

public abstract record CreateResult
{
    private CreateResult() { }

    public sealed record Created(Creative Creative) : CreateResult;
    public sealed record InvalidImage(string Message) : CreateResult;
}
