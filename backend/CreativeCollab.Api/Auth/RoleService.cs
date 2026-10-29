using CreativeCollab.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace CreativeCollab.Api.Auth;

public enum RoleChange
{
    Changed,
    AccountNotFound,
    /// <summary>Refused: it would leave the app without an Admin.</summary>
    WouldRemoveLastAdmin,
}

/// <summary>Changing an Account's role. The new role reaches the account's token at its next refresh (SessionService reads roles then).</summary>
public sealed class RoleService(UserManager<AppUser> users, CreativeDbContext db)
{
    public async Task<RoleChange> ChangeRoleAsync(string accountId, AccountRole role)
    {
        if (await users.FindByIdAsync(accountId) is not { } account)
            return RoleChange.AccountNotFound;

        // One transaction: a failure part-way must not leave the account without a role, and the last-Admin
        // check and the change see the same data.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var current = await users.GetRolesAsync(account);
        if (!current.Contains(role.Name))
        {
            if (current.Contains(AccountRoles.Admin) && (await users.GetUsersInRoleAsync(AccountRoles.Admin)).Count <= 1)
                return RoleChange.WouldRemoveLastAdmin;

            Throw(await users.RemoveFromRolesAsync(account, current));
            Throw(await users.AddToRoleAsync(account, role.Name));
        }
        await transaction.CommitAsync();
        return RoleChange.Changed;
    }

    private static void Throw(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Changing the role failed: " + string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
