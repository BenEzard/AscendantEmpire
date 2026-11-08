using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Maps.MapGallery.Services;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Features.Military;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.BackendContracts.Turns.CreateCampaignTurnQueue;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Military;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Technologies;
using AscendantEmpire.Domain.Enums.Turns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using AscendantEmpire.Application.Options;

using Microsoft.Extensions.Options;

using static AscendantEmpire.Application.Common.ServiceResultBase;

using DomainPlayer = AscendantEmpire.Domain.Entities.Games.Player;

namespace AscendantEmpire.Application.Features.Game.Services;

internal class GameSetupService(
    ILogger<GameSetupService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ICampaignMapGalleryService campaignMapGalleryService,
    IForceLifecycleWriter forceLifecycleWriter,
    IEnumerable<ITechnologyRequirementGenerator> technologyRequirementGenerators,
    IPlayerTechnologyAvailabilityService technologyAvailabilityService,
    IOptions<GameSettings> gameSettings,
    IMapResourceInstantiationService mapResourceInstantiationService,
    IPlayerMapResourceDiscoveryService playerMapResourceDiscoveryService
) : IGameSetupService
{
    /// <summary>
    /// The Round the campaign Turn Queue is opened on when a Game is set up.
    /// </summary>
    private const int _FIRST_ROUND_NUMBER = 1;

    /// <summary>
    /// How many people every player starts the game needing for one directed research point.
    /// </summary>
    /// <remarks>
    /// The opening value of <see cref="PlayerResearchState.TechPointsPerPopulationSize"/>, which moves per player as the game runs.
    /// </remarks>
    private const int _DEFAULT_TECH_POINTS_PER_POPULATION_SIZE = 100;

    /// <summary>
    /// The global unit template codes every player starts the game with — unlocked, and spawned once each at the player's start position. 
    /// </summary>
    /// <remarks>
    /// Each must resolve to an unled organisation tag (see <see cref="SeedStarterUnitsAsync"/>).
    /// These are <b>hand-authored catalogue content</b>, not seeded: game setup reads them and fails if they are absent. See <c>docs/design/units/military-organisation-implementation-notes.md</c>.
    /// </remarks>
    public static readonly string[] StarterTemplateCodes = ["SETTLER", "SCOUT"];

    /// <summary>
    /// The <c>assets.AssetMetaCategory</c> codes every Player starts the game with unlocked, and so which icons their Assets toolbar group shows.
    /// </summary>
    /// <remarks>
    /// All five, so every icon is present from the first Round. Which Assets sit under each one is <see cref="StarterAssetCodes"/>; 
    /// a meta-category with nothing available under it still shows its icon over an empty panel.
    /// </remarks>
    public static readonly string[] StarterAssetMetaCategoryCodes = ["ConstructionMaterials", "Weapons", "RawMaterials", "Foodstuffs", "Society"];

    /// <summary>
    /// The <c>assets.Asset</c> codes every Player starts the game with unlocked — a stone-age start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Held back until Technology exists: the named timbers, every Plank and Brick, the harder and prestige stones, the spear and the sword, every Medium and
    /// High grade (those need buildings and specialists rather than merely the material), everything needing processing, and every Foodstuff beyond the four
    /// a people can pick up off the ground.
    /// </para>
    /// <para>
    /// These are <b>seeded catalogue content</b>, unlike <see cref="StarterTemplateCodes"/>: they come from <c>AssetSeedData</c>, so a game set up before the
    /// Assets seed has run finds none of them and fails rather than starting a Player with an empty asset panel.
    /// </para>
    /// </remarks>
    public static readonly string[] StarterAssetCodes =
    [
        "ConstructionMaterials.Wood.Logs.GenericLog",
        "ConstructionMaterials.Wood.Logs.Bamboo",
        "ConstructionMaterials.Stone.RawRock.GenericRock",
        "ConstructionMaterials.Stone.RawRock.Flint",
        "ConstructionMaterials.Stone.RawRock.Limestone",
        "Weapons.MeleeWeapons.WoodClub.Low",
        "Weapons.MeleeWeapons.BoneKnife.Low",
        "Weapons.MeleeWeapons.StoneAxe.Low",
        "RawMaterials.RawMaterials.Bone",
        "RawMaterials.RawMaterials.Clay",
        "RawMaterials.RawMaterials.Ochre",
        "RawMaterials.RawMaterials.Chalk",
        "RawMaterials.RawMaterials.Fodder",
        "RawMaterials.RawMaterials.Hides",
        "RawMaterials.RawMaterials.Tallow",

        // The foods a people gathers by hand with no tool, technique or season behind it. Hunting, fishing, digging and threshing are all held back, which
        // is why Game, Fish, Roots and the Wild Grains are absent from a start that can still feed itself on insects, fruit, greens and berries.
        "Foodstuffs.Meat.Insects",
        "Foodstuffs.PlantFoods.Fruit",
        "Foodstuffs.PlantFoods.Berries",
        "Foodstuffs.PlantFoods.Greens",

        // Population stats; excluding Airforce and Navy which need to be unlocked.
        // The foreign Assets are unlocked from the first Round even though nobody has met anybody yet.
        // An unlock row names a Player and an Asset and nothing else, so there is no unlocking these per opponent: one row makes Slaves visible in every projected category.
        // What decides whether a foreign category is drawn at all is the projection in GetPlayerAssetsHandler, not these rows.
        "Society.Population.MyPeople.Civilians",
        "Society.Population.MyPeople.Slaves",
        "Society.Population.MyPeople.Interned",
        "Society.Population.MyPeople.PoW",
        "Society.Population.Forces.Army",
        "Society.ForeignPopulation.Subjugated.Slaves",
        "Society.ForeignPopulation.Subjugated.Interned",
        "Society.ForeignPopulation.Subjugated.PoW"
    ];

    /// <summary>
    /// The <c>technology.Technology</c> codes every Player starts the game already having discovered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deliberately empty.</b> Nothing is granted Discovered at setup: a Player starts the game having researched nothing, and what they may research first
    /// is decided by the prerequisite graph instead. The catalogue's roots - the technologies with no prerequisite - come out Available from the first Round,
    /// which is the whole of what "starting technologies" means today.
    /// </para>
    /// <para>
    /// The guard in <c>SeedPlayerTechnologiesAsync</c> is kept even though an empty array can never trip it. These would be <b>seeded catalogue content</b>
    /// like <see cref="StarterAssetCodes"/>, so the moment a code is put back here it has to resolve against <c>TechnologySeedData</c> - and a code that does
    /// not must fail game creation loudly rather than quietly granting nothing.
    /// </para>
    /// </remarks>
    public static readonly string[] StarterTechnologyCodes =
    [
    ];

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

            // Each player's own technology requirements, their starting discoveries, and their research state.
            // Done before the map so a missing technology catalogue fails setup early rather than after the expensive map generation.
            ServiceResultBase technologyResult = await SeedPlayerTechnologyAsync(context, newGameIndex, addedPlayers, cancellationToken);

            if (technologyResult.HasSucceeded == false)
            {
                return ServiceResult<int>.Fail(technologyResult.Errors);
            }

            // Copy the 'global' view of the map gallery to the campaign map for this game.
            var generateCampaignMapResult = await GenerateCampaignMapAsync(context, newGameIndex, mapProperties.Id, cancellationToken);

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

            // The MapResources the map was authored with, rolled from this Game's seed. After the map because it addresses the tiles the copy above created,
            // and before the Player maps because the MapResources are world state that the Players' views of it follow.
            ServiceResult<int> mapResourceResult = await mapResourceInstantiationService.InstantiateForGameAsync(
                gameId,
                mapProperties.Id,
                newGameIndex.GenerationSeed,
                newGameIndex.CampaignMapOffsetId,
                newGameIndex.MapWidth,
                cancellationToken);

            if (mapResourceResult.HasSucceeded == false)
            {
                const string error = "An error occurred while creating the Game's MapResources during Game setup.";
                logger.LogError("{Error} Errors: {Errors}", error, string.Join("; ", mapResourceResult.Errors));
                return ServiceResult<int>.Fail(mapResourceResult.Errors.Prepend(error));
            }

            // What the Players start out knowing of those MapResources. After the instances because it records the rows they were given ids by.
            ServiceResult<int> discoveryResult = await playerMapResourceDiscoveryService.SeedDefaultDiscoveriesAsync(
                context,
                gameId,
                [.. addedPlayers.Select(player => player.Id)],
                cancellationToken);

            if (discoveryResult.HasSucceeded == false)
            {
                const string error = "An error occurred while recording the Players' known MapResources during Game setup.";
                logger.LogError("{Error} Errors: {Errors}", error, string.Join("; ", discoveryResult.Errors));
                return ServiceResult<int>.Fail(discoveryResult.Errors.Prepend(error));
            }

            cancellationToken.ThrowIfCancellationRequested();

            //  Then create the player-specific campaign map entries for each Player
            ServiceResult<(IReadOnlyCollection<DomainPlayer> AddedPlayers, int RecordsGenerated)> generatePlayerCampaignMapsResult = 
                await GeneratePlayerCampaignMapsAsync(context, newGameIndex, addedPlayers, cancellationToken);

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

            // Unlock the starter templates for every player and place their starting units (settler + scout) at the map's authored start positions.
            ServiceResultBase starterUnitsResult = await SeedStarterUnitsAsync(context, gameId, mapProperties.Id, addedPlayers, cancellationToken);

            if (starterUnitsResult.HasSucceeded == false)
            {
                const string error = "An error occurred while creating the starting units during Game setup.";
                logger.LogError("{Error} Errors: {Errors}", error, string.Join("; ", starterUnitsResult.Errors));
                return ServiceResult<int>.Fail(starterUnitsResult.Errors.Prepend(error));
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
                CurrentRoundNumber = _FIRST_ROUND_NUMBER,
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
    /// Finalizes the setup process for a Game and opens its lobby, so Players can find it and claim a slot.
    /// </summary>
    /// <remarks>
    /// This does <b>not</b> start the Game. Setup finishes with every slot unclaimed and every Turn Queue Participant Pending, so there is
    /// nobody to take the first Turn and the Turn clock is deliberately left unset. The <i>last player to define themselves</i> is what starts
    /// play - not the last seat to be claimed, since a claimed seat nobody has filled the form in for has no identity to play as. See
    /// <c>UpdatePlayerDefinitionHandler</c> and <c>ICampaignRoundService.StartGameAsync</c>.
    /// </remarks>
    private static async Task CompleteGameSetupAsync(AscendantEmpireContext context, int gameId, CancellationToken cancellationToken)
    {
        await context.GameIndex
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(g => g
                .SetProperty(g => g.CreationStatus, GameCreationStatus.SetupComplete)
                .SetProperty(g => g.PlayStatus, GamePlayStatus.AwaitingPlayers),
                cancellationToken);
    }

    // Not static: the GameConfiguration row it writes alongside the GameIndex is seeded from the configured technology settings.
    private async Task<GameIndex> AddGameIndexAsync(
        AscendantEmpireContext context,
        SetupGameRequest request,
        CampaignMapGalleryProperties mapProperties,
        CancellationToken cancellationToken)
    {
        var newGame = new GameIndex
        {
            GameName = request.GameName,
            CampaignMapIndexId = request.GalleryMapId,
            NumberOfPlayers = request.NumberOfPlayers,
            CreationStatus = GameCreationStatus.SetupInProgress,
            PlayStatus = GamePlayStatus.SetupInProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            HorizontalWrap = mapProperties.HorizontalWrap,
            VerticalWrap = mapProperties.VerticalWrap,
            // Use the caller-supplied seed if provided; otherwise generate a random one so the player can reproduce the same biome distribution later if desired.
            GenerationSeed = request.GenerationSeed ?? Random.Shared.NextInt64(),
            BattleMapSize = request.BattleMapSize,
            // Snapshot the gallery map's dimensions onto the game. Tile ids decode back into coordinates using this width, so it has to stay fixed for the life of the game even
            // if the shared gallery map it was copied from is later resized.
            MapWidth = mapProperties.Width,
            MapHeight = mapProperties.Height,
        };

        context.GameIndex.Add(newGame);
        await context.SaveChangesAsync(cancellationToken);

        // Unconditionally, in the same setup rather than on first read: a lazily created settings row would mean every reader coalescing to a hard-coded
        // default, and two games created a week apart behaving differently with nothing in the data to explain why.
        //
        // The configured values are copied onto the row rather than read back from settings later, for the same reason: retuning the defaults must not
        // silently change the rules of a game that is already under way.
        TechnologySettings technologySettings = gameSettings.Value.TechnologySettings;

        context.GameConfigurations.Add(new GameConfiguration
        {
            GameId = newGame.Id,
            EmpireBaseResearchPoints = technologySettings.BaseResearchPoints,
            TechnologySpeedMultiplierPercent = technologySettings.TechnologySpeedMultiplierPercent,
            TechnologyFuzzinessPercent = technologySettings.TechnologyFuzzinessPercent,
            TechnologyCustomisation = TechnologyCustomisation.Identical,
            ResearchOverflowEnabled = false,
            UnspentResearchPointsBehaviour = UnspentResearchPointsBehaviour.Lost
        });

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
                RulerTitle = DomainPlayer.DEFAULT_RULER_TITLE,
                RulerName = $"Player {i}",
                FactionName = $"Faction {i}",
                // Placeholders in the same shape as the names above: a seeded slot has to read as *something* in the
                // lobby, and the setup dialog makes the player replace all four before it will save.
                PeopleName = $"People {i}",
                Demonym = $"Person {i}",
                CultureName = $"Culture {i}",
                Language = $"Language {i}",
                // Not a placeholder in the shape of the names above, but genuinely nothing: a portrait is the one value
                // that must be unique within the game once chosen, so seeding real ones would have the slots hold every
                // face between them before a single player had claimed anything.
                RulerPortraitPath = DomainPlayer.DEFAULT_RULER_PORTRAIT_PATH,
                Status = PlayerStatus.Active,
            });
        }

        context.Players.AddRange(playersToAdd);
        await context.SaveChangesAsync(cancellationToken);

        return playersToAdd;
    }

    /// <summary>
    /// Seeds every player's default officer rank ladders — one full ladder per seeded movement domain — from the code-defined <see cref="MilitaryOrganisationDefaults"/>.
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
    /// Generates every Player's technology requirements, grants their starting discoveries, and opens their research state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row is written for <b>every Player and every technology</b>, not just the reachable ones. That is what materialised per-Player requirements force:
    /// <c>RandomisedEqual</c> has to balance each Player's totals against every other's, which can only be done with the whole set in hand. The consequence
    /// is that the presence of a row means nothing - <c>Status</c> is the question.
    /// </para>
    /// <para>
    /// Round 0 for the starting discoveries, matching the asset and component unlocks: these were granted at setup, before the first Round opened.
    /// </para>
    /// <para>
    /// A missing technology catalogue fails setup loudly. A Player whose every technology is locked with no way in would otherwise be a silently unplayable
    /// game. (The method is self-saving.)
    /// </para>
    /// </remarks>
    private async Task<ServiceResultBase> SeedPlayerTechnologyAsync(
        AscendantEmpireContext context,
        GameIndex game,
        IReadOnlyCollection<DomainPlayer> addedPlayers,
        CancellationToken cancellationToken)
    {
        GameConfiguration configuration = await context.GameConfigurations.SingleAsync(c => c.GameId == game.Id, cancellationToken);

        ITechnologyRequirementGenerator? generator = technologyRequirementGenerators
            .SingleOrDefault(candidate => candidate.Customisation == configuration.TechnologyCustomisation);

        if (generator is null)
        {
            return ServiceResultBase.Fail($"No technology requirement generator is registered for customisation '{configuration.TechnologyCustomisation}'.");
        }

        List<Technology> catalogue = await context.Technologies.ToListAsync(cancellationToken);

        if (catalogue.Count == 0)
        {
            return ServiceResultBase.Fail(
                "The technology catalogue is empty. A game cannot start without it - run the Technologies database seed from the Entity Setup Tool.");
        }

        Dictionary<string, int> starterIdsByCode = catalogue
            .Where(technology => StarterTechnologyCodes.Contains(technology.Code))
            .ToDictionary(technology => technology.Code, technology => technology.Id);

        string[] missingStarterCodes = [.. StarterTechnologyCodes.Except(starterIdsByCode.Keys, StringComparer.OrdinalIgnoreCase)];

        if (missingStarterCodes.Length > 0)
        {
            return ServiceResultBase.Fail(
                $"Starter technolog(ies) not found in the catalogue: {string.Join(", ", missingStarterCodes)}. "
                + "A game cannot start without them - run the Technologies database seed from the Entity Setup Tool.");
        }

        IReadOnlyCollection<PlayerTechnology> rows = generator.Generate(catalogue, addedPlayers, configuration);

        HashSet<int> starterIds = [.. starterIdsByCode.Values];

        foreach (PlayerTechnology row in rows.Where(row => starterIds.Contains(row.TechnologyId)))
        {
            // Discovered outright, with the buckets filled to their requirement so the totals are consistent with the status rather than a special case every
            // reader has to know about. No acquisition round is stamped: a starting technology was never paid for.
            row.Status = TechnologyStatus.Discovered;
            row.DiscoveredOnRound = 0;
            row.DirectedPoints = row.DirectedRequired ?? 0;
            row.ExperiencePoints = row.ExperienceRequired ?? 0;
            row.DiffusionPoints = row.DiffusionRequired ?? 0;
        }

        context.PlayerTechnologies.AddRange(rows);

        context.PlayerResearchStates.AddRange(addedPlayers.Select(player => new PlayerResearchState
        {
            PlayerId = player.Id,
            GameId = game.Id,
            TechPointsPerPopulationSize = _DEFAULT_TECH_POINTS_PER_POPULATION_SIZE,
            UnspentDirectedPoints = 0,
            UnspentDiffusionPoints = 0
        }));

        await context.SaveChangesAsync(cancellationToken);

        // Flips the starters' dependents from Locked to Available. Left to the one owner of that question rather than worked out here, so there is a single
        // place it can be got wrong.
        int reconciled = await technologyAvailabilityService.ReconcileGameAsync(context, game.Id, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {RowCount} player technology row(s) for {PlayerCount} player(s) in game {GameId} using {Customisation} customisation, "
            + "granting {StarterCount} starting technolog(ies) each; {ReconciledCount} row(s) became available.",
            rows.Count,
            addedPlayers.Count,
            game.Id,
            configuration.TechnologyCustomisation,
            starterIds.Count,
            reconciled);

        return ServiceResultBase.Succeed();
    }

    /// <summary>
    /// Unlocks the starter templates (and their components) for every player, then spawns each player's starting units — one of each starter template — at the map's authored start positions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Starter templates are hand-authored global templates identified by <see cref="StarterTemplateCodes"/>; nothing creates them, so this reads the catalogue as it finds it. 
    /// Each must be unled (its compiled organisation tag declares <c>LeadershipModel.Unled</c>) because the unit's starting size is the tag's authored <c>UnledDefaultSize</c> 
    /// — a commissioned template has no equivalent single default.
    /// </para>
    /// <para>
    /// A missing template, missing snapshot, non-unled organisation, or missing start position fails game setup loudly rather than starting a game with no units. (The method is self-saving.)
    /// </para>
    /// </remarks>
    private async Task<ServiceResultBase> SeedStarterUnitsAsync(
        AscendantEmpireContext context,
        int gameId,
        int galleryMapId,
        IReadOnlyCollection<DomainPlayer> addedPlayers,
        CancellationToken cancellationToken)
    {
        // Load the starter templates (with their fitted components, for the unlock rows).
        List<UnitTemplate> starterTemplates = await context.UnitTemplates
            .Include(t => t.Components)
            .Where(t => t.Code != null && StarterTemplateCodes.Contains(t.Code))
            .ToListAsync(cancellationToken);

        // Compare case-insensitively: the database matches codes under its own (case-insensitive) collation, and the Unit Designer lower-cases the code it derives from
        // the display name — so a template saved as "settler" satisfies the "SETTLER" requirement and must not be reported missing. Nothing downstream may branch on the
        // casing of a starter code for the same reason.
        string[] missingCodes = [.. StarterTemplateCodes.Except(starterTemplates.Select(t => t.Code!), StringComparer.OrdinalIgnoreCase)];

        if (missingCodes.Length > 0)
        {
            return ServiceResultBase.Fail($"Starter template(s) not found in the catalogue: {string.Join(", ", missingCodes)}. A game cannot start without them — author a global unit template with each of these codes in the Unit Designer.");
        }

        // The compiled snapshots carry the resolved organisation tag, which supplies each unit's starting size.
        int[] starterTemplateIds = [.. starterTemplates.Select(t => t.Id)];

        List<CompiledUnitTemplate> compiledTemplates = await context.CompiledUnitTemplates
            .Include(c => c.OrganisationTag)
            .Where(c => starterTemplateIds.Contains(c.UnitTemplateId))
            .ToListAsync(cancellationToken);

        // Resolve each starter's spawn size and movement allowance up front so a misconfigured catalogue fails before anything is written.
        List<(UnitTemplate Template, int SpawnSize, long CampaignMovementPoints, long BattlefieldMovementPoints)> startersToSpawn = [];

        foreach (UnitTemplate template in starterTemplates)
        {
            CompiledUnitTemplate? compiled = compiledTemplates.FirstOrDefault(c => c.UnitTemplateId == template.Id);

            if (compiled is null)
            {
                return ServiceResultBase.Fail($"Starter template '{template.Code}' has no compiled snapshot — recompile the catalogue templates.");
            }

            if (compiled.OrganisationTag is not { LeadershipModel: LeadershipModel.Unled } organisationTag)
            {
                return ServiceResultBase.Fail($"Starter template '{template.Code}' is not unled — starting units must resolve to an unled organisation tag, which supplies their default size.");
            }

            if (organisationTag.UnledDefaultSize is not > 0)
            {
                return ServiceResultBase.Fail($"Starter template '{template.Code}' resolves to unled tag '{organisationTag.Code}', which has no authored UnledDefaultSize.");
            }

            // The spawn size is the authored one, never a figure fixed here: it is the catalogue's job to say how large a settler party or a scout starts,
            // and the check above has already established that the tag carries one.
            startersToSpawn.Add((template, organisationTag.UnledDefaultSize.Value, compiled.CampaignMovementPoints, compiled.BattlefieldMovementPoints));
        }

        // One start position per player, best-scored first (unscored positions come last).
        List<CampaignMapStartPosition> startPositions = await context.CampaignMapStartPositions
            .Where(sp => sp.CampaignMapGalleryIndexId == galleryMapId)
            .OrderByDescending(sp => sp.LocationScore ?? int.MinValue)
            .ThenBy(sp => sp.Id)
            .ToListAsync(cancellationToken);

        if (startPositions.Count < addedPlayers.Count)
        {
            return ServiceResultBase.Fail($"The map has {startPositions.Count} start position(s) but the game has {addedPlayers.Count} players — every player needs one.");
        }

        // UnitInstance FKs the per-game CampaignMap rows (identity keys), so resolve each start coordinate to
        // its actual tile row for this game rather than deriving an id from the coordinates.
        List<CampaignMap> startTiles = [];

        foreach (CampaignMapStartPosition position in startPositions.Take(addedPlayers.Count))
        {
            CampaignMap? tile = await context.CampaignMaps
                .FirstOrDefaultAsync(cm => cm.GameId == gameId && cm.XCoord == position.XCoord && cm.YCoord == position.YCoord, cancellationToken);

            if (tile is null)
            {
                return ServiceResultBase.Fail($"Start position ({position.XCoord}, {position.YCoord}) has no matching campaign map tile for game {gameId}.");
            }

            startTiles.Add(tile);
        }

        // Every player starts with the same unlocks: each starter template and all of its components.
        int[] starterComponentIds = [.. starterTemplates
            .SelectMany(t => t.Components.Select(c => c.UnitComponentId).Append(t.BodyComponentId))
            .Distinct()];

        // The starter asset catalogue rows, resolved by code. Unlike the templates these are seeded rather than hand-authored, so an unseeded catalogue is
        // the likely cause of a miss — say so, because a Player with no assets would otherwise be a silent empty panel.
        Dictionary<string, int> starterMetaCategoryIdsByCode = await context.AssetMetaCategories
            .Where(m => StarterAssetMetaCategoryCodes.Contains(m.Code))
            .ToDictionaryAsync(m => m.Code, m => m.Id, cancellationToken);

        Dictionary<string, int> starterAssetIdsByCode = await context.Assets
            .Where(r => StarterAssetCodes.Contains(r.Code))
            .ToDictionaryAsync(r => r.Code, r => r.Id, cancellationToken);

        string[] missingAssetCodes =
        [
            .. StarterAssetMetaCategoryCodes.Except(starterMetaCategoryIdsByCode.Keys, StringComparer.OrdinalIgnoreCase),
            .. StarterAssetCodes.Except(starterAssetIdsByCode.Keys, StringComparer.OrdinalIgnoreCase)
        ];

        if (missingAssetCodes.Length > 0)
        {
            return ServiceResultBase.Fail($"Starter asset(s) not found in the catalogue: {string.Join(", ", missingAssetCodes)}. A game cannot start without them — run the Assets database seed from the Entity Setup Tool.");
        }

        List<DomainPlayer> playersInOrder = [.. addedPlayers.OrderBy(p => p.Id)];

        // Collected across every player and handed to the lifecycle writer in one go below, rather than added to the context as they are built.
        List<UnitInstance> startingForces = [];

        for (int playerIndex = 0; playerIndex < playersInOrder.Count; playerIndex++)
        {
            DomainPlayer player = playersInOrder[playerIndex];
            CampaignMap startTile = startTiles[playerIndex];

            context.PlayerUnlockedComponents.AddRange(starterComponentIds.Select(componentId => new PlayerUnlockedComponent
            {
                PlayerId = player.Id,
                UnitComponentId = componentId,
                UnlockedOnTurn = 0,
            }));

            context.PlayerUnlockedTemplates.AddRange(starterTemplates.Select(template => new PlayerUnlockedTemplate
            {
                PlayerId = player.Id,
                UnitTemplateId = template.Id,
                UnlockedOnTurn = 0,
            }));

            // Round 0 rather than _FIRST_ROUND_NUMBER, matching the unlock rows above: these were granted at setup, before the first Round opened.
            context.PlayerUnlockedAssetMetaCategories.AddRange(starterMetaCategoryIdsByCode.Values.Select(metaCategoryId => new PlayerUnlockedAssetMetaCategory
            {
                PlayerId = player.Id,
                AssetMetaCategoryId = metaCategoryId,
                UnlockedOnRound = 0,
            }));

            context.PlayerUnlockedAssets.AddRange(starterAssetIdsByCode.Values.Select(assetId => new PlayerUnlockedAsset
            {
                PlayerId = player.Id,
                AssetId = assetId,
                UnlockedOnRound = 0,
            }));

            // One unit of each starter template, at the player's start tile, at its tag's default size, all healthy.
            startingForces.AddRange(startersToSpawn.Select(starter => new UnitInstance
            {
                GameId = gameId,
                PlayerId = player.Id,
                CampaignMapTileId = startTile.CampaignMapId,
                UnitTemplateId = starter.Template.Id,
                InitialStrength = starter.SpawnSize,
                HealthyCount = starter.SpawnSize,
                InjuredCount = 0,
                CampaignMovementRemaining = starter.CampaignMovementPoints,
                BattleMovementRemaining = starter.BattlefieldMovementPoints,

                // Setup opens the Queue on the first Round and StartGameAsync runs that Round's start-of-Turn pipeline straight afterwards.
                // Stamping the starters as already granted for it is what stops them being handed a second allowance before anybody has played.
                CampaignMovementReplenishedOnRound = _FIRST_ROUND_NUMBER,
            }));
        }

        // Through the lifecycle writer like every other Force coming into existence, but with no Turn. None is open until StartGameAsync runs, and nobody could have watched an army that was
        // standing there before the Game began, so the starting Forces are placed with no MovementHistory rows. Their first step records where they started.
        //
        // The writer saves, which also flushes the unlock rows added above - this method was already self-saving, so nothing changes for the caller.
        ServiceResultBase placeResult = await forceLifecycleWriter.CreateAsync(context, startingForces, playerTurnId: null, cancellationToken);

        if (placeResult.HasSucceeded == false)
        {
            return ServiceResultBase.Fail(placeResult.Errors);
        }

        logger.LogInformation("Spawned {UnitCount} starting unit(s) for each of {PlayerCount} player(s) in game {GameId}.",
            startersToSpawn.Count, playersInOrder.Count, gameId);

        return ServiceResultBase.Succeed();
    }

    /// <summary>
    /// Generates the CampaignMap for the Game by copying the relevant data from the specified map in the Map Gallery. 
    /// </summary>
    /// <remarks>
    /// <para>This is done via a SQL statement to efficiently copy all of the necessary rows in a single query, rather than loading them into memory and then re-inserting them.
    /// (The method is self-saving)</para>
    /// <para>
    /// Each tile's id is calculated from its coordinates rather than left to an IDENTITY column, so
    /// that <c>CampaignMapOffsetId + XCoord + YCoord * MapWidth</c> addresses a tile by construction.
    /// Letting the database number the rows made that formula depend on the order the query plan
    /// emitted them in, which is not the coordinate order the map code assumes.
    /// </para>
    /// <para>
    /// The block of ids is reserved by reading the current maximum. Only one game is set up at a
    /// time, and if that ever stops being true two overlapping setups collide on the primary key and
    /// fail outright rather than quietly interleaving their tiles.
    /// </para>
    /// </remarks>
    /// <returns>The number of rows inserted into the CampaignMap table.</returns>
    private async Task<ServiceResult<int>> GenerateCampaignMapAsync(
        AscendantEmpireContext context,
        GameIndex game,
        int campaignMapGalleryIndexId,
        CancellationToken cancellationToken)
    {
        int gameId = game.Id;

        bool alreadyHasMap = await context.CampaignMaps.AnyAsync(x => x.GameId == gameId, cancellationToken);

        if (alreadyHasMap)
        {
            logger.LogWarning("Attempted to generate Campaign Map for GameId {GameId}, but it already has CampaignMap rows.", gameId);
            return ServiceResult<int>.Fail($"GameId {gameId} already has CampaignMap rows.");
        }

        // Reserve this game's block of tile ids, starting immediately after every existing map.
        int? highestExistingTileId = await context.CampaignMaps.MaxAsync(x => (int?)x.CampaignMapId, cancellationToken);
        int campaignMapOffsetId = (highestExistingTileId ?? 0) + 1;
        int mapWidth = game.MapWidth;

        int inserted = await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO [game].[CampaignMap]
            (
                [CampaignMapId],
                [GameId],
                [XCoord],
                [YCoord],
                [FirstSeenOnTurnId],
                [FirstSeenByPlayerId],
                [LastVisitedOnTurnId],
                [LastVisitedByPlayerId],
                [BiomeTypeId],
                [CampaignMapGeographyTypeId],
                [SouthRiver],
                [EastRiver]
            )
            SELECT
                {campaignMapOffsetId} + g.[XCoord] + (g.[YCoord] * {mapWidth}),
                {gameId},
                g.[XCoord],
                g.[YCoord],
                NULL,
                NULL,
                NULL,
                NULL,
                g.[BiomeTypeId],
                g.[CampaignMapGeographyTypeId],
                g.[SouthRiver],
                g.[EastRiver]
            FROM [mapgallery].[CampaignMapGallery] g
            WHERE g.[CampaignMapGalleryIndexId] = {campaignMapGalleryIndexId};
            ", cancellationToken);

        // Record where the block starts, so the map code can convert between a tile id and its
        // coordinates without having to query for it.
        game.CampaignMapOffsetId = campaignMapOffsetId;
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<int>.Succeed(inserted);
    }

    /// <summary>
    /// Generates Player Campaign Map records for a specified Game and updates the provided Player collection with Campaign Offset identifiers.
    /// </summary>
    /// <remarks>
    /// <para>(The method is self-saving)</para>
    /// <para>
    /// Every player gets one contiguous block of ids laid out in the same row-major order as the
    /// world map, so a player's tile and the world tile beneath it sit at the same offset within
    /// their respective blocks. <c>ROW_NUMBER()</c> fixes each player's position in the sequence,
    /// which keeps the block a player receives independent of the order the rows are inserted in.
    /// </para>
    /// </remarks>
    /// <returns>The collection of Players (with updated Campaign Offset identifiers) and the number of records generated.</returns>
    private async Task<ServiceResult<(IReadOnlyCollection<DomainPlayer> AddedPlayers, int RecordsGenerated)>> GeneratePlayerCampaignMapsAsync(
        AscendantEmpireContext context,
        GameIndex game,
        IReadOnlyCollection<DomainPlayer> addedPlayers,
        CancellationToken cancellationToken)
    {
        int gameId = game.Id;

        bool alreadyHasRows = await context.PlayerCampaignMaps.AnyAsync(x => x.GameId == gameId, cancellationToken);

        if (alreadyHasRows)
        {
            logger.LogWarning("Attempted to generate PlayerCampaignMap rows for GameId {GameId}, but it already has PlayerCampaignMap rows.", gameId);
            return ServiceResult<(IReadOnlyCollection<DomainPlayer>, int)>.Fail($"GameId {gameId} already has PlayerCampaignMap rows.");
        }

        // Reserve a block per player, immediately after every existing player map. See the remarks on
        // GenerateCampaignMapAsync for why these ids are calculated rather than left to the database.
        int? highestExistingTileId = await context.PlayerCampaignMaps.MaxAsync(x => (int?)x.PlayerCampaignMapId, cancellationToken);
        int playerMapOffsetId = (highestExistingTileId ?? 0) + 1;
        int mapWidth = game.MapWidth;
        int tilesPerPlayer = game.MapWidth * game.MapHeight;

        int inserted = await context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO [game].[PlayerCampaignMap]
            (
                [PlayerCampaignMapId],
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
                {playerMapOffsetId} + ((p.[PlayerOrdinal] - 1) * {tilesPerPlayer}) + cm.[XCoord] + (cm.[YCoord] * {mapWidth}),
                cm.[GameId],
                cm.[CampaignMapId],
                p.[Id],
                cm.[XCoord],
                cm.[YCoord],
                CAST(0 AS INT),
                CAST(0 AS INT),
                NULL,
                NULL
            FROM [game].[CampaignMap] cm
            CROSS JOIN
            (
                SELECT [Id], ROW_NUMBER() OVER (ORDER BY [Id]) AS [PlayerOrdinal]
                FROM [game].[Player]
                WHERE [GameId] = {gameId}
            ) p
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
}
