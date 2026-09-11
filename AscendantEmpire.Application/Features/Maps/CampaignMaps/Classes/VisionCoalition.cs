namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// A set of Players who share vision, so that what any one of them sees counts for all of them.
/// </summary>
/// <remarks>
/// <para>
/// Intelligence is folded per coalition rather than per Player: three allied Units watching one road produce one sighting, not three. Reports are still
/// addressed to individual members. See "Coalitions" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// Coalitions in one Game are disjoint — a Player belongs to exactly one.
/// </para>
/// </remarks>
/// <param name="CoalitionId">Stable only within one resolution. Today it is the sole member's Player id.</param>
internal sealed record VisionCoalition(int CoalitionId, IReadOnlyCollection<int> MemberPlayerIds);

/// <summary>
/// The coalition rule for a Game with no shared vision: every Player is a coalition of one.
/// </summary>
/// <remarks>
/// There is no foreign relations model yet, so this is the whole rule for now. F-27 replaces it with coalitions resolved from diplomatic arrangements.
/// </remarks>
internal static class SoloCoalitions
{
    /// <summary>
    /// One coalition per distinct Player, identified by that Player's id, in ascending order.
    /// </summary>
    internal static IReadOnlyCollection<VisionCoalition> From(IEnumerable<int> playerIds)
    {
        ArgumentNullException.ThrowIfNull(playerIds);

        return [.. playerIds.Distinct().Order().Select(playerId => new VisionCoalition(playerId, [playerId]))];
    }
}
