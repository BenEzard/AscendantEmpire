using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.UnitComponents.Classes;

/// <summary>
/// Locates the unit template a save would collide with by name, and counts the troops fielded from it.
/// </summary>
/// <remarks>
/// <para>Shared by the player-owned and global/common save handlers, so the clash a create reports and the clash an update enforces are decided the
/// same way.</para>
/// <para>Names are matched trimmed and case-insensitively. The comparison lowercases both sides explicitly rather than leaning on SQL Server's
/// case-insensitive collation, because the EF in-memory provider is case-sensitive — relying on the collation would make tier-2 tests behave
/// differently from production.</para>
/// </remarks>
internal static class UnitTemplateNameLookup
{
    /// <summary>
    /// Finds the caller's own custom template carrying <paramref name="displayName"/>, or null when the name is free.
    /// </summary>
    /// <param name="excludeTemplateId">
    /// A template to ignore, so an update that keeps its own name doesn't collide with itself. Null considers every template.
    /// </param>
    public static async Task<UnitTemplate?> FindPlayerTemplateByNameAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        string displayName,
        int? excludeTemplateId,
        CancellationToken cancelToken)
    {
        string normalized = Normalize(displayName);

        return await context.UnitTemplates
            .Where(t => t.GameId == gameId
                && t.PlayerId == playerId
                && t.DisplayName.ToLower() == normalized
                && (excludeTemplateId == null || t.Id != excludeTemplateId))
            .FirstOrDefaultAsync(cancelToken);
    }

    /// <summary>
    /// Finds the global/common template carrying <paramref name="displayName"/>, or null when no shared template uses it.
    /// </summary>
    /// <remarks>
    /// Every global template is considered, not only the ones a given player has unlocked. Otherwise a player could name a design "Legionary", unlock
    /// the real Legionary later, and end up holding two designs with the same name.
    /// </remarks>
    public static async Task<UnitTemplate?> FindGlobalTemplateByNameAsync(AscendantEmpireContext context, string displayName, CancellationToken cancelToken)
    {
        string normalized = Normalize(displayName);

        return await context.UnitTemplates
            .Where(t => t.GameId == null && t.PlayerId == null && t.DisplayName.ToLower() == normalized)
            .FirstOrDefaultAsync(cancelToken);
    }

    /// <summary>
    /// Finds the global/common template whose <c>Code</c> is <paramref name="code"/>, or null when the code is free.
    /// </summary>
    /// <remarks>
    /// The dev tool matches on Code rather than DisplayName: Code is the unique natural key, and a duplicate Code is what the create endpoint already
    /// rejects with a conflict.
    /// </remarks>
    public static async Task<UnitTemplate?> FindGlobalTemplateByCodeAsync(AscendantEmpireContext context, string code, CancellationToken cancelToken)
    {
        return await context.UnitTemplates
            .Where(t => t.Code == code)
            .FirstOrDefaultAsync(cancelToken);
    }

    /// <summary>
    /// Counts the troops currently fielded from a template.
    /// </summary>
    /// <remarks>
    /// <see cref="UnitTemplate"/> has no inverse collection for troops (the foreign key is configured <c>WithMany()</c>), so this has to go through the
    /// <c>TroopInstances</c> set rather than a navigation property.
    /// </remarks>
    public static async Task<int> CountTroopInstancesAsync(AscendantEmpireContext context, int templateId, CancellationToken cancelToken)
    {
        return await context.TroopInstances.CountAsync(t => t.UnitTemplateId == templateId, cancelToken);
    }

    /// <summary>
    /// Trims and lowercases a display name so it can be compared against a lowercased column.
    /// </summary>
    private static string Normalize(string displayName)
    {
        return displayName.Trim().ToLowerInvariant();
    }
}
