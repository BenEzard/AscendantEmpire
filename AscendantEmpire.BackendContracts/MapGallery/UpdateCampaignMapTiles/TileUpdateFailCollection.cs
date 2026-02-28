namespace AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapTiles;

public class TileUpdateFailCollection
{
    public required IReadOnlyCollection<TileUpdateFail> TileUpdateFails { get; init; }
}
