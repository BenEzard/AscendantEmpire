namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

/// <summary>
/// The body of a fog-of-war read: how much of the map to load.
/// </summary>
/// <remarks>
/// The game and player are deliberately absent — they come from the caller's game token, so the read cannot be aimed at another player's view of the map.
/// </remarks>
public record GetPlayerCampaignMapDto
{
    public bool LoadWholeMap { get; init; }
}
