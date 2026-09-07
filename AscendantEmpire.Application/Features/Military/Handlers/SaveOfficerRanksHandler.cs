using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Military.SaveOfficerRanks;
using AscendantEmpire.Domain.Entities.Military;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Military.Handlers;

/// <summary>
/// Bulk-saves the calling player's officer rank ladders. Every posted domain is replaced wholesale with the
/// rows supplied for it, so the client sends the full ladder for each domain it edits.
/// </summary>
public record SaveOfficerRanksRequest(int PlayerId, IReadOnlyCollection<SaveOfficerRankDto> Ranks) : IRequest<ServiceResultBase>;

internal class SaveOfficerRanksHandler(
    ILogger<SaveOfficerRanksHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<SaveOfficerRanksRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(SaveOfficerRanksRequest request, CancellationToken cancelToken)
    {
        try
        {
            List<string> errors = Validate(request.Ranks);
            if (errors.Count > 0)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(SaveOfficerRanksRequest), string.Join("; ", errors));
                return ServiceResultBase.Fail(errors);
            }

            // The request is the complete desired state for the domains it includes. Upsert by the natural key
            // (domain, level): update rows that already exist in place, insert new ones, and delete any the
            // request no longer includes. Unchanged rows keep their ids (no churn).
            List<DomainType> requestDomains = [.. request.Ranks.Select(r => r.MovementDomain).Distinct()];

            List<PlayerOfficerRank> existing = await context.PlayerOfficerRanks
                .Where(r => r.PlayerId == request.PlayerId)
                .Where(r => requestDomains.Contains(r.MovementDomain))
                .ToListAsync(cancelToken);

            Dictionary<(DomainType Domain, int Level), PlayerOfficerRank> existingByKey =
                existing.ToDictionary(r => (r.MovementDomain, r.Level));

            HashSet<(DomainType Domain, int Level)> requestKeys = [.. request.Ranks.Select(r => (r.MovementDomain, r.Level))];

            foreach (SaveOfficerRankDto dto in request.Ranks)
            {
                if (existingByKey.TryGetValue((dto.MovementDomain, dto.Level), out PlayerOfficerRank? row))
                {
                    // Update in place — the row keeps its id.
                    row.Title = dto.Title;
                    row.MaxSoldiers = dto.MaxSoldiers;
                    row.MaxAttachedSpecialists = dto.MaxAttachedSpecialists;
                    row.InUse = dto.InUse;
                }
                else
                {
                    context.PlayerOfficerRanks.Add(new PlayerOfficerRank
                    {
                        PlayerId = request.PlayerId,
                        MovementDomain = dto.MovementDomain,
                        Level = dto.Level,
                        Title = dto.Title,
                        MaxSoldiers = dto.MaxSoldiers,
                        MaxAttachedSpecialists = dto.MaxAttachedSpecialists,
                        InUse = dto.InUse,
                    });
                }
            }

            // Delete any existing rows in the request's domains that the request no longer includes.
            List<PlayerOfficerRank> toRemove = [.. existing.Where(r => !requestKeys.Contains((r.MovementDomain, r.Level)))];
            context.PlayerOfficerRanks.RemoveRange(toRemove);

            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation("Saved {Count} officer rank row(s) for player {PlayerId}.", request.Ranks.Count, request.PlayerId);

            return ServiceResultBase.SuccessNoContent();
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(SaveOfficerRanksHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResultBase.InternalServerError(error);
        }
    }

    /// <summary>
    /// Shape-validates the posted ranks: levels in range, no duplicate (domain, level), non-negative caps, and
    /// a non-blank title on any level that is in use.
    /// </summary>
    private static List<string> Validate(IReadOnlyCollection<SaveOfficerRankDto> ranks)
    {
        List<string> errors = [];

        foreach (SaveOfficerRankDto rank in ranks)
        {
            if (rank.Level < 1 || rank.Level > PlayerOfficerRank.MAX_LEVEL)
            {
                errors.Add($"Rank level {rank.Level} ({rank.MovementDomain}) is out of range 1..{PlayerOfficerRank.MAX_LEVEL}.");
            }

            if (rank.InUse && string.IsNullOrWhiteSpace(rank.Title))
            {
                errors.Add($"Rank level {rank.Level} ({rank.MovementDomain}) is in use but has no title.");
            }

            if (rank.MaxSoldiers is < 0)
            {
                errors.Add($"Rank level {rank.Level} ({rank.MovementDomain}) has a negative soldier ceiling.");
            }

            if (rank.MaxAttachedSpecialists < 0)
            {
                errors.Add($"Rank level {rank.Level} ({rank.MovementDomain}) has a negative specialist cap.");
            }
        }

        List<string> duplicates = [.. ranks
            .GroupBy(r => new { r.MovementDomain, r.Level })
            .Where(g => g.Count() > 1)
            .Select(g => $"Rank level {g.Key.Level} ({g.Key.MovementDomain}) is specified multiple times.")];

        errors.AddRange(duplicates);

        return errors;
    }
}
