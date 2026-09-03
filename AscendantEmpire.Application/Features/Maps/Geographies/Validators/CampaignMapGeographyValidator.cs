using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Geographies.AddCampaignMapGeography;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Geographies.Validators;

internal class CampaignMapGeographyValidator : Validator<AddOrUpdateCampaignMapGeographyDto>
{
    public CampaignMapGeographyValidator()
    {
        RuleFor(g => g.CampaignMapGeographyType)
            .Must(value => Enum.IsDefined(typeof(CampaignMapGeographyType), value))
            .WithMessage(g => $"{nameof(g.CampaignMapGeographyType)} must be a valid Campaign Map Geography Type value.");

        RuleFor(g => g.RelativeElevation).GreaterThanOrEqualTo(-2).WithMessage("Relative Elevation must be 0 or greater.");

        RuleFor(g => g.Name).NotEmpty().WithMessage("Name is required.");

        RuleFor(g => g.MovementCost).GreaterThan(0).WithMessage("Movement Cost must be greater than 0.");
    }

    public static ServiceResultBase ValidateGeographies(AscendantEmpireContext context, IReadOnlyCollection<AddOrUpdateCampaignMapGeographyDto> geographies)
    {
        var validator = new CampaignMapGeographyValidator();

        // Validate individual geographies
        (string GeographyName, ValidationResult ValidationResults)[] validationResults = [.. geographies.Select(g => (g.Name, validator.Validate(g)))];

        // Check for any duplicate CampaignMapGeographyTypeIds in the request
        string[] duplicateGeographiesInRequest = [.. geographies
            .GroupBy(b => b.CampaignMapGeographyType)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} is specified multiple times in the request.")];

        string[] groupedErrors = [.. validationResults
            .Where(result => result.ValidationResults.Errors.Any())
            .SelectMany(result =>
                result.ValidationResults.Errors.Select(error =>
                    $"Geography Name '{result.GeographyName}': {error.ErrorMessage}"))];

        if (groupedErrors.Length != 0 || duplicateGeographiesInRequest.Length != 0)
        {
            return ServiceResultBase.Fail(groupedErrors.Concat(duplicateGeographiesInRequest));
        }

        return ServiceResultBase.Succeed();
    }
}