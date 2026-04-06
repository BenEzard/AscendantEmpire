namespace AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapTiles;

public record class TileUpdateFail
{
    public required string ErrorMessage { get; init; }
    
    /// <summary>
    /// Precise original values per tile so UI can reset exactly.
    /// </summary>
    public required IReadOnlyCollection<TileUpdateFailTile> FailedTiles { get; init; }
}