using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CreativeCollab.Api.Auth;

/// <summary>Issues access tokens (signed JWTs) and refresh tokens (opaque, stored hashed).</summary>
public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider time)
{
    private readonly JwtOptions _jwt = options.Value;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(_jwt.AccessTokenMinutes);
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_jwt.RefreshTokenDays);

    public string CreateAccessToken(AppUser user, IEnumerable<string> roles)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var roleList = roles.ToArray();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + AccessTokenLifetime,
            SigningCredentials = new SigningCredentials(JwtBearerSetup.SigningKey(_jwt), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id,
                [JwtRegisteredClaimNames.Name] = user.DisplayName,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
                ["role"] = roleList.Length == 1 ? roleList[0] : roleList,
            },
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>A new random token and its hash. Only the hash is stored.</summary>
    public static (string Token, string Hash) NewRefreshToken()
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
