using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CreativeCollab.Api.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CreativeCollab.Api.Tests;

/// <summary>A real account on a test host, signed in with a real token. No authentication is bypassed.</summary>
internal sealed record Account(HttpClient Client, Session Session, string Id, string Name)
{
    public string AccessToken => Session.AccessToken;
}

internal static class TestAccounts
{
    /// <summary>Registers an account, gives it the role through the Admin endpoint, and signs it in.</summary>
    public static async Task<Account> NewAccountAsync(this ApiFactory host, string role = AccountRoles.Editor, string? name = null)
    {
        name ??= AuthClient.UniqueName();
        var client = host.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var registered = await client.RegisterAsync(email, displayName: name);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var id = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        if (role != AccountRoles.Default)
            await ChangeRoleAsAdminAsync(host, id, role);

        return await RegisterAndSignInAsync(client, email, id, name);
    }

    /// <summary>An account whose token is valid but carries no known role.</summary>
    public static async Task<Account> NewAccountWithoutRoleAsync(this ApiFactory host, string name = "Nobody")
    {
        var client = host.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var registered = await client.RegisterAsync(email, displayName: name);
        var id = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        using (var scope = host.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var account = (await users.FindByIdAsync(id))!;
            await users.RemoveFromRolesAsync(account, await users.GetRolesAsync(account));
        }
        return await RegisterAndSignInAsync(client, email, id, name);
    }

    /// <summary>A client that sends the account's access token with every request.</summary>
    public static async Task<HttpClient> SignedInAsync(this ApiFactory host, string role = AccountRoles.Editor, string? name = null) =>
        (await host.NewAccountAsync(role, name)).Client;

    private static async Task<Account> RegisterAndSignInAsync(HttpClient client, string email, string id, string name)
    {
        var signIn = await client.SignInAsync(email);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var session = await AuthClient.ReadSessionAsync(signIn);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return new Account(client, session, id, name);
    }

    private static async Task ChangeRoleAsAdminAsync(ApiFactory host, string id, string role)
    {
        var admin = host.CreateClient();
        var token = (await AuthClient.ReadSessionAsync(await admin.SignInAsync(ApiFactory.AdminEmail))).AccessToken;
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var changed = await admin.PutAsJsonAsync($"/api/admin/accounts/{id}/role", new { role });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
    }
}
