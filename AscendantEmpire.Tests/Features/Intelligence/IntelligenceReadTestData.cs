using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// A small in-memory Game for the intelligence read handlers: three Players, two Rounds of Turns, and builders for reports and known Forces.
/// </summary>
/// <remarks>
/// <para>
/// The Turns are the shape the feed window has to get right. Round 1: the acting Player's Turn 11, then the reader's Turn 12, both ended. Round 2: the acting Player's Turn 13, ended, then the
/// reader's Turn 14, still open. The other Player has never taken a Turn at all.
/// </para>
/// <para>
/// The map is 10 x 8 with its world-map ids starting at 100, so the handlers' placement of tile ids is actually exercised.
/// </para>
/// </remarks>
internal static class IntelligenceReadTestData
{
    internal const int GAME_ID = 1;
    internal const int ACTING_PLAYER_ID = 1;
    internal const int READER_PLAYER_ID = 2;
    internal const int OTHER_PLAYER_ID = 3;

    /// <summary>A Player who belongs to a different Game.</summary>
    internal const int OUTSIDER_PLAYER_ID = 4;

    internal const int ROUND_ONE_ACTING_TURN_ID = 11;
    internal const int ROUND_ONE_READER_TURN_ID = 12;
    internal const int ROUND_TWO_ACTING_TURN_ID = 13;
    internal const int ROUND_TWO_READER_TURN_ID = 14;

    internal static readonly CampaignMapGeometry GEOMETRY = new(CampaignMapOffsetId: 100, Width: 10, Height: 8, WrapsHorizontally: false, WrapsVertically: false);

    internal static int TileId(int x, int y)
    {
        return GEOMETRY.GetCampaignMapId(new Coord(x, y));
    }

    /// <summary>Seeds the Game, its Players, its campaign lane and its four Turns.</summary>
    internal static async Task SeedGameAsync(IDbContextFactory<AscendantEmpireContext> dbFactory)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync();

        context.GameIndex.Add(new GameIndex
        {
            Id = GAME_ID,
            GameName = "Watched Game",
            NumberOfPlayers = 3,
            CreationStatus = GameCreationStatus.SetupPending,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = 30,
            CampaignMapOffsetId = GEOMETRY.CampaignMapOffsetId,
            MapWidth = GEOMETRY.Width,
            MapHeight = GEOMETRY.Height
        });

        context.Players.AddRange(
            NewPlayer(ACTING_PLAYER_ID, GAME_ID),
            NewPlayer(READER_PLAYER_ID, GAME_ID),
            NewPlayer(OTHER_PLAYER_ID, GAME_ID),
            NewPlayer(OUTSIDER_PLAYER_ID, GAME_ID + 1));

        var campaignQueue = new TurnQueue
        {
            GameId = GAME_ID,
            QueueType = TurnQueueType.Campaign,
            CurrentRoundNumber = 2,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.TurnQueues.Add(campaignQueue);
        await context.SaveChangesAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;

        context.PlayerTurns.AddRange(
            NewTurn(ROUND_ONE_ACTING_TURN_ID, campaignQueue.Id, ACTING_PLAYER_ID, roundNumber: 1, endedAt: now),
            NewTurn(ROUND_ONE_READER_TURN_ID, campaignQueue.Id, READER_PLAYER_ID, roundNumber: 1, endedAt: now),
            NewTurn(ROUND_TWO_ACTING_TURN_ID, campaignQueue.Id, ACTING_PLAYER_ID, roundNumber: 2, endedAt: now),
            NewTurn(ROUND_TWO_READER_TURN_ID, campaignQueue.Id, READER_PLAYER_ID, roundNumber: 2, endedAt: null));

        await context.SaveChangesAsync();
    }

    /// <summary>Saves rows into the per-test store.</summary>
    internal static async Task AddAsync(IDbContextFactory<AscendantEmpireContext> dbFactory, params object[] rows)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync();

        context.AddRange(rows);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// A Force report about the acting Player's Force 99 — a template-3 Force of 45 soldiers — seen along the given path.
    /// </summary>
    internal static PlayerReport Report(long id, int recipientPlayerId, int raisedDuringTurnId, SightingFidelity fidelity, params (int X, int Y)[] path)
    {
        return new PlayerReport
        {
            Id = id,
            GameId = GAME_ID,
            RecipientPlayerId = recipientPlayerId,
            RaisedDuringPlayerTurnId = raisedDuringTurnId,
            ReportType = PlayerReportType.ForceSighted,
            SubjectUnitInstanceId = 99,
            SubjectOwnerPlayerId = ACTING_PLAYER_ID,
            SubjectUnitTemplateId = 3,
            SubjectHealthyTroops = 45,
            Fidelity = fidelity,
            FirstCampaignMapTileId = TileId(path[0].X, path[0].Y),
            LastCampaignMapTileId = TileId(path[^1].X, path[^1].Y),
            FirstTurnSequence = 1,
            LastTurnSequence = path.Length,
            ExitDirection = MoveDirection.East,
            FirstWitnessId = 50,
            FirstWitnessRecordType = RecordType.Unit,
            FirstWitnessOwnerPlayerId = recipientPlayerId,
            PathTiles = [.. path.Select((tile, ordinal) => new PlayerReportPathTile { Ordinal = ordinal, CampaignMapTileId = TileId(tile.X, tile.Y) })]
        };
    }

