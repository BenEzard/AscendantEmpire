using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Validators;

/// <summary>
/// Validates one MapResourceType as the map editor adds or updates it, including everything it owns.
/// </summary>
/// <remarks>
/// <para>
/// Most of the rules follow from the type's kind (see <c>docs/design/campaign-map/map-resources.md</c>). A value or child that does not apply to the kind must be left
/// empty rather than ignored, so the catalogue never holds a value that looks meaningful but is not:
/// <list type="table">
///   <listheader><term>Kind</term><description>Needs</description></listheader>
///   <item><term>Neither renewable nor stratified</term><description>Yields; habitats with an initial quantity range.</description></item>
///   <item><term>Renewable</term><description>Yields; SurvivalRounds and ReseedLikelihood; habitats with an initial quantity range, a MaximumQuantity and a
///   GrowthPercentage, and optionally spread values.</description></item>
///   <item><term>Distribution (renewable)</term><description>Scenarios; SurvivalRounds and ReseedLikelihood; habitats with a MaximumQuantity and a GrowthPercentage,
///   and never spread values.</description></item>
///   <item><term>Stratified</term><description>Layers numbered 1, 2, 3...; habitats with no values; no fuzzy factor range of its own.</description></item>
/// </list>
/// </para>
/// <para>
/// Whether the Assets and category named exist is the handler's check, since it needs the database. The database itself enforces the two flag rules as well
/// (<c>CK_MapResourceType_RenewableOrStratified</c> and <c>CK_MapResourceType_DistributionIsRenewable</c>).
/// </para>
/// </remarks>
internal class MapResourceTypeValidator : Validator<IAddOrUpdateMapResourceTypeDto>
{
    /// <summary>
    /// The highest a likelihood, percentage or fuzzy factor can be: 1, a certainty (or a whole quantity).
    /// </summary>
    private const decimal _MAXIMUM_FRACTION = 1m;

