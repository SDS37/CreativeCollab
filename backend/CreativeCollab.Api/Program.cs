using System.Text;
using System.Threading.RateLimiting;
using CreativeCollab.Api.Auth;
using CreativeCollab.Api.Data;
using CreativeCollab.Api.Endpoints;
using CreativeCollab.Api.Hubs;
using CreativeCollab.Api.Infrastructure;
using CreativeCollab.Api.Services;
using CreativeCollab.Api.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<CreativeDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("Creatives") ?? "Data Source=creativecollab.db"));
builder.Services.Configure<ImageValidationOptions>(builder.Configuration.GetSection("ImageValidation"));
builder.Services.AddSingleton<IHostAddressResolver, DnsHostAddressResolver>();
builder.Services.AddHttpClient<IImageUrlValidator, ImageUrlValidator>((sp, client) =>
        client.Timeout = sp.GetRequiredService<IOptions<ImageValidationOptions>>().Value.Timeout)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<ICreativeRepository, EfCreativeRepository>();
builder.Services.AddScoped<CreativeService>();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials())); // SignalR needs credentials, so the origin list is explicit, never a wildcard
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(o => Encoding.UTF8.GetByteCount(o.Key) >= JwtOptions.MinimumKeyBytes,
        $"Jwt:Key must be at least {JwtOptions.MinimumKeyBytes} bytes (256 bits). Set it with user-secrets or the Jwt__Key environment variable.")
    .ValidateOnStart();
builder.Services.AddIdentityCore<AppUser>(options =>
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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerSetup>();
builder.Services.AddAuthorization(options => options.AddCreativeCollabPolicies());
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<SessionService>();
builder.Services.Configure<AuthRateLimitOptions>(builder.Configuration.GetSection("RateLimiting:Auth"));
builder.Services.AddRateLimiter(options =>
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
builder.Services.AddSignalR();
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateCreativeRequestValidator>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    await app.InitializeDatabaseAsync();

// Outside Development the database is migrated by the deployer; the first Admin is created once it exists.
await app.SeedFirstAdminAsync();

app.MapCreativeEndpoints();
app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapHub<CreativeHub>("/hubs/creatives");

app.Run();

public partial class Program;
