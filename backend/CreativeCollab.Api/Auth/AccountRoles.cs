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
