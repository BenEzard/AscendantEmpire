namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// The <see cref="Tag.Code"/> values of the ability tags the application itself gates behaviour on, as opposed to the ones only an author ever names.
/// </summary>
/// <remarks>
/// <para>
/// A Tag's Code is its fixed key once saved, so code that must find one particular capability has to match on the literal. Naming those literals here keeps
/// the seed data, the strategies that gate on them, the turn processes that count them and the tests all pointing at the same string - a rename that misses
/// one of those would otherwise compile and quietly stop matching.
/// </para>
/// <para>
/// Only ability tags belong here, and only the ones something decides on. Classification tags and the long tail of authored buff tags are ordinary catalogue
/// content: nothing in the application names them, so a constant would be a second place to keep them correct for no gain. Organisation tags are named by
/// the seed rather than decided on, so the few that need a constant have their own - see <see cref="OrganisationTagCodes"/>.
/// </para>
/// <para>
/// A tag listed here is still ordinary catalogue data. It can be absent from a database that has not run the seed's Tags target, and every caller has to cope
/// with no Force carrying it.
/// </para>
/// </remarks>
public static class CapabilityTagCodes
{
    /// <summary>
    /// Lets a Force found a settlement, and makes its people settlers rather than soldiers.
    /// </summary>
    /// <remarks>Authored onto the <c>camping-materials</c> equipment component. Gated on by <c>CreateVillageStrategy</c>.</remarks>
    public const string CREATE_SETTLEMENT = "create-settlement";

    /// <summary>
    /// Lets a Force establish an early camp - the smaller sibling of <see cref="CREATE_SETTLEMENT"/>, on a much lower troop requirement.
    /// </summary>
    /// <remarks>
    /// Authored onto the same component, but separately, so a future component can grant camping without granting settlement. Anything asking "is this Force
    /// carrying settlers?" therefore has to accept either of the two rather than only the first.
    /// </remarks>
    public const string CREATE_EARLY_CAMP = "create-early-camp";
}
