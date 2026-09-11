namespace AscendantEmpire.Domain.Enums.Intelligence;

/// <summary>
/// What a <c>PlayerReport</c> is about — the producer that raised it.
/// </summary>
/// <remarks>
/// <para>
/// One member per producer, not one per sentence. "Seen at (X, Y)", "seen passing through" and "heading north-east" are three renderings of one <see cref="ForceSighted"/> record,
/// composed by the client from its fields. See "Reports" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// Numbered from 1, so a value nobody set reads as no report at all. The values are persisted, so a member may be renamed but never renumbered.
/// </para>
/// </remarks>
public enum PlayerReportType
{
    /// <summary>
    /// A Force was watched moving, appearing or being destroyed: one contiguous track of it, as one coalition saw it.
    /// </summary>
    ForceSighted = 1,
}
