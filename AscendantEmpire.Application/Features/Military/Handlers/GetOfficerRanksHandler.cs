using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Military.GetOfficerRanks;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Military.Handlers;

/// <summary>
/// Returns the calling player's officer rank ladders across all seeded domains, for the Military Advisor's
/// Officer Class page.
/// </summary>
/// <remarks>
/// Games are seeded with a full set of ranks per player at setup. As a safety net (older games, or a domain
/// that was never seeded), any domain with no persisted rows falls back to the code defaults so the page is
/// never empty.
/// </remarks>
public record GetOfficerRanksRequest(int PlayerId) : IRequest<ServiceResult<GetOfficerRanksResult>>;

internal class GetOfficerRanksHandler(
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetOfficerRanksRequest, ServiceResult<GetOfficerRanksResult>>
{
    public async Task<ServiceResult<GetOfficerRanksResult>> Handle(GetOfficerRanksRequest request, CancellationToken cancelToken)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        List<OfficerRankView> persisted = await context.PlayerOfficerRanks
            .AsNoTracking()
            .Where(r => r.PlayerId == request.PlayerId)
            .OrderBy(r => r.MovementDomain)
            .ThenBy(r => r.Level)
            .Select(r => new OfficerRankView
            {
                MovementDomain = r.MovementDomain,
                Level = r.Level,
                Title = r.Title,
                MaxSoldiers = r.MaxSoldiers,
                MaxAttachedSpecialists = r.MaxAttachedSpecialists,
                InUse = r.InUse,
            })
            .ToListAsync(cancelToken);

        // Fill in any seeded domain that has no persisted rows with the code defaults, so the page always
        // has a complete ladder to show for every domain it offers.
        List<OfficerRankView> ranks = [.. persisted];
        HashSet<DomainType> domainsWithRows = [.. persisted.Select(r => r.MovementDomain)];

        foreach (DomainType domain in MilitaryOrganisationDefaults.SeededDomains)
        {
            if (domainsWithRows.Contains(domain))
            {
                continue;
            }

            ranks.AddRange(MilitaryOrganisationDefaults.RanksForDomain(domain).Select(d => new OfficerRankView
            {
                MovementDomain = domain,
                Level = d.Level,
                Title = d.Title,
                MaxSoldiers = d.MaxSoldiers,
                MaxAttachedSpecialists = d.MaxAttachedSpecialists,
                InUse = true,
            }));
        }

        var result = new GetOfficerRanksResult
        {
            Ranks = [.. ranks.OrderBy(r => r.MovementDomain).ThenBy(r => r.Level)],
        };

        return ServiceResult<GetOfficerRanksResult>.Succeed(result);
    }
}
