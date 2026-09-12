namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <summary>
/// The one rule for whether a new sighting should replace what a Player already remembers, shared by known Forces and known Structures.
/// </summary>
/// <remarks>
/// <para>
/// Compared on (Turn, sequence). Turn ids only ever increase, the same ordering <c>CampaignTurnService</c> relies on for "most recent", so a later Turn always wins and an older one never
/// overwrites newer knowledge.
/// </para>
/// <para>
/// An <b>equal</b> sighting also wins. That is a re-run of the same Turn, and replacing it with itself is what makes the sightings step safe to retry.
/// </para>
/// </remarks>
internal static class SightingRecency
{
    /// <summary>
    /// Whether a sighting at the candidate Turn and sequence should replace one remembered at the existing Turn and sequence.
    /// </summary>
    internal static bool Supersedes(int candidatePlayerTurnId, int candidateTurnSequence, int existingPlayerTurnId, int existingTurnSequence)
    {
        if (candidatePlayerTurnId != existingPlayerTurnId)
        {
            return candidatePlayerTurnId > existingPlayerTurnId;
        }

        return candidateTurnSequence >= existingTurnSequence;
    }
}
