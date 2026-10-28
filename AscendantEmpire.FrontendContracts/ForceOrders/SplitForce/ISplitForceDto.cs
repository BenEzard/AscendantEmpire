namespace AscendantEmpire.FrontendContracts.ForceOrders.SplitForce;

/// <summary>
/// The body of a split order: which Force is divided, and into what sizes.
/// </summary>
/// <remarks>
/// <para>
/// The game and player are deliberately absent — they come from the caller's game token, so a split order cannot be aimed at another player's Forces.
/// The tile is absent for the same reason a step takes a direction rather than a destination: the groups stand where the Force already stood.
/// </para>
/// <para>
/// Sizes only, never casualty counts. Where the wounded end up is the server's decision, apportioned in proportion to group size; letting the client place
/// them would allow a player to sweep every casualty into one group and disband it.
/// </para>
/// </remarks>
public interface ISplitForceDto
{
    /// <summary>
    /// Which Force is splitting. A Force is a category rather than a type, so this is a <c>UnitInstance</c> id today; see <c>docs/Terminology.md</c>.
    /// </summary>
    int UnitInstanceId { get; }

    /// <summary>
    /// The strength of each group the split should produce, in the order the player composed them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must describe a legal unled division of the Force: at least two groups, every group at least one base quantum, the sizes adding up to exactly the
    /// Force's current strength, and at most one group carrying a remainder that is not a whole multiple of the quantum.
    /// </para>
    /// <para>
    /// The order matters only in that the <b>first</b> group is the one the original Force keeps; the rest are created as new Forces beside it.
    /// </para>
    /// </remarks>
    IReadOnlyCollection<int> GroupSizes { get; }
}
