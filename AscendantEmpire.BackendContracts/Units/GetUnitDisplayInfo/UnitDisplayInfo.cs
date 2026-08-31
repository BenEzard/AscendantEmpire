namespace AscendantEmpire.BackendContracts.Units.GetUnitDisplayInfo;

public record UnitDisplayInfo
{
    public int UnitTemplateId { get; init; }
    public string MapImagePath { get; init; } = null!;
}
