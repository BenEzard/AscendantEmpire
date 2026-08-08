namespace AscendantEmpire.BackendContracts.Units.GetTroopDisplayInfo;

public record TroopDisplayInfo
{
    public int UnitTemplateId { get; init; }
    public string MapImagePath { get; init; } = null!;
}