    public MapResourceTypeValidator()
    {
        #region General Properties
        RuleFor(t => t.Code)
            .NotEmpty().WithMessage(t => $"{nameof(t.Code)} is required.");

        RuleFor(t => t.Code)
            .MaximumLength(MapResourceType.CODE_MAX_LENGTH)
            .WithMessage(t => $"{nameof(t.Code)} must be at most {MapResourceType.CODE_MAX_LENGTH} characters.");

        RuleFor(t => t.DisplayName)
            .NotEmpty().WithMessage(t => $"{t.Code}: {nameof(t.DisplayName)} is required.");

        RuleFor(t => t.DisplayName)
            .MaximumLength(MapResourceType.DISPLAY_NAME_MAX_LENGTH)
            .WithMessage(t => $"{t.Code}: {nameof(t.DisplayName)} must be at most {MapResourceType.DISPLAY_NAME_MAX_LENGTH} characters.");

        // Whether the category exists is the handler's check: it may be created in the same save.
        RuleFor(t => t.MapResourceCategoryCode)
            .NotEmpty().WithMessage(t => $"{t.Code}: {nameof(t.MapResourceCategoryCode)} is required.");

        RuleFor(t => t.IconPath)
            .MaximumLength(MapResourceType.ICON_PATH_MAX_LENGTH)
            .WithMessage(t => $"{t.Code}: {nameof(t.IconPath)} must be at most {MapResourceType.ICON_PATH_MAX_LENGTH} characters.");
        #endregion

        #region Kind
        // The spec's two flag rules. Everything below follows from the kind they set.
        RuleFor(t => t)
            .Must(t => (t.IsRenewable && t.IsStratified) == false)
            .WithMessage(t => $"{t.Code}: a MapResourceType cannot be both {nameof(t.IsRenewable)} and {nameof(t.IsStratified)}.");

        RuleFor(t => t)
            .Must(t => t.IsDistribution == false || t.IsRenewable)
            .WithMessage(t => $"{t.Code}: a {nameof(t.IsDistribution)} MapResourceType must also be {nameof(t.IsRenewable)}.");
        #endregion

        #region Extraction
        RuleFor(t => t.ExtractionMethod)
            .IsInEnum()
            .WithMessage(t => $"{t.Code}: {nameof(t.ExtractionMethod)} must be a valid Extraction Method.");

        // Only a Capture can fail, so only a Capture has a chance of succeeding.
        RuleFor(t => t)
            .Must(t => (t.ExtractionMethod == ExtractionMethod.Capture) == (t.CaptureLikelihood != null))
            .WithMessage(t => $"{t.Code}: {nameof(t.CaptureLikelihood)} must be set when, and only when, the {nameof(t.ExtractionMethod)} is {ExtractionMethod.Capture}.");

        RuleFor(t => t.CaptureLikelihood)
            .Must(IsFractionOrEmpty)
            .WithMessage(t => $"{t.Code}: {nameof(t.CaptureLikelihood)} must be between 0 and {_MAXIMUM_FRACTION}.");
        #endregion

        #region Discovery
        RuleFor(t => t.DiscoveryLikelihood)
            .Must(IsFraction)
            .WithMessage(t => $"{t.Code}: {nameof(t.DiscoveryLikelihood)} must be between 0 and {_MAXIMUM_FRACTION}.");

        // A stratified type judges each layer separately, so the range lives on its layers; every other kind needs one of its own.
        RuleFor(t => t).Custom((t, context) =>
        {
            if (t.IsStratified)
            {
                if (t.EstimationFuzzyFactorMin != null || t.EstimationFuzzyFactorMax != null)
                {
                    context.AddFailure($"{t.Code}: a stratified MapResourceType sets its fuzzy factor range on each layer, so {nameof(t.EstimationFuzzyFactorMin)} and {nameof(t.EstimationFuzzyFactorMax)} must be empty.");
                }

                return;
            }

            if (IsValidRange(t.EstimationFuzzyFactorMin, t.EstimationFuzzyFactorMax, upperBound: _MAXIMUM_FRACTION) == false)
            {
                context.AddFailure($"{t.Code}: {nameof(t.EstimationFuzzyFactorMin)} and {nameof(t.EstimationFuzzyFactorMax)} are required, between 0 and {_MAXIMUM_FRACTION}, with the minimum no more than the maximum.");
            }
        });
        #endregion

        #region Exhaustion
        // Only a renewable instance goes dormant when exhausted, so only a renewable type says how long it survives and how likely it is to come back.
        RuleFor(t => t)
            .Must(t => t.IsRenewable == (t.SurvivalRounds != null && t.ReseedLikelihood != null))
            .WithMessage(t => $"{t.Code}: {nameof(t.SurvivalRounds)} and {nameof(t.ReseedLikelihood)} must both be set for a renewable MapResourceType, and both be empty otherwise.");

        RuleFor(t => t.SurvivalRounds)
            .Must(rounds => rounds == null || rounds >= 1)
            .WithMessage(t => $"{t.Code}: {nameof(t.SurvivalRounds)} must be at least 1.");

        RuleFor(t => t.ReseedLikelihood)
            .Must(IsFractionOrEmpty)
            .WithMessage(t => $"{t.Code}: {nameof(t.ReseedLikelihood)} must be between 0 and {_MAXIMUM_FRACTION}.");
        #endregion

        #region Children by kind
        // Each kind holds its Assets in exactly one way: yields, scenarios or layers. The other two must be empty.
        RuleFor(t => t).Custom((t, context) =>
        {
            if (t.IsDistribution)
            {
                RequireOnly(t, context, "a Distribution", hasYields: false, hasScenarios: true, hasLayers: false);
            }
            else if (t.IsStratified)
            {
                RequireOnly(t, context, "a stratified MapResourceType", hasYields: false, hasScenarios: false, hasLayers: true);
            }
            else
            {
                RequireOnly(t, context, "a MapResourceType that is neither a Distribution nor stratified", hasYields: true, hasScenarios: false, hasLayers: false);
            }
        });

        // Without a habitat the type could never exist anywhere.
        RuleFor(t => t.Habitats)
            .Must(habitats => habitats.Count > 0)
            .WithMessage(t => $"{t.Code}: at least one habitat is required.");
        #endregion

        RuleFor(t => t).Custom(ValidateYields);
        RuleFor(t => t).Custom(ValidateHabitats);
        RuleFor(t => t).Custom(ValidateScenarios);
        RuleFor(t => t).Custom(ValidateLayers);
    }

