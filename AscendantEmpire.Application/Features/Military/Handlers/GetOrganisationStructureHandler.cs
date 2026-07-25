using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Military.GetOrganisationStructure;
using AscendantEmpire.Domain.Entities.Military;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Military.Handlers;

/// <summary>
/// Returns the calling player's organisation structure for a single MilitaryOrganisationalGroup tag. When the
/// player has not yet customised one, the code-default structure is returned (unsaved) so the page always has
/// tiers to render and save.
/// </summary>
public record GetOrganisationStructureRequest(int PlayerId, int TagId) : IRequest<ServiceResult<OrganisationStructureView>>;

internal class GetOrganisationStructureHandler(
    ILogger<GetOrganisationStructureHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetOrganisationStructureRequest, ServiceResult<OrganisationStructureView>>
{
    public async Task<ServiceResult<OrganisationStructureView>> Handle(GetOrganisationStructureRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // The tag must exist and be an organisation group — the structure is bound to it.
            Tag? tag = await context.Tags
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.TagId, cancelToken);

            if (tag is null || tag.TagType != TagType.MilitaryOrganisationalGroup)
            {
                return ServiceResult<OrganisationStructureView>.NotFound(
                    $"No military organisation tag exists with id {request.TagId}.");
            }

            List<OrganisationTierView> savedTiers = await context.PlayerOrganisationStructures
                .AsNoTracking()
                .Where(s => s.PlayerId == request.PlayerId && s.TagId == request.TagId)
                .SelectMany(s => s.Tiers)
                .OrderBy(t => t.TierIndex)
                .Select(t => new OrganisationTierView
                {
                    TierIndex = t.TierIndex,
                    Name = t.Name,
                    Multiplier = t.Multiplier,
                })
                .ToListAsync(cancelToken);

            // Derive the tier cap and the per-tier rank titles from the tag's domain. A tag with no domain is
            // unconstrained (the absolute tier maximum, and no rank titles to show).
            IReadOnlyList<string> rankTitles = [];
            int maxTiers = PlayerOrganisationStructureTier.MAX_TIERS;

            if (tag.MovementDomain is not null)
            {
                rankTitles = await OfficerRankLadder.GetInUseRankTitlesAsync(context, request.PlayerId, tag.MovementDomain.Value, cancelToken);
                maxTiers = MilitaryOrganisationDefaults.MaxTiersForRankCount(rankTitles.Count);
            }

            bool isDefault = savedTiers.Count == 0;

            IReadOnlyCollection<OrganisationTierView> tiers = isDefault
                // The code default is trimmed to the domain's tier cap, so a defaulted hierarchy never starts
                // out over the limit.
                ? [.. MilitaryOrganisationDefaults.DefaultStructureTiers
                    .Where(d => d.TierIndex <= maxTiers)
                    .Select(d => new OrganisationTierView
                    {
                        TierIndex = d.TierIndex,
                        Name = d.Name,
                        Multiplier = d.Multiplier,
                    })]
                : savedTiers;

            var view = new OrganisationStructureView
            {
                TagId = tag.Id,
                TagCode = tag.Code,
                TagDisplayName = tag.DisplayName,
                MovementDomain = tag.MovementDomain,
                MaxTiers = maxTiers,
                RankTitles = rankTitles,
                IsDefault = isDefault,
                Tiers = tiers,
            };

            return ServiceResult<OrganisationStructureView>.Succeed(view);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(GetOrganisationStructureHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<OrganisationStructureView>.InternalServerError(ex, error);
        }
    }
}
