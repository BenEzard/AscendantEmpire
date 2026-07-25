using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Military;

/// <summary>
/// Reads a player's in-use officer rank ladder for a domain — the basis for an organisation hierarchy's tier
/// cap and its per-tier rank titles.
/// </summary>
public static class OfficerRankLadder
{
    /// <summary>
    /// The in-use rank titles for a player, in a domain, ordered by level (index 0 = level 1). When the player
    /// has no persisted rows for the domain (e.g. an un-seeded game), the code defaults (all levels in use) are
    /// returned, matching the read handler's fallback.
    /// </summary>
    public static async Task<IReadOnlyList<string>> GetInUseRankTitlesAsync(
        AscendantEmpireContext context,
        int playerId,
        DomainType domain,
        CancellationToken cancelToken)
    {
        List<(int Level, string Title, bool InUse)> rows = await context.PlayerOfficerRanks
            .AsNoTracking()
            .Where(r => r.PlayerId == playerId && r.MovementDomain == domain)
            .OrderBy(r => r.Level)
            .Select(r => new ValueTuple<int, string, bool>(r.Level, r.Title, r.InUse))
            .ToListAsync(cancelToken);

        if (rows.Count == 0)
        {
            // No persisted ladder for this domain — fall back to the code defaults (every level in use).
            return [.. MilitaryOrganisationDefaults.RanksForDomain(domain).Select(d => d.Title)];
        }

        return [.. rows.Where(r => r.InUse).OrderBy(r => r.Level).Select(r => r.Title)];
    }
}
