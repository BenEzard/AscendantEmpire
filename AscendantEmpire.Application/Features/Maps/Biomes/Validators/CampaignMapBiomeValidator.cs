using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Biomes;

using FluentValidation;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Validators;

internal class CampaignMapBiomeValidator : AbstractValidator<IAddOrUpdateCampaignMapBiomeDto>
{
    /// <summary>
    /// Validate each Campaign Map Biome
    /// </summary>
    /// <remarks>Checks the following:
    /// <list type="bullet">
    /// <item>BiomeType is a valid BiomeType enum value</item>
    /// <item>Name is not empty</item>
    /// <item>MovementCost is between 0.01 and 99.99</item>
    /// <item>IsWater Biome cannot have DoesBlockSight set to true</item>
    /// <item>RegenerationRate must be greater than 1, if specified</item>
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

        RuleFor(b => b.RegenerationRate)
            .Must(rr => rr == null || rr > 1)
            .WithMessage(b => $"{nameof(b.RegenerationRate)} must be greater than 1.");

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
}
