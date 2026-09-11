namespace AscendantEmpire.Domain.Enums;

/// <summary>
/// What kind of record a map projection is describing, so one flattened shape can carry several kinds.
/// </summary>
/// <remarks>
/// <para>
/// This is the discriminator behind the <b>Force</b> umbrella. A Force is a <see cref="Unit"/> or a <see cref="Command"/> — the two kinds that
/// occupy a tile and move as one — and is deliberately a category rather than a type: there is no Force entity, no base class and no polymorphic
/// foreign key anywhere in the model. Asking "is this a Force?" is therefore a test <b>over</b> this enum
/// (<c>RecordType is Unit or Command</c>), never a member <b>of</b> it.
/// </para>
/// <para>
/// <see cref="Structure"/> is a record type but is <b>not</b> a Force, which is why this enum is the broader concept and why
/// the campaign map's <c>SightObserver</c> can flatten structures and forces into one shape. See <c>docs/work-in-progress/unit-command-terminology.md</c>.
/// </para>
/// <para>
/// The numeric values are load-bearing: <c>structure.Structures.RecordTypeId</c> persists them, so a member may be renamed but never renumbered.
/// </para>
/// </remarks>
public enum RecordType
{
    /// <summary>
    /// Not a record at all. Used by the whole-map pseudo-observer, which has no owning entity.
    /// </summary>
    None = 0,

    /// <summary>
    /// A single fielded body of soldiers built from one Unit Template. A Force.
    /// </summary>
    Unit = 1,

    /// <summary>
    /// Two or more Units under a shared command structure on one campaign tile. A Force.
    /// </summary>
    Command = 2,

    /// <summary>
    /// A building or terrain improvement on a tile. Not a Force.
    /// </summary>
    Structure = 3,
}
