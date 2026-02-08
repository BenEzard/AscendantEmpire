namespace AscendantEmpire.BackendContracts.Players.GetPlayerStructures;

public record PlayerStructure
{
    public int CampaignMapTileId { get; init; }
    public int? BattleMapTileId { get; init; }
    public int XCoord { get; init; }
    public int YCoord { get; init; }
    public int StructureId { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public double CompletionPercent { get; init; }
    public int OwnerPlayerId { get; init; }
    public int SightRange { get; init; }
}
