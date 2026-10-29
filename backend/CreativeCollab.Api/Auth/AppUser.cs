using Microsoft.AspNetCore.Identity;

namespace CreativeCollab.Api.Auth;

/// <summary>An account. The Display name is the name recorded on Creatives and shown in Presence.</summary>
public sealed class AppUser : IdentityUser
{
    public const int DisplayNameMaxLength = 100;

    public string DisplayName { get; set; } = "";
}
