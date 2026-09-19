using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.FrontendContracts.MapResources.PutMapResourceCatalogue;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Validators;

/// <summary>
/// Validates one MapResourceCategory as the MapResource Type Manager adds or updates it.
/// </summary>
internal class MapResourceCategoryValidator : Validator<IAddOrUpdateMapResourceCategoryDto>
{
    public MapResourceCategoryValidator()
    {
        RuleFor(c => c.Code)
            .NotEmpty().WithMessage(c => $"Category {nameof(c.Code)} is required.");

        RuleFor(c => c.Code)
            .MaximumLength(MapResourceCategory.CODE_MAX_LENGTH)
            .WithMessage(c => $"Category {nameof(c.Code)} must be at most {MapResourceCategory.CODE_MAX_LENGTH} characters.");

        RuleFor(c => c.DisplayName)
            .NotEmpty().WithMessage(c => $"Category {c.Code}: {nameof(c.DisplayName)} is required.");

        RuleFor(c => c.DisplayName)
            .MaximumLength(MapResourceCategory.DISPLAY_NAME_MAX_LENGTH)
            .WithMessage(c => $"Category {c.Code}: {nameof(c.DisplayName)} must be at most {MapResourceCategory.DISPLAY_NAME_MAX_LENGTH} characters.");
    }
}
