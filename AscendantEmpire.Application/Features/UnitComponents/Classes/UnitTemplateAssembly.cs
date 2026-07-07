using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Classes;

/// <summary>
/// Resolves a base/slot-codes save request against the catalog and validates the resulting fitted-component
/// tree. Shared by the player-owned and global/common unit-template create+update handlers so all four
/// apply identical assembly rules before writing anything.
/// </summary>
internal static class UnitTemplateAssembly
{
    /// <summary>
    /// The base component and fitted-component rows resolved from a save request, ready to be persisted
    /// (or attached to an existing tracked <see cref="UnitTemplate"/>).
    /// </summary>
    public sealed record ResolvedAssembly(UnitComponent BaseComponent, List<UnitTemplateComponent> FittedComponents);

    /// <summary>
    /// Resolves and validates a template's base + fitted components.
    /// </summary>
    /// <param name="playerId">
    /// The owning player, whose unlocks every referenced component is checked against — or <c>null</c> for
    /// a global/common (dev-authored) template, which skips the unlock check entirely since the catalog
    /// itself is being authored rather than a specific player's already-unlocked parts.
    /// </param>
    public static async Task<ServiceResult<ResolvedAssembly>> ResolveAsync(
        AscendantEmpireContext context,
        int? playerId,
        string displayName,
        string mapImagePath,
        string baseCode,
        IReadOnlyList<string?>? slotComponentCodes,
        CancellationToken cancelToken)
    {
        List<string> shapeErrors = ValidateShape(displayName, mapImagePath, baseCode, slotComponentCodes);

        if (shapeErrors.Count > 0)
        {
            return ServiceResult<ResolvedAssembly>.Fail(shapeErrors);
        }

        UnitComponent? baseComponent = await context.UnitComponents
            .Include(c => c.Body!).ThenInclude(b => b.Slots).ThenInclude(s => s.Rules)
            .Include(c => c.Tags).ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(c => c.Code == baseCode, cancelToken);

        if (baseComponent is null)
        {
            return ServiceResult<ResolvedAssembly>.NotFound($"No component exists with code '{baseCode}'.");
        }

        if (baseComponent.ComponentType != ComponentType.Body)
        {
            return ServiceResult<ResolvedAssembly>.Fail($"Component '{baseCode}' is not a Body component.");
        }

        // Null means "skip the unlock check" (global/common templates authored by the dev tool); a real
        // playerId means every referenced component must be in this player's unlocked set.
        HashSet<int>? unlockedComponentIds = playerId is { } ownerId
            ? await context.PlayerUnlockedComponents
                .Where(u => u.PlayerId == ownerId && u.ObsoleteOnTurn == null)
                .Select(u => u.UnitComponentId)
                .ToHashSetAsync(cancelToken)
            : null;

        if (unlockedComponentIds != null && !unlockedComponentIds.Contains(baseComponent.Id))
        {
            return ServiceResult<ResolvedAssembly>.Forbidden($"Component '{baseCode}' has not been unlocked.");
        }

        // Non-null fitted codes, keyed by the slot index the frontend positioned them at.
        List<(int SlotIndex, string Code)> fittedEntries = [.. slotComponentCodes!
            .Select((code, index) => (SlotIndex: index, Code: code))
            .Where(entry => entry.Code != null)
            .Select(entry => (entry.SlotIndex, Code: entry.Code!))];

        HashSet<string> fittedCodes = [.. fittedEntries.Select(e => e.Code)];

        Dictionary<string, UnitComponent> fittedByCode = fittedCodes.Count > 0
            ? await context.UnitComponents
                .Include(c => c.Tags).ThenInclude(t => t.Tag)
                .Where(c => fittedCodes.Contains(c.Code))
                .ToDictionaryAsync(c => c.Code, cancelToken)
            : [];

        List<string> missingCodes = [.. fittedCodes.Where(code => !fittedByCode.ContainsKey(code))];

        if (missingCodes.Count > 0)
        {
            return ServiceResult<ResolvedAssembly>.NotFound(
                missingCodes.Select(code => $"No component exists with code '{code}'."));
        }

        if (unlockedComponentIds != null)
        {
            List<string> lockedCodes = [.. fittedByCode.Values
                .Where(c => !unlockedComponentIds.Contains(c.Id))
                .Select(c => c.Code)];

            if (lockedCodes.Count > 0)
            {
                return ServiceResult<ResolvedAssembly>.Forbidden(
                    lockedCodes.Select(code => $"Component '{code}' has not been unlocked."));
            }
        }

        // Base slots ordered deterministically (by Id), matching the frontend's positional slot indexing.
        List<UnitComponentSlot> orderedSlots = [.. baseComponent.Body!.Slots.OrderBy(s => s.Id)];

        List<string> indexErrors = [];
        var fitted = new List<UnitTemplateComponent>();

        foreach ((int slotIndex, string code) in fittedEntries)
        {
            if (slotIndex >= orderedSlots.Count)
            {
                indexErrors.Add($"Slot index {slotIndex} is out of range for '{baseCode}', which has {orderedSlots.Count} slot(s).");
                continue;
            }

            UnitComponentSlot slot = orderedSlots[slotIndex];
            UnitComponent component = fittedByCode[code];

            // Navigation properties (not just FKs) are set here because SlotAssemblyValidator reads them,
            // never the raw ids — this works whether or not these rows are yet tracked by the context.
            fitted.Add(new UnitTemplateComponent
            {
                UnitComponentSlotId = slot.Id,
                UnitComponentId = component.Id,
                UnitComponentSlot = slot,
                UnitComponent = component,
            });
        }

        if (indexErrors.Count > 0)
        {
            return ServiceResult<ResolvedAssembly>.Fail(indexErrors);
        }

        List<string> assemblyErrors = SlotAssemblyValidator.Validate(baseComponent.Id, fitted);

        if (assemblyErrors.Count > 0)
        {
            return ServiceResult<ResolvedAssembly>.Fail(assemblyErrors);
        }

        return ServiceResult<ResolvedAssembly>.Succeed(new ResolvedAssembly(baseComponent, fitted));
    }

    private static List<string> ValidateShape(string displayName, string mapImagePath, string baseCode, IReadOnlyList<string?>? slotComponentCodes)
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add("The template is missing its DisplayName.");
        }
        else if (displayName.Length > UnitTemplate.DISPLAY_NAME_MAX_LENGTH)
        {
            errors.Add($"Template DisplayName exceeds {UnitTemplate.DISPLAY_NAME_MAX_LENGTH} characters.");
        }

        if (string.IsNullOrWhiteSpace(mapImagePath))
        {
            errors.Add("The template is missing its MapImagePath.");
        }
        else if (mapImagePath.Length > UnitTemplate.MAP_IMAGE_PATH_MAX_LENGTH)
        {
            errors.Add($"Template MapImagePath exceeds {UnitTemplate.MAP_IMAGE_PATH_MAX_LENGTH} characters.");
        }

        if (string.IsNullOrWhiteSpace(baseCode))
        {
            errors.Add("The template is missing its BaseCode.");
        }

        if (slotComponentCodes is null)
        {
            errors.Add("The template is missing its SlotComponentCodes.");
        }

        return errors;
    }
}
