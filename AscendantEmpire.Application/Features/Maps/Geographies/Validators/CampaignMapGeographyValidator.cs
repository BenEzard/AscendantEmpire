using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Geographies.AddCampaignMapGeography;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Geographies.Validators;

internal class CampaignMapGeographyValidator : Validator<AddOrUpdateCampaignMapGeographyDto>
{
    public CampaignMapGeographyValidator()
    {
        RuleFor(g => g.CampaignMapGeographyType)
            .Must(value => Enum.IsDefined(typeof(CampaignMapGeographyType), value))
            .WithMessage(g => $"{nameof(g.CampaignMapGeographyType)} must be a valid Campaign Map Geography Type value.");

        RuleFor(g => g.RelativeElevation).GreaterThanOrEqualTo(-2).WithMessage("Relative Elevation must be -2 or greater.");

        RuleFor(g => g.Name).NotEmpty().WithMessage("Name is required.");

        RuleFor(g => g.MovementCost).GreaterThan(0).WithMessage("Movement Cost must be greater than 0.");

        // The same pairing tile painting refuses, enforced here so the catalogue cannot hold a combination the map editor would reject.
        RuleFor(g => g)
            .Must(g => g.IsWater == false || g.RelativeElevation <= 0)
            .WithMessage(g => $"A water Geography cannot be elevated ({nameof(g.RelativeElevation)} must be 0 or less when {nameof(g.IsWater)} is set).");

        // One-directional on purpose: the impassable cost must mean untraversable, but an untraversable Geography need not carry it. Unknown is
        // untraversable because it stands for "no data", not because the ground is expensive - the same shape as the Void biome, which is untraversable
        // at an ordinary cost.
        RuleFor(g => g)
            .Must(g => g.MovementCost < CampaignGeography.IMPASSABLE_MOVEMENT_COST || g.IsTraversable == false)
            .WithMessage(g => $"A Geography whose {nameof(g.MovementCost)} is {CampaignGeography.IMPASSABLE_MOVEMENT_COST} or more cannot be {nameof(g.IsTraversable)}.");
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