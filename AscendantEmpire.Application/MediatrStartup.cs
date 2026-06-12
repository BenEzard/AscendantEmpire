using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Maps.MapGallery.Executors;
using AscendantEmpire.Application.Features.Maps.MapGallery.Services;
using AscendantEmpire.Application.Features.TroopOrders.Movement;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;

using Microsoft.Extensions.DependencyInjection;

namespace AscendantEmpire.Application;

public static class MediatrStartup
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICampaignTurnService, CampaignTurnService>();
        services.AddScoped<ICampaignMapGalleryService, CampaignMapGalleryService>();
        services.AddScoped<IGameSetupService, GameSetupService>();
        services.AddScoped<IMovementProfileFactory, MovementProfileFactory>();

        // Possible action pipeline
        services.AddScoped<IEndCampaignTurnPossibleActionService, EndCampaignTurnPossibleActionService>();
        services.AddScoped<IPossibleActionCheck, CampaignRemainingMovementCheck>();
        services.AddScoped<IPossibleActionCheck, BattleRemainingMovementCheck>();
        services.AddScoped<IPossibleActionCheck, ResearchSelectionCheck>();
        services.AddScoped<IPossibleActionCheck, TradeExpiryCheck>();

        // Generic background processing pipeline.
        // The channel is singleton so the same instance is shared between the handlers
        // that enqueue jobs and the dispatcher BackgroundService that drains them.
        // Each ProcessType is handled by an IProcessExecutor resolved per-job from a fresh scope.
        services.AddSingleton<IBackgroundProcessingChannel, BackgroundProcessingChannel>();
        services.AddHostedService<BackgroundProcessingService>();
        services.AddScoped<IProcessExecutor, MapExportExecutor>();
        services.AddScoped<IProcessExecutor, GenerateCampaignMapExecutor>();
        services.AddScoped<IProcessExecutor, RenderCampaignMapPreviewExecutor>();

        return services;
    }
}
