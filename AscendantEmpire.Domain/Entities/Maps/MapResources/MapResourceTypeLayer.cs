namespace AscendantEmpire.Domain.Entities.Maps.MapResources;

/// <summary>
/// One layer of a stratified MapResourceType, e.g. a coal seam's layer 3: none, or some high-quality coal. Keyed on the type and the layer number.
/// </summary>
/// <remarks>
/// <para>
/// A template: each instance rolls its own contents for every layer at game setup, into <c>game.MapResourceInstanceLayer</c>. A layer may hold a different Asset from the
/// one above it, and may turn out empty.
/// </para>
/// <para>
/// Layers are extracted in order, each exhausted before the next can be reached. Each layer below the first has three gates: its own <see cref="DiscoveryLikelihood"/>, a
/// survey tech (a <c>PlayerMapResourceSwitch</c> of kind <c>SurveyLayer</c> until Technology is modelled), and <see cref="RequiredMineLevel"/>.
/// </para>
/// </remarks>
public class MapResourceTypeLayer
{
    public int MapResourceTypeId { get; set; }

    public MapResourceType MapResourceType { get; set; } = null!;

    /// <summary>
    /// The layer's depth, from 1 at the top.
    /// </summary>
    public int LayerNumber { get; set; }

    /// <summary>
    /// The Asset the layer holds when it is not empty (a <c>assets.Asset</c>).
    /// </summary>
    public int AssetId { get; set; }

    /// <summary>
    /// The chance, as a fraction, that an instance's layer is created empty.
    /// </summary>
    public decimal EmptyLikelihood { get; set; }

    /// <summary>
    /// The lowest quantity a layer that is not empty is created with, stored scaled by <c>ScaledAmount.Scale</c> (1,000).
    /// </summary>
    public long QuantityMin { get; set; }

    /// <summary>
    /// The highest quantity a layer that is not empty is created with, stored scaled by <c>ScaledAmount.Scale</c> (1,000).
    /// </summary>
    public long QuantityMax { get; set; }

    /// <summary>
    /// The chance, as a fraction, of discovering this layer once the one above it is known. <see langword="null"/> for layer 1, which uses the type's
    /// <c>DiscoveryLikelihood</c>.
    /// </summary>
    public decimal? DiscoveryLikelihood { get; set; }

    /// <summary>
    /// The mine level needed to reach this layer, or <see langword="null"/> for layer 1. Stored now, and checked once mines are modelled.
    /// </summary>
    public int? RequiredMineLevel { get; set; }

    /// <summary>
    /// The lowest fuzzy factor an instance's layer is created with, as a fraction. Each layer judges its quantity separately, so a deeper layer can be harder to judge.
    /// </summary>
    public decimal EstimationFuzzyFactorMin { get; set; }

    /// <summary>
    /// The highest fuzzy factor an instance's layer is created with, as a fraction.
    /// </summary>
    public decimal EstimationFuzzyFactorMax { get; set; }
}
