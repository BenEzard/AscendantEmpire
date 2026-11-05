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
using AscendantEmpire.Application.Features.Settlements.Services;
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

        // Every validator in this assembly is discovered, so a new one needs no line here.
        services.AddValidatorsFromAssembly(typeof(ApplicationStartup).Assembly);

        // Registered first, which makes it the outermost behaviour, so it covers validation as well as the handler.
        services.AddTransient(typeof(IPipelineBehaviour<,>), typeof(ExceptionHandlingBehaviour<,>));
        services.AddTransient(typeof(IPipelineBehaviour<,>), typeof(ValidationBehaviour<,>));

        // Stateless, so a singleton.
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddScoped<IRefreshSessionService, RefreshSessionService>();

        // Its own hosted service rather than a job on the background pipeline below, because that pipeline runs user-initiated work and has no clock.
        services.AddHostedService<RefreshSessionPruningService>();

        services.AddScoped<ICampaignTurnService, CampaignTurnService>();
        services.AddScoped<ICampaignRoundService, CampaignRoundService>();
        services.AddScoped<ICampaignTurnAccessService, CampaignTurnAccessService>();
        services.AddScoped<IPlayerTurnService, PlayerTurnService>();

        // Lifecycle writers: the single writer for each of a Force's, a Structure's and a Battle's existence and position.
        services.AddScoped<IForceLifecycleWriter, ForceLifecycleWriter>();
        services.AddScoped<IStructureLifecycleWriter, StructureLifecycleWriter>();
        services.AddScoped<IBattleLifecycleWriter, BattleLifecycleWriter>();
        services.AddScoped<IBattleEngagementService, BattleEngagementService>();
        services.AddScoped<IPlayerBattleMapService, PlayerBattleMapService>();

        // Campaign map vision, rivers, ownership and territory.
        services.AddScoped<ISightDataReader, SightDataReader>();
        services.AddScoped<IFieldOfViewService, FieldOfViewService>();
        services.AddScoped<IWitnessService, WitnessService>();
        services.AddScoped<ICampaignRiverReader, CampaignRiverReader>();
        services.AddScoped<ICampaignOwnershipReader, CampaignOwnershipReader>();
        services.AddScoped<ICampaignTerritoryReader, CampaignTerritoryReader>();
        services.AddScoped<ITileTerritoryService, TileTerritoryService>();

        // Tile sustenance: what a tile yields, whether it can feed what stands on it, and what it regrows.
        services.AddScoped<IPlayerSustenanceValueReader, PlayerSustenanceValueReader>();
        services.AddScoped<IUnderfedForceResolver, StarvationUnderfedForceResolver>();
        services.AddScoped<ITileSupportEvaluator, TileSupportEvaluator>();
        services.AddScoped<IBiomeRegenerationValueReader, BiomeRegenerationValueReader>();

        services.AddScoped<ICoalitionResolver, SoloCoalitionResolver>();

        // Sighting folds, run in this order by the RaiseSightingReports end-of-Turn process.
        services.AddScoped<IForceSightingFoldService, ForceSightingFoldService>();
        services.AddScoped<IStructureSightingFoldService, StructureSightingFoldService>();
        services.AddScoped<ICombatSightingFoldService, CombatSightingFoldService>();

        services.AddScoped<ICampaignMapGalleryService, CampaignMapGalleryService>();
        services.AddScoped<IGameSetupService, GameSetupService>();
        services.AddScoped<IGameContextService, GameContextService>();

        // Scoped so it shares the calling handler's context, which is what lets its placement writes join that handler's transaction.
        services.AddScoped<IBiomeImpliedMapResourceService, BiomeImpliedMapResourceService>();

        // Asset banks.
        services.AddScoped<IAssetBankReader, AssetBankReader>();
        services.AddScoped<IAssetBankDrawdownService, AssetBankDrawdownService>();
        services.AddScoped<IAssetBankProvisioningService, AssetBankProvisioningService>();
        services.AddScoped<IAssetNetworkConnectivityService, AssetNetworkConnectivityService>();
        services.AddScoped<IAssetBankConquestService, AssetBankConquestService>();
        services.AddScoped<IAssetBankCapacityService, AssetBankCapacityService>();
        services.AddScoped<IAssetBankDeliveryService, AssetBankDeliveryService>();
        services.AddScoped<IPlayerAssetQuantityReader, PlayerAssetQuantityReader>();

        services.AddScoped<ICampParentResolver, CampParentResolver>();

        // Force context action pipeline. Display order comes from ForceActionType's value, so registration order here does not matter.
        services.AddScoped<IForceContextActionService, ForceContextActionService>();
        services.AddScoped<IForceActionContextBuilder, ForceActionContextBuilder>();
        services.AddScoped<IForceActionStrategy, SplitForceStrategy>();
        services.AddScoped<IForceActionStrategy, CreateVillageStrategy>();
        services.AddScoped<IForceActionStrategy, EstablishCampStrategy>();
        services.AddScoped<IForceActionStrategy, BattleStrategy>();

        // Movement domain rules. Every DomainType needs an entry here - a domain with no strategy has no rule to be judged against, and its every move is refused.
        services.AddScoped<IMovementDomainStrategy, LandMovementStrategy>();
        services.AddScoped<IMovementDomainStrategy, WaterMovementStrategy>();
        services.AddScoped<IMovementDomainStrategy, BelowWaterMovementStrategy>();
        services.AddScoped<IMovementDomainStrategy, AirMovementStrategy>();
        services.AddScoped<IMovementDomainStrategy, NearEarthOrbitMovementStrategy>();

        // Possible action pipeline.
        services.AddScoped<IEndCampaignTurnPossibleActionService, EndCampaignTurnPossibleActionService>();
        services.AddScoped<IPossibleActionCheck, CampaignRemainingMovementCheck>();
        services.AddScoped<IPossibleActionCheck, BattleRemainingMovementCheck>();
        services.AddScoped<IPossibleActionCheck, ResearchSelectionCheck>();
        services.AddScoped<IPossibleActionCheck, TradeExpiryCheck>();

        // Turn pipelines. One open generic covers every pipeline, so a new pipeline needs no registration of its own - only its processes. A process left
        // unregistered fails silently, because the IEnumerable simply resolves without it. Registration order does not matter: each pipeline runs in
        // ExecutionOrder, which every process reads from EndCampaignTurnProcessOrder or one of its siblings.
        services.AddScoped(typeof(ITurnServicePipeline<>), typeof(TurnServicePipeline<>));

        // End of a Campaign Turn — runs for the Player whose turn is ending.
        services.AddScoped<IEndCampaignTurnProcess, Gathering>();
        services.AddScoped<IEndCampaignTurnProcess, BuildingStructure>();
        services.AddScoped<IEndCampaignTurnProcess, DeliverCampProduction>();
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

        // Generic background processing pipeline. The channel is a singleton so the handlers that enqueue jobs and the dispatcher that drains them share one
        // instance; each job's executor is resolved per-job from a fresh scope.
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

        // Each selectable part of the database seed is one ISeedStep, and the seed executor and the GET /system/seed/targets catalogue enumerate this same
        // collection, so the dialog's dependency graph and the order the seed runs in cannot drift apart. Registration order is not the run order -
        // SeedTargetResolver sorts by group and sort order, then by dependency.
        services.AddScoped<ISeedStep, GeographySeedStep>();
        services.AddScoped<ISeedStep, BiomeSeedStep>();
        services.AddScoped<ISeedStep, MapResourceCategorySeedStep>();
        services.AddScoped<ISeedStep, TagTypeSeedStep>();
        services.AddScoped<ISeedStep, TagSeedStep>();
        services.AddScoped<ISeedStep, UnitComponentSeedStep>();
        services.AddScoped<ISeedStep, StarterTemplateSeedStep>();
        services.AddScoped<ISeedStep, StorageClassSeedStep>();
        services.AddScoped<ISeedStep, AssetSeedStep>();
        services.AddScoped<ISeedStep, ExperienceTypeSeedStep>();
        services.AddScoped<ISeedStep, TechnologySeedStep>();
        services.AddScoped<ISeedStep, MapResourceTypeSeedStep>();
        services.AddScoped<ISeedStep, StructureSeedStep>();

        // One technology requirement generator per customisation mode, so the selector finds something for every member of the enum and an unimplemented mode
        // refuses by name rather than failing as "nothing registered". Implementing a mode means swapping its registration for a real generator.
        services.AddScoped<ITechnologyRequirementGenerator, IdenticalTechnologyRequirementGenerator>();
        services.AddScoped<ITechnologyRequirementGenerator>(
            _ => new UnimplementedTechnologyRequirementGenerator(TechnologyCustomisation.RandomisedEqual));
        services.AddScoped<ITechnologyRequirementGenerator>(
            _ => new UnimplementedTechnologyRequirementGenerator(TechnologyCustomisation.FullyRandomised));

        services.AddScoped<IPlayerTechnologyAvailabilityService, PlayerTechnologyAvailabilityService>();

        return services;
    }
}
