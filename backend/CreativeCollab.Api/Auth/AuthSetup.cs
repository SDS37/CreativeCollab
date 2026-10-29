using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using CreativeCollab.Api.Data;

namespace CreativeCollab.Api.Auth;

/// <summary>Everything Program.cs needs for accounts: Identity, JWT bearer, policies, the auth rate limit and the session services.</summary>
public static class AuthSetup
{
    public static IServiceCollection AddCreativeCollabAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection("Jwt"))
            .Validate(o => Encoding.UTF8.GetByteCount(o.Key) >= JwtOptions.MinimumKeyBytes,
                $"Jwt:Key must be at least {JwtOptions.MinimumKeyBytes} bytes (256 bits). Set it with user-secrets or the Jwt__Key environment variable.")
            .Validate(o => o.ClockSkew >= TimeSpan.Zero && o.ClockSkew <= JwtOptions.MaximumClockSkew,
                $"Jwt:ClockSkew must be between zero and {JwtOptions.MaximumClockSkew.TotalSeconds} seconds.")
            .ValidateOnStart();
        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 12; // length only, no composition rules
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<CreativeDbContext>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerSetup>();
        services.AddAuthorization(options => options.AddCreativeCollabPolicies());
        services.AddSingleton<TokenService>();
        services.AddScoped<SessionService>();
        services.AddScoped<AccountService>();
        services.AddScoped<RoleService>();
        services.Configure<AuthRateLimitOptions>(configuration.GetSection("RateLimiting:Auth"));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitOptions.PolicyName, http =>
            {
                var limit = http.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                var client = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                // Key on the matched endpoint, not the raw path: routing ignores case and a trailing slash, so the path would not.
                var endpoint = http.GetEndpoint()?.DisplayName ?? http.Request.Path.Value?.TrimEnd('/').ToLowerInvariant() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter($"{endpoint}|{client}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limit.PermitLimit,
                    Window = limit.Window,
                    QueueLimit = 0,
                });
            });
        });
        return services;
    }

    /// <summary>Rate limiting, then authentication, then authorization, in the order the pipeline needs them.</summary>
    public static IApplicationBuilder UseCreativeCollabAuth(this IApplicationBuilder app) =>
        app.UseRateLimiter().UseAuthentication().UseAuthorization();
}
