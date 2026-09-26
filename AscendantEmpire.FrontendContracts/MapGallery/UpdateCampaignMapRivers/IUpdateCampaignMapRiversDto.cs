namespace AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapRivers;

/// <summary>
/// The body of a request to set river edges on a gallery map.
/// </summary>
/// <remarks>
/// A batch rather than a single edge, so that an importer or a future multi-edge gesture can be judged as one river network instead of edge by edge.
/// The whole batch is applied or none of it is: the authoring rules are properties of the network as a whole, so a half-applied batch could leave a
/// river that none of the individual changes would have been allowed to create.
/// </remarks>
public interface IUpdateCampaignMapRiversDto
{
    /// <summary>The edges to set. Each one names the tile that owns it.</summary>
    IReadOnlyCollection<IRiverEdgeChange> Changes { get; }
}
