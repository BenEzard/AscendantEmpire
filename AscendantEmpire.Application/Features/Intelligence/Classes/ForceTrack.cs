using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <summary>
/// One maximal run of a Force's steps that a coalition could see without a break — the unit a report is raised for.
/// </summary>
/// <remarks>
/// <para>
/// One structure renders every sentence the feed needs: a single tile not still visible is "seen at (X, Y), heading north-east"; a single tile still visible is "seen at (X, Y)"; several tiles
/// are "seen passing through", with a heading when it left. See "Tracks" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// "Without a break" means consecutive steps <b>of this Force</b>, not consecutive Turn sequence numbers: every Force in a Turn draws from one counter, so a Force's own sequence numbers are
/// rarely consecutive even when it is watched the whole way.
/// </para>
/// </remarks>
internal sealed record ForceTrack
{
    /// <summary>
    /// The track's position among the Force's tracks for its coalition this Turn, from 0, counted after the track cap.
    /// </summary>
    public required int TrackOrdinal { get; init; }

    /// <summary>
    /// The tiles the Force was seen on, in order, with consecutive repeats collapsed. Never empty.
    /// </summary>
    /// <remarks>
    /// Collapsing is a presentation concern and happens only here: a Force destroyed on the tile it has just entered is on that tile once, not twice.
    /// </remarks>
    public required IReadOnlyList<int> PathTileIds { get; init; }

    public int FirstCampaignMapTileId => PathTileIds[0];

    public int LastCampaignMapTileId => PathTileIds[^1];

    /// <summary>
    /// The sequence of the first step seen, 0 when that was the Force's origin.
    /// </summary>
    public required int FirstTurnSequence { get; init; }

    public required int LastTurnSequence { get; init; }

    /// <summary>
    /// The direction of the step that took the Force out of view, or null when it never left — still visible at the end of the Turn, or destroyed.
    /// </summary>
    public required MoveDirection? ExitDirection { get; init; }

    /// <summary>
    /// Whether the run reaches the Force's last step and that step is not its destruction.
    /// </summary>
    public required bool StillVisible { get; init; }

    public required bool AppearedInView { get; init; }

    public required bool DestroyedInView { get; init; }

    /// <summary>
    /// The most any of the coalition's witnesses made out at any step of the run.
    /// </summary>
    public required SightingFidelity Fidelity { get; init; }

    /// <summary>
    /// The nearest witness at the first step of the run: the Observer that picked the Force up.
    /// </summary>
    public required Witness FirstWitness { get; init; }
}

/// <summary>
/// Every track one coalition saw of one Force during a Turn.
/// </summary>
internal sealed record CoalitionForceTracks
{
    public required int CoalitionId { get; init; }

    /// <summary>
    /// The Players the coalition's reports fan out to, one report each.
    /// </summary>
    public required IReadOnlyCollection<int> MemberPlayerIds { get; init; }

    /// <summary>
    /// The coalition's tracks of the Force, in the order they happened. Never empty, and never longer than <see cref="ForceTrackFolder.MAX_TRACKS_PER_FORCE"/>.
    /// </summary>
    public required IReadOnlyList<ForceTrack> Tracks { get; init; }

    /// <summary>
    /// The last track the coalition saw, which is what its known-Force memory is refreshed from.
    /// </summary>
    /// <remarks>
    /// The cap always keeps the last track, so this is the true latest sighting however many tracks were dropped.
    /// </remarks>
    public ForceTrack LatestTrack => Tracks[^1];
}
