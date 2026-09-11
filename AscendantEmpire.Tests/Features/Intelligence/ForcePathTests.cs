using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Pure unit tests for <see cref="ForcePath"/> — no database, because a path is built from history rows it is handed.
/// </summary>
/// <remarks>
/// The map is 10 x 8 with its world-map ids starting at 100, so a tile id and its coordinate are told apart.
/// </remarks>
[TestFixture]
internal class ForcePathTests
{
    private const int _UNIT_INSTANCE_ID = 7;
    private const int _PLAYER_ID = 1;
    private const int _UNIT_TEMPLATE_ID = 3;
    private const int _PLAYER_TURN_ID = 40;

    private static readonly CampaignMapGeometry _GEOMETRY = new(CampaignMapOffsetId: 100, Width: 10, Height: 8, WrapsHorizontally: false, WrapsVertically: false);

    private static int TileId(int x, int y)
    {
        return _GEOMETRY.GetCampaignMapId(new Coord(x, y));
    }

    private static MovementHistory Created(long id, int x, int y, int sequence, int unitInstanceId = _UNIT_INSTANCE_ID)
    {
        return Row(id, ForceHistoryEventType.Created, fromTileId: null, TileId(x, y), sequence, unitInstanceId);
    }

    private static MovementHistory Moved(long id, (int X, int Y) from, (int X, int Y) to, int sequence, int unitInstanceId = _UNIT_INSTANCE_ID)
    {
        return Row(id, ForceHistoryEventType.Moved, TileId(from.X, from.Y), TileId(to.X, to.Y), sequence, unitInstanceId);
    }

    private static MovementHistory Row(long id, ForceHistoryEventType eventType, int? fromTileId, int tileId, int sequence, int unitInstanceId)
    {
        return new MovementHistory
        {
            Id = id,
            GameId = 1,
            UnitInstanceId = unitInstanceId,
            PlayerId = _PLAYER_ID,
            UnitTemplateId = _UNIT_TEMPLATE_ID,
            EventType = eventType,
            FromCampaignMapTileId = fromTileId,
            CampaignMapTileId = tileId,
            PlayerTurnId = _PLAYER_TURN_ID,
            TurnSequence = sequence
        };
    }

    [Test, Description("A Force whose Turn begins with a move was standing somewhere first, and that ground is a step it could have been seen on, before anything in the Turn happened.")]
    public void ShouldAddAnOriginStepWhenTheTurnBeginsWithAMove()
    {
        ForcePath path = ForcePath.Build([Moved(11, from: (2, 2), to: (3, 2), sequence: 4)], _GEOMETRY);

        ForceStep origin = path.Steps[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(path.Steps.Select(step => step.Kind), Is.EqualTo(new[] { ForceStepKind.Origin, ForceStepKind.Moved }));
            Assert.That(origin.CampaignMapTileId, Is.EqualTo(TileId(2, 2)));
            Assert.That(origin.Location, Is.EqualTo(new Coord(2, 2)), "The origin is placed from its tile id with the Game's geometry.");
            Assert.That(origin.TurnSequence, Is.Zero);
            Assert.That(origin.ProbeKey, Is.EqualTo(-11), "The negated id of the move, so it can never collide with a real row's key.");
            Assert.That(path.Steps[1].ProbeKey, Is.EqualTo(11));
        }
    }

    [Test, Description("A Force created this Turn stood nowhere before it, so its path starts with its creation and has no origin step.")]
    public void ShouldNotAddAnOriginStepWhenTheForceWasCreatedThisTurn()
    {
        ForcePath path = ForcePath.Build([Created(11, 2, 2, sequence: 1), Moved(12, from: (2, 2), to: (3, 2), sequence: 2)], _GEOMETRY);

        Assert.That(path.Steps.Select(step => step.Kind), Is.EqualTo(new[] { ForceStepKind.Created, ForceStepKind.Moved }));
    }

    [Test, Description("Steps follow the Turn's sequence, whatever order the rows were read in, because exit directions and tracks are only meaningful in the order things happened.")]
    public void ShouldOrderStepsBySequenceWhateverOrderTheRowsArriveIn()
    {
        ForcePath path = ForcePath.Build(
            [
                Moved(13, from: (4, 2), to: (5, 2), sequence: 9),
                Moved(11, from: (2, 2), to: (3, 2), sequence: 1),
                Moved(12, from: (3, 2), to: (4, 2), sequence: 5)
            ],
            _GEOMETRY);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(path.Steps.Select(step => step.TurnSequence), Is.EqualTo(new[] { 0, 1, 5, 9 }));
            Assert.That(path.Steps[0].ProbeKey, Is.EqualTo(-11), "The origin comes from the first move in sequence, not the first row read.");
        }
    }

    [Test, Description("A Turn's rows are split into one path per Force, and every step across all of them has its own probe key, which one witness query needs.")]
    public void ShouldBuildOnePathPerForceInForceIdOrder()
    {
        IReadOnlyList<ForcePath> paths = ForcePath.BuildAll(
            [
                Moved(21, from: (6, 6), to: (7, 6), sequence: 2, unitInstanceId: 9),
                Moved(11, from: (2, 2), to: (3, 2), sequence: 1, unitInstanceId: 5),
                Moved(22, from: (7, 6), to: (8, 6), sequence: 3, unitInstanceId: 9)
            ],
            _GEOMETRY);

        long[] probeKeys = [.. paths.SelectMany(path => path.Steps).Select(step => step.ProbeKey)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths.Select(path => path.UnitInstanceId), Is.EqualTo(new[] { 5, 9 }));
            Assert.That(paths.Select(path => path.Steps.Count), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(probeKeys, Is.Unique);
        }
    }

    [Test, Description("A path is one Force's story, so rows from two Forces are a caller bug and refused rather than stitched into a path nobody took.")]
    public void ShouldRefuseRowsBelongingToMoreThanOneForce()
    {
        Assert.That(
            () => ForcePath.Build([Created(11, 2, 2, sequence: 1, unitInstanceId: 5), Created(12, 3, 3, sequence: 1, unitInstanceId: 6)], _GEOMETRY),
            Throws.ArgumentException);
    }
}
