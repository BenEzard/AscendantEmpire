using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.FrontendContracts.Games;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Game;

public record SetupGameRequest(int MapId, string GameName, int NumberOfPlayers) : IRequest<ServiceResult<Guid>>;

internal class SetupGameHandler(
    ILogger<SetupGameHandler> logger,
    AscendantEmpireContext context
    ) : IRequestHandler<SetupGameRequest, ServiceResult<Guid>>
{
    public async Task<ServiceResult<Guid>> Handle(SetupGameRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate the request objects
            ServiceResultBase validation = ValidateRequest(context, request);

            if (validation.HasSucceeded == false)
            {
                return ServiceResult<Guid>.Fail(validation.Errors);
            }

            // Add a new record to the GameIndex table
            GameIndex newGameIndex = await AddGameIndexAsync(request, CancellationToken.None);
            int gameId = newGameIndex.GameIndexId;

            // Generate game.Player records
            IReadOnlyCollection<int> playerIds = await AddPlayersAsync(gameId, request.NumberOfPlayers, CancellationToken.None);

            // Generate the Campaign Map for the game
            await GenerateCampaignMapAsync(gameId, request.MapId, CancellationToken.None);

            // Generate the Player Campaign Maps for each player
            await GeneratePlayerCampaignMapsAsync(gameId, CancellationToken.None);

            return ServiceResult<Guid>.Succeed(newGameIndex.GameGuid);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while setting up a new game.";
            logger.LogError(ex, error);
            return ServiceResult<Guid>.Fail(error);
        }
    }

    private async Task<GameIndex> AddGameIndexAsync(SetupGameRequest request, CancellationToken cancelToken)
    {
        // Get all Guids
        HashSet<Guid> existingGuids = [.. context.GameIndex.Select(g => g.GameGuid)];

        Guid gameGuid;

        do
        {
            gameGuid = Guid.NewGuid();
        } while (existingGuids.Contains(gameGuid));

        // Insert a record into the GameIndex table
        var newGame = new GameIndex
        {
            GameName = request.GameName,
            CampaignMapIndexId = request.MapId,
            NumberOfPlayers = request.NumberOfPlayers,
            GameGuid = gameGuid,
            CreationStatus = Domain.Enums.Games.GameCreationStatus.SetupInProgress,
            PlayStatus = Domain.Enums.Games.GamePlayStatus.SetupInProgress,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.GameIndex.Add(newGame);
        await context.SaveChangesAsync(cancelToken);

        return newGame;
    }

    private async Task<IReadOnlyCollection<int>> AddPlayersAsync(int gameId, int numberOfPlayers, CancellationToken cancelToken)
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
                Status = PlayerStatus.Active
            });
        }

        context.Players.AddRange(playersToAdd);
        await context.SaveChangesAsync(cancelToken);

        return [.. playersToAdd.Select(p => p.PlayerId)];
    }

private async Task<int> GenerateCampaignMapAsync(
    int gameId,
    int campaignMapGalleryIndexId,
    CancellationToken cancelToken)
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
            [FirstVisitedPlayerId],
            [LastVisitedTurnId],
            [LastVisitedPlayerId],
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
            NULL                            AS [FirstVisitedPlayerId],
            NULL                            AS [LastVisitedTurnId],
            NULL                            AS [LastVisitedPlayerId],
            g.BiomeTypeId                   AS [BiomeTypeId],
            g.CampaignMapGeographyTypeId    AS [CampaignMapGeographyTypeId]
        FROM [mapgallery].[CampaignMapGallery] g
        WHERE g.[CampaignMapGalleryIndexId] = {campaignMapGalleryIndexId};
        ", cancelToken);

    return inserted;
}

private async Task<int> GeneratePlayerCampaignMapsAsync(int gameId, CancellationToken cancelToken)
{
    // Optional safety guard
    bool alreadyHasRows = await context.Set<PlayerCampaignMap>()
        .AsNoTracking()
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
            [FirstVisitedTurnId],
            [LastVisitedTurnId]
        )
        SELECT
            cm.[GameId]             AS [GameId],
            cm.[CampaignMapId]      AS [CampaignMapId],
            p.[PlayerId]            AS [PlayerId],
            cm.[XCoord]             AS [XCoord],
            cm.[YCoord]             AS [YCoord],
            CAST(0 AS INT)          AS [BiomeTypeId],
            CAST(0 AS INT)          AS [CampaignMapGeographyTypeId],
            NULL                    AS [FirstVisitedTurnId],
            NULL                    AS [LastVisitedTurnId]
        FROM [game].[CampaignMap] cm
        INNER JOIN [game].[Player] p
            ON p.[GameId] = cm.[GameId]
        WHERE cm.[GameId] = {gameId};
        ", cancelToken);

    return inserted;
}


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

    private static string[] GetRulerTitles() => new[]
    {
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
    };

    private string GetRandomRulerTitle(Random? rng = null)
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

