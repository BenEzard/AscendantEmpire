using AscendantEmpire.Application.Features.TroopOrders.Movement;

using Microsoft.Extensions.DependencyInjection;

namespace AscendantEmpire.Application;

public static class MediatrStartup
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IMovementProfileFactory, MovementProfileFactory>();

        return services;
    }
}
