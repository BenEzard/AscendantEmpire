namespace AscendantEmpire.BackendContracts.Military.GetOfficerRanks;

/// <summary>
/// The response envelope for "get the calling player's officer rank ladders" (all domains).
/// </summary>
public record GetOfficerRanksResult
{
    /// <summary>Every rank row for the player, across all seeded domains, ordered by domain then level.</summary>
    public required IReadOnlyCollection<OfficerRankView> Ranks { get; init; }
}
