namespace AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;

/// <summary>
/// One layer of a stratified MapResourceType.
/// </summary>
public interface IMapResourceTypeLayerDto
{
    /// <summary>
    /// The layer's depth. A type's layers are numbered 1, 2, 3... with no gaps.
    /// </summary>
    int LayerNumber { get; }

    /// <summary>
    /// The Asset the layer holds when it is not empty.
    /// </summary>
    int AssetId { get; }

    /// <summary>
    /// The chance, as a fraction, that an instance's layer is created empty.
    /// </summary>
    decimal EmptyLikelihood { get; }

    /// <summary>
    /// The lowest quantity a layer that is not empty is created with, as a whole number.
    /// </summary>
    int QuantityMin { get; }

    /// <summary>
    /// The highest quantity a layer that is not empty is created with, as a whole number.
    /// </summary>
    int QuantityMax { get; }

    /// <summary>
    /// The chance, as a fraction, of discovering this layer once the one above it is known. Empty for layer 1, and required for every deeper layer.
    /// </summary>
    decimal? DiscoveryLikelihood { get; }

    /// <summary>
    /// The mine level needed to reach this layer. Empty for layer 1, and required for every deeper layer.
    /// </summary>
    int? RequiredMineLevel { get; }

    /// <summary>
    /// The lowest fuzzy factor an instance's layer is created with, as a fraction.
    /// </summary>
    decimal EstimationFuzzyFactorMin { get; }

    /// <summary>
    /// The highest fuzzy factor an instance's layer is created with, as a fraction.
    /// </summary>
    decimal EstimationFuzzyFactorMax { get; }
}
