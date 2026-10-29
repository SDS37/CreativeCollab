using Microsoft.AspNetCore.Authorization;

namespace CreativeCollab.Api.Auth;

/// <summary>
/// Authorization policies. Roles build on each other: an Editor can do what a Viewer can, an Admin what an Editor can.
/// A valid token with no known role is authenticated but forbidden (403).
/// </summary>
public static class Policies
{
    public const string CanRead = nameof(CanRead);
    public const string CanEdit = nameof(CanEdit);
    public const string CanAdminister = nameof(CanAdminister);

    public static void AddCreativeCollabPolicies(this AuthorizationOptions options)
    {
        // Anything that does not say otherwise needs a signed-in user, so a new endpoint is protected by default.
        // The few open endpoints (register, sign-in, refresh, logout) opt out with AllowAnonymous.
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        options.AddPolicy(CanRead, p => p.RequireRole(AccountRoles.Viewer, AccountRoles.Editor, AccountRoles.Admin));
        options.AddPolicy(CanEdit, p => p.RequireRole(AccountRoles.Editor, AccountRoles.Admin));
        options.AddPolicy(CanAdminister, p => p.RequireRole(AccountRoles.Admin));
    }
}
