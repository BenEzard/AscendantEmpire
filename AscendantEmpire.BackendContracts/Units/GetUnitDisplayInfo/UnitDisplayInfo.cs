namespace AscendantEmpire.BackendContracts.Units.GetUnitDisplayInfo;

public record UnitDisplayInfo
{
    public int UnitTemplateId { get; init; }
    public string MapImagePath { get; init; } = null!;

    /// <summary>
    /// The template's name, as shown to the player — "Settler", "Warrior".
    /// </summary>
    /// <remarks>
    /// The name of the <em>template</em>, not of any Unit built from it. A Unit can carry the player's own name
    /// (<see cref="Domain.Entities.Units.UnitInstance.Name"/>), but nothing sets one yet.
    /// </remarks>
    public string DisplayName { get; init; } = null!;
}
