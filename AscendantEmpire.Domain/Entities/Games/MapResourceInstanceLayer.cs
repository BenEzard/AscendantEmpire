namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// One layer of a stratified instance, e.g. layer 2 of the coal seam on tile (4,5). Keyed on the instance and the layer number.
/// </summary>
/// <remarks>
/// Its contents were rolled from the type's <c>MapResourceTypeLayer</c> when the instance was created. Layers are extracted in order, each exhausted before the next is
/// reached, and a Player surveys them in order (see <see cref="PlayerKnownMapResource.DeepestLayerSurveyed"/>).
/// </remarks>
public class MapResourceInstanceLayer
{
    public int MapResourceInstanceId { get; set; }

    /// <summary>
    /// The layer's depth, from 1 at the top.
    /// </summary>
    public int LayerNumber { get; set; }

    /// <summary>
    /// The Game, carried so the game delete can purge these rows directly.
    /// </summary>
    public int GameId { get; set; }

    /// <summary>
    /// The Resource the layer holds (a <c>resources.Resource</c>), or <see langword="null"/> when the layer was created empty.
    /// </summary>
    /// <remarks>
    /// A survey can reveal that a layer is empty, so an empty layer still has a row.
    /// </remarks>
    public int? ResourceId { get; set; }

    /// <summary>
    /// The quantity left in the layer, stored scaled by <c>ScaledAmount.Scale</c> (1,000). Never negative, and always 0 for an empty layer.
    /// </summary>
    public long Quantity { get; set; }

    /// <summary>
    /// How far off the quantity shown to Players can be for this layer, as a fraction, rolled when the instance was created.
    /// </summary>
    /// <remarks>
    /// Per layer, so a deeper layer can be harder to judge than the one above it.
    /// </remarks>
    public decimal EstimationFuzzyFactor { get; set; }
}
