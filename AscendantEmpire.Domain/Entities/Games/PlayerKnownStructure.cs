using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// A Player's memory of a Structure they have watched: who held it, how far built it was, and whether it still stood, as last seen.
/// </summary>
/// <remarks>
/// <para>
/// The sibling of <see cref="PlayerKnownForce"/>, refreshed by the end-of-turn sightings step from the Structure events a Player's coalition witnessed. A report is read once; this persists, and is
/// what the client draws as a last-known marker on ground the Player can no longer see. See "Known forces and known structures" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// <list type="bullet">
///   <item><description><b>Never deleted by play</b>, only superseded or restatused. A Structure you watched being razed is knowledge.</description></item>
///   <item><description><b>No server-side expiry.</b> The client ages the marker from the Round of <see cref="LastSeenPlayerTurnId"/>.</description></item>
///   <item><description><b>Stores the truth</b>, and is subject to the same fidelity projection as reports, or the map layer would become the leak the report layer prevents.</description></item>
///   <item><description><b>Only learns from witnessed events.</b> A Structure a Player has looked at live but never watched change has no row; remembering live sight is F-28's.</description></item>
/// </list>
/// </para>
/// </remarks>
public class PlayerKnownStructure
{
    public int Id { get; set; }

    public int GameId { get; set; }

    /// <summary>
    /// The Player who knows about the Structure.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// The Structure known about.
    /// </summary>
    /// <remarks>
    /// A real foreign key, unlike <see cref="PlayerKnownForce.UnitInstanceId"/>: a razed Structure is soft-deleted, so the row this points at outlives it.
    /// </remarks>
    public int StructureInstanceId { get; set; }

    /// <summary>
    /// What kind of Structure it is.
    /// </summary>
    public int StructureId { get; set; }

    /// <summary>
    /// Whose Structure it was, as last observed. A witnessed capture changes it.
    /// </summary>
    public int OwnerPlayerId { get; set; }

    /// <summary>
    /// How far built the Structure was when it was last seen, from 0 to 100.
    /// </summary>
    public double ObservedCompletionPercent { get; set; }

    public KnownStructureStatus Status { get; set; }

    /// <summary>
    /// How well the last sighting made the Structure out, which is what the read projection discloses up to.
    /// </summary>
    public SightingFidelity Fidelity { get; set; }

    /// <summary>
    /// The tile the Structure stands on.
    /// </summary>
    public int CampaignMapTileId { get; set; }

    /// <summary>
    /// The Turn of the last sighting. With <see cref="LastSeenTurnSequence"/>, what decides whether a new sighting supersedes this one.
    /// </summary>
    public int LastSeenPlayerTurnId { get; set; }

    public int LastSeenTurnSequence { get; set; }

    /// <summary>
    /// Whether the Structure was still standing and in view when the Turn of the last sighting ended.
    /// </summary>
    public bool StillVisible { get; set; }
}
