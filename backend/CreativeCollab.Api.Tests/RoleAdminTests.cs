using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CreativeCollab.Api.Tests;

public sealed class RoleAdminTests : IDisposable
{
    private readonly FailingInsertInterceptor _failure = new();
    private readonly ApiFactory _host;

    public RoleAdminTests() => _host = new ApiFactory(null, null, _failure);

    public void Dispose() => _host.Dispose();

    /// <summary>Stands in for a database failure part-way through a role replacement.</summary>
    private sealed class FailingInsertInterceptor : DbCommandInterceptor
    {
        public volatile bool Armed;

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) =>
            Check(command, result);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Check(command, result));

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) =>
            Check(command, result);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Check(command, result));

        private InterceptionResult<T> Check<T>(DbCommand command, InterceptionResult<T> result) =>
            Armed && command.CommandText.Contains("INSERT INTO \"AspNetUserRoles\"", StringComparison.Ordinal)
                ? throw new InvalidOperationException("simulated database failure")
                : result;
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private static Task<HttpResponseMessage> ChangeRoleAsync(HttpClient client, string? userId, string role, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/users/{userId}/role") { Content = JsonContent.Create(new { role }) };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var registered = await client.RegisterAsync(email);
        return (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task<string> AdminTokenAsync(HttpClient client) =>
        (await AuthClient.ReadSessionAsync(await client.LoginAsync(ApiFactory.AdminEmail))).AccessToken;

    private static async Task<string?> RoleOfAsync(HttpClient client, string token) =>
        (await (await client.MeAsync(token)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString();

    [Fact]
    public async Task Changing_a_role_without_signing_in_is_401()
    {
        var client = _host.CreateClient();
        var id = await RegisterAsync(client, NewEmail());

        Assert.Equal(HttpStatusCode.Unauthorized, (await ChangeRoleAsync(client, id, "Viewer", null)).StatusCode);
    }

    [Fact]
    public async Task Changing_a_role_as_an_editor_is_403()
    {
        var client = _host.CreateClient();
        var editor = await client.SignInAsync(NewEmail());
        var id = await RegisterAsync(client, NewEmail());

        Assert.Equal(HttpStatusCode.Forbidden, (await ChangeRoleAsync(client, id, "Viewer", editor.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task An_admin_changes_a_role_and_it_applies_after_the_next_token_refresh()
    {
        var client = _host.CreateClient();
        var email = NewEmail();
        var id = await RegisterAsync(client, email);
        var session = await AuthClient.ReadSessionAsync(await client.LoginAsync(email));

        var changed = await ChangeRoleAsync(client, id, "Viewer", await AdminTokenAsync(client));

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal("Editor", await RoleOfAsync(client, session.AccessToken));
        var renewed = await AuthClient.ReadSessionAsync(await client.RefreshAsync(session.RefreshCookie));
        Assert.Equal("Viewer", await RoleOfAsync(client, renewed.AccessToken));
    }

    [Fact]
    public async Task An_unknown_role_is_400_and_an_unknown_account_is_404()
    {
        var client = _host.CreateClient();
        var id = await RegisterAsync(client, NewEmail());
        var admin = await AdminTokenAsync(client);

        Assert.Equal(HttpStatusCode.BadRequest, (await ChangeRoleAsync(client, id, "Overlord", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ChangeRoleAsync(client, Guid.NewGuid().ToString(), "Viewer", admin)).StatusCode);
    }

    private static async Task<string> IdOfAsync(HttpClient client, string token) =>
        (await (await client.MeAsync(token)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sub").GetString()!;

    [Fact]
    public async Task The_only_admin_cannot_demote_themselves_and_keeps_the_role()
    {
        var client = _host.CreateClient();
        var token = await AdminTokenAsync(client);

        var response = await ChangeRoleAsync(client, await IdOfAsync(client, token), "Editor", token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("last Admin", await response.Content.ReadAsStringAsync());
        var renewed = await AuthClient.ReadSessionAsync(await client.LoginAsync(ApiFactory.AdminEmail));
        Assert.Equal("Admin", await RoleOfAsync(client, renewed.AccessToken));
    }

    [Fact]
    public async Task With_two_admins_one_can_be_demoted_and_then_the_other_is_the_last()
    {
        var client = _host.CreateClient();
        var second = await _host.NewAccountAsync("Admin");
        var rootToken = await AdminTokenAsync(client);
        var rootId = await IdOfAsync(client, rootToken);

        var demoted = await ChangeRoleAsync(client, rootId, "Viewer", second.AccessToken);
        var lastOne = await ChangeRoleAsync(client, second.Id, "Viewer", second.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, demoted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, lastOne.StatusCode);
    }

    [Fact]
    public async Task Keeping_the_admin_role_on_the_last_admin_is_allowed()
    {
        var client = _host.CreateClient();
        var token = await AdminTokenAsync(client);

        var response = await ChangeRoleAsync(client, await IdOfAsync(client, token), "Admin", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task A_failure_while_replacing_a_role_leaves_the_account_with_its_old_role()
    {
        var client = _host.CreateClient();
        var email = NewEmail();
        var id = await RegisterAsync(client, email);
        var admin = await AdminTokenAsync(client);
        _failure.Armed = true;

        var response = await ChangeRoleAsync(client, id, "Viewer", admin);
        _failure.Armed = false;

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var session = await AuthClient.ReadSessionAsync(await client.LoginAsync(email));
        Assert.Equal("Editor", await RoleOfAsync(client, session.AccessToken));
    }
}
