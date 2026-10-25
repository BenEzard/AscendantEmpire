using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Technologies.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.Technologies.GetPlayerResearch;
using AscendantEmpire.BackendContracts.Technologies.GetTechnologies;
using AscendantEmpire.Domain.Enums.Technologies;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Technologies.Handlers;

public record GetPlayerResearchRequest(int GameId, int PlayerId) : IRequest<ServiceResult<PlayerResearchOverview>>;

/// <summary>
/// Returns the Player's current research settings - their current project, their banked points, and everything they could research now.
/// </summary>
/// <remarks>
/// <para>
/// A pure read. It never backfills or recomputes a status: <c>IPlayerTechnologyAvailabilityService</c> is the single owner of Locked versus Available.
/// </para>
/// </remarks>
internal class GetPlayerResearchHandler(
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ICampaignTurnService campaignTurnService
) : IRequestHandler<GetPlayerResearchRequest, ServiceResult<PlayerResearchOverview>>
{
    public async Task<ServiceResult<PlayerResearchOverview>> Handle(GetPlayerResearchRequest request, CancellationToken cancellationToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

        // One row per Player, written at game setup, so its absence means the Player was never set up rather than that they have chosen nothing.
        var researchState = await context.PlayerResearchStates
            .AsNoTracking()
            .Where(state => state.PlayerId == request.PlayerId)
            .Select(state => new
            {
                state.CurrentTechnologyId,
                CurrentTechnologyDisplayName = state.CurrentTechnology == null ? null : state.CurrentTechnology.DisplayName,
                state.NextTechnologyId,
                NextTechnologyDisplayName = state.NextTechnology == null ? null : state.NextTechnology.DisplayName,
                state.UnspentDirectedPoints,
                state.UnspentDiffusionPoints
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (researchState is null)
        {
            return ServiceResult<PlayerResearchOverview>.NotFound(
                $"Player {request.PlayerId} has no research state. One is written for every Player at game setup, so this Player has not been set up.");
        }

        // The Round the Player research progress is reported against. Taken from the turn service rather than queried here, so this read agrees with
        // every other reader about which lane is live - a completed lane still holds the Round the Game ended on.
        ServiceResult<int> currentRound = await campaignTurnService.GetCurrentRoundNumberAsync(context, request.GameId, cancellationToken);

        if (currentRound.HasSucceeded == false)
        {
            // Passed on with its own status: no live lane is a NotFound, two of them a plain failure, and flattening them would hide the second.
            return new ServiceResult<PlayerResearchOverview> { StatusCode = currentRound.StatusCode, Errors = currentRound.Errors };
        }

        // Lifted into a local before the query rather than read off the projection's anonymous type inside it, so the comparison below closes over a
        // plain int? that EF parameterises.
        int? currentTechnologyId = researchState.CurrentTechnologyId;

        // "At least Available, but not yet Discovered" is two comparisons rather than a set membership because TechnologyStatus is ordered so that a
        // higher value is strictly further along - which is the reason it is ordered.
        //
        // Filtered on GameId as well as PlayerId. The two always agree, since both come off the same game token, but the index is keyed that way and
        // the redundant predicate costs nothing.
        var rows = await context.PlayerTechnologies
            .AsNoTracking()
            .Where(row => row.PlayerId == request.PlayerId && row.GameId == request.GameId)
            .Where(row => row.Status >= TechnologyStatus.Available && row.Status < TechnologyStatus.Discovered)
            .OrderBy(row => row.Technology.Era)
            .ThenBy(row => row.Technology.SortOrder)
            .Select(row => new
            {
                row.TechnologyId,
                row.Technology.Code,
                row.Technology.DisplayName,
                row.Technology.Description,
                row.Technology.IconPath,
                row.Technology.Era,
                row.Status,

                // The Player's own figures, off their own row - never the catalogue's base numbers on row.Technology.
                row.DirectedRequired,
                row.ExperienceRequired,
                row.DiffusionRequired,
                row.DirectedPoints,
                row.ExperiencePoints,
                row.DiffusionPoints,

                AcquisitionCosts = row.Technology.AcquisitionCosts
                    .OrderBy(cost => cost.Asset.DisplayName)
                    .Select(cost => new CatalogueAcquisitionCost
                    {
                        AssetId = cost.AssetId,
                        AssetCode = cost.Asset.Code,
                        AssetDisplayName = cost.Asset.DisplayName,
                        AssetIconPath = cost.Asset.IconPath,
                        Quantity = cost.Quantity
                    })
                    .ToList(),
                ExperienceSources = row.Technology.ExperienceTypes
                    .OrderBy(mapping => mapping.ExperienceType.SortOrder)
                    .Select(mapping => new CatalogueExperienceSource
                    {
                        ExperienceTypeId = mapping.ExperienceTypeId,
                        ExperienceTypeCode = mapping.ExperienceType.Code,
                        ExperienceTypeDisplayName = mapping.ExperienceType.DisplayName
                    })
                    .ToList(),

                // Left raw and resolved below, because a grant's TargetId points at a different table per target type and so cannot be joined in one
                // query. Same two-step the catalogue read uses.
                RawGrants = row.Technology.Grants
                    .Select(grant => new
                    {
                        grant.TargetType,
                        grant.TargetId,
                        grant.GrantEffect
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        GrantTargetNames targetNames = await TechnologyGrantTargetResolver.LoadAsync(
            context,
            [.. rows.SelectMany(row => row.RawGrants).Select(grant => (grant.TargetType, grant.TargetId))],
            cancellationToken);

        List<ResearchableTechnology> researchable = [.. rows.Select(row => new ResearchableTechnology
        {
            TechnologyId = row.TechnologyId,
            Code = row.Code,
            DisplayName = row.DisplayName,
            Description = row.Description,
            IconPath = row.IconPath,
            Era = row.Era,
            Status = row.Status,
            DirectedRequired = row.DirectedRequired,
            ExperienceRequired = row.ExperienceRequired,
            DiffusionRequired = row.DiffusionRequired,
            DirectedPoints = row.DirectedPoints,
            ExperiencePoints = row.ExperiencePoints,
            DiffusionPoints = row.DiffusionPoints,
            AcquisitionCosts = row.AcquisitionCosts,
            ExperienceSources = row.ExperienceSources,
            Grants = [.. row.RawGrants
                .Select(grant =>
                {
                    (string? code, string? displayName) = targetNames.Resolve(grant.TargetType, grant.TargetId);

                    return new CatalogueGrant
                    {
                        TargetType = grant.TargetType,
                        TargetId = grant.TargetId,
                        TargetCode = code,
                        TargetDisplayName = displayName,
                        GrantEffect = grant.GrantEffect
                    };
                })
                // Ordered after resolution rather than in the query, since the name being ordered by is not in the grant row.
                .OrderBy(grant => grant.TargetType)
                .ThenBy(grant => grant.TargetDisplayName)],
            IsCurrentProject = row.TechnologyId == currentTechnologyId
        })];

        var overview = new PlayerResearchOverview
        {
            CurrentTechnologyId = researchState.CurrentTechnologyId,
            CurrentTechnologyDisplayName = researchState.CurrentTechnologyDisplayName,
            NextTechnologyId = researchState.NextTechnologyId,
            NextTechnologyDisplayName = researchState.NextTechnologyDisplayName,
            UnspentDirectedPoints = researchState.UnspentDirectedPoints,
            UnspentDiffusionPoints = researchState.UnspentDiffusionPoints,
            CurrentRoundNumber = currentRound.SuccessValue,
            Researchable = researchable
        };

        return ServiceResult<PlayerResearchOverview>.Succeed(overview);
    }
}
