namespace AscendantEmpire.Domain.Enums.UnitDesigns;

/// <summary>
/// How a formation under a <see cref="TagType.MilitaryOrganisationalGroup"/> tag is led.
/// </summary>
/// <remarks>
/// <para>
/// Authored on the tag (catalogue data) rather than on the player's organisation structure, so a player cannot
/// opt a hierarchy out of the officer economy by editing their own data. See
/// <c>docs/design/units/military-organisation.md</c> ("Leadership model").
/// </para>
/// </remarks>
public enum LeadershipModel
{
    /// <summary>
    /// A ranked officer hierarchy: tier N is commanded by a Rank-N officer, sizes are the structure's
    /// authored tier sizes, and each tier is bounded by its rank's soldier ceiling.
    /// </summary>
    Commissioned,

    /// <summary>
    /// No officers at any tier (scouts, snipers, drones, settlers). The unit sits outside the officer
    /// economy entirely; its legal sizes are any multiple of the tag's base quantum up to the tag's max.
    /// </summary>
    Unled,
}
