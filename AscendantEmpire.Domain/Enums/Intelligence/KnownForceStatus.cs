namespace AscendantEmpire.Domain.Enums.Intelligence;

/// <summary>
/// What a Player last knew to be true of a Force they have watched.
/// </summary>
/// <remarks>
/// <para>
/// A Force watched being destroyed is knowledge, not an absence of it, which is why a known Force is restatused rather than deleted. See "Known forces and known structures" in
/// <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// Numbered from 1, and persisted, so a member may be renamed but never renumbered.
/// </para>
/// </remarks>
public enum KnownForceStatus
{
    /// <summary>
    /// Last seen alive, at the recorded tile.
    /// </summary>
    Sighted = 1,

    /// <summary>
    /// Watched being destroyed, on the recorded tile.
    /// </summary>
    Destroyed = 2,
}
