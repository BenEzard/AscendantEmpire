using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// Something that can see the campaign map — a Unit, a Command or a Structure — reduced to the facts the sight rules need.
/// </summary>
/// <remarks>
/// <para>
/// A flat snapshot, so no EF entity reaches the rule layer: the live map read and the witness service both build these from the database and hand them
/// to <see cref="FieldOfView"/>, which is how the two cannot disagree about what an Observer sees.
/// </para>
/// <para>
/// Every Unit and every Structure is its own Observer, even when several share a tile. A union of ranges radiating from one tile is arithmetically the
/// largest of them, so the Command and Structure sight rules in <c>docs/design/campaign-map/vision.md</c> hold without being special-cased.
/// </para>
/// </remarks>
internal sealed record SightObserver
{
    /// <summary>
    /// The id of the Unit, Command or Structure, read together with <see cref="RecordType"/>.
    /// </summary>
    public required int Id { get; init; }

    public required RecordType RecordType { get; init; }

    /// <summary>
    /// Who owns the Observer, and so which coalition its sightings belong to.
    /// </summary>
    public required int PlayerId { get; init; }

    public required Coord Location { get; init; }

    /// <summary>
    /// The Observer's own Sight in whole tiles, <b>before</b> the ground it stands on is taken into account.
    /// </summary>
    /// <remarks>
    /// Negative means it is not an Observer at all and sees nothing, not even its own tile. The terrain adjustment is applied by
    /// <see cref="FieldOfView.ResolveSightRange"/>, once the occupied tile has been read.
    /// </remarks>
    public required int BaseSightRange { get; init; }

    /// <summary>
    /// Which terrain the Observer could cross, used only when dense terrain has blinded it.
    /// </summary>
    /// <remarks>
    /// A Structure cannot move and has no domain of its own; it is judged as <see cref="DomainType.Land"/>, so a settlement in a coastal Jungle still
    /// uncovers the sea beside it.
    /// </remarks>
    public DomainType MovementDomain { get; init; } = DomainType.Land;

    /// <summary>
    /// How well the Observer picks out other Forces, fixed-point at Scale 1,000.
    /// </summary>
    /// <remarks>
    /// <c>CompiledUnitTemplate.TotalDetection</c> for a Unit. A Structure has no Detection stat yet and carries
    /// <see cref="SightingFidelityRules.STRUCTURE_DETECTION"/>.
    /// </remarks>
    public required long Detection { get; init; }

    /// <summary>
    /// The unwounded soldiers in the Observer, or null where that is not known — a Structure, or a Unit reconstructed after it was destroyed.
    /// </summary>
    public int? HealthyTroops { get; init; }

    /// <summary>
    /// The template a Unit was built from, or null for a Structure.
    /// </summary>
    public int? UnitTemplateId { get; init; }

    /// <summary>
    /// The sequence of the current Turn at which the Observer was destroyed, or null while it still stands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// During one Player's Turn every other Observer is frozen in place, but the acting Player can destroy them. An Observer that saw something at
    /// sequence 3 and was destroyed at sequence 9 still saw it, so it counts as a witness for every sequence <b>before</b> this one and for none from it
    /// onwards. See "Observers destroyed mid-turn" in <c>docs/design/intelligence-and-sightings.md</c>.
    /// </para>
    /// <para>
    /// Set for Units from their <c>MovementHistory</c> <c>Destroyed</c> row, and for Structures from the first <c>Captured</c> or <c>Razed</c> row of their <c>StructureHistory</c>. A capture
    /// counts because it takes the Structure away from the Player whose Observer it was, and such a Structure is rebuilt as that earlier owner's.
    /// </para>
    /// </remarks>
    public int? DestroyedAtSequence { get; init; }
}
