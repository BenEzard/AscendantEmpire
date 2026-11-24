using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Validation;
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

    /// <summary>
    /// Validates a set of Geographies against the rules above, reporting every broken rule and any Geography Type named more than once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared by every writer of the Geography catalogue — the add and upsert handlers, and the campaign map import — so a Geography cannot reach the table
    /// through one path carrying something another path would refuse.
    /// </para>
    /// <para>
    /// Whether a Geography Type is already stored is deliberately not checked here, because an upsert names existing Geographies on purpose. That rule
    /// belongs to adding, and <see cref="Handlers.AddCampaignMapGeographyHandler"/> carries it.
    /// </para>
    /// </remarks>
    public static ServiceResultBase ValidateGeographies(IReadOnlyCollection<AddOrUpdateCampaignMapGeographyDto> geographies)
    {
        var validator = new CampaignMapGeographyValidator();

        string[] ruleErrors = [.. geographies
            .Select(geography => (geography.Name, Result: validator.Validate(geography)))
            .Where(validated => validated.Result.Errors.Count != 0)
            .SelectMany(validated => validated.Result.Errors.Select(error => $"Geography '{validated.Name}': {error.ErrorMessage}"))];

        // Named rather than counted, because the Geography Type is the key: a second row for one would be a silent overwrite at best.
        string[] duplicateErrors = [.. geographies
            .GroupBy(geography => geography.CampaignMapGeographyType)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} is listed more than once.")];

        if (ruleErrors.Length != 0 || duplicateErrors.Length != 0)
        {
            return ServiceResultBase.Fail([.. ruleErrors, .. duplicateErrors]);
        }

        return ServiceResultBase.Succeed();
    }
}