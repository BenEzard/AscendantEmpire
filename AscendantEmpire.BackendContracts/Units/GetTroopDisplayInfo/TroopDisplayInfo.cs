namespace AscendantEmpire.BackendContracts.Units.GetTroopDisplayInfo;

public record TroopDisplayInfo
{
    public int UnitDesignId { get; init; }
    public string MapImagePath { get; init; } = null!;
}
