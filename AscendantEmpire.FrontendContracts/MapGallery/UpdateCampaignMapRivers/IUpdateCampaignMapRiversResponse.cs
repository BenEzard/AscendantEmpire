namespace AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapRivers;

/// <summary>
/// The result of setting river edges on a gallery map.
/// </summary>
/// <remarks>
/// Success only. Unlike a tile paint, a river batch never partly lands: the authoring rules judge the network the batch would produce, so either every
/// change is written or none is. A refused batch comes back as an error instead, carrying <see cref="IRiverEdgeUpdateFailCollection"/> as its detail.
/// </remarks>
public interface IUpdateCampaignMapRiversResponse
{
    /// <summary>How many edges were written.</summary>
    int EdgesUpdated { get; }
}
