namespace AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapTiles;

/// <summary>
/// Represents a collection of Tiles that have failed for a specific reason.
/// </summary>
public interface ITileGroupUpdateFail
{
    public string ErrorMessage { get; }

    /// <summary>
    /// Collection of Tiles that failed to update, along with their original values.
    /// </summary>
    public IReadOnlyCollection<ITileUpdateFailTile> FailedTiles { get; }
}
