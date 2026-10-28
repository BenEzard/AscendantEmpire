using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.FrontendContracts.ForceOrders.SplitForce;

/// <summary>
/// The numbers the split dialog needs to offer a legal division of one unled Force.
/// </summary>
/// <remarks>
/// <para>
/// Fetched when the dialog opens rather than carried on the context-actions list endpoint. That endpoint says only whether an action is available, and
/// giving it a per-action payload would make every action's contract the union of every action's needs — see
/// <c>docs/design/units/force-context-action-toolbar.md</c>, "Splitting needs parameters".
/// </para>
/// <para>
/// Only ever returned for an unled Force. A commissioned one detaches along tier lines instead, which is a different mechanic that asks for a tier and a
/// count rather than a list of sizes, so this contract would not describe it.
/// </para>
/// </remarks>
public interface ISplitForceOptions
{
    /// <summary>
    /// The Force being split. A Force is a category rather than a type, so this is a <c>UnitInstance</c> id today; see <c>docs/Terminology.md</c>.
    /// </summary>
    int UnitInstanceId { get; }

    /// <summary>The Force's name, for the dialog's title.</summary>
    string DisplayName { get; }

    /// <summary>
    /// How the Force's organisation is led, for the dialog's title.
    /// </summary>
    /// <remarks>
    /// Always <see cref="Domain.Enums.UnitDesigns.LeadershipModel.Unled"/> while this endpoint serves unled Forces alone. Sent rather than assumed by the
    /// dialog, so the title stays honest when the commissioned branch of splitting is built.
    /// </remarks>
    LeadershipModel LeadershipModel { get; }

    /// <summary>How many of the Force's soldiers are unwounded.</summary>
    int HealthyCount { get; }

    /// <summary>
    /// How many of the Force's soldiers are wounded.
    /// </summary>
    /// <remarks>
    /// The wounded travel with their group rather than being left behind, so they count towards the size a split is measured against. The server decides
    /// which group each ends up in, in proportion to group size.
    /// </remarks>
    int InjuredCount { get; }

    /// <summary>
    /// The Force's current strength — healthy and injured together, which is the figure the split rules measure.
    /// </summary>
    /// <remarks>Sent rather than left to the client to add up, so the two cannot disagree about what is being divided.</remarks>
    int TotalStrength { get; }

    /// <summary>
    /// The organisation tag's authored base quantum: the smallest group this Force divides into, and the unit every group is a multiple of.
    /// </summary>
    /// <remarks>
    /// Always greater than zero. An unled tag authored without one is a configuration error, and the endpoint refuses rather than returning a Force the
    /// dialog would then offer impossible splits of.
    /// </remarks>
    int BaseSize { get; }
}