    /// <summary>A memory of the acting Player's template-3 Force of 45 soldiers.</summary>
    internal static PlayerKnownForce KnownForce(int id, int playerId, int unitInstanceId, int lastSeenTurnId, (int X, int Y) lastSeenTile, SightingFidelity fidelity)
    {
        return new PlayerKnownForce
        {
            Id = id,
            GameId = GAME_ID,
            PlayerId = playerId,
            UnitInstanceId = unitInstanceId,
            OwnerPlayerId = ACTING_PLAYER_ID,
            UnitTemplateId = 3,
            HealthyTroops = 45,
            Status = KnownForceStatus.Sighted,
            Fidelity = fidelity,
            LastSeenCampaignMapTileId = TileId(lastSeenTile.X, lastSeenTile.Y),
            LastSeenPlayerTurnId = lastSeenTurnId,
            LastSeenTurnSequence = 4,
            ExitDirection = MoveDirection.West,
            StillVisible = false
        };
    }

    /// <summary>
    /// A Structure report about the reader's Structure 60 - a kind-7 Structure, 80% built - seen captured by the acting Player and then razed, on one tile.
    /// </summary>
    internal static PlayerReport StructureReport(long id, int recipientPlayerId, int raisedDuringTurnId, SightingFidelity fidelity, (int X, int Y) tile)
    {
        return new PlayerReport
        {
            Id = id,
            GameId = GAME_ID,
            RecipientPlayerId = recipientPlayerId,
            RaisedDuringPlayerTurnId = raisedDuringTurnId,
            ReportType = PlayerReportType.StructureSighted,
            SubjectStructureInstanceId = 60,
            SubjectStructureId = 7,
            SubjectOwnerPlayerId = ACTING_PLAYER_ID,
            SubjectPreviousOwnerPlayerId = READER_PLAYER_ID,
            SubjectCompletionPercent = 80,
            Fidelity = fidelity,
            FirstCampaignMapTileId = TileId(tile.X, tile.Y),
            LastCampaignMapTileId = TileId(tile.X, tile.Y),
            FirstTurnSequence = 2,
            LastTurnSequence = 5,
            ExitDirection = null,
            StillVisible = false,
            AppearedInView = false,
            DestroyedInView = true,
            CompletedInView = false,
            CapturedInView = true,
            FirstWitnessId = 50,
            FirstWitnessRecordType = RecordType.Unit,
            FirstWitnessOwnerPlayerId = recipientPlayerId
        };
    }

    /// <summary>
    /// A combat report about Battle 70 - fought to a finish, with the acting Player left holding the ground at a cost of 120 troops - on one tile.
    /// </summary>
    internal static PlayerReport CombatReport(long id, int recipientPlayerId, int raisedDuringTurnId, SightingFidelity fidelity, (int X, int Y) tile)
    {
        return new PlayerReport
        {
            Id = id,
            GameId = GAME_ID,
            RecipientPlayerId = recipientPlayerId,
            RaisedDuringPlayerTurnId = raisedDuringTurnId,
            ReportType = PlayerReportType.CombatSighted,
            SubjectBattleId = 70,
            SubjectGroundHeldByPlayerId = ACTING_PLAYER_ID,
            SubjectTroopsLost = 120,
            Fidelity = fidelity,
            FirstCampaignMapTileId = TileId(tile.X, tile.Y),
            LastCampaignMapTileId = TileId(tile.X, tile.Y),
            FirstTurnSequence = 3,
            LastTurnSequence = 9,
            ExitDirection = null,
            StillVisible = false,
            AppearedInView = true,
            DestroyedInView = true,
            SideJoinedInView = false,
            SideLeftInView = true,
            FirstWitnessId = 50,
            FirstWitnessRecordType = RecordType.Unit,
            FirstWitnessOwnerPlayerId = recipientPlayerId
        };
    }

    /// <summary>A memory of the acting Player's kind-7 Structure, 80% built.</summary>
    internal static PlayerKnownStructure KnownStructure(
        int id,
        int playerId,
        int structureInstanceId,
        int lastSeenTurnId,
        (int X, int Y) tile,
        SightingFidelity fidelity,
        KnownStructureStatus status = KnownStructureStatus.Standing)
    {
        return new PlayerKnownStructure
        {
            Id = id,
            GameId = GAME_ID,
            PlayerId = playerId,
            StructureInstanceId = structureInstanceId,
            StructureId = 7,
            OwnerPlayerId = ACTING_PLAYER_ID,
            ObservedCompletionPercent = 80,
            Status = status,
            Fidelity = fidelity,
            CampaignMapTileId = TileId(tile.X, tile.Y),
            LastSeenPlayerTurnId = lastSeenTurnId,
            LastSeenTurnSequence = 4,
            StillVisible = false
        };
    }

    private static Player NewPlayer(int id, int gameId)
    {
        return new Player
        {
            Id = id,
            GameId = gameId,
            RulerTitle = "King",
            RulerName = $"Player {id}",
            FactionName = $"Faction {id}",
            PeopleName = "People",
            Demonym = "Person",
            Language = "Language",
            RulerPortraitPath = "/portraits/01_western_king.png",
            Status = PlayerStatus.Active
        };
    }

    private static PlayerTurn NewTurn(int id, int turnQueueId, int playerId, int roundNumber, DateTimeOffset? endedAt)
    {
        return new PlayerTurn
        {
            Id = id,
            TurnQueueId = turnQueueId,
            PlayerId = playerId,
            RoundNumber = roundNumber,
            StartedAt = DateTimeOffset.UtcNow,
            EndedAt = endedAt
        };
    }
}
