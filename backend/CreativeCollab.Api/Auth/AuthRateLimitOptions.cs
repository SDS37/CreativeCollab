namespace CreativeCollab.Api.Auth;

/// <summary>Limit on register, login and refresh, counted per client address and per endpoint.</summary>
public sealed class AuthRateLimitOptions
{
    public const string PolicyName = "auth";

    public int PermitLimit { get; set; } = 10;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
