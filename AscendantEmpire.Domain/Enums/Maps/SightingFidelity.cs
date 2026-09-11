namespace AscendantEmpire.Domain.Enums.Maps;

/// <summary>
/// How much an observer learned about what it saw, from "something passed" up to full identification.
/// </summary>
/// <remarks>
/// <para>
/// The levels are ordered, so a higher value always discloses everything a lower one does and more. That lets a coalition's best sighting be taken as a
/// plain maximum across its witnesses.
/// </para>
/// <para>
/// Fidelity is a <b>disclosure</b> rule, not a storage rule: whatever level an observer earned, what is stored is the truth, and a read projection strips
/// it back to the level earned. See "Fidelity" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// The numeric values are load-bearing once reports persist them, so a member may be renamed but never renumbered.
/// </para>
/// </remarks>
public enum SightingFidelity
{
    /// <summary>
    /// Something passed through the tile. Nothing about what it was.
    /// </summary>
    Movement = 1,

    /// <summary>
    /// What kind of thing it was — a settler, a scout.
    /// </summary>
    Type = 2,

    /// <summary>
    /// Whose it was.
    /// </summary>
    Owner = 3,

    /// <summary>
    /// Its strength and condition.
    /// </summary>
    Detail = 4,
}
