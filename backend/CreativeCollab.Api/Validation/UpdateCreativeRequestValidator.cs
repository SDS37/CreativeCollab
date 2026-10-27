using CreativeCollab.Api.Models;
using FluentValidation;

namespace CreativeCollab.Api.Validation;

public sealed class UpdateCreativeRequestValidator : CreativeFieldsValidator<UpdateCreativeRequest>
{
    public UpdateCreativeRequestValidator()
    {
        RuleFor(x => x.Version)
            .NotNull().WithMessage("'Version' is required.")
            .GreaterThanOrEqualTo(1);
    }
}
