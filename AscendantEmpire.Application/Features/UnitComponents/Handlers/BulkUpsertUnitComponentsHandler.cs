using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Bulk-upserts unit components (with their full type-specific graph) into the global catalog.
/// </summary>
/// <remarks>
/// <para>Components whose <c>Code</c> is new are inserted; components whose <c>Code</c> already
/// exists are updated in place — scalar fields, the type-specific extension and every child
/// collection (slots, rules, domain modifiers and tag joins) are overwritten from the payload.</para>
/// <para>An existing component's <c>ComponentType</c> is immutable: re-submitting a code with a
/// different type is rejected rather than silently swapping its extension.</para>
/// </remarks>
public record BulkUpsertUnitComponentsRequest(BulkUpsertUnitComponentsDto Catalog) : IRequest<ServiceResult<BulkUpsertUnitComponentsResult>>;

internal class BulkUpsertUnitComponentsHandler(
    ILogger<BulkUpsertUnitComponentsHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<BulkUpsertUnitComponentsRequest, ServiceResult<BulkUpsertUnitComponentsResult>>
{
    public async Task<ServiceResult<BulkUpsertUnitComponentsResult>> Handle(BulkUpsertUnitComponentsRequest request, CancellationToken cancelToken)
    {
        try
        {
            BulkUpsertUnitComponentsDto catalog = request.Catalog;

            // Request-level validation. Collect every problem so the caller gets a complete picture rather than one error at a time.
            List<string> errors = ValidateRequest(catalog);

            if (errors.Count > 0)
            {
                logger.LogWarning("Validation failed for {Request}. Errors: {Errors}", nameof(BulkUpsertUnitComponentsRequest), string.Join("; ", errors));
                return ServiceResult<BulkUpsertUnitComponentsResult>.Fail(errors);
            }

            // Bulk-load existing state to avoid N+1 queries. For upsert we need the full graph of any
            // component that already exists (so its children can be replaced) and any tags that already
            // exist (so they can be re-used rather than duplicated).
            string[] componentCodes = [.. catalog.Components.Select(c => c.Code)];

            Dictionary<string, UnitComponent> existingByCode = await context.UnitComponents
                .Where(c => componentCodes.Contains(c.Code))
                .Include(c => c.Body!).ThenInclude(b => b.Slots).ThenInclude(s => s.Rules)
                .Include(c => c.Weapon!).ThenInclude(w => w.DomainModifiers)
                .Include(c => c.Armour)
                .Include(c => c.Tags).ThenInclude(t => t.Tag)
                .ToDictionaryAsync(c => c.Code, cancelToken);

            // All tag codes referenced anywhere in the payload, plus those defined in the Tags list.
            string[] referencedTagCodes = [.. catalog.Components
                .SelectMany(c => c.TagCodes)
                .Concat(catalog.Tags.Select(t => t.Code))
                .Distinct()];

            Dictionary<string, Tag> tagsByCode = referencedTagCodes.Length > 0
                ? await context.Tags
                    .Where(t => referencedTagCodes.Contains(t.Code))
                    .ToDictionaryAsync(t => t.Code, cancelToken)
                : [];

            // Validate tag references against payload + DB now that we know what exists.
            List<string> tagErrors = ValidateTagReferences(catalog, tagsByCode);

            if (tagErrors.Count > 0)
            {
                logger.LogWarning("Tag-reference validation failed for {Request}. Errors: {Errors}", nameof(BulkUpsertUnitComponentsRequest), string.Join("; ", tagErrors));
                return ServiceResult<BulkUpsertUnitComponentsResult>.Fail(tagErrors);
            }

            // Create any tags defined in the payload that don't already exist.
            foreach (TagDto tagDto in catalog.Tags)
            {
                if (!tagsByCode.ContainsKey(tagDto.Code))
                {
                    var tag = new Tag
                    {
                        Code = tagDto.Code,
                        DisplayName = tagDto.DisplayName,
                        Description = tagDto.Description,
                    };
                    context.Tags.Add(tag);
                    tagsByCode[tagDto.Code] = tag;
                }
            }

            // A component's type is immutable. Reject any code re-submitted with a different type
            // before touching the graph so a rejected request writes nothing.
            List<string> typeChangeErrors = ValidateTypesUnchanged(catalog, existingByCode);

            if (typeChangeErrors.Count > 0)
            {
                logger.LogWarning("Type-change validation failed for {Request}. Errors: {Errors}", nameof(BulkUpsertUnitComponentsRequest), string.Join("; ", typeChangeErrors));
                return ServiceResult<BulkUpsertUnitComponentsResult>.Fail(typeChangeErrors);
            }

            // Split the payload into inserts (new code) and updates (existing code). New components are
            // built fresh; existing ones are mutated in place with their children fully replaced.
            List<string> addedCodes = [];
            List<string> updatedCodes = [];

            foreach (UnitComponentDto componentDto in catalog.Components)
            {
                if (existingByCode.TryGetValue(componentDto.Code, out UnitComponent? existing))
                {
                    UpdateComponent(existing, componentDto, tagsByCode);
                    updatedCodes.Add(componentDto.Code);
                }
                else
                {
                    context.UnitComponents.Add(BuildComponent(componentDto, tagsByCode));
                    addedCodes.Add(componentDto.Code);
                }
            }

            // Single SaveChanges => the whole graph (components, extensions, slots, rules, modifiers, tags and joins) is written in one transaction.
            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation("Bulk upserted unit components: {AddedCount} added, {UpdatedCount} updated.",
                addedCodes.Count, updatedCodes.Count);

            var result = new BulkUpsertUnitComponentsResult
            {
                AddedCount = addedCodes.Count,
                UpdatedCount = updatedCodes.Count,
                UpdatedCodes = updatedCodes,
            };

            // 201 when the request was purely new rows; 200 when it touched (updated) existing ones.
            return updatedCodes.Count > 0
                ? ServiceResult<BulkUpsertUnitComponentsResult>.Succeed(result)
                : ServiceResult<BulkUpsertUnitComponentsResult>.Created(result);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(BulkUpsertUnitComponentsHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<BulkUpsertUnitComponentsResult>.InternalServerError(ex, error);
        }
    }

    /// <summary>
    /// Validates the shape of the request: non-empty components, unique codes, type/extension
    /// consistency and field constraints. Returns an empty list when the request is valid.
    /// </summary>
    private static List<string> ValidateRequest(BulkUpsertUnitComponentsDto catalog)
    {
        List<string> errors = [];

        if (catalog.Components.Count == 0)
        {
            errors.Add("At least one component must be provided.");
            return errors;
        }

        // Duplicate component codes within the payload.
        string[] duplicateComponentCodes = [.. catalog.Components
            .GroupBy(c => c.Code)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];

        if (duplicateComponentCodes.Length > 0)
        {
            errors.Add($"Duplicate component code(s) in the payload: {string.Join(", ", duplicateComponentCodes)}");
        }

        // Duplicate tag codes within the payload's Tags list.
        string[] duplicateTagCodes = [.. catalog.Tags
            .GroupBy(t => t.Code)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];

        if (duplicateTagCodes.Length > 0)
        {
            errors.Add($"Duplicate tag code(s) in the payload: {string.Join(", ", duplicateTagCodes)}");
        }

        foreach (UnitComponentDto component in catalog.Components)
        {
            ValidateComponent(component, errors);
        }

        foreach (TagDto tag in catalog.Tags)
        {
            if (string.IsNullOrWhiteSpace(tag.Code))
            {
                errors.Add("A tag is missing its Code.");
            }
            else if (tag.Code.Length > Tag.CODE_MAX_LENGTH)
            {
                errors.Add($"Tag code '{tag.Code}' exceeds {Tag.CODE_MAX_LENGTH} characters.");
            }

            if (string.IsNullOrWhiteSpace(tag.DisplayName))
            {
                errors.Add($"Tag '{tag.Code}' is missing its DisplayName.");
            }
            else if (tag.DisplayName.Length > Tag.DISPLAY_NAME_MAX_LENGTH)
            {
                errors.Add($"Tag '{tag.Code}' DisplayName exceeds {Tag.DISPLAY_NAME_MAX_LENGTH} characters.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates a single component's fields and that its populated extension matches its
    /// <see cref="UnitComponentDto.ComponentType"/>.
    /// </summary>
    private static void ValidateComponent(UnitComponentDto component, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(component.Code))
        {
            errors.Add("A component is missing its Code.");
        }
        else if (component.Code.Length > UnitComponent.CODE_MAX_LENGTH)
        {
            errors.Add($"Component code '{component.Code}' exceeds {UnitComponent.CODE_MAX_LENGTH} characters.");
        }

        if (string.IsNullOrWhiteSpace(component.DisplayName))
        {
            errors.Add($"Component '{component.Code}' is missing its DisplayName.");
        }
        else if (component.DisplayName.Length > UnitComponent.DISPLAY_NAME_MAX_LENGTH)
        {
            errors.Add($"Component '{component.Code}' DisplayName exceeds {UnitComponent.DISPLAY_NAME_MAX_LENGTH} characters.");
        }

        // Exactly the extension matching the component type may be present.
        bool bodyAllowed = component.ComponentType == ComponentType.Body;
        bool weaponAllowed = component.ComponentType == ComponentType.Weapon;
        bool armourAllowed = component.ComponentType == ComponentType.Armour;

        if (bodyAllowed && component.Body is null)
        {
            errors.Add($"Component '{component.Code}' is a Body but has no Body data.");
        }

        if (!bodyAllowed && component.Body is not null)
        {
            errors.Add($"Component '{component.Code}' is {component.ComponentType} but carries Body data.");
        }

        if (weaponAllowed && component.Weapon is null)
        {
            errors.Add($"Component '{component.Code}' is a Weapon but has no Weapon data.");
        }

        if (!weaponAllowed && component.Weapon is not null)
        {
            errors.Add($"Component '{component.Code}' is {component.ComponentType} but carries Weapon data.");
        }

        if (armourAllowed && component.Armour is null)
        {
            errors.Add($"Component '{component.Code}' is Armour but has no Armour data.");
        }

        if (!armourAllowed && component.Armour is not null)
        {
            errors.Add($"Component '{component.Code}' is {component.ComponentType} but carries Armour data.");
        }

        // Weapon range sanity + slot-rule value length.
        if (component.Weapon is { } weapon && weapon.MinimumRange > weapon.MaximumRange)
        {
            errors.Add($"Component '{component.Code}' weapon MinimumRange ({weapon.MinimumRange}) exceeds MaximumRange ({weapon.MaximumRange}).");
        }

        if (component.Body is { } body)
        {
            foreach (UnitComponentSlotDto slot in body.Slots)
            {
                if (slot.DisplayName is { Length: > UnitComponentSlot.DISPLAY_NAME_MAX_LENGTH })
                {
                    errors.Add($"Component '{component.Code}' has a slot DisplayName exceeding {UnitComponentSlot.DISPLAY_NAME_MAX_LENGTH} characters.");
                }

                foreach (UnitComponentSlotRuleDto rule in slot.Rules)
                {
                    if (string.IsNullOrWhiteSpace(rule.Value))
                    {
                        errors.Add($"Component '{component.Code}' has a slot rule with an empty Value.");
                    }
                    else if (rule.Value.Length > UnitComponentSlotRule.VALUE_MAX_LENGTH)
                    {
                        errors.Add($"Component '{component.Code}' has a slot rule Value exceeding {UnitComponentSlotRule.VALUE_MAX_LENGTH} characters.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates that every tag code referenced by a component resolves to a tag defined in the
    /// payload or already present in the database.
    /// </summary>
    private static List<string> ValidateTagReferences(BulkUpsertUnitComponentsDto catalog, IReadOnlyDictionary<string, Tag> existingTagsByCode)
    {
        List<string> errors = [];

        HashSet<string> knownTagCodes = [.. existingTagsByCode.Keys];

        foreach (TagDto tag in catalog.Tags)
        {
            knownTagCodes.Add(tag.Code);
        }

        foreach (UnitComponentDto component in catalog.Components)
        {
            foreach (string tagCode in component.TagCodes)
            {
                if (!knownTagCodes.Contains(tagCode))
                {
                    errors.Add($"Component '{component.Code}' references unknown tag code '{tagCode}'.");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates that no existing component is being re-submitted with a different
    /// <see cref="UnitComponentDto.ComponentType"/>, which the upsert treats as immutable.
    /// </summary>
    private static List<string> ValidateTypesUnchanged(BulkUpsertUnitComponentsDto catalog, IReadOnlyDictionary<string, UnitComponent> existingByCode)
    {
        List<string> errors = [];

        foreach (UnitComponentDto component in catalog.Components)
        {
            if (existingByCode.TryGetValue(component.Code, out UnitComponent? existing) && existing.ComponentType != component.ComponentType)
            {
                errors.Add($"Component '{component.Code}' already exists as {existing.ComponentType} and cannot be changed to {component.ComponentType}.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Builds the full <see cref="UnitComponent"/> entity graph (parent + type-specific extension +
    /// tag joins) from a DTO, wiring everything via navigation properties so EF assigns the shared
    /// keys and foreign keys on save.
    /// </summary>
    private static UnitComponent BuildComponent(UnitComponentDto dto, IReadOnlyDictionary<string, Tag> tagsByCode)
    {
        return new UnitComponent
        {
            Code = dto.Code,
            DisplayName = dto.DisplayName,
            Description = dto.Description,
            ComponentType = dto.ComponentType,
            Weight = dto.Weight,
            Detection = dto.Detection,
            Visibility = dto.Visibility,
            Body = dto.Body is { } bodyDto ? BuildBody(bodyDto) : null,
            Weapon = dto.Weapon is { } weaponDto ? BuildWeapon(weaponDto) : null,
            Armour = dto.Armour is { } armourDto ? BuildArmour(armourDto) : null,
            Tags = BuildTagJoins(dto.TagCodes, tagsByCode),
        };
    }

    private static UnitComponentBody BuildBody(UnitComponentBodyDto dto)
    {
        return new UnitComponentBody
        {
            MovementDomain = dto.MovementDomain,
            CampaignMovementPoints = dto.CampaignMovementPoints,
            BattlefieldMovementPoints = dto.BattlefieldMovementPoints,
            MinStrength = dto.MinStrength,
            MaxStrength = dto.MaxStrength,
            IncrementStrength = dto.IncrementStrength,
            CrushAttack = dto.CrushAttack,
            Slots = [.. dto.Slots.Select(BuildSlot)],
        };
    }

    private static UnitComponentSlot BuildSlot(UnitComponentSlotDto dto)
    {
        return new UnitComponentSlot
        {
            DisplayName = dto.DisplayName,
            IsVisible = dto.IsVisible,
            IsLocked = dto.IsLocked,
            UIXPosition = dto.UIXPosition,
            UIYPosition = dto.UIYPosition,
            Rules = [.. dto.Rules.Select(BuildSlotRule)],
        };
    }

    private static UnitComponentSlotRule BuildSlotRule(UnitComponentSlotRuleDto dto)
    {
        return new UnitComponentSlotRule
        {
            RuleType = dto.RuleType,
            Operator = dto.Operator,
            Value = dto.Value,
        };
    }

    private static UnitComponentWeapon BuildWeapon(UnitComponentWeaponDto dto)
    {
        return new UnitComponentWeapon
        {
            Attack = ToDamageProfile(dto.Attack),
            MinimumRange = dto.MinimumRange,
            MaximumRange = dto.MaximumRange,
            DomainModifiers = [.. dto.DomainModifiers.Select(BuildDomainModifier)],
        };
    }

    private static UnitComponentWeaponDomainModifier BuildDomainModifier(UnitComponentWeaponDomainModifierDto dto)
    {
        return new UnitComponentWeaponDomainModifier
        {
            Domain = dto.Domain,
            DamageMultiplier = dto.DamageMultiplier,
        };
    }

    private static UnitComponentArmour BuildArmour(UnitComponentArmourDto dto)
    {
        return new UnitComponentArmour
        {
            Resistance = ToDamageProfile(dto.Resistance),
        };
    }

    /// <summary>
    /// Builds the tag-join rows for a component, resolving each code to its (existing or newly added)
    /// <see cref="Tag"/> entity via navigation.
    /// </summary>
    private static ICollection<UnitComponentTag> BuildTagJoins(IReadOnlyCollection<string> tagCodes, IReadOnlyDictionary<string, Tag> tagsByCode)
    {
        return [.. tagCodes.Select(code => new UnitComponentTag { Tag = tagsByCode[code] })];
    }

    /// <summary>
    /// Overwrites an existing tracked component from the DTO: scalar fields, the type-specific
    /// extension (its type is guaranteed unchanged by validation) and every child collection.
    /// </summary>
    private void UpdateComponent(UnitComponent existing, UnitComponentDto dto, IReadOnlyDictionary<string, Tag> tagsByCode)
    {
        existing.DisplayName = dto.DisplayName;
        existing.Description = dto.Description;
        existing.Weight = dto.Weight;
        existing.Detection = dto.Detection;
        existing.Visibility = dto.Visibility;

        switch (dto.ComponentType)
        {
            case ComponentType.Body:
                UpdateBody(existing.Body!, dto.Body!);
                break;

            case ComponentType.Weapon:
                UpdateWeapon(existing.Weapon!, dto.Weapon!);
                break;

            case ComponentType.Armour:
                existing.Armour!.Resistance = ToDamageProfile(dto.Armour!.Resistance);
                break;
        }

        MergeTagJoins(existing, dto.TagCodes, tagsByCode);
    }

    /// <summary>
    /// Reconciles a component's tag joins to exactly the requested codes by adding the missing joins
    /// and deleting the surplus ones. Merging (rather than delete-all + re-add) avoids deleting and
    /// re-inserting the same composite-key join row in one transaction.
    /// </summary>
    private void MergeTagJoins(UnitComponent existing, IReadOnlyCollection<string> desiredTagCodes, IReadOnlyDictionary<string, Tag> tagsByCode)
    {
        HashSet<string> desiredCodes = [.. desiredTagCodes];
        HashSet<string> currentCodes = [.. existing.Tags.Select(join => join.Tag.Code)];

        // Drop joins whose tag is no longer requested (NoAction FK => delete the row explicitly).
        List<UnitComponentTag> removedJoins = [.. existing.Tags.Where(join => !desiredCodes.Contains(join.Tag.Code))];
        context.UnitComponentTags.RemoveRange(removedJoins);

        foreach (UnitComponentTag removedJoin in removedJoins)
        {
            existing.Tags.Remove(removedJoin);
        }

        // Add joins for newly requested tags.
        foreach (string code in desiredCodes)
        {
            if (!currentCodes.Contains(code))
            {
                existing.Tags.Add(new UnitComponentTag { Tag = tagsByCode[code] });
            }
        }
    }

    /// <summary>
    /// Overwrites a body extension's scalars and fully replaces its slots (and their rules).
    /// </summary>
    private void UpdateBody(UnitComponentBody body, UnitComponentBodyDto dto)
    {
        body.MovementDomain = dto.MovementDomain;
        body.CampaignMovementPoints = dto.CampaignMovementPoints;
        body.BattlefieldMovementPoints = dto.BattlefieldMovementPoints;
        body.MinStrength = dto.MinStrength;
        body.MaxStrength = dto.MaxStrength;
        body.IncrementStrength = dto.IncrementStrength;
        body.CrushAttack = dto.CrushAttack;

        // Explicitly delete the old slots and their rules (NoAction FKs => no orphan cascade), then
        // rebuild from the payload. Deleting rules first keeps the FK order valid.
        List<UnitComponentSlot> oldSlots = [.. body.Slots];

        foreach (UnitComponentSlot oldSlot in oldSlots)
        {
            context.UnitComponentSlotRules.RemoveRange(oldSlot.Rules);
        }

        context.UnitComponentSlots.RemoveRange(oldSlots);
        body.Slots = [.. dto.Slots.Select(BuildSlot)];
    }

    /// <summary>
    /// Overwrites a weapon extension's scalars and fully replaces its domain modifiers.
    /// </summary>
    private void UpdateWeapon(UnitComponentWeapon weapon, UnitComponentWeaponDto dto)
    {
        weapon.Attack = ToDamageProfile(dto.Attack);
        weapon.MinimumRange = dto.MinimumRange;
        weapon.MaximumRange = dto.MaximumRange;

        MergeDomainModifiers(weapon, dto.DomainModifiers);
    }

    /// <summary>
    /// Reconciles a weapon's domain modifiers to exactly the requested domains: existing rows are
    /// updated, missing domains added and surplus domains deleted. Merging by <c>Domain</c> (the
    /// natural key behind the unique index) avoids deleting and re-inserting the same (weapon, domain)
    /// row in one transaction, which would trip the unique index.
    /// </summary>
    private void MergeDomainModifiers(UnitComponentWeapon weapon, IReadOnlyCollection<UnitComponentWeaponDomainModifierDto> desiredModifiers)
    {
        Dictionary<DomainType, UnitComponentWeaponDomainModifierDto> desiredByDomain = desiredModifiers.ToDictionary(m => m.Domain);
        Dictionary<DomainType, UnitComponentWeaponDomainModifier> currentByDomain = weapon.DomainModifiers.ToDictionary(m => m.Domain);

        // Delete modifiers whose domain is no longer requested (NoAction FK => delete explicitly).
        List<UnitComponentWeaponDomainModifier> removedModifiers = [.. weapon.DomainModifiers.Where(m => !desiredByDomain.ContainsKey(m.Domain))];
        context.UnitComponentWeaponDomainModifiers.RemoveRange(removedModifiers);

        foreach (UnitComponentWeaponDomainModifier removedModifier in removedModifiers)
        {
            weapon.DomainModifiers.Remove(removedModifier);
        }

        foreach (UnitComponentWeaponDomainModifierDto desired in desiredModifiers)
        {
            if (currentByDomain.TryGetValue(desired.Domain, out UnitComponentWeaponDomainModifier? current))
            {
                // Existing domain: update its multiplier in place.
                current.DamageMultiplier = desired.DamageMultiplier;
            }
            else
            {
                // New domain: add a fresh modifier.
                weapon.DomainModifiers.Add(BuildDomainModifier(desired));
            }
        }
    }

    /// <summary>
    /// Maps a <see cref="DamageProfileDto"/> onto the EF <see cref="DamageProfile"/> complex type.
    /// </summary>
    private static DamageProfile ToDamageProfile(DamageProfileDto dto)
    {
        return new DamageProfile
        {
            Slash = dto.Slash,
            Pierce = dto.Pierce,
            Crush = dto.Crush,
            Burn = dto.Burn,
            Poison = dto.Poison,
        };
    }
}
