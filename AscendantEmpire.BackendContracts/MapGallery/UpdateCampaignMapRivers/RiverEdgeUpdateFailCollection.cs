using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapRivers;

namespace AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapRivers;

/// <inheritdoc cref="IRiverEdgeUpdateFailCollection" />
public record RiverEdgeUpdateFailCollection : IRiverEdgeUpdateFailCollection
{
    /// <inheritdoc />
    public required IReadOnlyCollection<RiverEdgeUpdateFail> RefusedEdges { get; init; }

    IReadOnlyCollection<IRiverEdgeUpdateFail> IRiverEdgeUpdateFailCollection.RefusedEdges => RefusedEdges;
}
