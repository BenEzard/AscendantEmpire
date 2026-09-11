using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Pure unit tests for <see cref="ForceTrackFolder"/> — no database, because it folds a path and witness answers it is handed.
/// </summary>
/// <remarks>
/// <para>
/// The witness answers are written out by hand rather than computed, so each test says exactly which steps a coalition saw. What makes an Observer a witness is pinned separately, in
/// <c>WitnessResolutionTests</c>.
/// </para>
/// <para>
/// The map is 10 x 8 with ids starting at 0. Coalition 2 is the watching Player; coalition 3 a second one where a test needs it.
/// </para>
/// </remarks>
[TestFixture]
internal class ForceTrackFolderTests
{
    private const int _WATCHING_COALITION_ID = 2;
    private const int _OTHER_COALITION_ID = 3;

    private static readonly CampaignMapGeometry _GEOMETRY = new(CampaignMapOffsetId: 0, Width: 10, Height: 8, WrapsHorizontally: false, WrapsVertically: false);

    private static int TileId(int x, int y)
    {
        return _GEOMETRY.GetCampaignMapId(new Coord(x, y));
    }

    private static ForceStep Step(long probeKey, ForceStepKind kind, int x, int y, int sequence)
    {
        return new ForceStep { ProbeKey = probeKey, Kind = kind, CampaignMapTileId = TileId(x, y), Location = new Coord(x, y), TurnSequence = sequence };
    }

    private static ForcePath PathOf(params ForceStep[] steps)
    {
        return new ForcePath { UnitInstanceId = 99, OwnerPlayerId = 1, UnitTemplateId = 3, Steps = steps };
    }

    private static Witness WitnessAt(int observerId, int distance, SightingFidelity fidelity = SightingFidelity.Detail, int ownerPlayerId = _WATCHING_COALITION_ID)
    {
        return new Witness { ObserverId = observerId, RecordType = RecordType.Unit, OwnerPlayerId = ownerPlayerId, Location = new Coord(0, 0), Distance = distance, Fidelity = fidelity };
    }

    private static WitnessingCoalition Coalition(int coalitionId, params Witness[] witnessesNearestFirst)
    {
        return new WitnessingCoalition { CoalitionId = coalitionId, MemberPlayerIds = [coalitionId], Witnesses = witnessesNearestFirst };
    }

