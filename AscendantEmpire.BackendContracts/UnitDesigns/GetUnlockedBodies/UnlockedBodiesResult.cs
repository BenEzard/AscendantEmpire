namespace AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;

/// <summary>
/// Response envelope for the unlocked-bodies feed. Wrapping the list (rather than returning a bare
/// array) leaves room to add metadata later without breaking the response shape.
/// </summary>
public record UnlockedBodiesResult
{
    public required IReadOnlyCollection<DesignBody> Bodies { get; init; }
}
