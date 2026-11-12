using AscendantEmpire.Domain.Enums.Assets;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// A group of Assets that satisfy one ongoing need, e.g. the foods a man will eat or the fuels an engine will burn.
/// </summary>
/// <remarks>
/// <para>
/// Global: rows are not owned by any Player or Game, and <see cref="Code"/> is the unique natural key, so a set can be authored and seeded without knowing
/// database identities.
/// </para>
/// <para>
/// A set is a grouping of Assets rather than a unit design concern, which is why it lives in the <c>assets</c> schema alongside the catalogue it draws
/// from. What consumes a set is a <c>UnitComponentSupplyRequirement</c> today; Settlements and Structures are expected to later.
/// </para>
/// <para>
/// How the members relate to one another follows from <see cref="SupplyType"/>: Food and Fuel are satisfied from whichever member is available, while a
/// Repair set has one member drawn from it at random. See <c>docs/design/units/unit-support-and-supply.md</c>.
/// </para>
/// </remarks>
public class SupplySet
{
    public const int CODE_MAX_LENGTH = 50;
    public const int DISPLAY_NAME_MAX_LENGTH = 100;

    public int Id { get; set; }

    /// <summary>
    /// Unique, stable key for the set, e.g. "human-food". What seed data matches on.
    /// </summary>
    [MaxLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    /// <summary>
    /// The name shown in the dev Entity Setup Tool, e.g. "Human Food".
    /// </summary>
    [MaxLength(DISPLAY_NAME_MAX_LENGTH)]
    public required string DisplayName { get; set; }

    /// <summary>
    /// The need this set satisfies, which decides how a requirement's quantity is measured.
    /// </summary>
    public required SupplyType SupplyType { get; set; }

    /// <summary>
    /// The Assets in the set, with what each costs and how heavily it is weighted (1 or more).
    /// </summary>
    /// <remarks>
    /// A set with no members can never be satisfied, so the upsert refuses one rather than leaving an unmeetable requirement in the catalogue.
    /// </remarks>
    public ICollection<SupplySetAsset> Assets { get; set; } = [];
}