    /// <summary>
    /// Answers for a path where the watching coalition saw exactly the given steps, by index, through one Observer, and nobody saw the rest.
    /// </summary>
    private static Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> SeenAtSteps(ForcePath path, params int[] seenStepIndices)
    {
        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = [];

        for (int stepIndex = 0; stepIndex < path.Steps.Count; stepIndex++)
        {
            answers[path.Steps[stepIndex].ProbeKey] = seenStepIndices.Contains(stepIndex) ? [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 2))] : [];
        }

        return answers;
    }

    private static ForceTrack SingleTrack(IReadOnlyList<CoalitionForceTracks> result)
    {
        return result.Single().Tracks.Single();
    }

    [Test, Description("A Force seen standing on one tile and then walking out of view is 'seen at (X, Y), heading east': the step that took it away is a real observation.")]
    public void ShouldReportASingleTileWithItsExitDirectionWhenTheForceWalksOutOfView()
    {
        ForcePath path = PathOf(Step(-1, ForceStepKind.Origin, 4, 4, 0), Step(1, ForceStepKind.Moved, 5, 4, 3));

        ForceTrack track = SingleTrack(ForceTrackFolder.Fold(path, SeenAtSteps(path, 0), _GEOMETRY));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(track.PathTileIds, Is.EqualTo(new[] { TileId(4, 4) }));
            Assert.That(track.ExitDirection, Is.EqualTo(MoveDirection.East));
            Assert.That(track.StillVisible, Is.False);
            Assert.That(track.FirstTurnSequence, Is.Zero, "It was picked up where it stood as the Turn opened.");
        }
    }

    [Test, Description("A Force still in view when the Turn ends has not left, so it has no heading and is 'seen at (X, Y)'.")]
    public void ShouldReportNoExitDirectionWhenTheForceIsStillInViewAtTheEnd()
    {
        ForcePath path = PathOf(Step(-1, ForceStepKind.Origin, 3, 4, 0), Step(1, ForceStepKind.Moved, 4, 4, 6));

        ForceTrack track = SingleTrack(ForceTrackFolder.Fold(path, SeenAtSteps(path, 1), _GEOMETRY));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(track.PathTileIds, Is.EqualTo(new[] { TileId(4, 4) }));
            Assert.That(track.ExitDirection, Is.Null);
            Assert.That(track.StillVisible, Is.True);
            Assert.That(track.FirstTurnSequence, Is.EqualTo(6));
            Assert.That(track.LastTurnSequence, Is.EqualTo(6));
        }
    }

    [Test, Description("Several tiles seen in a row are one track 'passing through' them in order, heading the way the unseen step after them went.")]
    public void ShouldReportThePathPassedThroughWhenSeveralTilesAreSeen()
    {
        ForcePath path = PathOf(
            Step(-1, ForceStepKind.Origin, 2, 4, 0),
            Step(1, ForceStepKind.Moved, 3, 4, 1),
            Step(2, ForceStepKind.Moved, 4, 4, 2),
            Step(3, ForceStepKind.Moved, 5, 5, 3));

        ForceTrack track = SingleTrack(ForceTrackFolder.Fold(path, SeenAtSteps(path, 1, 2), _GEOMETRY));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(track.PathTileIds, Is.EqualTo(new[] { TileId(3, 4), TileId(4, 4) }));
            Assert.That(track.ExitDirection, Is.EqualTo(MoveDirection.SouthEast));
            Assert.That(track.StillVisible, Is.False);
        }
    }

    [Test, Description("A Force that leaves view and comes back is two tracks, because the watcher cannot know what it did while out of sight.")]
    public void ShouldSplitIntoTwoTracksWhenTheForceLeavesViewAndReturns()
    {
        ForcePath path = PathOf(
            Step(1, ForceStepKind.Created, 4, 4, 1),
            Step(2, ForceStepKind.Moved, 5, 4, 2),
            Step(3, ForceStepKind.Moved, 6, 4, 3));

        IReadOnlyList<ForceTrack> tracks = ForceTrackFolder.Fold(path, SeenAtSteps(path, 0, 2), _GEOMETRY).Single().Tracks;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tracks.Select(track => track.TrackOrdinal), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(tracks[0].PathTileIds, Is.EqualTo(new[] { TileId(4, 4) }));
            Assert.That(tracks[0].ExitDirection, Is.EqualTo(MoveDirection.East));
            Assert.That(tracks[1].PathTileIds, Is.EqualTo(new[] { TileId(6, 4) }));
            Assert.That(tracks[1].StillVisible, Is.True);
        }
    }

    [Test, Description("Unbroken sight of consecutive steps is one track even when their sequence numbers are far apart, because every Force in the Turn draws from the same counter.")]
    public void ShouldTreatConsecutiveStepsAsOneRunWhateverTheirSequenceNumbers()
    {
        ForcePath path = PathOf(
            Step(1, ForceStepKind.Created, 4, 4, 3),
            Step(2, ForceStepKind.Moved, 5, 4, 9),
            Step(3, ForceStepKind.Moved, 6, 4, 15));

        ForceTrack track = SingleTrack(ForceTrackFolder.Fold(path, SeenAtSteps(path, 0, 1, 2), _GEOMETRY));

        Assert.That(track.PathTileIds, Is.EqualTo(new[] { TileId(4, 4), TileId(5, 4), TileId(6, 4) }));
    }

    [Test, Description("A scout circling a watchtower cannot flood the feed: past the cap the first tracks and the last are kept, so where it was last seen always survives.")]
    public void ShouldKeepTheFirstTracksAndTheLastWhenTheCapIsExceeded()
    {
        // In and out of view along a row: seen at steps 0, 2, 4, 6 and 8, which is five separate tracks.
        ForcePath path = PathOf([.. Enumerable.Range(0, 9).Select(stepIndex => Step(stepIndex + 1, stepIndex == 0 ? ForceStepKind.Created : ForceStepKind.Moved, stepIndex, 1, stepIndex + 1))]);

        IReadOnlyList<ForceTrack> tracks = ForceTrackFolder.Fold(path, SeenAtSteps(path, 0, 2, 4, 6, 8), _GEOMETRY).Single().Tracks;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tracks, Has.Count.EqualTo(ForceTrackFolder.MAX_TRACKS_PER_FORCE));
            Assert.That(tracks.Select(track => track.FirstCampaignMapTileId), Is.EqualTo(new[] { TileId(0, 1), TileId(2, 1), TileId(8, 1) }), "The first two and the very last.");
            Assert.That(tracks.Select(track => track.TrackOrdinal), Is.EqualTo(new[] { 0, 1, 2 }), "Numbered after the cap, with no gap where the dropped tracks were.");
            Assert.That(tracks[^1].StillVisible, Is.True);
        }
    }

    [Test, Description("A Force created and destroyed on one tile in view is one sighting of that tile, with both events recorded on it.")]
    public void ShouldReportTheForceAppearingAndBeingDestroyedInView()
    {
        ForcePath path = PathOf(Step(1, ForceStepKind.Created, 4, 4, 1), Step(2, ForceStepKind.Destroyed, 4, 4, 5));

        ForceTrack track = SingleTrack(ForceTrackFolder.Fold(path, SeenAtSteps(path, 0, 1), _GEOMETRY));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(track.PathTileIds, Is.EqualTo(new[] { TileId(4, 4) }), "The repeated tile is collapsed.");
            Assert.That(track.AppearedInView, Is.True);
            Assert.That(track.DestroyedInView, Is.True);
            Assert.That(track.StillVisible, Is.False, "A destroyed Force is not still in view.");
            Assert.That(track.ExitDirection, Is.Null);
        }
    }

    [Test, Description("A Force destroyed just out of sight did not go anywhere the watcher saw, so it has no heading and is not reported destroyed.")]
    public void ShouldGiveNoExitDirectionWhenTheForceIsDestroyedJustOutOfView()
    {
        ForcePath path = PathOf(Step(1, ForceStepKind.Created, 4, 4, 1), Step(2, ForceStepKind.Destroyed, 4, 4, 5));

        ForceTrack track = SingleTrack(ForceTrackFolder.Fold(path, SeenAtSteps(path, 0), _GEOMETRY));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(track.ExitDirection, Is.Null);
            Assert.That(track.DestroyedInView, Is.False);
            Assert.That(track.StillVisible, Is.False);
        }
    }

    [Test, Description("Several Observers of one coalition watching the same step fold into one track, naming the nearest as the one that picked the Force up.")]
    public void ShouldFoldEveryObserverOfACoalitionIntoOneTrackNamingTheNearest()
    {
        ForcePath path = PathOf(Step(1, ForceStepKind.Created, 4, 4, 1));

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = new()
        {
            [1] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 1), WitnessAt(observerId: 11, distance: 3))]
        };

        ForceTrack track = SingleTrack(ForceTrackFolder.Fold(path, answers, _GEOMETRY));

        Assert.That(track.FirstWitness.ObserverId, Is.EqualTo(10));
    }

    [Test, Description("Two coalitions that each saw the Force each get their own tracks, in coalition order, because each is told separately.")]
    public void ShouldGiveEachWatchingCoalitionItsOwnTracks()
    {
        ForcePath path = PathOf(Step(1, ForceStepKind.Created, 4, 4, 1));

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = new()
        {
            [1] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(10, 1)), Coalition(_OTHER_COALITION_ID, WitnessAt(20, 2, ownerPlayerId: _OTHER_COALITION_ID))]
        };

        IReadOnlyList<CoalitionForceTracks> result = ForceTrackFolder.Fold(path, answers, _GEOMETRY);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Select(coalition => coalition.CoalitionId), Is.EqualTo(new[] { _WATCHING_COALITION_ID, _OTHER_COALITION_ID }));
            Assert.That(result[1].LatestTrack.FirstWitness.ObserverId, Is.EqualTo(20));
        }
    }

    [Test, Description("A track discloses the most any step of it earned, because a Force made out clearly at one step is not forgotten at the next.")]
    public void ShouldTakeTheBestFidelityAnyStepOfTheRunEarned()
    {
        ForcePath path = PathOf(Step(1, ForceStepKind.Created, 4, 4, 1), Step(2, ForceStepKind.Moved, 5, 4, 2));

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = new()
        {
            [1] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(10, 3, SightingFidelity.Movement))],
            [2] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(10, 2, SightingFidelity.Owner))]
        };

        ForceTrack track = SingleTrack(ForceTrackFolder.Fold(path, answers, _GEOMETRY));

        Assert.That(track.Fidelity, Is.EqualTo(SightingFidelity.Owner));
    }

    [Test, Description("A Force walking off the east edge of a wrapping map is heading east, not nine tiles west.")]
    public void ShouldNameAnExitAcrossAWrappedEdge()
    {
        var wrappingGeometry = new CampaignMapGeometry(CampaignMapOffsetId: 0, Width: 10, Height: 8, WrapsHorizontally: true, WrapsVertically: false);

        ForcePath path = PathOf(Step(-1, ForceStepKind.Origin, 9, 4, 0), Step(1, ForceStepKind.Moved, 0, 4, 1));

        ForceTrack track = SingleTrack(ForceTrackFolder.Fold(path, SeenAtSteps(path, 0), wrappingGeometry));

        Assert.That(track.ExitDirection, Is.EqualTo(MoveDirection.East));
    }

    [Test, Description("A Force nobody saw produces no tracks at all, which is what keeps unwatched Turns out of everyone's feed.")]
    public void ShouldReturnNothingWhenNobodySawTheForce()
    {
        ForcePath path = PathOf(Step(1, ForceStepKind.Created, 4, 4, 1), Step(2, ForceStepKind.Moved, 5, 4, 2));

        Assert.That(ForceTrackFolder.Fold(path, SeenAtSteps(path), _GEOMETRY), Is.Empty);
    }

    [Test, Description("The witness service answers every probe, so a step with no answer means the query was built wrongly, and is refused rather than read as unseen.")]
    public void ShouldThrowWhenAStepHasNoWitnessAnswer()
    {
        ForcePath path = PathOf(Step(1, ForceStepKind.Created, 4, 4, 1));

        Assert.That(() => ForceTrackFolder.Fold(path, new Dictionary<long, IReadOnlyCollection<WitnessingCoalition>>(), _GEOMETRY), Throws.ArgumentException);
    }
}
