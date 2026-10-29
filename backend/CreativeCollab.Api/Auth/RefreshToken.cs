namespace CreativeCollab.Api.Auth;

/// <summary>
/// One refresh token in a family. Only the hash is stored. A token is used once: rotating it marks it
/// used and issues the next token in the same family. Presenting a used or revoked token revokes the family.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
