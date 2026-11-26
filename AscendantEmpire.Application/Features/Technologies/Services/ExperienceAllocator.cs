using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Technologies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Technologies.Services;

/// <inheritdoc cref="IExperienceAllocator"/>
/// <remarks>
/// <para>
/// <b>Writes through the change tracker rather than with <c>ExecuteUpdateAsync</c></b>, which is a deliberate departure from
/// <see cref="PendingExperienceAccrual"/>'s own remarks. The exploration accrual has to commit with the sighting stamp that earned it or not at all, and a
/// bulk update would commit on its own, outside whatever transaction the caller holds. Contention is per Player and per Turn, so the cost of losing the bulk
/// update's atomicity is small.
/// </para>
/// <para>
/// Codes are resolved in one query before any row is staged, so a batch naming a code the catalogue does not have leaves the context exactly as it was
/// handed over.
/// </para>
/// </remarks>
internal sealed class ExperienceAllocator(ILogger<ExperienceAllocator> logger) : IExperienceAllocator
{
    /// <inheritdoc />
    public async Task<ServiceResultBase> AccrueAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        IReadOnlyDictionary<string, int> pointsByCode,
        CancellationToken cancelToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(pointsByCode);

        Dictionary<string, int> earned = pointsByCode
            .Where(entry => entry.Value > 0)
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        if (earned.Count == 0)
        {
            return ServiceResultBase.Succeed();
        }

        string[] codes = [.. earned.Keys];

        Dictionary<string, int> typeIdsByCode = await context.ExperienceTypes
            .AsNoTracking()
            .Where(experienceType => codes.Contains(experienceType.Code))
            .ToDictionaryAsync(experienceType => experienceType.Code, experienceType => experienceType.Id, cancelToken);

        string[] unresolved = [.. codes.Where(code => typeIdsByCode.ContainsKey(code) == false)];

        if (unresolved.Length > 0)
        {
            string unresolvedCodes = string.Join(", ", unresolved);

            logger.LogError(
                "Experience type code(s) {UnresolvedCodes} are not in the catalogue, so no experience was accrued for Player Id {PlayerId} in Game Id {GameId}.",
                unresolvedCodes,
                playerId,
                gameId);

            return ServiceResultBase.Fail(
                $"Experience type code(s) {unresolvedCodes} are not in the catalogue, so no experience was accrued for Player Id {playerId}.");
        }

        foreach ((string code, int points) in earned)
        {
            int experienceTypeId = typeIdsByCode[code];

            PendingExperienceAccrual? accrual = await context.PendingExperienceAccruals
                .FirstOrDefaultAsync(row => row.PlayerId == playerId && row.ExperienceTypeId == experienceTypeId, cancelToken);

            if (accrual is null)
            {
                context.PendingExperienceAccruals.Add(new PendingExperienceAccrual
                {
                    GameId = gameId,
                    PlayerId = playerId,
                    ExperienceTypeId = experienceTypeId,
                    Points = points
                });
            }
            else
            {
                accrual.Points += points;
            }
        }

        return ServiceResultBase.Succeed();
    }
}
