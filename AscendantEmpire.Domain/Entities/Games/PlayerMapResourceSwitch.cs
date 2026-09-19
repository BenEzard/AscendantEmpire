using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Modifiers;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// One source turning something about a MapResourceType on or off for one Player: discovering it, getting one Resource from its yield, or surveying one of its layers.
/// </summary>
/// <remarks>
/// <para>
/// The spec's "switched on and off per Player by game mechanics" (see <c>docs/design/campaign-map/map-resources.md</c>). For example, uranium is placed at map generation
/// but no Player can discover it until a tech switches discovery on; a Deer's pelt is off until hide-working switches it on.
/// </para>
/// <para>
/// Resolved per Player, and per (<see cref="Kind"/>, type, <see cref="ResourceId"/>, <see cref="LayerNumber"/>):
/// <list type="number">
///   <item><description>any row with <see cref="IsEnabled"/> <see langword="false"/> wins, so a mechanic that forbids something (a taboo, a treaty) overrides the tech that
///   allowed it;</description></item>
///   <item><description>otherwise any row with <see cref="IsEnabled"/> <see langword="true"/> turns it on;</description></item>
///   <item><description>otherwise the default applies: the type's <c>IsDiscoverableByDefault</c>, the yield's <c>IsEnabledByDefault</c>, or, for a layer, on for layer 1
///   and off for every deeper one.</description></item>
/// </list>
/// </para>
/// <para>
/// Like modifiers, the result is always derived from the rows the Player currently holds, so losing a source is just deleting its rows. No navigation properties, the same as
/// <see cref="PlayerSustenanceModifier"/>.
/// </para>
/// </remarks>
public class PlayerMapResourceSwitch
{
    /// <summary>
    /// Surrogate key.
    /// </summary>
    /// <remarks>
    /// The natural key includes the nullable <see cref="ResourceId"/> and <see cref="LayerNumber"/>, which cannot be part of a primary key. Uniqueness is enforced by the
    /// <c>UX_PlayerMapResourceSwitch_Player_Source_Kind_Type_Resource_Layer</c> index instead.
    /// </remarks>
    public int Id { get; set; }

    /// <summary>
    /// The Game the Player belongs to, carried so the game delete can purge these rows directly.
    /// </summary>
    public int GameId { get; set; }

    /// <summary>
    /// The Player the switch applies to.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// What kind of thing set this switch.
    /// </summary>
    public ModifierSourceType SourceType { get; set; }

    /// <summary>
    /// The particular Technology, Specialist or Department that set this switch, according to <see cref="SourceType"/>.
    /// </summary>
    /// <remarks>
    /// Not a foreign key: it points at a different table for each source type, and none of those tables exist yet.
    /// </remarks>
    public int SourceId { get; set; }

    /// <summary>
    /// What is switched.
    /// </summary>
    public MapResourceSwitchKind Kind { get; set; }

    /// <summary>
    /// The type switched (a <c>map.MapResourceType</c>).
    /// </summary>
    public int MapResourceTypeId { get; set; }

    /// <summary>
    /// The Resource in the type's yield that is switched, for <see cref="MapResourceSwitchKind.YieldResource"/>; otherwise <see langword="null"/>.
    /// </summary>
    public int? ResourceId { get; set; }

    /// <summary>
    /// The layer that is switched, for <see cref="MapResourceSwitchKind.SurveyLayer"/>; otherwise <see langword="null"/>.
    /// </summary>
    public int? LayerNumber { get; set; }

    /// <summary>
    /// Whether this source turns it on (<see langword="true"/>) or off (<see langword="false"/>). An off always wins over an on.
    /// </summary>
    public bool IsEnabled { get; set; }
}
