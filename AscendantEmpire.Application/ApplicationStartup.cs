using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Application.Features.Battles.Services;
using AscendantEmpire.Application.Features.DatabaseSeeding.Executors;
using AscendantEmpire.Application.Features.DatabaseSeeding.Steps;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Application.Features.Game.Executors;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Intelligence.Services;
using AscendantEmpire.Application.Features.Maps.BattleMaps.Services;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Features.Maps.MapGallery.Executors;
using AscendantEmpire.Application.Features.Maps.MapGallery.Services;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Features.UnitComponents.Executors;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Structures.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;
using AscendantEmpire.Application.Features.Units.Services;

using AscendantEmpire.Application.Common.Behaviours;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;

using AscendantEmpire.Domain.Entities.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AscendantEmpire.Application;

public static class ApplicationStartup
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Request validation. Every validator in this assembly is discovered and registered, so a new one needs no line here — only a rule body beside the
        // request it validates.
        services.AddValidatorsFromAssembly(typeof(ApplicationStartup).Assembly);

        // Turns anything a handler throws into a logged 500 failure result carrying a correlation id. Registered first, which makes it the outermost
        // behaviour, so it covers validation and every other behaviour as well as the handler itself.
        services.AddTransient(typeof(IPipelineBehaviour<,>), typeof(ExceptionHandlingBehaviour<,>));

        // Runs those validators ahead of the handler, answering a 400 failure result rather than letting an invalid request through. Constrained to
        // responses that are a ServiceResultBase, so the container closes it only for requests that can carry a failure result.
        services.AddTransient(typeof(IPipelineBehaviour<,>), typeof(ValidationBehaviour<,>));

        // Hashes and verifies user passwords. Stateless, so a singleton is fine.
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        // Manages revocable login sessions behind refresh tokens.
        services.AddScoped<IRefreshSessionService, RefreshSessionService>();

        // Housekeeping for the table that service writes to: a timer that deletes sessions whose expiry passed longer ago than the retention. Its own
        // hosted service rather than a job on the background pipeline below, because the pipeline runs user-initiated work and has no clock.
        services.AddHostedService<RefreshSessionPruningService>();

        services.AddScoped<ICampaignTurnService, CampaignTurnService>();
        services.AddScoped<ICampaignRoundService, CampaignRoundService>();

        // The membership-and-open-Turn check both Campaign Turn requests start with: ending a Turn, and reading what is still outstanding on one.
        services.AddScoped<ICampaignTurnAccessService, CampaignTurnAccessService>();

        // Opens and closes PlayerTurn rows, and hands out the sequence numbers the raw activity feeds order themselves by. Lane-agnostic, so the Battle
        // pipeline can use it unchanged once it exists.
        services.AddScoped<IPlayerTurnService, PlayerTurnService>();

        // The single writer for where a Force stands and whether it exists. Every create, move and destroy goes through it so that each one is recorded in MovementHistory, stamped with a
        // sequence from the Turn it happened in.
        services.AddScoped<IForceLifecycleWriter, ForceLifecycleWriter>();

        // Its Structure sibling: the single writer for founding, construction, capture and razing, recording each in StructureHistory against the same Turn counter. Nothing in play calls it yet.
        services.AddScoped<IStructureLifecycleWriter, StructureLifecycleWriter>();

        // Its Battle sibling: the single writer for an engagement commencing, gaining or losing a side, and concluding, recording each in BattleHistory against the same Turn counter. Only
        // commencing is called in play so far - a campaign step onto an enemy-held tile starts a Battle - and there is no combat subsystem to join, leave or conclude one.
        services.AddScoped<IBattleLifecycleWriter, BattleLifecycleWriter>();

        // Starts a Battle when a Force steps onto another Player's Forces: generates or reuses the battle map, places both sides on it, and commences the engagement through the writer above.
        services.AddScoped<IBattleEngagementService, BattleEngagementService>();

        // Each battle participant's view of the battle map (game.PlayerBattleMap): initialises their rows when a Battle commences, and explores the map for whoever is given vision of it.
        services.AddScoped<IPlayerBattleMapService, PlayerBattleMapService>();

        // Campaign map vision. The sight rules themselves are pure (FieldOfView); these are the services around them. ISightDataReader holds every query the
        // rules need; IFieldOfViewService answers "what can these Observers see", for the live map read; IWitnessService answers the inverted question
        // intelligence asks, "who could see this tile at this point in the Turn", through the same field-of-view service so the two cannot drift apart.
        services.AddScoped<ISightDataReader, SightDataReader>();
        services.AddScoped<IFieldOfViewService, FieldOfViewService>();
        services.AddScoped<IWitnessService, WitnessService>();

        // Rivers on the campaign map a Player is shown. Not a sight concern - a river is terrain, reported for any explored tile whether or not something is
        // watching it - so it is its own reader rather than another method on ISightDataReader.
        services.AddScoped<ICampaignRiverReader, CampaignRiverReader>();
        services.AddScoped<ICampaignOwnershipReader, CampaignOwnershipReader>();

        // Who holds the ground, and the per-Round pass that decides it. The rules are pure (TileTerritory, in the shape of FieldOfView); these are the reader
        // that loads a whole Game's tiles and the service that writes the diff back. ITileTerritoryService is the single writer of the ownership columns and
        // the claim junction, so the end-of-Round pass and a Structure asserting over its ground cannot disagree about what ownership does to a claim.
        services.AddScoped<ICampaignTerritoryReader, CampaignTerritoryReader>();
        services.AddScoped<ITileTerritoryService, TileTerritoryService>();

        // A Player's effective Forage, Hunt, Fish and Fodder values: each biome's base with the Player's sustenance modifiers applied. The only place that formula is used, so depletion,
        // replenishment and the tile toolbar cannot disagree. Nothing writes modifiers yet.
        services.AddScoped<IPlayerSustenanceValueReader, PlayerSustenanceValueReader>();

        // What happens to a Force that did not get all the food it needed at the end of its Turn, called by the Gathering process below. Only logs until a damage model for
        // partial feeding exists.
        services.AddScoped<IUnderfedForceResolver, StarvationUnderfedForceResolver>();

        // Whether a tile can feed the Player's Forces on it (and any selected Forces about to move there), for the tile toolbar's background and the hover marker. Shares
        // Gathering's pool rules, so the colour shown and the feed itself cannot disagree.
        services.AddScoped<ITileSupportEvaluator, TileSupportEvaluator>();

        // Each biome's Regeneration Percent and base values: what an unclaimed tile regrows against at the end of a Round. Shared by the end-of-Round regrowth and the support
        // forecast, so the Turns a tile is shown to last and the regrowth itself cannot disagree.
        services.AddScoped<IBiomeRegenerationValueReader, BiomeRegenerationValueReader>();

        // Which Players share vision. Every Player is a coalition of one until a foreign relations model exists to say otherwise.
        services.AddScoped<ICoalitionResolver, SoloCoalitionResolver>();

        // The sightings step: folds a Turn's movement history through the witness service into reports and known-Force memories for the Players who watched. Run by the RaiseSightingReports
        // end-of-turn process below.
        services.AddScoped<IForceSightingFoldService, ForceSightingFoldService>();

        // Its Structure half: folds the Turn's structure history the same way into structure reports and known-Structure memories. Run by the same process, straight after the Force step.
        services.AddScoped<IStructureSightingFoldService, StructureSightingFoldService>();

        // Its combat half: folds the Turn's battle history into combat reports, and no memories - an engagement is an event, not a thing to remember the position of. Run by the same process after
        // the Structure step. A service rather than pipeline logic because the design gives it a second host, end of battle Turn, which is not built yet.
        services.AddScoped<ICombatSightingFoldService, CombatSightingFoldService>();

        services.AddScoped<ICampaignMapGalleryService, CampaignMapGalleryService>();
        services.AddScoped<IGameSetupService, GameSetupService>();
        services.AddScoped<IGameContextService, GameContextService>();

        // Pre-assigns the MapResources a tile's Biome implies - Timber on Forest, Jungle and Woodland - when a map is generated, and keeps them in step when
        // the map editor's Biome brush repaints a tile. Which Biomes count is read from the type's habitats, so a habitat added in the MapResource Type
        // Manager takes effect with no code change. Scoped so it shares the calling handler's context, which is what lets its placement writes join that
        // handler's transaction.
        services.AddScoped<IBiomeImpliedMapResourceService, BiomeImpliedMapResourceService>();

        // Asset banks. IAssetBankReader is the one place a bank's balances are read, in either of the two shapes the design doc calls for - one bank, or all
        // of a Player's - so what a settlement panel shows and what a summary ticker adds up cannot drift apart.
        services.AddScoped<IAssetBankReader, AssetBankReader>();

        // The single spender: every build order, technology purchase and upkeep charge debits through here, which is what makes "the Player can afford this"
        // one answer rather than several. It routes on Asset.StorageScope and works on the caller's context without saving, so a debit and whatever it paid
        // for commit together.
        services.AddScoped<IAssetBankDrawdownService, AssetBankDrawdownService>();

        // The one place a Settlement is given the bank it is born with, called from inside the settle transaction so a founding and its bank land together.
        services.AddScoped<IAssetBankProvisioningService, AssetBankProvisioningService>();

        // Resolves what a Player holds of each Asset for the read path, routing on the same Asset.StorageScope column the drawdown routes a debit on, so the
        // figure a ticker reports and the figure a purchase is checked against cannot come from different places.
        services.AddScoped<IPlayerAssetQuantityReader, PlayerAssetQuantityReader>();

        // Force context action pipeline. Each IForceActionStrategy decides one toolbar action; the service builds the shared context once and
        // judges every strategy against it. Display order comes from ForceActionType's value, so registration order here does not matter.
        services.AddScoped<IForceContextActionService, ForceContextActionService>();
        services.AddScoped<IForceActionContextBuilder, ForceActionContextBuilder>();
        services.AddScoped<IForceActionStrategy, SplitForceStrategy>();
        services.AddScoped<IForceActionStrategy, CreateVillageStrategy>();
        services.AddScoped<IForceActionStrategy, EstablishCampStrategy>();
        services.AddScoped<IForceActionStrategy, BattleStrategy>();

        // Movement domain rules. One IMovementDomainStrategy per DomainType decides which terrain a Force of that domain may enter; the step handler
        // resolves the one matching the Force's compiled template. Every DomainType needs an entry here — a domain with no strategy has no rule to be
        // judged against, and its every move is refused.
        services.AddScoped<IMovementDomainStrategy, LandMovementStrategy>();
        services.AddScoped<IMovementDomainStrategy, WaterMovementStrategy>();
        services.AddScoped<IMovementDomainStrategy, BelowWaterMovementStrategy>();
        services.AddScoped<IMovementDomainStrategy, AirMovementStrategy>();
        services.AddScoped<IMovementDomainStrategy, NearEarthOrbitMovementStrategy>();

        // Possible action pipeline
        services.AddScoped<IEndCampaignTurnPossibleActionService, EndCampaignTurnPossibleActionService>();
        services.AddScoped<IPossibleActionCheck, CampaignRemainingMovementCheck>();
        services.AddScoped<IPossibleActionCheck, BattleRemainingMovementCheck>();
        services.AddScoped<IPossibleActionCheck, ResearchSelectionCheck>();
        services.AddScoped<IPossibleActionCheck, TradeExpiryCheck>();

        // Turn pipelines. One open generic covers every pipeline: TurnServicePipeline<T> just collects the IEnumerable<T> registered below and runs
        // them in ExecutionOrder, so a new pipeline needs no registration of its own — only its processes.
        //
        // A pipeline with no processes registered is legal: the IEnumerable resolves empty and the pipeline succeeds having done nothing. That is what
        // IStartCampaignRoundProcess is today. It also means a process left unregistered fails silently rather than loudly, so anything added under
        // TurnPipeline/ needs a line here to actually run.
        //
        // Registration order below does not matter. Each pipeline runs in ExecutionOrder, and every process reads that from its pipeline's order enum
        // — EndCampaignTurnProcessOrder and its siblings — which is where the sequence is decided and changed.
        services.AddScoped(typeof(ITurnServicePipeline<>), typeof(TurnServicePipeline<>));

        // End of a Campaign Turn — runs for the Player whose turn is ending.
        services.AddScoped<IEndCampaignTurnProcess, Gathering>();
        services.AddScoped<IEndCampaignTurnProcess, BuildingStructure>();
        services.AddScoped<IEndCampaignTurnProcess, Fortifying>();
        services.AddScoped<IEndCampaignTurnProcess, EvaluateForceOnHostileTerrain>();
        services.AddScoped<IEndCampaignTurnProcess, ApplyResearch>();
        services.AddScoped<IEndCampaignTurnProcess, RaiseSightingReports>();

        // End of a Campaign Round — runs once, after the last Player's turn in the Round.
        services.AddScoped<IEndCampaignRoundProcess, HealTroops>();
        services.AddScoped<IEndCampaignRoundProcess, RegenerateTileSustenance>();
        services.AddScoped<IEndCampaignRoundProcess, ResolveTileTerritory>();
        services.AddScoped<IEndCampaignRoundProcess, EvaluateVictoryConditions>();

        // Start of a Campaign Turn — runs for the Player whose turn is beginning.
        services.AddScoped<IStartCampaignTurnProcess, AlertSentries>();
        services.AddScoped<IStartCampaignTurnProcess, CheckUnitSupply>();
        services.AddScoped<IStartCampaignTurnProcess, ReplenishMovementPoints>();
        services.AddScoped<IStartCampaignTurnProcess, GenerateResearchPoints>();

        // Generic background processing pipeline.
        // The channel is singleton so the same instance is shared between the handlers
        // that enqueue jobs and the dispatcher BackgroundService that drains them.
        // Each ProcessType is handled by an IProcessExecutor resolved per-job from a fresh scope.
        services.AddSingleton<IBackgroundProcessingChannel, BackgroundProcessingChannel>();
        services.AddHostedService<BackgroundProcessingService>();
        services.AddScoped<IProcessExecutor, MapExportExecutor>();
        services.AddScoped<IProcessExecutor, MapImportExecutor>();
        services.AddScoped<IProcessExecutor, GenerateCampaignMapExecutor>();
        services.AddScoped<IProcessExecutor, RenderCampaignMapPreviewExecutor>();
        services.AddScoped<IProcessExecutor, CatalogueExportExecutor>();
        services.AddScoped<IProcessExecutor, CatalogueImportExecutor>();
        services.AddScoped<IProcessExecutor, DatabaseSeedExecutor>();
        services.AddScoped<IProcessExecutor, GameDeleteExecutor>();

        // Each selectable part of the database seed is one ISeedStep.
        // The seed executor and the GET /system/seed/targets catalogue enumerate this same collection, so the dependency graph the selection dialog draws
        // and the order the seed runs in can never drift apart.
        // Registration order is not the run order — SeedTargetResolver sorts by group and sort order, then by dependency.
        services.AddScoped<ISeedStep, GeographySeedStep>();
        services.AddScoped<ISeedStep, BiomeSeedStep>();
        services.AddScoped<ISeedStep, MapResourceCategorySeedStep>();
        services.AddScoped<ISeedStep, TagTypeSeedStep>();
        services.AddScoped<ISeedStep, TagSeedStep>();
        services.AddScoped<ISeedStep, UnitComponentSeedStep>();
        services.AddScoped<ISeedStep, StarterTemplateSeedStep>();
        services.AddScoped<ISeedStep, AssetSeedStep>();
        services.AddScoped<ISeedStep, ExperienceTypeSeedStep>();
        services.AddScoped<ISeedStep, TechnologySeedStep>();
        services.AddScoped<ISeedStep, MapResourceTypeSeedStep>();
        services.AddScoped<ISeedStep, StructureSeedStep>();

        // One technology requirement generator per customisation mode, so the selector finds something for every member of the enum and an unimplemented
        // mode refuses by name rather than failing as "nothing registered". Implementing a mode means swapping its registration for a real generator.
        services.AddScoped<ITechnologyRequirementGenerator, IdenticalTechnologyRequirementGenerator>();
        services.AddScoped<ITechnologyRequirementGenerator>(
            _ => new UnimplementedTechnologyRequirementGenerator(TechnologyCustomisation.RandomisedEqual));
        services.AddScoped<ITechnologyRequirementGenerator>(
            _ => new UnimplementedTechnologyRequirementGenerator(TechnologyCustomisation.FullyRandomised));

        services.AddScoped<IPlayerTechnologyAvailabilityService, PlayerTechnologyAvailabilityService>();

        return services;
    }
}