    /// <summary>
    /// Adds a failure for each of yields, scenarios and layers that the kind needs but lacks, or has but must not.
    /// </summary>
    private static void RequireOnly(IAddOrUpdateMapResourceTypeDto type, ValidationContext<IAddOrUpdateMapResourceTypeDto> context, string kind, bool hasYields, bool hasScenarios, bool hasLayers)
    {
        RequireChildren(context, type.Code, kind, "yield", type.Yields.Count, hasYields);
        RequireChildren(context, type.Code, kind, "Distribution scenario", type.DistributionScenarios.Count, hasScenarios);
        RequireChildren(context, type.Code, kind, "layer", type.Layers.Count, hasLayers);
    }

    private static void RequireChildren(ValidationContext<IAddOrUpdateMapResourceTypeDto> context, string code, string kind, string child, int count, bool required)
    {
        if (required && count == 0)
        {
            context.AddFailure($"{code}: {kind} needs at least one {child}.");
        }
        else if (required == false && count > 0)
        {
            context.AddFailure($"{code}: {kind} cannot have any {child}s.");
        }
    }

    private static void ValidateYields(IAddOrUpdateMapResourceTypeDto type, ValidationContext<IAddOrUpdateMapResourceTypeDto> context)
    {
        foreach (int assetId in Duplicates(type.Yields.Select(y => y.AssetId)))
        {
            context.AddFailure($"{type.Code}: Asset {assetId} appears more than once in the yields.");
        }

        foreach (IMapResourceTypeYieldDto yield in type.Yields)
        {
            if (yield.AmountPerUnit <= 0)
            {
                context.AddFailure($"{type.Code}: the yield of Asset {yield.AssetId} must give more than 0 per unit extracted.");
            }
        }
    }

    private static void ValidateHabitats(IAddOrUpdateMapResourceTypeDto type, ValidationContext<IAddOrUpdateMapResourceTypeDto> context)
    {
        foreach ((BiomeType biome, CampaignMapGeographyType? geography) in Duplicates(type.Habitats.Select(h => (h.BiomeType, h.GeographyType))))
        {
            context.AddFailure($"{type.Code}: there is more than one habitat for {Describe(biome, geography)}.");
        }

        // What each kind needs from a habitat. A single-quantity kind is rolled from an initial range; a renewable kind grows to a ceiling; only a renewable type that is
        // not a Distribution can spread.
        bool needsInitialQuantity = type.IsDistribution == false && type.IsStratified == false;
        bool needsGrowth = type.IsRenewable;
        bool canSpread = type.IsRenewable && type.IsDistribution == false;

        foreach (IMapResourceHabitatDto habitat in type.Habitats)
        {
            string where = $"{type.Code} in {Describe(habitat.BiomeType, habitat.GeographyType)}";

            if (Enum.IsDefined(habitat.BiomeType) == false)
            {
                context.AddFailure($"{where}: {nameof(habitat.BiomeType)} must be a valid Biome Type.");
            }

            if (habitat.GeographyType != null && Enum.IsDefined(habitat.GeographyType.Value) == false)
            {
                context.AddFailure($"{where}: {nameof(habitat.GeographyType)} must be a valid Geography Type.");
            }

            // Initial quantity.
            if (needsInitialQuantity)
            {
                if (IsValidRange(habitat.InitialQuantityMin, habitat.InitialQuantityMax, upperBound: null) == false)
                {
                    context.AddFailure($"{where}: {nameof(habitat.InitialQuantityMin)} and {nameof(habitat.InitialQuantityMax)} are required, at least 0, with the minimum no more than the maximum.");
                }
                else if (needsGrowth && habitat.MaximumQuantity != null && habitat.InitialQuantityMax > habitat.MaximumQuantity)
                {
                    context.AddFailure($"{where}: {nameof(habitat.InitialQuantityMax)} cannot be above {nameof(habitat.MaximumQuantity)}.");
                }
            }
            else if (habitat.InitialQuantityMin != null || habitat.InitialQuantityMax != null)
            {
                context.AddFailure($"{where}: a Distribution or stratified MapResourceType does not start from an initial quantity, so {nameof(habitat.InitialQuantityMin)} and {nameof(habitat.InitialQuantityMax)} must be empty.");
            }

            // Growth.
            if (needsGrowth)
            {
                if (habitat.MaximumQuantity is null or <= 0)
                {
                    context.AddFailure($"{where}: {nameof(habitat.MaximumQuantity)} is required for a renewable MapResourceType, and must be more than 0.");
                }

                // Above 0: a renewable instance that could never grow would not be renewable.
                if (habitat.GrowthPercentage is null or <= 0m or > _MAXIMUM_FRACTION)
                {
                    context.AddFailure($"{where}: {nameof(habitat.GrowthPercentage)} is required for a renewable MapResourceType, more than 0 and at most {_MAXIMUM_FRACTION}.");
                }
            }
            else if (habitat.MaximumQuantity != null || habitat.GrowthPercentage != null)
            {
                context.AddFailure($"{where}: only a renewable MapResourceType grows, so {nameof(habitat.MaximumQuantity)} and {nameof(habitat.GrowthPercentage)} must be empty.");
            }

            // Spread: all three values together, or none. A renewable type may be set never to spread in a habitat by leaving them empty.
            bool hasAnySpread = habitat.SpreadLikelihood != null || habitat.SpreadLimitMin != null || habitat.SpreadLimitMax != null;

            if (hasAnySpread && canSpread == false)
            {
                context.AddFailure($"{where}: only a renewable MapResourceType that is not a Distribution can spread, so {nameof(habitat.SpreadLikelihood)}, {nameof(habitat.SpreadLimitMin)} and {nameof(habitat.SpreadLimitMax)} must be empty.");
            }
            else if (hasAnySpread
                && (habitat.SpreadLikelihood == null
                    || IsFraction(habitat.SpreadLikelihood.Value) == false
                    || IsValidRange(habitat.SpreadLimitMin, habitat.SpreadLimitMax, upperBound: null) == false))
            {
                context.AddFailure($"{where}: {nameof(habitat.SpreadLikelihood)} (between 0 and {_MAXIMUM_FRACTION}) and the {nameof(habitat.SpreadLimitMin)} to {nameof(habitat.SpreadLimitMax)} range (at least 0, minimum no more than maximum) must be set together.");
            }
        }
    }

