using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Features.Game.Services;
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

        // Map export background service and its in-process channel.
        // The channel is singleton so the same instance is shared between
        // the handler that writes to it and the BackgroundService that reads from it.
        services.AddSingleton<IMapExportChannel, MapExportChannel>();
        services.AddHostedService<MapExportBackgroundService>();

        return services;
    }
}
