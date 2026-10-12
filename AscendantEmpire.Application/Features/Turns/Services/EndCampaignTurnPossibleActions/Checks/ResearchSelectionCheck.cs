using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Technologies;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;

/// <summary>
/// Flags when the Player has research they could be doing and no project chosen to put it into.
/// </summary>
/// <remarks>
/// Both halves matter. An unselected project is only worth raising if there is something to select: a Player whose every technology is still Locked - which
/// is every Player who has discovered nothing and sits behind the <c>Broken</c> placeholder root - would otherwise be nagged every Round about a choice they
/// cannot make. The check therefore asks whether anything is at Available or better, not merely whether the catalogue is non-empty.
/// </remarks>
internal sealed class ResearchSelectionCheck(
    IDbContextFactory<AscendantEmpireContext> dbFactory) : IPossibleActionCheck
{
    public string CheckName => "Research Selection";
    public int ExecutionOrder => (int)EndTurnPossibleActionOrder.AreWeResearchingSomething;

    public async Task<IReadOnlyList<PossibleAction>> EvaluateAsync(
        PossibleActionContext context,
        CancellationToken cancellationToken)
    {
        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Projected into a wrapper rather than straight to the nullable id, because FirstOrDefault over an int? cannot tell "no row" from "a row holding
        // null" - and those are different situations. A row exists for every Player from game setup onwards, so no row means this Player predates the
        // technology schema and has nowhere to record a project, which is not something to nag them about.
        var researchState = await db.PlayerResearchStates
            .AsNoTracking()
            .Where(state => state.PlayerId == context.PlayerId)
            .Select(state => new { state.CurrentTechnologyId })
            .FirstOrDefaultAsync(cancellationToken);

        if (researchState is null || researchState.CurrentTechnologyId is not null)
        {
            return [];
        }

        // "At least Available" is one comparison because TechnologyStatus is ordered so that a higher value is strictly further along. Discovered counts as
        // further along but is not researchable, so it is excluded explicitly.
        bool hasSomethingToResearch = await db.PlayerTechnologies
            .AsNoTracking()
            .Where(row => row.PlayerId == context.PlayerId && row.GameId == context.GameId)
            .AnyAsync(row => row.Status >= TechnologyStatus.Available && row.Status < TechnologyStatus.Discovered, cancellationToken);

        if (!hasSomethingToResearch)
        {
            return [];
        }

        return
        [
            new PossibleAction
            {
                Category = InformationCategory.Technology,
                Description = "No research project is selected.",

                // Research is an empire-wide decision rather than something a record on the map holds, so there is no id to carry and no tile to pan to.
                // RecordType.None exists for exactly this: an action with no owning entity.
                Id = 0,
                RecordTypeId = (int)RecordType.None
            }
        ];
    }
}
