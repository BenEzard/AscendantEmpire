using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapRivers;

namespace AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapRivers;

/// <inheritdoc cref="IRiverEdgeChange" />
public record RiverEdgeChange : IRiverEdgeChange
{
    /// <inheritdoc />
    public int X { get; init; }

    /// <inheritdoc />
    public int Y { get; init; }

    /// <inheritdoc />
    public RiverEdgeSide Edge { get; init; }

    /// <inheritdoc />
    public RiverWidth Width { get; init; }

    public override string ToString() => $"({X}, {Y}) {Edge}";
}
