using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Military.SaveOrganisationStructure;
using AscendantEmpire.Domain.Entities.Military;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Military.Handlers;

/// <summary>
/// Saves (creates or replaces) the calling player's organisation structure for a single tag. The whole tier
/// set is replaced with the supplied tiers.
/// </summary>
/// <remarks>
/// Validation here is deliberately structural (tier count, contiguity, positive multipliers). The richer
/// design rules — fan-out ≥ 2 and "tier size ≤ the rank ceiling of its level" — are not yet enforced; see the
/// outstanding-work list.
/// </remarks>
public record SaveOrganisationStructureRequest(int PlayerId, int TagId, IReadOnlyCollection<SaveOrganisationTierDto> Tiers)
    : IRequest<ServiceResultBase>;

internal class SaveOrganisationStructureHandler(
    ILogger<SaveOrganisationStructureHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<SaveOrganisationStructureRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(SaveOrganisationStructureRequest request, CancellationToken cancelToken)
    {
        try
        {
            // The tag must exist and be an organisation group.
            Tag? tag = await context.Tags
                .FirstOrDefaultAsync(t => t.Id == request.TagId, cancelToken);

            if (tag is null || tag.TagType != TagType.MilitaryOrganisationalGroup)
            {
                return ServiceResultBase.NotFound($"No military organisation tag exists with id {request.TagId}.");
            }

            List<string> errors = Validate(request.Tiers);
            if (errors.Count > 0)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(SaveOrganisationStructureRequest), string.Join("; ", errors));
                return ServiceResultBase.Fail(errors);
            }

            // Domain-derived tier cap: a tier maps positionally to an officer rank, so the tier count may not
            // exceed what the tag's domain's rank ladder allows (max(1, in-use ranks - 1)). A tag with no domain
            // is only bound by the structural maximum already checked above.
            if (tag.MovementDomain is not null)
            {
                IReadOnlyList<string> rankTitles = await OfficerRankLadder.GetInUseRankTitlesAsync(context, request.PlayerId, tag.MovementDomain.Value, cancelToken);
                int maxTiers = MilitaryOrganisationDefaults.MaxTiersForRankCount(rankTitles.Count);

                if (request.Tiers.Count > maxTiers)
                {
                    logger.LogWarning("Rejected organisation save: {Count} tiers exceed the {Max}-tier cap for domain {Domain}.",
                        request.Tiers.Count, maxTiers, tag.MovementDomain.Value);
                    return ServiceResultBase.Fail(
                        $"This organisation group's domain allows at most {maxTiers} tier(s); {request.Tiers.Count} were supplied.");
                }
            }

            PlayerOrganisationStructure? structure = await context.PlayerOrganisationStructures
                .Include(s => s.Tiers)
                .FirstOrDefaultAsync(s => s.PlayerId == request.PlayerId && s.TagId == request.TagId, cancelToken);

            if (structure is null)
            {
                structure = new PlayerOrganisationStructure
                {
                    PlayerId = request.PlayerId,
                    TagId = request.TagId,
                };
                context.PlayerOrganisationStructures.Add(structure);
            }

            // Upsert tiers by TierIndex: update the overlap in place (keeping their ids), insert new indices,
            // and delete any indices the request dropped. Only genuinely added/removed tiers churn; the
            // structure row itself always keeps its id. (For a freshly-created structure, Tiers is empty, so
            // every request tier is an insert.)
            Dictionary<int, PlayerOrganisationStructureTier> existingByIndex =
                structure.Tiers.ToDictionary(t => t.TierIndex);

            HashSet<int> requestIndices = [.. request.Tiers.Select(t => t.TierIndex)];

            foreach (SaveOrganisationTierDto tier in request.Tiers.OrderBy(t => t.TierIndex))
            {
                if (existingByIndex.TryGetValue(tier.TierIndex, out PlayerOrganisationStructureTier? row))
                {
                    row.Name = tier.Name;
                    row.Multiplier = tier.Multiplier;
                }
                else
                {
                    structure.Tiers.Add(new PlayerOrganisationStructureTier
                    {
                        TierIndex = tier.TierIndex,
                        Name = tier.Name,
                        Multiplier = tier.Multiplier,
                    });
                }
            }

            // Delete the tiers whose index is no longer present in the request.
            List<PlayerOrganisationStructureTier> tiersToRemove =
                [.. existingByIndex.Values.Where(t => !requestIndices.Contains(t.TierIndex))];

            foreach (PlayerOrganisationStructureTier tier in tiersToRemove)
            {
                structure.Tiers.Remove(tier);
                context.PlayerOrganisationStructureTiers.Remove(tier);
            }

            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation("Saved organisation structure ({TierCount} tiers) for player {PlayerId}, tag {TagId}.",
                request.Tiers.Count, request.PlayerId, request.TagId);

            return ServiceResultBase.SuccessNoContent();
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(SaveOrganisationStructureHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResultBase.InternalServerError(error);
        }
    }

    /// <summary>
    /// Structural validation: 1..MAX_TIERS tiers, indices contiguous from 1 with no gaps or duplicates, every
    /// name non-blank, and every multiplier at least 1.
    /// </summary>
    private static List<string> Validate(IReadOnlyCollection<SaveOrganisationTierDto> tiers)
    {
        List<string> errors = [];

        if (tiers.Count == 0)
        {
            errors.Add("A structure must have at least one tier.");
            return errors;
        }

        if (tiers.Count > PlayerOrganisationStructureTier.MAX_TIERS)
        {
            errors.Add($"A structure may have at most {PlayerOrganisationStructureTier.MAX_TIERS} tiers.");
        }

        // Indices must be exactly 1..n.
        List<int> indices = [.. tiers.Select(t => t.TierIndex).OrderBy(i => i)];
        for (int position = 0; position < indices.Count; position++)
        {
            if (indices[position] != position + 1)
            {
                errors.Add("Tier indices must run contiguously from 1 with no gaps or duplicates.");
                break;
            }
        }

        foreach (SaveOrganisationTierDto tier in tiers)
        {
            if (string.IsNullOrWhiteSpace(tier.Name))
            {
                errors.Add($"Tier {tier.TierIndex} has no name.");
            }

            if (tier.Multiplier < 1)
            {
                errors.Add($"Tier {tier.TierIndex} must have a multiplier of at least 1.");
            }
        }

        return errors;
    }
}
