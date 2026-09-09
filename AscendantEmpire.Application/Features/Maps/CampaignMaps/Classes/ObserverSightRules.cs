namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// The rules that turn an Observer's own Sight value into the range it actually sees, given the Tile it is standing on.
/// </summary>
/// <remarks>
/// <para>
/// Pure arithmetic over three facts about the occupied Tile, deliberately lifted out of <c>GetPlayerCampaignMapHandler</c>. There it lived inside a
/// method that queries the database, so the rules could not be exercised without a containerised SQL Server for what is ultimately a comparison and
/// an addition. See <c>docs/testing.md</c> on using the lightest tier that suffices.
/// </para>
/// <para>
/// The design these rules implement is in <c>docs/design/campaign-map.md</c>, "Vision on the campaign map".
/// </para>
/// </remarks>
internal static class ObserverSightRules
{
    /// <summary>
    /// Works out how far an Observer can see, applying the occupied Tile's sight penalty or elevation bonus to its own Sight value.
    /// </summary>
    /// <param name="baseSightRange">The Observer's own Sight, before the ground it stands on is taken into account.</param>
    /// <param name="occupiedTileBlocksSight">Whether the occupied Tile's Biome blocks sight — Jungle, Forest, Woodland, Mangrove or Void.</param>
    /// <param name="occupiedTileRelativeElevation">How high the occupied Tile's Geography stands, where Flat is zero, and Hilly and above are positive.</param>
    /// <returns>The sight range to walk from this Observer, which is zero whenever the occupied Tile blocks sight.</returns>
    /// <remarks>
    /// <para>
    /// A blocking Biome takes sight away outright: the Observer sees the Tile it occupies and nothing else. It beats the elevation bonus, because
    /// standing on a Hilly Jungle is still standing in a Jungle and the extra height buys nothing through the canopy.
    /// </para>
    /// <para>
    /// A blinded Observer is not left with no way to learn anything about its surroundings. It still reveals the adjacent Tiles it could never have
    /// walked onto to find out — see <see cref="BlockedSightReveal"/>, which is a separate rule applied after this one.
    /// </para>
    /// </remarks>
    internal static int ResolveSightRange(int baseSightRange, bool occupiedTileBlocksSight, int occupiedTileRelativeElevation)
    {
        if (occupiedTileBlocksSight)
        {
            return 0;
        }

        // Only high ground is a bonus. Sea-level and below-sea-level Geographies sit at zero or less and are simply not a penalty, so they leave the
        // Observer's own Sight alone rather than eating into it.
        if (occupiedTileRelativeElevation <= 0)
        {
            return baseSightRange;
        }

        return baseSightRange + occupiedTileRelativeElevation;
    }
}
