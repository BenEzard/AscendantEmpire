namespace AscendantEmpire.Domain.Enums.Intelligence;

/// <summary>
/// What a Player last knew to be true of a Structure they have watched.
/// </summary>
/// <remarks>
/// <para>
/// A Structure watched being razed is knowledge, not an absence of it, which is why a known Structure is restatused rather than deleted. See "Known forces and known structures" in
/// <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// Numbered from 1, and persisted, so a member may be renamed but never renumbered.
/// </para>
/// </remarks>
public enum KnownStructureStatus
{
    /// <summary>
    /// Last seen standing, whether finished or still being built.
    /// </summary>
    Standing = 1,

    /// <summary>
    /// Watched being razed.
    /// </summary>
    Razed = 2,
}
