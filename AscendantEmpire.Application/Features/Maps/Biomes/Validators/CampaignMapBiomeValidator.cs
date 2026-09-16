using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Biomes;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Validators;

internal class CampaignMapBiomeValidator : Validator<IAddOrUpdateCampaignMapBiomeDto>
{
    /// <summary>
    /// The lowest Regeneration Rate a Biome with sustenance values may have.
    /// </summary>
    private const decimal _MINIMUM_REGENERATION_RATE = 1.05m;

    /// <summary>
    /// Validate each Campaign Map Biome
    /// </summary>
    /// <remarks>Checks the following:
    /// <list type="bullet">
    /// <item>BiomeType is a valid BiomeType enum value</item>
    /// <item>Name is not empty</item>
    /// <item>MovementCost is between 0.01 and 99.99</item>
    /// <item>IsWater Biome cannot have DoesBlockSight set to true</item>
    /// <item>RegenerationRate must be specified, and at least 1.05, when the Biome has any sustenance value (BaseForageValue, BaseHuntValue, BaseFishValue or BaseFodderValue)</item>
    /// <item>RegenerationRate must be null when the Biome has no sustenance value, since there is nothing to regrow</item>
    /// <item>BaseForageValue, BaseHuntValue, BaseFishValue, BaseFodderValue, BaseBoneValue, BaseWoodValue, BaseStoneValue, and BaseSpecialValue must be greater than or equal to 0, if specified</item>
    /// <item>Water Biomes cannot have Hunt, Fodder, Wood, or Stone values</item>
    /// <item>Water Biomes cannot be elevated</item>
    /// </list> 
    /// </remarks>
    public CampaignMapBiomeValidator()
    {
        RuleFor(b => b.BiomeType)
            .Must(value => Enum.IsDefined(typeof(BiomeType), value))
            .WithMessage(b => $"{nameof(b.BiomeType)} must be a valid Biome Type value.");

        RuleFor(b => b.Name)
            .NotEmpty().WithMessage(b => $"{nameof(b.Name)} is required.");

        RuleFor(b => b.MovementCost)
            .InclusiveBetween(0.01m, 99.99m)
            .WithMessage(b => $"{nameof(b.MovementCost)} must be between 0 and 100 (exclusive).");

        RuleFor(b => b)
            .Must(b => (b.IsWater && b.DoesBlockSight) == false)
            .WithMessage(b => $"An {nameof(b.IsWater)} Biome cannot be Does Block Sight = true");

        // The rate only drives sustenance regrowth at the end of a Round, so it is required exactly when the Biome has something to regrow. A base of 0 still counts as having
        // the resource: only null means the Biome does not have it.
        RuleFor(b => b)
            .Must(b => HasSustenanceValue(b) == false || b.RegenerationRate >= _MINIMUM_REGENERATION_RATE)
            .WithMessage(b => $"{b.BiomeType}: {nameof(b.RegenerationRate)} must be at least {_MINIMUM_REGENERATION_RATE} when the Biome has Forage, Hunt, Fish or Fodder values.");

        RuleFor(b => b)
            .Must(b => HasSustenanceValue(b) || b.RegenerationRate == null)
            .WithMessage(b => $"{b.BiomeType}: {nameof(b.RegenerationRate)} must be empty when the Biome has no Forage, Hunt, Fish or Fodder values.");

        RuleFor(b => b.BaseForageValue)
            .Must(v => v == null || v == 0 || v >= 9)
            .WithMessage(b => $"{nameof(b.BaseForageValue)} must be 0 or greater than 9, if specified.");

        RuleFor(b => b.BaseHuntValue)
            .Must(v => v == null || v == 0 || v >= 9)
            .WithMessage(b => $"{nameof(b.BaseHuntValue)} must be 0 or greater than 9, if specified.");

        RuleFor(b => b.BaseFishValue)
            .Must(v => v == null || v == 0 || v >= 9)
            .WithMessage(b => $"{nameof(b.BaseFishValue)} must be 0 or greater than 9, if specified.");

        RuleFor(b => b.BaseFodderValue)
            .Must(v => v == null || v == 0 || v >= 9)
            .WithMessage(b => $"{nameof(b.BaseFodderValue)} must be 0 or greater than 9, if specified.");

        RuleFor(b => b.BaseBoneValue)
            .Must(v => v == null || v == 0 || v >= 9)
            .WithMessage(b => $"{nameof(b.BaseBoneValue)} must be 0 or greater than 9, if specified.");

        RuleFor(b => b.BaseWoodValue)
            .Must(v => v == null || v == 0 || v >= 9)
            .WithMessage(b => $"{nameof(b.BaseWoodValue)} must be 0 or greater than 9, if specified.");

        RuleFor(b => b.BaseStoneValue)
            .Must(v => v == null || v == 0 || v >= 9)
            .WithMessage(b => $"{nameof(b.BaseStoneValue)} must be 0 or greater than 9, if specified.");

        RuleFor(b => b.BaseSpecialValue)
            .Must(v => v == null || v == 0 || v >= 9)
            .WithMessage(b => $"{nameof(b.BaseSpecialValue)} must be 0 or greater than 9, if specified.");

        RuleFor(b => b)
            .Must(b =>
                b.IsWater == false || 
                    b.BaseHuntValue == null &&
                    b.BaseFodderValue == null &&
                    b.BaseWoodValue == null &&
                    b.BaseStoneValue == null
                )
            .WithMessage(b => $"{b.BiomeType}: Water biomes cannot have Hunt, Fodder, Wood, or Stone values.");

        RuleFor(b => b)
            .Must(b => b.IsWater == false || b.CanBeElevated == false)
            .WithMessage(b => $"{b.BiomeType}: Water biomes cannot be elevated.");

        RuleFor(b => b)
            .Must(b => b.IsWater == false || b.DoesBlockSight == false)
            .WithMessage(b => $"{b.BiomeType}: Water biomes cannot Block Sight.");


        RuleFor(b => b.MapEditorHtmlColor)
            .Matches(@"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")
            .WithMessage(b => $"{nameof(b.MapEditorHtmlColor)}: must be a valid hex color (e.g., #RGB or #RRGGBB).");

        RuleFor(b => b).Custom((biome, context) =>
        {
            if (biome.IsPaintableOnCampaignMap == false)
            {
                if (biome.RegenerationRate != null) context.AddFailure($"{nameof(biome.RegenerationRate)} must be null if Biome is not paintable on Campaign Map.");
                if (biome.BaseForageValue != null) context.AddFailure($"{nameof(biome.BaseForageValue)} must be null if Biome is not paintable on Campaign Map.");
                if (biome.BaseHuntValue != null) context.AddFailure($"{nameof(biome.BaseHuntValue)} must be null if Biome is not paintable on Campaign Map.");
                if (biome.BaseFishValue != null) context.AddFailure($"{nameof(biome.BaseFishValue)} must be null if Biome is not paintable on Campaign Map.");
                if (biome.BaseFodderValue != null) context.AddFailure($"{nameof(biome.BaseFodderValue)} must be null if Biome is not paintable on Campaign Map.");
                if (biome.BaseBoneValue != null) context.AddFailure($"{nameof(biome.BaseBoneValue)} must be null if Biome is not paintable on Campaign Map.");
                if (biome.BaseWoodValue != null) context.AddFailure($"{nameof(biome.BaseWoodValue)} must be null if Biome is not paintable on Campaign Map.");
                if (biome.BaseStoneValue != null) context.AddFailure($"{nameof(biome.BaseStoneValue)} must be null if Biome is not paintable on Campaign Map.");
                if (biome.BaseSpecialValue != null) context.AddFailure($"{nameof(biome.BaseSpecialValue)} must be null if Biome is not paintable on Campaign Map.");
            }
        });

    }

    /// <summary>
    /// Whether the Biome has any sustenance resource - Forage, Hunt, Fish or Fodder - that is, a non-null base value for one of them.
    /// </summary>
    private static bool HasSustenanceValue(IAddOrUpdateCampaignMapBiomeDto biome)
    {
        return biome.BaseForageValue != null || biome.BaseHuntValue != null || biome.BaseFishValue != null || biome.BaseFodderValue != null;
    }
}
