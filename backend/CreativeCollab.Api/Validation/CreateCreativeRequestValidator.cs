using CreativeCollab.Api.Models;
using FluentValidation;

namespace CreativeCollab.Api.Validation;

public sealed class CreateCreativeRequestValidator : AbstractValidator<CreateCreativeRequest>
{
    public const int NameMaxLength = 100;
    public const int HeadlineMaxLength = 40;
    public const int BodyMaxLength = 200;
    public const int CtaMaxLength = 20;
    public const int ImageUrlMaxLength = 2048;

    public CreateCreativeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(NameMaxLength);
        RuleFor(x => x.Headline).NotEmpty().MaximumLength(HeadlineMaxLength);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(BodyMaxLength);
        RuleFor(x => x.Cta).NotEmpty().MaximumLength(CtaMaxLength);
        RuleFor(x => x.DisplayName).NotEmpty();
        RuleFor(x => x.ImageUrl)
            .MaximumLength(ImageUrlMaxLength)
            .Must(BeAbsoluteHttpUrl).WithMessage("'{PropertyName}' must be an absolute http or https URL.")
            .When(x => !string.IsNullOrEmpty(x.ImageUrl));
    }

    private static bool BeAbsoluteHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
