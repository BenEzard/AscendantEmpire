namespace AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;

/// <summary>
/// Response envelope for the player's available templates. Wrapping the list (rather than returning a
/// bare array) leaves room to add metadata later without breaking the response shape.
/// </summary>
public record PlayerTemplatesResult
{
    public required IReadOnlyCollection<DesignTemplate> Templates { get; init; }
}
