using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Application.Features.DatabaseSeeding.Executors;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Maps.MapGallery.Executors;
using AscendantEmpire.Application.Features.Maps.MapGallery.Services;
using AscendantEmpire.Application.Features.UnitComponents.Executors;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;

using AscendantEmpire.Application.Common.Behaviours;
using AscendantEmpire.Application.Common.Validation;

using AscendantEmpire.Domain.Entities.Identity;

using MediatR;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AscendantEmpire.Application;

public static class MediatrStartup
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Request validation. Every validator in this assembly is discovered and registered, so a new one needs no line here — only a rule body beside the
        // request it validates.
        services.AddValidatorsFromAssembly(typeof(MediatrStartup).Assembly);

        // Runs those validators ahead of the handler, answering a 400 failure result rather than letting an invalid request through. Constrained to
        // responses that are a ServiceResultBase, so the container closes it only for requests that can carry a failure result.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        // Hashes and verifies user passwords. Stateless, so a singleton is fine.
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        // Manages revocable login sessions behind refresh tokens.
        services.AddScoped<IRefreshSessionService, RefreshSessionService>();

        services.AddScoped<ICampaignTurnService, CampaignTurnService>();
        services.AddScoped<ICampaignRoundService, CampaignRoundService>();
        services.AddScoped<ICampaignMapGalleryService, CampaignMapGalleryService>();
        services.AddScoped<IGameSetupService, GameSetupService>();
        services.AddScoped<IOpenGameService, OpenGameService>();
        services.AddScoped<IMovementProfileFactory, MovementProfileFactory>();

        // Force context action pipeline. Each IForceActionStrategy decides one toolbar action; the service builds the shared context once and
        // judges every strategy against it. Display order comes from ForceActionType's value, so registration order here does not matter.
        services.AddScoped<IForceContextActionService, ForceContextActionService>();
        services.AddScoped<IForceActionContextBuilder, ForceActionContextBuilder>();
        services.AddScoped<IForceActionStrategy, SplitForceStrategy>();
        services.AddScoped<IForceActionStrategy, CreateVillageStrategy>();
        services.AddScoped<IForceActionStrategy, EstablishCampStrategy>();

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

        // End of a Campaign Round — runs once, after the last Player's turn in the Round.
        services.AddScoped<IEndCampaignRoundProcess, HealTroops>();
        services.AddScoped<IEndCampaignRoundProcess, EvaluateVictoryConditions>();

        // Start of a Campaign Turn — runs for the Player whose turn is beginning.
        services.AddScoped<IStartCampaignTurnProcess, AlertSentries>();
        services.AddScoped<IStartCampaignTurnProcess, CheckUnitSupply>();
        services.AddScoped<IStartCampaignTurnProcess, ReplenishMovementPoints>();

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

        return services;
    }
}
