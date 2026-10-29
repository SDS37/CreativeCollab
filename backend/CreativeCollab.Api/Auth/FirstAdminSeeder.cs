using Microsoft.AspNetCore.Identity;

namespace CreativeCollab.Api.Auth;

public static class FirstAdminSeeder
{
    /// <summary>
    /// Creates the first Admin from <c>Admin:Email</c>, <c>Admin:Password</c> and <c>Admin:DisplayName</c> when they are
    /// configured and no account has that email yet. Needs a migrated database. An existing account is never changed.
    /// </summary>
    public static async Task SeedFirstAdminAsync(this WebApplication app)
    {
        var section = app.Configuration.GetSection("Admin");
        var email = section["Email"];
        var password = section["Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.FindByEmailAsync(email) is not null)
            return;

        var admin = new AppUser { UserName = email, Email = email, DisplayName = section["DisplayName"] is { Length: > 0 } name ? name : "Admin" };
        var created = await users.CreateAsync(admin, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                "The configured first Admin could not be created: " + string.Join(" ", created.Errors.Select(e => e.Description)));

        await users.AddToRoleAsync(admin, AccountRoles.Admin);
    }
}
