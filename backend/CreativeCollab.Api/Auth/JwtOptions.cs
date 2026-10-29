namespace CreativeCollab.Api.Auth;

public sealed class JwtOptions
{
    public const int MinimumKeyBytes = 32; // HS256 needs a 256-bit key

    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";

    /// <summary>From user-secrets in development and an environment variable (<c>Jwt__Key</c>) in production. Never in appsettings.</summary>
    public string Key { get; set; } = "";

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>Allowed clock difference when checking token lifetime.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);
}
