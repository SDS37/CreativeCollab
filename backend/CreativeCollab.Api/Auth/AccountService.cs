using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CreativeCollab.Api.Auth;

public abstract record RegisterResult
{
    private RegisterResult() { }

    public sealed record Registered(AppUser Account) : RegisterResult;
    /// <summary>Request fields mapped to what is wrong with them.</summary>
    public sealed record Invalid(Dictionary<string, string[]> Problems) : RegisterResult;
}

public abstract record SignInResult
{
    private SignInResult() { }

    public sealed record SignedIn(AppUser Account) : SignInResult;
    public sealed record LockedOut : SignInResult;
    /// <summary>Unknown email and wrong password are one outcome, so the caller cannot tell them apart.</summary>
    public sealed record InvalidCredentials : SignInResult;
}

/// <summary>Registering and signing in an Account: the lockout rule, the default role and the mapping of Identity errors.</summary>
public sealed class AccountService(UserManager<AppUser> users, IPasswordHasher<AppUser> hasher)
{
    public const string GenericRegistrationMessage = "Registration could not be completed with these details. Check the details and try again, or sign in.";

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request)
    {
        // Display names are public, so saying one is taken is fine (unlike an email).
        var normalized = DisplayNames.Normalize(request.DisplayName!);
        if (await users.Users.AnyAsync(u => u.NormalizedDisplayName == normalized))
            return new RegisterResult.Invalid(DisplayNameTaken());

        var account = new AppUser { UserName = request.Email, Email = request.Email, DisplayName = request.DisplayName! };
        IdentityResult created;
        try
        {
            created = await users.CreateAsync(account, request.Password!);
        }
        catch (DbUpdateException)
        {
            // Lost a race for the same name (the unique index caught it); anything else is a real failure.
            if (!await users.Users.AnyAsync(u => u.NormalizedDisplayName == normalized)) throw;
            return new RegisterResult.Invalid(DisplayNameTaken());
        }
        if (!created.Succeeded)
            return new RegisterResult.Invalid(ToProblems(created));

        await users.AddToRoleAsync(account, AccountRoles.Default);
        return new RegisterResult.Registered(account);
    }

    public async Task<SignInResult> SignInAsync(SignInRequest request)
    {
        var account = await users.FindByEmailAsync(request.Email ?? "");
        if (account is not null && await users.IsLockedOutAsync(account))
            return new SignInResult.LockedOut();

        // An unknown email still pays for one password hash check, so response time does not reveal which emails have accounts.
        if (account is null)
            hasher.VerifyHashedPassword(new AppUser(), DummyHash(hasher), request.Password ?? "");

        if (account is null || !await users.CheckPasswordAsync(account, request.Password ?? ""))
        {
            if (account is not null) await users.AccessFailedAsync(account);
            return new SignInResult.InvalidCredentials();
        }

        await users.ResetAccessFailedCountAsync(account);
        return new SignInResult.SignedIn(account);
    }

    private static Dictionary<string, string[]> DisplayNameTaken() => new()
    {
        ["displayName"] = ["That name is taken. Please choose another."],
    };

    private static string? _dummyHash;

    private static string DummyHash(IPasswordHasher<AppUser> hasher) =>
        _dummyHash ??= hasher.HashPassword(new AppUser(), Guid.NewGuid().ToString("N"));

    /// <summary>
    /// A duplicate email or user name gets one generic message, so registering cannot be used to learn which emails have accounts.
    /// (A successful registration still differs from a failed one: without email verification that cannot be closed. ADR 0003.)
    /// </summary>
    private static Dictionary<string, string[]> ToProblems(IdentityResult result)
    {
        var problems = new Dictionary<string, List<string>>();
        foreach (var error in result.Errors)
        {
            var (field, message) = error.Code switch
            {
                nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName) => ("email", GenericRegistrationMessage),
                var code when code.Contains("Password", StringComparison.Ordinal) => ("password", error.Description),
                _ => ("email", error.Description),
            };
            if (!problems.TryGetValue(field, out var list)) problems[field] = list = [];
            if (!list.Contains(message)) list.Add(message);
        }
        return problems.ToDictionary(p => p.Key, p => p.Value.ToArray());
    }
}