    private static void ValidateScenarios(IAddOrUpdateMapResourceTypeDto type, ValidationContext<IAddOrUpdateMapResourceTypeDto> context)
    {
        foreach (int number in Duplicates(type.DistributionScenarios.Select(s => s.ScenarioNumber)))
        {
            context.AddFailure($"{type.Code}: Distribution scenario {number} appears more than once.");
        }

        foreach (IMapResourceDistributionScenarioDto scenario in type.DistributionScenarios)
        {
            string which = $"{type.Code} scenario {scenario.ScenarioNumber}";

            if (scenario.ScenarioNumber < 1)
            {
                context.AddFailure($"{which}: {nameof(scenario.ScenarioNumber)} must be at least 1.");
            }

            if (scenario.DisplayName?.Length > MapResourceDistributionScenario.DISPLAY_NAME_MAX_LENGTH)
            {
                context.AddFailure($"{which}: {nameof(scenario.DisplayName)} must be at most {MapResourceDistributionScenario.DISPLAY_NAME_MAX_LENGTH} characters.");
            }

            // A scenario with nothing in it would create an instance with nothing to extract.
            if (scenario.Amounts.Count == 0)
            {
                context.AddFailure($"{which}: at least one Asset amount is required.");
            }

            foreach (int assetId in Duplicates(scenario.Amounts.Select(a => a.AssetId)))
            {
                context.AddFailure($"{which}: Asset {assetId} appears more than once.");
            }

            foreach (IMapResourceDistributionScenarioAmountDto amount in scenario.Amounts.Where(a => a.Amount <= 0))
            {
                context.AddFailure($"{which}: the amount of Asset {amount.AssetId} must be more than 0.");
            }

            foreach (BiomeType biome in Duplicates(scenario.BiomeWeights.Select(w => w.BiomeType)))
            {
                context.AddFailure($"{which}: {biome} has more than one weight.");
            }

            foreach (IMapResourceDistributionScenarioBiomeWeightDto weight in scenario.BiomeWeights)
            {
                if (Enum.IsDefined(weight.BiomeType) == false)
                {
                    context.AddFailure($"{which}: {nameof(weight.BiomeType)} {(int)weight.BiomeType} must be a valid Biome Type.");
                }

                // A Biome where the scenario should never be picked just has no weight.
                if (weight.Weight <= 0)
                {
                    context.AddFailure($"{which}: the weight in {weight.BiomeType} must be more than 0.");
                }
            }
        }
    }

