namespace CreativeCollab.Api.Auth;

public static class AccountRoles
{
    public const string Viewer = "Viewer";
    public const string Editor = "Editor";
    public const string Admin = "Admin";

    /// <summary>Assigned at registration.</summary>
    public const string Default = Editor;

    public static readonly string[] Names = [Viewer, Editor, Admin];
}

/// <summary>A role that is known to exist. Parse a request's text once at the edge; past that point nothing re-checks it.</summary>
public readonly record struct AccountRole
{
    private AccountRole(string name) => Name = name;

    public string Name { get; }

    /// <summary>Null when the text is not one of the role names (the match is exact).</summary>
    public static AccountRole? Parse(string? text) =>
        text is not null && AccountRoles.Names.Contains(text) ? new AccountRole(text) : null;

    public static string Expected => $"Role must be one of: {string.Join(", ", AccountRoles.Names)}.";
}
