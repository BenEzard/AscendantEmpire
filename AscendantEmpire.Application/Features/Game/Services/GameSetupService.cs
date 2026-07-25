using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.MapGallery.Services;
using AscendantEmpire.Application.Features.Military;
using AscendantEmpire.BackendContracts.Turns.CreateCampaignTurnQueue;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Military;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

using DomainPlayer = AscendantEmpire.Domain.Entities.Games.Player;

namespace AscendantEmpire.Application.Features.Game.Services;

internal class GameSetupService(
    ILogger<GameSetupService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ICampaignMapGalleryService campaignMapGalleryService
) : IGameSetupService
{
    public const int MIN_NUMBER_OF_PLAYERS = 2; // TODO: Make this configurable as AppSettings.
    public const int MAX_NUMBER_OF_PLAYERS = 8; // TODO: Make this configurable as AppSettings.
    public const int TURN_MAX_DURATION_HOURS = 24; // TODO: Make this configurable as a property of the Game.

    /// <inheritdoc/>
    public async Task<ServiceResult<int>> SetupGameAsync(SetupGameRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            ServiceResult<CampaignMapGalleryProperties> mapPropertiesResult = await campaignMapGalleryService.GetCampaignMapGalleryProperties(request.GalleryMapId, cancellationToken);
            if (mapPropertiesResult.HasSucceeded == false || mapPropertiesResult.SuccessValue is null)
            {
                return ServiceResult<int>.Fail(mapPropertiesResult.Errors);
            }

            CampaignMapGalleryProperties mapProperties = mapPropertiesResult.SuccessValue;

            if (mapProperties.CreationStatus != MapCreationStatus.Complete)
            {
                logger.LogWarning("Requested map for new game setup does not exist or is not in a valid state. GalleryMapId: {GalleryMapId}", request.GalleryMapId);
                return ServiceResult<int>.NotFound($"Requested map for new game setup does not exist or is not in a valid state. GalleryMapId: {request.GalleryMapId}");
            }

            cancellationToken.ThrowIfCancellationRequested();

            GameIndex newGameIndex = await AddGameIndexAsync(context, request, mapProperties, cancellationToken);
            int gameId = newGameIndex.Id;

            IReadOnlyCollection<DomainPlayer> addedPlayers = await AddPlayersAsync(context, gameId, request.NumberOfPlayers, cancellationToken);

            // Seed each player's default military organisation config (officer rank ladders). Organisation
            // structures are created on demand when a player first customises a tag, so nothing is seeded here.
            await SeedPlayerMilitaryAsync(context, addedPlayers, cancellationToken);

            // Copy the 'global' view of the map gallery to the campaign map for this game.
            var generateCampaignMapResult = await GenerateCampaignMapAsync(context, gameId, mapProperties.Id, cancellationToken);

            int expectedRows = mapProperties.Width * mapProperties.Height;
            if (generateCampaignMapResult.HasSucceeded == false)
            {
                const string error = "An error occurred while generating the Campaign Map during Game setup.";
                return ServiceResult<int>.Fail(generateCampaignMapResult.Errors.Prepend(error));
            }
            else if (generateCampaignMapResult.SuccessValue != expectedRows)
            {
                logger.LogWarning("The number of records inserted into the CampaignMap ({Actual}) was less than expected ({Expected}). GameId: {GameId}", 
                    generateCampaignMapResult.SuccessValue, 
                    expectedRows, 
                    gameId);

                return ServiceResult<int>.Fail($"The number of records inserted into the Campaign Map was less than expected. Something went wrong.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            //  Then create the player-specific campaign map entries for each Player
            ServiceResult<(IReadOnlyCollection<DomainPlayer> AddedPlayers, int RecordsGenerated)> generatePlayerCampaignMapsResult = 
                await GeneratePlayerCampaignMapsAsync(context, gameId, addedPlayers, cancellationToken);

            expectedRows = request.NumberOfPlayers * expectedRows;
            if (generatePlayerCampaignMapsResult.HasSucceeded == false)
            {
                const string error = "An error occurred while generating Player Campaign Map entries during Game setup.";
                return ServiceResult<int>.Fail(generatePlayerCampaignMapsResult.Errors.Prepend(error));
            }
            else if (generatePlayerCampaignMapsResult.SuccessValue.RecordsGenerated != expectedRows)
            {
                logger.LogWarning("The number of records inserted into the PlayerCampaignMap ({Actual}) was less than expected ({Expected}). GameId: {GameId}", 
                    generatePlayerCampaignMapsResult.SuccessValue.RecordsGenerated, 
                    expectedRows, 
                    gameId);

                return ServiceResult<int>.Fail($"The number of records inserted into the Player Campaign Map's was less than expected. Something went wrong.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            int[] playerIds = [.. addedPlayers.Select(p => p.Id)];
            ServiceResult<FirstPlayerInRound> queueResult = await CreateCampaignTurnQueueAsync(context, gameId, playerIds, cancellationToken);
            if (queueResult.HasSucceeded == false)
            {
                const string error = "An error occurred while creating the Campaign Turn Queue during Game setup.";
                logger.LogError("{Error} Errors: {Errors}", error, string.Join("; ", queueResult.Errors));
                return ServiceResult<int>.Fail(queueResult.Errors.Prepend(error));
            }

            await CompleteGameSetupAsync(context, gameId, cancellationToken);

            return ServiceResult<int>.Succeed(gameId);
        }
        catch (OperationCanceledException)
        {
            const string error = "Game setup was cancelled.";
            logger.LogWarning(error + " (Game Name: {id})", request.GameName);

            return ServiceResult<int>.Fail(error);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while setting up a new game.";
            logger.LogError(ex, error);
            return ServiceResult<int>.Fail(error);
        }
    }

    /// <summary>
    /// Creates a new Campaign Turn Queue for the specified Game and Players, populating Turn Queue and Turn Queue Participants.
    /// (This method is self-saving)
    /// </summary>
    /// <returns>A ServiceResult containing the first player in the round if the Campaign Turn Queue is successfully created; otherwise, a failure result with an error message.</returns>
    private async Task<ServiceResult<FirstPlayerInRound>> CreateCampaignTurnQueueAsync(AscendantEmpireContext context, int gameId, IEnumerable<int> playerIds, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check the Campaign Turn Queue doesn't already exist for this Game, to avoid accidentally creating multiple queues if this method is called more than once for the same Game.
            bool exists = await context.TurnQueues
                .Where(tq => tq.GameId == gameId)
                .Where(tq => tq.QueueType == TurnQueueType.Campaign)
                .AnyAsync(cancellationToken);

            if (exists)
            {
                logger.LogWarning("Attempted to create Campaign Turn Queue for GameId {GameId}, but one already exists.", gameId);
                return ServiceResult<FirstPlayerInRound>.Fail($"A Campaign Turn Queue already exists for GameId {gameId}.");
            }

            int[] playerIdsInTurnOrder = [.. playerIds.OrderBy(_ => Guid.NewGuid())];
            DateTimeOffset now = DateTimeOffset.UtcNow;

            var turnQueue = new TurnQueue
            {
                GameId = gameId,
                QueueType = TurnQueueType.Campaign,
                BattleId = null,
                CurrentRoundNumber = 1,
                CurrentTurnStartAt = null,
                CurrentScheduledTurnEndAt = null,
                CurrentActualTurnEndAt = null,
                IsCompleted = false,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = [],
                TurnQueueParticipants = [.. playerIdsInTurnOrder
                .Select((playerId, index) => new TurnQueueParticipant
                {
                    PlayerId = playerId,
                    TurnOrder = index + 1,
                    Status = TurnQueueParticipantStatus.Pending,
                    IsActiveInLane = true,
                    LastCompletedRoundNumber = null,
                    LastTurnStartedAt = null,
                    LastTurnEndedAt = null,
                    RowVersion = []
                })]
            };

            context.TurnQueues.Add(turnQueue);

            await context.SaveChangesAsync(cancellationToken);

            TurnQueueParticipant firstParticipant = turnQueue.TurnQueueParticipants
                .OrderBy(p => p.TurnOrder)
                .First();

            return ServiceResult<FirstPlayerInRound>.Succeed(new FirstPlayerInRound
            {
                TurnQueueId = turnQueue.Id,
                PlayerId = firstParticipant.PlayerId,
                RoundNumber = turnQueue.CurrentRoundNumber
            });
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while trying to create the Campaign Turn Queue.";
            logger.LogError(ex, "{Error} Game Id: {GameId}", error, gameId);
            return ServiceResult<FirstPlayerInRound>.Fail(error);
        }
    }

    /// <summary>
    /// Finalizes the setup process for a game and transitions it to the in-progress state, enabling the first Player's turn to begin.
    /// </summary>
    /// <remarks>This method:
    /// <list type="bullet">
    /// <item>Sets the initial turn start and end times for the campaign turn queue.</item>
    /// <item>Updates the game's status to indicate that setup is complete and play is in progress.</item>
    /// </list></remarks>
    private static async Task CompleteGameSetupAsync(AscendantEmpireContext context, int gameId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Set the first Player's turn to start immediately, so that the game can begin as soon as setup is complete.
        await context.TurnQueues
            .Where(tq => tq.GameId == gameId)
            .Where(tq => tq.QueueType == TurnQueueType.Campaign)
            .ExecuteUpdateAsync(tq => tq
                .SetProperty(tq => tq.CurrentTurnStartAt, now)
                .SetProperty(tq => tq.CurrentScheduledTurnEndAt, now.AddHours(24)), // TODO: Make the turn time configurable as a property of the Game.
                cancellationToken);

        await context.GameIndex
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(g => g
                .SetProperty(g => g.CreationStatus, GameCreationStatus.SetupComplete)
                .SetProperty(g => g.PlayStatus, GamePlayStatus.InProgress),
                cancellationToken);
    }

    private static async Task<GameIndex> AddGameIndexAsync(
        AscendantEmpireContext context,
        SetupGameRequest request,
        CampaignMapGalleryProperties mapProperties,
        CancellationToken cancellationToken)
    {
        var newGame = new GameIndex
        {
            GameName           = request.GameName,
            CampaignMapIndexId = request.GalleryMapId,
            NumberOfPlayers    = request.NumberOfPlayers,
            CreationStatus     = GameCreationStatus.SetupInProgress,
            PlayStatus         = GamePlayStatus.SetupInProgress,
            CreatedAt          = DateTimeOffset.UtcNow,
            HorizontalWrap     = mapProperties.HorizontalWrap,
            VerticalWrap       = mapProperties.VerticalWrap,
            // Use the caller-supplied seed if provided; otherwise generate a random one
            // so the player can reproduce the same biome distribution later if desired.
            GenerationSeed     = request.GenerationSeed ?? Random.Shared.NextInt64(),
            BattleMapSize      = request.BattleMapSize,
        };

        context.GameIndex.Add(newGame);
        await context.SaveChangesAsync(cancellationToken);

        return newGame;
    }

    private static async Task<IReadOnlyCollection<DomainPlayer>> AddPlayersAsync(
        AscendantEmpireContext context,
        int gameId,
        int numberOfPlayers,
        CancellationToken cancellationToken)
    {
        List<DomainPlayer> playersToAdd = [];
        for (int i = 1; i <= numberOfPlayers; i++)
        {
            playersToAdd.Add(new DomainPlayer
            {
                GameId = gameId,
                UserId = null,
                RulerTitle = GetRandomRulerTitle(),
                RulerName = $"Player {i}",
                FactionName = $"Faction {i}",
                Status = PlayerStatus.Active,
            });
        }

        context.Players.AddRange(playersToAdd);
        await context.SaveChangesAsync(cancellationToken);

        return playersToAdd;
    }

    /// <summary>
    /// Seeds every player's default officer rank ladders — one full ladder per seeded movement domain — from
    /// the code-defined <see cref="MilitaryOrganisationDefaults"/>.
    /// </summary>
    /// <remarks>(The method is self-saving.)</remarks>
    private static async Task SeedPlayerMilitaryAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<DomainPlayer> addedPlayers,
        CancellationToken cancellationToken)
    {
        List<PlayerOfficerRank> ranksToAdd = [];

        foreach (DomainPlayer player in addedPlayers)
        {
            foreach (DomainType domain in MilitaryOrganisationDefaults.SeededDomains)
            {
                foreach (MilitaryOrganisationDefaults.RankDefault rank in MilitaryOrganisationDefaults.RanksForDomain(domain))
                {
                    ranksToAdd.Add(new PlayerOfficerRank
                    {
                        PlayerId = player.Id,
                        MovementDomain = domain,
                        Level = rank.Level,
                        Title = rank.Title,
                        MaxSoldiers = rank.MaxSoldiers,
                        MaxAttachedSpecialists = rank.MaxAttachedSpecialists,
                        InUse = true,
                    });
                }
            }
        }

        context.PlayerOfficerRanks.AddRange(ranksToAdd);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Generates the CampaignMap for the Game by copying the relevant data from the specified map in the Map Gallery. 
    /// </summary>
    /// <remarks>This is done via a SQL statement to efficiently copy all of the necessary rows in a single query, rather than loading them into memory and then re-inserting them.
    /// (The method is self-saving)</remarks>
    /// <returns>The number of rows inserted into the CampaignMap table.</returns>
    private async Task<ServiceResult<int>> GenerateCampaignMapAsync(
        AscendantEmpireContext context,
        int gameId,
        int campaignMapGalleryIndexId,
        CancellationToken cancellationToken)
    {
        bool alreadyHasMap = await context.CampaignMaps.AnyAsync(x => x.GameId == gameId, cancellationToken);

        if (alreadyHasMap)
        {
            logger.LogWarning("Attempted to generate Campaign Map for GameId {GameId}, but it already has CampaignMap rows.", gameId);
            return ServiceResult<int>.Fail($"GameId {gameId} already has CampaignMap rows.");
        }

        int inserted = await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO [game].[CampaignMap]
            (
                [GameId],
                [XCoord],
                [YCoord],
                [ForageAmount],
                [HuntAmount],
                [FishAmount],
                [FodderAmount],
                [FirstSeenOnTurnId],
                [FirstSeenByPlayerId],
                [LastVisitedOnTurnId],
                [LastVisitedByPlayerId],
                [BiomeTypeId],
                [CampaignMapGeographyTypeId]
            )
            SELECT
                {gameId}                        AS [GameId],
                g.[XCoord]                      AS [XCoord],
                g.[YCoord]                      AS [YCoord],
                NULL                            AS [ForageAmount],
                NULL                            AS [HuntAmount],
                NULL                            AS [FishAmount],
                NULL                            AS [FodderAmount],
                NULL                            AS [FirstSeenOnTurnId],
                NULL                            AS [FirstSeenByPlayerId],
                NULL                            AS [LastVisitedOnTurnId],
                NULL                            AS [LastVisitedByPlayerId],
                g.BiomeTypeId                   AS [BiomeTypeId],
                g.CampaignMapGeographyTypeId    AS [CampaignMapGeographyTypeId]
            FROM [mapgallery].[CampaignMapGallery] g
            WHERE g.[CampaignMapGalleryIndexId] = {campaignMapGalleryIndexId};
            ", cancellationToken);

        return ServiceResult<int>.Succeed(inserted);
    }

    /// <summary>
    /// Generates Player Campaign Map records for a specified Game and updates the provided Player collection with Campaign Offset identifiers.
    /// </summary>
    /// <remarks>(The method is self-saving)</remarks>
    /// <returns>The collection of Players (with updated Campaign Offset identifiers) and the number of records generated.</returns>
    private async Task<ServiceResult<(IReadOnlyCollection<DomainPlayer> AddedPlayers, int RecordsGenerated)>> GeneratePlayerCampaignMapsAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<DomainPlayer> addedPlayers,
        CancellationToken cancellationToken)
    {
        bool alreadyHasRows = await context.PlayerCampaignMaps.AnyAsync(x => x.GameId == gameId, cancellationToken);

        if (alreadyHasRows)
        {
            logger.LogWarning("Attempted to generate PlayerCampaignMap rows for GameId {GameId}, but it already has PlayerCampaignMap rows.", gameId);
            return ServiceResult<(IReadOnlyCollection<DomainPlayer>, int)>.Fail($"GameId {gameId} already has PlayerCampaignMap rows.");
        }

        int inserted = await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO [game].[PlayerCampaignMap]
            (
                [GameId],
                [CampaignMapId],
                [PlayerId],
                [XCoord],
                [YCoord],
                [BiomeTypeId],
                [CampaignMapGeographyTypeId],
                [FirstSeenOnTurnId],
                [LastSeenOnTurnId]
            )
            SELECT
                cm.[GameId]             AS [GameId],
                cm.[CampaignMapId]      AS [CampaignMapId],
                p.[Id]                  AS [PlayerId],
                cm.[XCoord]             AS [XCoord],
                cm.[YCoord]             AS [YCoord],
                CAST(0 AS INT)          AS [BiomeTypeId],
                CAST(0 AS INT)          AS [CampaignMapGeographyTypeId],
                NULL                    AS [FirstSeenOnTurnId],
                NULL                    AS [LastSeenOnTurnId]
            FROM [game].[CampaignMap] cm
            INNER JOIN [game].[Player] p
                ON p.[GameId] = cm.[GameId]
            WHERE cm.[GameId] = {gameId};
            ", cancellationToken);

        Dictionary<int, int> offsetInfo = await context.PlayerCampaignMaps
            .Where(pcm => pcm.GameId == gameId)
            .Where(pcm => pcm.XCoord == 0 && pcm.YCoord == 0)
            .Select(pcm => new { pcm.PlayerId, pcm.PlayerCampaignMapId })
            .ToDictionaryAsync(x => x.PlayerId, x => x.PlayerCampaignMapId, cancellationToken);

        foreach ((int playerId, int playerCampaignMapId) in offsetInfo)
        {
            DomainPlayer? player = addedPlayers.FirstOrDefault(p => p.Id == playerId);
            if (player != null)
            {
                player.CampaignOffsetId = playerCampaignMapId;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<(IReadOnlyCollection<DomainPlayer>, int)>.Succeed((addedPlayers, inserted));
    }

    private static string[] GetMaleRulerTitles() =>
    [
        "King",
        "Emperor",
        "High King",
        "Prince",
        "Duke",
        "Grand Duke",
        "Archduke",
        "Count",
        "Earl",
        "Lord",
        "Baron",
        "Sultan",
        "Shah",
        "Khan",
        "Overlord",
        "Lord Protector",
        "Imperator",
        "System Lord"
    ];

    private static string[] GetFemaleRulerTitles() =>
    [
        "Queen",
        "Empress",
        "High Queen",
        "Princess",
        "Duchess",
        "Grand Duchess",
        "Archduchess",
        "Countess",
        "Lady",
        "Baroness",
        "Sultana",
        "Shahbanu",
        "Khatun",
        "Overlady",
        "Lady Protector",
        "Imperatrix",
        "System Lady"
    ];

    private static string[] GetNonGenderSpecificRulerTitles() =>
    [
        "Sovereign",
        "High Sovereign",
        "Monarch",
        "Ruler",
        "Regent",
        "Pharaoh",
        "Chieftain",
        "High Chieftain",
        "Warlord",
        "Supreme Commander",
        "Grand Marshal",
        "First Citizen",
        "Archon",
        "High Executor",
        "Planetary Governor",
        "Oracle-Ruler",
        "High Arbiter",
        "Dominion Speaker",
        "Thronekeeper"
    ];

    public static string[] GetRulerTitles() =>
        GetMaleRulerTitles()
        .Concat(GetFemaleRulerTitles())
        .Concat(GetNonGenderSpecificRulerTitles())
        .ToArray();

    private static string GetRandomRulerTitle(Random? rng = null)
    {
        rng ??= Random.Shared;
        string[] titles = GetRulerTitles();
        return titles[rng.Next(titles.Length)];
    }
}
