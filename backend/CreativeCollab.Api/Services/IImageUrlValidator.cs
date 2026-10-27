namespace CreativeCollab.Api.Services;

public interface IImageUrlValidator
{
    /// <summary>Never throws for a bad URL or a network failure: those are reported in the result.</summary>
    Task<ImageCheckResult> CheckAsync(string url, CancellationToken ct);
}

public sealed record ImageCheckResult(bool IsValid, string? Message)
{
    public static ImageCheckResult Ok { get; } = new(true, null);
    public static ImageCheckResult Fail(string message) => new(false, message);
}
