using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CreativeCollab.Api.Auth;

/// <summary>
/// Token validation. Options are read lazily so the key can come from any configuration source.
/// The algorithm is pinned, so a token signed with another algorithm (or none) is rejected.
/// </summary>
public sealed class JwtBearerSetup(IOptions<JwtOptions> jwt, TimeProvider time) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        var settings = jwt.Value;
        options.MapInboundClaims = false; // keep sub, name, role as issued
        options.Events = new JwtBearerEvents { OnMessageReceived = AcceptQueryTokenOnHubPaths };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey(settings),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = settings.ClockSkew,
            // The lifetime check reads the injected clock, so time is controllable in tests.
            LifetimeValidator = (notBefore, expires, _, parameters) =>
            {
                var now = time.GetUtcNow().UtcDateTime;
                return expires is not null
                    && now <= expires.Value.ToUniversalTime() + parameters.ClockSkew
                    && (notBefore is null || now >= notBefore.Value.ToUniversalTime() - parameters.ClockSkew);
            },
            NameClaimType = "name",
            RoleClaimType = "role",
        };
    }

    public const string HubPathPrefix = "/hubs";

    /// <summary>
    /// Browsers cannot set headers on a WebSocket or event-stream connection, so the hub takes the token as the
    /// <c>access_token</c> query value. Only on hub paths: a token in a URL elsewhere would end up in logs for no reason.
    /// </summary>
    private static Task AcceptQueryTokenOnHubPaths(MessageReceivedContext context)
    {
        if (context.Request.Path.StartsWithSegments(HubPathPrefix)
            && context.Request.Query["access_token"] is { Count: > 0 } token
            && !string.IsNullOrEmpty(token[0]))
            context.Token = token[0];
        return Task.CompletedTask;
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

    public static SymmetricSecurityKey SigningKey(JwtOptions settings) => new(Encoding.UTF8.GetBytes(settings.Key));
}
