using Microsoft.AspNetCore.Identity;

namespace CreativeCollab.Api.Auth;

/// <summary>An account. The Display name is unique, public, and the name recorded on Creatives and shown in Presence.</summary>
public sealed class AppUser : IdentityUser
{
    public const int DisplayNameMaxLength = 100; // column width; new names are limited by DisplayNames.MaxLength

    private string _displayName = "";

    public string DisplayName
    {
        get => _displayName;
        set
        {
            _displayName = value;
            NormalizedDisplayName = DisplayNames.Normalize(value);
        }
    }

    /// <summary>What the unique index compares: case and compatibility forms folded.</summary>
    public string NormalizedDisplayName { get; set; } = "";
}
