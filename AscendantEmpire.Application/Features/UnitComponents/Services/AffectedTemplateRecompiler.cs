using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;

using MediatR;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Services;

/// <summary>
/// Refreshes the <c>CompiledUnitTemplate</c> snapshots of every template built on a component that has just
/// been edited.
/// </summary>
/// <remarks>
/// <para>A snapshot is a denormalised copy of a template's resolved stats, so editing a catalogue component
/// silently stales the snapshot of every template using it — and
/// <c>CompiledUnitTemplateComponent.UnitComponentSlotId</c> has no foreign key, so a removed slot leaves a
/// stale id behind rather than failing loudly. Template save paths keep their own snapshot current; this
/// closes the gap on the component side.</para>
/// <para>Used in two halves around the component save: collect the affected template ids <i>before</i>
/// <c>SaveChanges</c> (while the rows that link them still exist — removing a slot deletes the fitted rows
/// that identify its templates), then recompile <i>after</i>, so each compile reads committed state.</para>
/// <para>Best-effort per template, matching <c>RecompileAllUnitTemplatesHandler</c>: a template whose
/// assembly no longer validates is reported rather than failing the component save, which has already been
/// committed by then.</para>
/// </remarks>
internal static class AffectedTemplateRecompiler
{
    /// <summary>
    /// Finds every template that would be stale after the given components change — those built on one as
    /// their body, plus those with one fitted into a slot.
    /// </summary>
    /// <remarks>
    /// Must be called before the save: a slot removal deletes the <c>UnitTemplateComponent</c> rows that are
    /// the only link between a template and the component it had fitted.
    /// </remarks>
    public static async Task<List<int>> FindAffectedTemplateIdsAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<int> componentIds,
        CancellationToken cancelToken)
    {
        // A brand-new component has id 0 and cannot be referenced by anything yet.
        int[] persistedIds = [.. componentIds.Where(id => id != 0).Distinct()];

        if (persistedIds.Length == 0)
        {
            return [];
        }

        return await context.UnitTemplates
            .Where(template => persistedIds.Contains(template.BodyComponentId)
                || template.Components.Any(fitted => persistedIds.Contains(fitted.UnitComponentId)))
            .Select(template => template.Id)
            .Distinct()
            .ToListAsync(cancelToken);
    }

    /// <summary>
    /// Recompiles the given templates, stamping each with the right turn.
    /// </summary>
    /// <remarks>
    /// Global/common templates carry the <c>0</c> sentinel; player-owned ones take their game's current
    /// campaign round, looked up once per game however many of its templates are recompiled.
    /// </remarks>
    /// <returns>A description of each template that could not be recompiled; empty when all succeeded.</returns>
    public static async Task<List<string>> RecompileAsync(
        AscendantEmpireContext context,
        IMediator mediator,
        ICampaignTurnService campaignTurnService,
        IReadOnlyCollection<int> templateIds,
        CancellationToken cancelToken)
    {
        if (templateIds.Count == 0)
        {
            return [];
        }

        // Only what the loop needs: the game for turn-stamping and a label for failure messages.
        var templates = await context.UnitTemplates
            .Where(template => templateIds.Contains(template.Id))
            .Select(template => new { template.Id, template.GameId, template.Code, template.DisplayName })
            .ToListAsync(cancelToken);

        Dictionary<int, int> roundByGameId = [];
        List<string> failures = [];

        foreach (var template in templates)
        {
            int currentTurn = 0;

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

            if (!compiled.HasSucceeded)
            {
                // Global templates are best known by Code; custom ones have none, so fall back to name + id.
                string label = template.Code ?? $"{template.DisplayName} (id {template.Id})";
                failures.Add($"Template '{label}': {string.Join("; ", compiled.Errors)}");
            }
        }

        return failures;
    }
}
