using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <summary>
/// The one rule for whether a campaign map tile can be entered at all.
/// </summary>
/// <remarks>
/// <para>
/// Shared by movement (<c>MovementEntryRules</c>, through <c>MovementTargetTile</c>) and by the per-Round territory pass, which uses it for two different things: an
/// impassable tile <b>seals</b> an enclosure ring, and it <b>carries no supply hop</b>. Both of those are the same fact movement already knows, so they read the same
/// rule rather than a copy of it - the precedent <see cref="CampaignTileWater"/> set for water.
/// </para>
/// <para>
/// Keeping it in one place is what lets the territory rules be stated as "impassable terrain seals" rather than "mountains and the Void seal". A future impassable
/// terrain then seals for free, and a map author who makes a mountain passable changes movement and borders together rather than making the two disagree.
/// </para>
/// </remarks>
internal static class CampaignTileImpassability
{
    /// <summary>
    /// Whether nothing can enter a tile with this biome and geography, whatever its movement domain.
    /// </summary>
    /// <remarks>
    /// Two independent markers, matching how the seed data expresses the idea: Mountainous uses the geography's <see cref="CampaignGeography.IMPASSABLE_MOVEMENT_COST"/>
    /// sentinel, while the Void biome is simply not traversable.
    /// </remarks>
    public static bool IsImpassable(bool biomeIsTraversable, decimal geographyMovementCost)
    {
        return biomeIsTraversable == false || GeographyIsImpassable(geographyMovementCost);
    }

    /// <summary>
    /// Whether it is the ground itself that cannot be crossed, rather than what is painted on it.
    /// </summary>
    /// <remarks>
    /// Only Mountainous carries the sentinel today, which is what lets a refusal name the mountain instead of falling back on the general wording the Void biome has to
    /// use. The comparison is <c>&gt;=</c> rather than <c>==</c>, so a map author who paints something costlier still gets impassable ground.
    /// </remarks>
    public static bool GeographyIsImpassable(decimal geographyMovementCost)
    {
        return geographyMovementCost >= CampaignGeography.IMPASSABLE_MOVEMENT_COST;
    }
}
