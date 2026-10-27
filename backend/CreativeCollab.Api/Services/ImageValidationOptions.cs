namespace CreativeCollab.Api.Services;

public sealed class ImageValidationOptions
{
    /// <summary>Budget for the whole check, and for each request within it.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
}
