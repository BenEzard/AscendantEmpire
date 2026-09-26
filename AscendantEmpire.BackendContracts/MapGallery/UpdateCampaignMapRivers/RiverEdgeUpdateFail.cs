using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapRivers;

namespace AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapRivers;

/// <inheritdoc cref="IRiverEdgeUpdateFail" />
public record RiverEdgeUpdateFail : IRiverEdgeUpdateFail
{
    /// <inheritdoc />
    public required int X { get; init; }

    /// <inheritdoc />
    public required int Y { get; init; }

    /// <inheritdoc />
    public required RiverEdgeSide Edge { get; init; }

    /// <inheritdoc />
    public required RiverWidth OriginalWidth { get; init; }

    /// <inheritdoc />
    public required string ErrorMessage { get; init; }
}
