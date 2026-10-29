using System.Security.Claims;

namespace CreativeCollab.Api.Auth;

/// <summary>The claim names in an access token (ADR 0003). They are kept as issued: inbound claim mapping is off.</summary>
public static class AccountClaims
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string TokenId = "jti";

    /// <summary>The Display name the server attested in the token, or null when the token has none.</summary>
    public static string? NameOf(ClaimsPrincipal? principal) => principal?.FindFirstValue(Name);
}
