using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// A Player's memory of a Force they have watched: where it was last seen, and in what state.
/// </summary>
/// <remarks>
/// <para>
/// The sibling of <see cref="PlayerKnownStructure"/>, and refreshed by the same end-of-turn sightings step that raises <see cref="PlayerReport"/> rows. A report is read once; this persists, and is
/// what the client draws as a last-known-position marker that ages visibly. See "Known forces and known structures" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// <list type="bullet">
///   <item><description><b>Never deleted by play</b>, only superseded or restatused. You cannot unknow a position, and a Force you watched die is knowledge.</description></item>
///   <item><description><b>No server-side expiry.</b> The client ages the marker from the Round of <see cref="LastSeenPlayerTurnId"/>.</description></item>
///   <item><description><b>Stores the truth</b>, and is subject to the same fidelity projection as reports, or the map layer would become the leak the report layer prevents.</description></item>
/// </list>
/// </para>
/// </remarks>
public class PlayerKnownForce
{
    public int Id { get; set; }

    public int GameId { get; set; }

    /// <summary>
    /// The Player who knows about the Force.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// The Force known about.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> a foreign key, for the same reason as <c>MovementHistory.UnitInstanceId</c>: a destroyed Force is hard-deleted, and this row has to outlive it to say so.
    /// </remarks>
    public int UnitInstanceId { get; set; }

    /// <summary>
    /// Whose Force it is, as last observed.
    /// </summary>
    public int OwnerPlayerId { get; set; }

    public int UnitTemplateId { get; set; }

    /// <summary>
    /// The Force's unwounded soldiers when it was last seen, or null when it no longer existed by the time the sightings step ran.
    /// </summary>
    public int? HealthyTroops { get; set; }

    public KnownForceStatus Status { get; set; }

    /// <summary>
    /// How well the last sighting made the Force out, which is what the read projection discloses up to.
    /// </summary>
    public SightingFidelity Fidelity { get; set; }

    public int LastSeenCampaignMapTileId { get; set; }

    /// <summary>
    /// The Turn of the last sighting. With <see cref="LastSeenTurnSequence"/>, what decides whether a new sighting supersedes this one.
    /// </summary>
    public int LastSeenPlayerTurnId { get; set; }

    public int LastSeenTurnSequence { get; set; }

    /// <summary>
    /// The direction the Force was last seen leaving in, or null when it was still in view or destroyed.
    /// </summary>
    public MoveDirection? ExitDirection { get; set; }

    /// <summary>
    /// Whether the Force was still in view when the Turn of the last sighting ended.
    /// </summary>
    public bool StillVisible { get; set; }
}
