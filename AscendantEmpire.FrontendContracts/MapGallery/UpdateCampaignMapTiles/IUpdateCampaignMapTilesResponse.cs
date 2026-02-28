namespace AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapTiles;

public interface IUpdateCampaignMapTilesResponse
{
    public int TilesUpdated { get; }

    /// <summary>
    /// Present when the update was only partially successful.
    /// Contains summary message + list of coords + precise per-tile revision info.
    /// </summary>
    public IReadOnlyCollection<ITileGroupUpdateFail>? Failures { get; }
}
