namespace AscendantEmpire.FrontendContracts.Units.GetUnitDisplayInfo;

public interface IUnitDisplayInfo
{
    public int UnitTemplateId { get; }
    public string MapImagePath { get; }

    /// <summary>
    /// The template's name, as shown to the player — "Settler", "Warrior".
    /// </summary>
    public string DisplayName { get; }
}
