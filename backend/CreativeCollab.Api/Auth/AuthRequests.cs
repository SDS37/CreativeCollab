namespace CreativeCollab.Api.Auth;

public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record AccessTokenResponse(string AccessToken, int ExpiresIn);
