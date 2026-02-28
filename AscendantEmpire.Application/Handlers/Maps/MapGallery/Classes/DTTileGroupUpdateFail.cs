using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapTiles;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery.Classes;

/// <summary>
/// Represents a collection of Tiles that have failed for a specific reason.
/// </summary>
public class DTTileGroupUpdateFail : ITileGroupUpdateFail
{
    /// <inheritdoc />
    public required string ErrorMessage { get; set; }

    /// <inheritdoc />
    public required IReadOnlyCollection<DTTileUpdateFailTile> FailedTiles { get; set; }

    IReadOnlyCollection<ITileUpdateFailTile> ITileGroupUpdateFail.FailedTiles => FailedTiles;
}
