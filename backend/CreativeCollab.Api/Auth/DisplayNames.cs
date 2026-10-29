using System.Text;
using System.Text.RegularExpressions;

namespace CreativeCollab.Api.Auth;

/// <summary>
/// The rule for a Display name. Names are public and unique, so nobody can pass as another account in
/// <c>updatedBy</c> or Presence. Letters, digits, single spaces and <c>. _ - '</c> only, 2 to 40 characters,
/// no leading, trailing or doubled spaces. Uniqueness compares the <see cref="Normalize">normalized</see> form.
/// </summary>
public static partial class DisplayNames
{
    public const int MinLength = 2;
    public const int MaxLength = 40;

    public const string Rule =
        "Use 2 to 40 letters, digits, spaces and . _ - ' only, with no leading, trailing or double spaces.";

    [GeneratedRegex(@"^[\p{L}\p{N}._'-]+( [\p{L}\p{N}._'-]+)*$")]
    private static partial Regex Allowed();

    public static bool IsValid(string? name) =>
        name is { Length: >= MinLength and <= MaxLength } && Allowed().IsMatch(name);

    /// <summary>Unicode compatibility form (so a full-width letter equals its plain one), then upper case.</summary>
    public static string Normalize(string name) => name.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
}
