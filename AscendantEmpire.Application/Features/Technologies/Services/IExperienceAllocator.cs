using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;

namespace AscendantEmpire.Application.Features.Technologies.Services;

/// <summary>
/// Accrues experience a Player has earned, for the end-of-Turn step to distribute.
/// </summary>
/// <remarks>
/// The one writer of <c>technology.PendingExperienceAccrual</c>. Every producing system — exploration, movement, settling, and extraction, refining,
/// manufacture and husbandry when they land — accrues through here, so there is a single path to copy rather than a choice to make.
/// </remarks>
public interface IExperienceAllocator
{
    /// <summary>
    /// Adds a batch of earned experience to the Player's pending accruals, staged on the caller's context.
    /// </summary>
    /// <param name="context">The caller's context. Written through its change tracker, so the accrual lands in whatever unit of work the caller owns.</param>
    /// <param name="pointsByCode">
    /// What was earned, keyed by <c>ExperienceType.Code</c>. Entries of zero or less are ignored. Callers name codes through
    /// <see cref="Classes.ExperienceTypeCodes"/> rather than spelling them out.
    /// </param>
    /// <returns>
    /// A failure where any code is not in the catalogue, in which case <b>nothing is staged at all</b>: a batch is accrued whole or refused whole, because a
    /// code that resolved to nothing would lose its points where no report could notice.
    /// </returns>
    /// <remarks>
    /// <b>Saves nothing.</b> The caller owns the <c>SaveChangesAsync</c>, which is what lets the exploration accrual commit with the sighting stamp that
    /// earned it, or not at all.
    /// </remarks>
    Task<ServiceResultBase> AccrueAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        IReadOnlyDictionary<string, int> pointsByCode,
        CancellationToken cancelToken);
}