    private static void ValidateLayers(IAddOrUpdateMapResourceTypeDto type, ValidationContext<IAddOrUpdateMapResourceTypeDto> context)
    {
        // Layers are reached in order from the top, so they must be numbered 1, 2, 3... with no gaps or repeats.
        List<int> numbers = type.Layers.Select(l => l.LayerNumber).OrderBy(n => n).ToList();

        if (numbers.SequenceEqual(Enumerable.Range(1, numbers.Count)) == false)
        {
            context.AddFailure($"{type.Code}: the layers must be numbered 1, 2, 3... with no gaps or repeats.");
        }

        foreach (IMapResourceTypeLayerDto layer in type.Layers)
        {
            string which = $"{type.Code} layer {layer.LayerNumber}";

            if (IsFraction(layer.EmptyLikelihood) == false)
            {
                context.AddFailure($"{which}: {nameof(layer.EmptyLikelihood)} must be between 0 and {_MAXIMUM_FRACTION}.");
            }

            if (IsValidRange(layer.QuantityMin, layer.QuantityMax, upperBound: null) == false)
            {
                context.AddFailure($"{which}: {nameof(layer.QuantityMin)} and {nameof(layer.QuantityMax)} must be at least 0, with the minimum no more than the maximum.");
            }

            if (IsValidRange(layer.EstimationFuzzyFactorMin, layer.EstimationFuzzyFactorMax, upperBound: _MAXIMUM_FRACTION) == false)
            {
                context.AddFailure($"{which}: {nameof(layer.EstimationFuzzyFactorMin)} and {nameof(layer.EstimationFuzzyFactorMax)} must be between 0 and {_MAXIMUM_FRACTION}, with the minimum no more than the maximum.");
            }

            // Layer 1 is found and reached through the type itself; every deeper layer has its own gates.
            if (layer.LayerNumber == 1)
            {
                if (layer.DiscoveryLikelihood != null || layer.RequiredMineLevel != null)
                {
                    context.AddFailure($"{which}: the first layer uses the type's {nameof(type.DiscoveryLikelihood)} and needs no mine, so {nameof(layer.DiscoveryLikelihood)} and {nameof(layer.RequiredMineLevel)} must be empty.");
                }
            }
            else
            {
                if (layer.DiscoveryLikelihood == null || IsFraction(layer.DiscoveryLikelihood.Value) == false)
                {
                    context.AddFailure($"{which}: {nameof(layer.DiscoveryLikelihood)} is required below the first layer, between 0 and {_MAXIMUM_FRACTION}.");
                }

                if (layer.RequiredMineLevel is null or < 0)
                {
                    context.AddFailure($"{which}: {nameof(layer.RequiredMineLevel)} is required below the first layer, and must be at least 0.");
                }
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="value"/> is a fraction from 0 to 1 inclusive.
    /// </summary>
    private static bool IsFraction(decimal value)
    {
        return value >= 0m && value <= _MAXIMUM_FRACTION;
    }

    private static bool IsFractionOrEmpty(decimal? value)
    {
        return value == null || IsFraction(value.Value);
    }

    /// <summary>
    /// Whether both ends of a range are set, at least 0, with <paramref name="low"/> no more than <paramref name="high"/>, and <paramref name="high"/> no more than
    /// <paramref name="upperBound"/> when one is given.
    /// </summary>
    /// <remarks>
    /// Takes decimals so the whole-number ranges (quantities, SpreadLimits) can share it through the implicit widening from <see langword="int"/>.
    /// </remarks>
    private static bool IsValidRange(decimal? low, decimal? high, decimal? upperBound)
    {
        return low != null
            && high != null
            && low >= 0m
            && low <= high
            && (upperBound == null || high <= upperBound);
    }

    /// <summary>
    /// The values that appear more than once.
    /// </summary>
    private static IEnumerable<T> Duplicates<T>(IEnumerable<T> values)
    {
        return values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
    }

    /// <summary>
    /// "Forest", or "Hilly Forest" for a Geography-specific habitat.
    /// </summary>
    private static string Describe(BiomeType biome, CampaignMapGeographyType? geography)
    {
        return geography == null ? $"{biome}" : $"{geography} {biome}";
    }
}
