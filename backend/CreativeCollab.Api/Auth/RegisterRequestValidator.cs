using FluentValidation;

namespace CreativeCollab.Api.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().Must(DisplayNames.IsValid).WithMessage(DisplayNames.Rule);
    }
}
