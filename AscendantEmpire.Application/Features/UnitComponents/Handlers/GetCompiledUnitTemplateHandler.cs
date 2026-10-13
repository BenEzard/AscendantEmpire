using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.UnitDesigns.GetCompiledUnitTemplate;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Returns the compiled snapshot of a saved template. Deliberately <b>not</b> unlock-filtered,
/// so it can render the player's own designs as well as enemy designs containing components the viewer never unlocked.
/// </summary>
public record GetCompiledUnitTemplateRequest(int UnitTemplateId) : IRequest<ServiceResult<CompiledUnitTemplateView>>;

internal class GetCompiledUnitTemplateHandler(
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetCompiledUnitTemplateRequest, ServiceResult<CompiledUnitTemplateView>>
{
    public async Task<ServiceResult<CompiledUnitTemplateView>> Handle(GetCompiledUnitTemplateRequest request, CancellationToken cancelToken)
    {
        if (request.UnitTemplateId <= 0)
        {
            return ServiceResult<CompiledUnitTemplateView>.Fail("A valid template id is required.");
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        CompiledUnitTemplateView? view = await context.CompiledUnitTemplates
            .Where(c => c.UnitTemplateId == request.UnitTemplateId)
            .Select(c => new CompiledUnitTemplateView
            {
                UnitTemplateId = c.UnitTemplateId,
                DisplayName = c.DisplayName,
                MapImagePath = c.MapImagePath,
                BodyComponentId = c.BodyComponentId,
                MovementDomain = c.MovementDomain,
                TotalWeight = c.TotalWeight,
                TotalSight = c.TotalSight,
                TotalDetection = c.TotalDetection,
                TotalVisibility = c.TotalVisibility,
                CampaignMovementPoints = c.CampaignMovementPoints,
                BattlefieldMovementPoints = c.BattlefieldMovementPoints,
                Power = c.Power,
                Hitpoints = c.Hitpoints,
                Attack = new CompiledDamageProfile
                {
                    Slash = c.Attack.Slash,
                    Pierce = c.Attack.Pierce,
                    Crush = c.Attack.Crush,
                    Burn = c.Attack.Burn,
                    Poison = c.Attack.Poison,
                },
                Resistance = new CompiledDamageProfile
                {
                    Slash = c.Resistance.Slash,
                    Pierce = c.Resistance.Pierce,
                    Crush = c.Resistance.Crush,
                    Burn = c.Resistance.Burn,
                    Poison = c.Resistance.Poison,
                },
                CompiledOnTurn = c.CompiledOnTurn,
                Components = c.Components
                    .Select(component => new CompiledComponentView
                    {
                        UnitComponentSlotId = component.UnitComponentSlotId,
                        UnitComponentId = component.UnitComponentId,
                        Code = component.Code,
                        DisplayName = component.DisplayName,
                        ComponentType = component.ComponentType,
                    })
                    .ToList(),
                Capabilities = c.Capabilities
                    .Select(capability => new CompiledCapabilityView
                    {
                        TagCode = capability.TagCode,
                        DisplayName = capability.DisplayName,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancelToken);

        if (view is null)
        {
            return ServiceResult<CompiledUnitTemplateView>.NotFound(
                $"No compiled snapshot exists for template {request.UnitTemplateId}.");
        }

        return ServiceResult<CompiledUnitTemplateView>.Succeed(view);
    }
}
