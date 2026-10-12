namespace AscendantEmpire.BackendContracts.Technologies;

/// <summary>
/// Which technology a Player's directed research should go into.
/// </summary>
/// <remarks>
/// Carries no Player or Game: both come off the caller's game token, as they do for every other game-scoped action.
/// </remarks>
public record SetResearchProjectDto
{
    /// <summary>
    /// The technology to research, or null to stop researching anything.
    /// </summary>
    /// <remarks>
    /// Null is a clear rather than a no-op: "no project selected" is a state the Player can legitimately return to, and the end-turn check reports it.
    /// </remarks>
    public int? TechnologyId { get; init; }
}
