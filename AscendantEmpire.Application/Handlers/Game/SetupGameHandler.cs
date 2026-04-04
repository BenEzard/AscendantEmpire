using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Turns;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Game;

public record SetupGameRequest(int MapId, string GameName, int NumberOfPlayers) : IRequest<ServiceResult<int>>;

internal class SetupGameHandler(
    ILogger<SetupGameHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator
    ) : IRequestHandler<SetupGameRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(SetupGameRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate the request objects
            ServiceResultBase validation = ValidateRequest(context, request);

            if (validation.HasSucceeded == false)
            {
                return ServiceResult<int>.Fail(validation.Errors);
            }

            // Add a new record to the GameIndex table
            GameIndex newGameIndex = await AddGameIndexAsync(request, CancellationToken.None);
            int gameId = newGameIndex.Id;

            // Generate game.Player records
            IReadOnlyCollection<Player> addedPlayers = await AddPlayersAsync(gameId, request.NumberOfPlayers, CancellationToken.None);

            // Generate the Campaign Map for the game
            await GenerateCampaignMapAsync(gameId, campaignMapGalleryIndexId: request.MapId, CancellationToken.None);

            // Generate the Player Campaign Maps for each player
            await GeneratePlayerCampaignMapsAsync(gameId, addedPlayers, CancellationToken.None);

            // Create the Campaign Roster
            ServiceResultBase rosterResult = await mediator.Send(new AddCampaignTurnsToRosterRequest(newGameIndex.Id), CancellationToken.None);

            if (rosterResult.HasSucceeded == false)
            {
                const string error = "An error occurred while creating the Campaign Roster during Game setup.";
                logger.LogError("{Error} Errors: {Errors}", error, rosterResult.Errors);
                return ServiceResult<int>.Fail(error);
            }

            await CompleteGameSetupAsync(gameId, context);

            return ServiceResult<int>.Succeed(newGameIndex.Id);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while setting up a new game.";
            logger.LogError(ex, error);
            return ServiceResult<int>.Fail(error);
        }
    }

    private static async Task CompleteGameSetupAsync(int gameId, AscendantEmpireContext context)
    {
        // Make the Turn Status for the first Player on the Roster to Active
        await context.TurnRosters
            .Where(r => r.GameId == gameId)
            .OrderBy(r => r.TurnOrder)
            .Take(1)
            .ExecuteUpdateAsync(r => r
                .SetProperty(r => r.Status, TurnStatus.TakingTurn)
                .SetProperty(r => r.TurnStartTime, DateTimeOffset.Now)
                .SetProperty(r => r.ScheduledTurnEndTime, DateTimeOffset.Now.AddHours(48)) // TODO: Make configurable
            );

        // Update the GameIndex to mark setup as Complete
        await context.GameIndex
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(g => g
                .SetProperty(g => g.CreationStatus, GameCreationStatus.SetupComplete)
                .SetProperty(g => g.PlayStatus, GamePlayStatus.InProgress));
    }

    private async Task<GameIndex> AddGameIndexAsync(SetupGameRequest request, CancellationToken cancelToken)
    {
        // Insert a record into the GameIndex table
        var newGame = new GameIndex
        {
            GameName = request.GameName,
            CampaignMapIndexId = request.MapId,
            NumberOfPlayers = request.NumberOfPlayers,
            CreationStatus = GameCreationStatus.SetupInProgress,
            PlayStatus = GamePlayStatus.SetupInProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            HorizontalWrap = false,
            VerticalWrap = false
        };

        context.GameIndex.Add(newGame);
        await context.SaveChangesAsync(cancelToken);

        return newGame;
    }

    /// <summary>
    /// Add Players to the game.Player table, generating unique Guids for each Player.
    /// </summary>
    /// <returns>The collection of added Player entities.</returns>
    private async Task<IReadOnlyCollection<Player>> AddPlayersAsync(int gameId, int numberOfPlayers, CancellationToken cancelToken = default)
    {
        List<Player> playersToAdd = [];
        for (int i = 1; i <= numberOfPlayers; i++)
        {
            playersToAdd.Add(new Player
            {
                GameId = gameId,
                UserId = null, // Will be null before a Player is assigned to the Game.
                RulerTitle = GetRandomRulerTitle(),
                RulerName = $"Player {i}",
                FactionName = $"Faction {i}",
                Status = PlayerStatus.Active,
            });
        }

        context.Players.AddRange(playersToAdd);
        await context.SaveChangesAsync(cancelToken);

        return playersToAdd;
    }

    private async Task<int> GenerateCampaignMapAsync(
        int gameId,
        int campaignMapGalleryIndexId,
        CancellationToken cancelToken = default)
    {
        // Optional safety: ensure we don't accidentally duplicate maps for the same game.
        // If you *always* call this only for new games, you can remove this guard.
        bool alreadyHasMap = await context.Set<CampaignMap>()
            .AsNoTracking()
            .AnyAsync(x => x.GameId == gameId, cancelToken);

        if (alreadyHasMap)
        {
            throw new InvalidOperationException($"GameId {gameId} already has CampaignMap rows.");
        }

        // Set-based insert = fast
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
            ", cancelToken);

        return inserted;
    }

    /// <summary>
    /// Generates PlayerCampaignMap entries for the specified game and players, initializing each player's campaign map data for the game.
    /// Also updates the CampaignOffsetId for each player to point to their starting tile.
    /// </summary>
    /// <remarks>This method creates one PlayerCampaignMap entry for each combination of the specified players
    /// and the campaign map tiles in the given game. It also updates each player's CampaignOffsetId to reference the
    /// starting tile of their campaign map. The operation is performed as a single batch insert for
    /// efficiency.</remarks>
    /// <param name="gameId">The unique identifier of the game for which player campaign maps are to be generated.</param>
    /// <param name="addedPlayers">A collection of players to associate with the campaign maps. Each player in this collection will receive
    /// campaign map entries for the specified game.</param>
    /// <param name="cancelToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>The number of PlayerCampaignMap rows inserted into the database.</returns>
    /// <exception cref="InvalidOperationException">Thrown if PlayerCampaignMap entries already exist for the specified game.</exception>
    private async Task<int> GeneratePlayerCampaignMapsAsync(
        int gameId, 
        IReadOnlyCollection<Player> addedPlayers, 
        CancellationToken cancelToken = default)
    {
        // Optional safety guard
        bool alreadyHasRows = await context.Set<PlayerCampaignMap>()
            .AnyAsync(x => x.GameId == gameId, cancelToken);

        if (alreadyHasRows)
        {
            throw new InvalidOperationException($"GameId {gameId} already has PlayerCampaignMap rows.");
        }

        // Insert one row per (Player x CampaignMap tile).
        // Biome/Geography copied from CampaignMap (which you forced to 0/0).
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
            INNER JOIN [game].[Players] p
                ON p.[GameId] = cm.[GameId]
            WHERE cm.[GameId] = {gameId};
            ", cancelToken);

        // Get the Id of the XCoord=0, YCoord=0 of each Player Campaign Map in the game
        Dictionary<int, int> offsetInfo = context.PlayerCampaignMaps
            .Where(pcm => pcm.GameId == gameId)
            .Where(pcm => pcm.XCoord == 0 && pcm.YCoord == 0)
            .Select(pcm => new { pcm.PlayerId, pcm.PlayerCampaignMapId })
            .ToDictionary(x => x.PlayerId, x => x.PlayerCampaignMapId);

        foreach (var offset in offsetInfo)
        {
            // Update each Player to set their CampaignOffsetId
            Player? player = addedPlayers.FirstOrDefault(p => p.Id == offset.Key);
            if (player != null)
            {
                player.CampaignOffsetId = offset.Value;
            }
        }

        await context.SaveChangesAsync(cancelToken);

        return inserted;
}


    /// <summary>
    /// Validates the specified game setup request
    /// </summary>
    private static ServiceResultBase ValidateRequest(AscendantEmpireContext context, SetupGameRequest request)
    {
        var validator = new SetupGameValidator();

        var validationResult = validator.Validate(request);

        List<string> errors = validationResult.IsValid == false
            ? [.. validationResult.Errors.Select(e => e.ErrorMessage)]
            : [];

        // Check that the map exists
        bool mapExists = context.CampaignMapGalleryIndex.Any(m => m.Id == request.MapId
            && m.CreationStatus == Domain.Enums.Maps.MapCreationStatus.Complete);

        if (mapExists == false)
        {
            errors.Add($"Map with ID {request.MapId} does not exist.");
        }

        return ServiceResult.Succeed();
    }

    /// <summary>
    /// Temporary method to provide a list of potential ruler titles.
    /// </summary>
    private static string[] GetRulerTitles() =>
    [
        "King",
        "Queen",
        "Emperor",
        "Empress",
        "Tsar",
        "Tsarina",
        "Kaiser",
        "Sultan",
        "Sultana",
        "Shah",
        "Shahanshah",
        "Pharaoh",
        "Caliph",
        "Pope",
        "Patriarch",
        "Archduke",
        "Archduchess",
        "Grand Duke",
        "Grand Duchess",
        "Duke",
        "Duchess",
        "Prince",
        "Princess",
        "Elector",
        "Margrave",
        "Landgrave",
        "Count",
        "Countess",
        "Earl",
        "Viscount",
        "Viscountess",
        "Baron",
        "Baroness",
        "Lord",
        "Lady",
        "High King",
        "Warlord",
        "Chieftain",
        "High Chieftain",
        "Khan",
        "Khagan",
        "Hetman",
        "Voivode",
        "Despot",
        "Satrap",
        "Doge",
        "Podestà",
        "Raja",
        "Maharaja",
        "Regent"
    ];

    /// <summary>
    /// Selects a random ruler title from the available list of titles.
    /// </summary>
    private static string GetRandomRulerTitle(Random? rng = null)
    {
        rng ??= Random.Shared; // .NET 6+
        var titles = GetRulerTitles();
        return titles[rng.Next(titles.Length)];
    }

    private class SetupGameValidator : AbstractValidator<SetupGameRequest>
    {
        const int MIN_PLAYERS = 2;
        const int MAX_PLAYERS = 8;

        public SetupGameValidator()
        {
            RuleFor(g => g.GameName)
                .NotEmpty().WithMessage(g => $"{nameof(g.GameName)} is required.")
                .MaximumLength(GameIndex.GAME_NAME_MAX_LENGTH).WithMessage(g => $"{nameof(g.GameName)} cannot exceed {GameIndex.GAME_NAME_MAX_LENGTH} characters.");
            RuleFor(g => g.NumberOfPlayers)
                .InclusiveBetween(MIN_PLAYERS, MAX_PLAYERS)
                .WithMessage(g => $"{nameof(g.NumberOfPlayers)} must be between {MIN_PLAYERS} and {MAX_PLAYERS} players.");
        }
    }
}

