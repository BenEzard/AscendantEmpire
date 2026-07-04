using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetComponent;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Returns a single component in full (general fields, its body/weapon/armour extension, slots, tags and
/// supersession links) by code, for the dev Unit Designer's edit flow. Applies no game/player or unlock
/// filtering — it is the lossless read counterpart of the bulk-upsert write side.
/// </summary>
public record GetComponentRequest(string Code) : IRequest<ServiceResult<EditableComponentResult>>;

internal class GetComponentHandler(
    ILogger<GetComponentHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetComponentRequest, ServiceResult<EditableComponentResult>>
{
    public async Task<ServiceResult<EditableComponentResult>> Handle(GetComponentRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Project the whole graph in one query (mirrors DesignComponentProjections, but keeps the
            // body extension and supersession links the catalog read drops). A single Select avoids any
            // explicit Include and translates cleanly to SQL.
            EditableComponentResult? result = await context.UnitComponents
                .AsNoTracking()
                .Where(c => c.Code == request.Code)
                .Select(c => new EditableComponentResult
                {
                    Id = c.Id,
                    Component = new UnitComponentDto
                    {
                        Code = c.Code,
                        DisplayName = c.DisplayName,
                        Description = c.Description,
                        DesignerImagePath = c.DesignerImagePath,
                        ComponentType = c.ComponentType,
                        Weight = c.Weight,
                        Detection = c.Detection,
                        Visibility = c.Visibility,
                        Body = c.Body == null
                            ? null
                            : new UnitComponentBodyDto
                            {
                                MovementDomain = c.Body.MovementDomain,
                                CampaignMovementPoints = c.Body.CampaignMovementPoints,
                                BattlefieldMovementPoints = c.Body.BattlefieldMovementPoints,
                                Power = c.Body.Power,
                                Hitpoints = c.Body.Hitpoints,
                                MinimumUnitSize = c.Body.MinimumUnitSize,
                                MaximumUnitSize = c.Body.MaximumUnitSize,
                                SizeIncrement = c.Body.SizeIncrement,
                            },
                        Weapon = c.Weapon == null
                            ? null
                            : new UnitComponentWeaponDto
                            {
                                Attack = new DamageProfileDto
                                {
                                    Slash = c.Weapon.Attack.Slash,
                                    Pierce = c.Weapon.Attack.Pierce,
                                    Crush = c.Weapon.Attack.Crush,
                                    Burn = c.Weapon.Attack.Burn,
                                    Poison = c.Weapon.Attack.Poison,
                                },
                                MinimumRange = c.Weapon.MinimumRange,
                                MaximumRange = c.Weapon.MaximumRange,
                                DualWield = c.Weapon.DualWield,
                                DomainModifiers = c.Weapon.DomainModifiers
                                    .Select(m => new UnitComponentWeaponDomainModifierDto
                                    {
                                        Domain = m.Domain,
                                        DamageMultiplier = m.DamageMultiplier,
                                    })
                                    .ToList(),
                            },
                        Armour = c.Armour == null
                            ? null
                            : new UnitComponentArmourDto
                            {
                                Resistance = new DamageProfileDto
                                {
                                    Slash = c.Armour.Resistance.Slash,
                                    Pierce = c.Armour.Resistance.Pierce,
                                    Crush = c.Armour.Resistance.Crush,
                                    Burn = c.Armour.Resistance.Burn,
                                    Poison = c.Armour.Resistance.Poison,
                                },
                            },
                        // Ordered by Id to match the canonical slot order UnitTemplateAssembly uses when a
                        // template is saved (baseComponent.Slots.OrderBy(s => s.Id)), so a template loaded
                        // back into the designer can match its fitted components onto the right slot.
                        Slots = c.Slots
                            .OrderBy(s => s.Id)
                            .Select(s => new UnitComponentSlotDto
                            {
                                Id = s.Id,
                                DisplayName = s.DisplayName,
                                IsVisible = s.IsVisible,
                                IsLocked = s.IsLocked,
                                UIXPosition = s.UIXPosition,
                                UIYPosition = s.UIYPosition,
                                Rules = s.Rules
                                    .Select(r => new UnitComponentSlotRuleDto
                                    {
                                        RuleType = r.RuleType,
                                        Operator = r.Operator,
                                        Value = r.Value,
                                    })
                                    .ToList(),
                            })
                            .ToList(),
                        TagCodes = c.Tags.Select(t => t.Tag.Code).ToList(),
                    },
                    // This component is always the predecessor here, so surface only the successor side.
                    SupersededBy = c.SupersededBy
                        .Select(link => new ComponentSupersessionDto
                        {
                            PredecessorCode = c.Code,
                            SuccessorCode = link.Successor.Code,
                            IsRecommended = link.IsRecommended,
                        })
                        .ToList(),
                })
                .FirstOrDefaultAsync(cancelToken);

            if (result == null)
            {
                return ServiceResult<EditableComponentResult>.NotFound($"No component found with code '{request.Code}'.");
            }

            return ServiceResult<EditableComponentResult>.Succeed(result);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(GetComponentHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<EditableComponentResult>.InternalServerError(ex, error);
        }
    }
}
