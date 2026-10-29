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

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);

    public static SymmetricSecurityKey SigningKey(JwtOptions settings) => new(Encoding.UTF8.GetBytes(settings.Key));
}
