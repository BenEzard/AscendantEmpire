using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.RecompileAllUnitTemplates;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Dev design tool: recompiles the <c>CompiledUnitTemplate</c> snapshot of every template — global/common and
/// player-owned custom alike — refreshing them after catalogue component changes.
/// </summary>
/// <remarks>
/// <para>Save paths keep snapshots current for the template being saved, but editing a catalogue <c>component</c> silently
/// stales the snapshots of every template already built with it — this is the recovery pass for exactly that.</para>
/// <para>Best-effort per template: each compile commits independently (no umbrella transaction), so one template whose
/// assembly no longer validates is reported as a failure without losing the rest. Global templates are stamped with
/// turn <c>0</c>; player-owned ones with their game's current campaign round (also <c>0</c> if the campaign hasn't started).</para>
/// </remarks>
public record RecompileAllUnitTemplatesRequest : IRequest<ServiceResult<RecompileAllUnitTemplatesResult>>;

internal class RecompileAllUnitTemplatesHandler(
    ILogger<RecompileAllUnitTemplatesHandler> logger,
    AscendantEmpireContext context,
    IMediator mediator,
    ICampaignTurnService campaignTurnService
) : IRequestHandler<RecompileAllUnitTemplatesRequest, ServiceResult<RecompileAllUnitTemplatesResult>>
{
    public async Task<ServiceResult<RecompileAllUnitTemplatesResult>> Handle(RecompileAllUnitTemplatesRequest request, CancellationToken cancelToken)
    {
        // Only what the loop needs: the id to compile, the game for turn-stamping, and a label for failure messages.
        var templates = await context.UnitTemplates
            .Select(t => new { t.Id, t.GameId, t.Code, t.DisplayName })
            .ToListAsync(cancelToken);

        // Each game's current round is looked up once, however many of its templates are recompiled.
        Dictionary<int, int> roundByGameId = [];

        int recompiledCount = 0;
        List<string> failures = [];

        foreach (var template in templates)
        {
            int currentTurn = 0;

            // Player-owned templates are stamped with their game's current campaign round; global ones stay at the 0 sentinel.
            if (template.GameId is { } gameId)
            {
                if (!roundByGameId.TryGetValue(gameId, out currentTurn))
                {
                    currentTurn = await campaignTurnService.GetCurrentRoundNumberAsync(gameId, cancelToken);
                    roundByGameId[gameId] = currentTurn;
                }
            }

            ServiceResult<int> compiled = await mediator.Send(
                new CompileUnitTemplateRequest(UnitTemplateId: template.Id, CompiledOnTurn: currentTurn), cancelToken);

            if (compiled.HasSucceeded)
            {
                recompiledCount++;
            }
            else
            {
                // Global templates are best known by Code; custom ones have none, so fall back to name + id.
                string label = template.Code ?? $"{template.DisplayName} (id {template.Id})";
                failures.Add($"Template '{label}': {string.Join("; ", compiled.Errors)}");
            }
        }

        if (failures.Count > 0)
        {
            logger.LogWarning("Recompiled {RecompiledCount} unit template(s); {FailedCount} failed. Failures: {Failures}",
                recompiledCount, failures.Count, string.Join(" | ", failures));
        }
        else
        {
            logger.LogInformation("Recompiled all {RecompiledCount} unit template(s).", recompiledCount);
        }

        return ServiceResult<RecompileAllUnitTemplatesResult>.Succeed(new RecompileAllUnitTemplatesResult
        {
            RecompiledCount = recompiledCount,
            Failures = failures,
        });
    }
}
