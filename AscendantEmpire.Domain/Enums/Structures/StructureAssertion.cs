namespace AscendantEmpire.Domain.Enums.Structures;

/// <summary>
/// What a completed Structure of a given kind asserts over the tile it stands on.
/// </summary>
/// <remarks>
/// <para>
/// Authored on the catalogue entry beside its sight values - see "Structures" in <c>docs/design/campaign-map/tile-ownership.md</c>. A totem pole or border marker asserts
/// a <see cref="Claim"/>; a foraging camp, sentry tower or Settlement asserts <see cref="Owned"/>.
/// </para>
/// <para>
/// <b>Two members, and deliberately no zero.</b> Nothing a Player builds can assert Occupied, because Occupied is by definition a rival standing on someone else's ground,
/// and a two-member enum makes that unrepresentable rather than merely forbidden. There is no <c>None</c> either: everything in the catalogue asserts something, so a
/// Structure that asserted nothing would be a different concept - a road, a fence - and can add a member when it exists.
/// </para>
/// <para>
/// Not <c>TileOwnershipStatus</c>, which is the <i>derived read</i> of a tile's state and crosses the wire to the client. Four of its seven members are illegal on a
/// catalogue row, so reusing it would make "constrained to two values" a comment rather than a type, and adding a read state would silently widen what a Structure may
/// assert.
/// </para>
/// <para>
/// Values are assigned explicitly and must never be renumbered: they are persisted on every catalogue row.
/// </para>
/// </remarks>
public enum StructureAssertion
{
    /// <summary>
    /// A political assertion over the tile, which is cheap, non-exclusive and revocable.
    /// </summary>
    /// <remarks>
    /// Several Players may claim one tile at once, and a claim lapses at the end of the Round unless it reaches owned land within the claim supply range.
    /// </remarks>
    Claim = 1,

    /// <summary>
    /// Real administrative control of the tile, which is exclusive and extinguishes every rival claim on it.
    /// </summary>
    Owned = 2,
}
