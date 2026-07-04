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
                .Include(c => c.Slots).ThenInclude(s => s.Rules)
                .Include(c => c.Body)
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

            // Create any tags defined in the payload that don't already exist; for those that do, let
            // the payload re-classify them (TagType is authoritative on re-import).
            foreach (TagDto tagDto in catalog.Tags)
            {
                if (tagsByCode.TryGetValue(tagDto.Code, out Tag? existingTag))
                {
                    existingTag.TagType = tagDto.TagType;
                }
                else
                {
                    var tag = new Tag
                    {
                        Code = tagDto.Code,
                        DisplayName = tagDto.DisplayName,
                        Description = tagDto.Description,
                        TagType = tagDto.TagType,
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
            // built fresh; existing ones are mutated in place with their children fully replaced. A
            // code -> entity map (existing + newly built) lets the supersession step wire links by
            // navigation, so EF assigns the foreign keys for brand-new components on save.
            List<string> addedCodes = [];
            List<string> updatedCodes = [];
            Dictionary<string, UnitComponent> componentsByCode = new(existingByCode);

            foreach (UnitComponentDto componentDto in catalog.Components)
            {
                if (existingByCode.TryGetValue(componentDto.Code, out UnitComponent? existing))
                {
                    UpdateComponent(existing, componentDto, tagsByCode);
                    updatedCodes.Add(componentDto.Code);
                }
                else
                {
                    UnitComponent built = BuildComponent(componentDto, tagsByCode);
                    context.UnitComponents.Add(built);
                    componentsByCode[componentDto.Code] = built;
                    addedCodes.Add(componentDto.Code);
                }
            }

            // Reconcile the component supersession (upgrade) links from the payload. Validation runs
            // before any links are staged, so a rejected request still writes nothing.
            if (catalog.ComponentSupersessions.Count > 0)
            {
                await LoadComponentsReferencedBySupersessionsAsync(catalog, componentsByCode, cancelToken);

                List<string> supersessionErrors = ValidateComponentSupersessions(catalog, componentsByCode);

                if (supersessionErrors.Count > 0)
                {
                    logger.LogWarning("Component-supersession validation failed for {Request}. Errors: {Errors}", nameof(BulkUpsertUnitComponentsRequest), string.Join("; ", supersessionErrors));
                    return ServiceResult<BulkUpsertUnitComponentsResult>.Fail(supersessionErrors);
                }

                await ReconcileComponentSupersessionsAsync(catalog, componentsByCode, cancelToken);
            }

            // Single SaveChanges => the whole graph (components, extensions, slots, rules, modifiers, tags, joins and supersession links) is written in one transaction.
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

            if (!Enum.IsDefined(tag.TagType))
            {
                errors.Add($"Tag '{tag.Code}' has an undefined TagType value.");
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

        // Which type-specific extension(s) a component may carry depends on its type:
        //   Body   -> a Body extension (required), and OPTIONALLY a Weapon and/or Armour describing the
        //             inherent attack/defence the body deals itself (e.g. a cavalry charge, a hide).
        //   Weapon -> a Weapon extension only.
        //   Armour -> an Armour extension only.
        //   other  -> no type-specific extension.
        bool isBody = component.ComponentType == ComponentType.Body;
        bool isWeapon = component.ComponentType == ComponentType.Weapon;
        bool isArmour = component.ComponentType == ComponentType.Armour;

        // Body data: required for a Body, forbidden for anything else.
        if (isBody && component.Body is null)
        {
            errors.Add($"Component '{component.Code}' is a Body but has no Body data.");
        }

        if (!isBody && component.Body is not null)
        {
            errors.Add($"Component '{component.Code}' is {component.ComponentType} but carries Body data.");
        }

        // Weapon data: required for a Weapon, optional for a Body (inherent attack), forbidden otherwise.
        if (isWeapon && component.Weapon is null)
        {
            errors.Add($"Component '{component.Code}' is a Weapon but has no Weapon data.");
        }

        if (!isWeapon && !isBody && component.Weapon is not null)
        {
            errors.Add($"Component '{component.Code}' is {component.ComponentType} but carries Weapon data.");
        }

        // Armour data: required for Armour, optional for a Body (inherent defence), forbidden otherwise.
        if (isArmour && component.Armour is null)
        {
            errors.Add($"Component '{component.Code}' is Armour but has no Armour data.");
        }

        if (!isArmour && !isBody && component.Armour is not null)
        {
            errors.Add($"Component '{component.Code}' is {component.ComponentType} but carries Armour data.");
        }

        // Weapon range sanity + slot-rule value length.
        if (component.Weapon is { } weapon && weapon.MinimumRange > weapon.MaximumRange)
        {
            errors.Add($"Component '{component.Code}' weapon MinimumRange ({weapon.MinimumRange}) exceeds MaximumRange ({weapon.MaximumRange}).");
        }

        // Slots can be exposed by any component type (not just bodies), so validate them at the component level.
        foreach (UnitComponentSlotDto slot in component.Slots)
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
    /// Loads any component referenced by a supersession link that isn't already tracked, because links
    /// may point at components that already exist in the database but aren't part of this payload.
    /// </summary>
    private async Task LoadComponentsReferencedBySupersessionsAsync(
        BulkUpsertUnitComponentsDto catalog,
        Dictionary<string, UnitComponent> componentsByCode,
        CancellationToken cancelToken)
    {
        string[] missingCodes = [.. catalog.ComponentSupersessions
            .SelectMany(s => new[] { s.PredecessorCode, s.SuccessorCode })
            .Where(code => !string.IsNullOrWhiteSpace(code) && !componentsByCode.ContainsKey(code))
            .Distinct()];

        if (missingCodes.Length == 0)
        {
            return;
        }

        List<UnitComponent> loaded = await context.UnitComponents
            .Where(c => missingCodes.Contains(c.Code))
            .ToListAsync(cancelToken);

        foreach (UnitComponent component in loaded)
        {
            componentsByCode[component.Code] = component;
        }
    }

    /// <summary>
    /// Validates the component supersession links: codes resolve, no self-supersession, no duplicate
    /// pairs and at most one recommended successor per predecessor. Returns an empty list when valid.
    /// </summary>
    private static List<string> ValidateComponentSupersessions(
        BulkUpsertUnitComponentsDto catalog,
        IReadOnlyDictionary<string, UnitComponent> componentsByCode)
    {
        List<string> errors = [];

        // Duplicate (predecessor, successor) pairs in the payload.
        string[] duplicatePairs = [.. catalog.ComponentSupersessions
            .GroupBy(s => (s.PredecessorCode, s.SuccessorCode))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.PredecessorCode} -> {g.Key.SuccessorCode}")];

        if (duplicatePairs.Length > 0)
        {
            errors.Add($"Duplicate component supersession pair(s): {string.Join(", ", duplicatePairs)}");
        }

        foreach (ComponentSupersessionDto link in catalog.ComponentSupersessions)
        {
            if (string.IsNullOrWhiteSpace(link.PredecessorCode) || string.IsNullOrWhiteSpace(link.SuccessorCode))
            {
                errors.Add("A component supersession is missing its PredecessorCode or SuccessorCode.");
                continue;
            }

            if (link.PredecessorCode == link.SuccessorCode)
            {
                errors.Add($"Component '{link.PredecessorCode}' cannot supersede itself.");
            }

            if (!componentsByCode.ContainsKey(link.PredecessorCode))
            {
                errors.Add($"Component supersession references unknown predecessor code '{link.PredecessorCode}'.");
            }

            if (!componentsByCode.ContainsKey(link.SuccessorCode))
            {
                errors.Add($"Component supersession references unknown successor code '{link.SuccessorCode}'.");
            }
        }

        // A predecessor may flag at most one of its successors as the recommended upgrade.
        string[] predecessorsWithMultipleRecommended = [.. catalog.ComponentSupersessions
            .Where(s => s.IsRecommended)
            .GroupBy(s => s.PredecessorCode)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)];

        if (predecessorsWithMultipleRecommended.Length > 0)
        {
            errors.Add($"More than one recommended successor for predecessor(s): {string.Join(", ", predecessorsWithMultipleRecommended)}");
        }

        return errors;
    }

    /// <summary>
    /// Reconciles each mentioned predecessor's outgoing supersession links to exactly those in the
    /// payload — updating <c>IsRecommended</c> on kept links, adding new ones and deleting the surplus.
    /// Predecessors absent from the payload are left untouched.
    /// </summary>
    private async Task ReconcileComponentSupersessionsAsync(
        BulkUpsertUnitComponentsDto catalog,
        IReadOnlyDictionary<string, UnitComponent> componentsByCode,
        CancellationToken cancelToken)
    {
        foreach (IGrouping<string, ComponentSupersessionDto> desiredForPredecessor in catalog.ComponentSupersessions.GroupBy(s => s.PredecessorCode))
        {
            UnitComponent predecessor = componentsByCode[desiredForPredecessor.Key];

            // Existing links only exist for an already-persisted predecessor (a brand-new component has none).
            List<ComponentSupersession> existingLinks = predecessor.Id == 0
                ? []
                : await context.ComponentSupersessions
                    .Where(s => s.PredecessorComponentId == predecessor.Id)
                    .ToListAsync(cancelToken);

            Dictionary<int, ComponentSupersession> existingBySuccessorId = existingLinks.ToDictionary(s => s.SuccessorComponentId);
            HashSet<int> keptSuccessorIds = [];

            foreach (ComponentSupersessionDto desired in desiredForPredecessor)
            {
                UnitComponent successor = componentsByCode[desired.SuccessorCode];

                // A persisted successor with an existing link is updated in place; everything else
                // (new successor, or a successor not yet linked) is added, wired by navigation so EF
                // resolves the foreign keys even when either end is a brand-new component.
                if (successor.Id != 0 && existingBySuccessorId.TryGetValue(successor.Id, out ComponentSupersession? existingLink))
                {
                    existingLink.IsRecommended = desired.IsRecommended;
                    keptSuccessorIds.Add(successor.Id);
                }
                else
                {
                    context.ComponentSupersessions.Add(new ComponentSupersession
                    {
                        Predecessor = predecessor,
                        Successor = successor,
                        IsRecommended = desired.IsRecommended,
                    });
                }
            }

            // Drop links whose successor is no longer requested (NoAction FK => delete explicitly).
            List<ComponentSupersession> removedLinks = [.. existingLinks.Where(s => !keptSuccessorIds.Contains(s.SuccessorComponentId))];
            context.ComponentSupersessions.RemoveRange(removedLinks);
        }
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
            DesignerImagePath = dto.DesignerImagePath,
            ComponentType = dto.ComponentType,
            Weight = dto.Weight,
            Detection = dto.Detection,
            Visibility = dto.Visibility,
            Body = dto.Body is { } bodyDto ? BuildBody(bodyDto) : null,
            Weapon = dto.Weapon is { } weaponDto ? BuildWeapon(weaponDto) : null,
            Armour = dto.Armour is { } armourDto ? BuildArmour(armourDto) : null,
            Slots = [.. dto.Slots.Select(BuildSlot)],
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
            Power = dto.Power,
            Hitpoints = dto.Hitpoints,
            MinimumUnitSize = dto.MinimumUnitSize,
            MaximumUnitSize = dto.MaximumUnitSize,
            SizeIncrement = dto.SizeIncrement,
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
            DualWield = dto.DualWield,
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
        existing.DesignerImagePath = dto.DesignerImagePath;
        existing.Weight = dto.Weight;
        existing.Detection = dto.Detection;
        existing.Visibility = dto.Visibility;

        switch (dto.ComponentType)
        {
            case ComponentType.Body:
                UpdateBody(existing.Body!, dto.Body!);
                // A body may additionally carry an inherent weapon and/or armour; reconcile each since the
                // payload can add, update or clear them between upserts.
                ReconcileOptionalWeapon(existing, dto.Weapon);
                ReconcileOptionalArmour(existing, dto.Armour);
                break;

            case ComponentType.Weapon:
                UpdateWeapon(existing.Weapon!, dto.Weapon!);
                break;

            case ComponentType.Armour:
                existing.Armour!.Resistance = ToDamageProfile(dto.Armour!.Resistance);
                break;
        }

        // Slots live on the component itself (any type), so reconcile them here rather than per-extension.
        UpdateSlots(existing, dto.Slots);

        MergeTagJoins(existing, dto.TagCodes, tagsByCode);
    }

    /// <summary>
    /// Fully replaces a component's slots (and their rules) from the payload. Old slots and rules are
    /// deleted explicitly (NoAction FKs => no orphan cascade); deleting rules first keeps the FK order valid.
    /// </summary>
    private void UpdateSlots(UnitComponent existing, IReadOnlyCollection<UnitComponentSlotDto> slots)
    {
        List<UnitComponentSlot> oldSlots = [.. existing.Slots];

        foreach (UnitComponentSlot oldSlot in oldSlots)
        {
            context.UnitComponentSlotRules.RemoveRange(oldSlot.Rules);
        }

        context.UnitComponentSlots.RemoveRange(oldSlots);
        existing.Slots = [.. slots.Select(BuildSlot)];
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
    /// Overwrites a body extension's scalars. Slots are reconciled separately at the component level
    /// (see <see cref="UpdateSlots"/>), since any component type can expose them.
    /// </summary>
    private static void UpdateBody(UnitComponentBody body, UnitComponentBodyDto dto)
    {
        body.MovementDomain = dto.MovementDomain;
        body.CampaignMovementPoints = dto.CampaignMovementPoints;
        body.BattlefieldMovementPoints = dto.BattlefieldMovementPoints;
        body.Power = dto.Power;
        body.Hitpoints = dto.Hitpoints;
        body.MinimumUnitSize = dto.MinimumUnitSize;
        body.MaximumUnitSize = dto.MaximumUnitSize;
        body.SizeIncrement = dto.SizeIncrement;
    }

    /// <summary>
    /// Reconciles a component's optional weapon extension against the payload: builds it when newly
    /// present, updates it in place when it stays, and deletes it (with its domain modifiers) when the
    /// payload drops it. Used for a body's inherent attack, which may be added or removed between upserts.
    /// </summary>
    private void ReconcileOptionalWeapon(UnitComponent existing, UnitComponentWeaponDto? dto)
    {
        if (dto is null)
        {
            if (existing.Weapon is { } weapon)
            {
                // NoAction FKs => delete the modifiers first, then the weapon row, explicitly.
                context.RemoveRange(weapon.DomainModifiers);
                context.Remove(weapon);
                existing.Weapon = null;
            }

            return;
        }

        if (existing.Weapon is { } existingWeapon)
        {
            UpdateWeapon(existingWeapon, dto);
        }
        else
        {
            existing.Weapon = BuildWeapon(dto);
        }
    }

    /// <summary>
    /// Reconciles a component's optional armour extension against the payload: builds it when newly
    /// present, updates it in place when it stays, and deletes it when the payload drops it.
    /// </summary>
    private void ReconcileOptionalArmour(UnitComponent existing, UnitComponentArmourDto? dto)
    {
        if (dto is null)
        {
            if (existing.Armour is { } armour)
            {
                context.Remove(armour);
                existing.Armour = null;
            }

            return;
        }

        if (existing.Armour is { } existingArmour)
        {
            existingArmour.Resistance = ToDamageProfile(dto.Resistance);
        }
        else
        {
            existing.Armour = BuildArmour(dto);
        }
    }

    /// <summary>
    /// Overwrites a weapon extension's scalars and fully replaces its domain modifiers.
    /// </summary>
    private void UpdateWeapon(UnitComponentWeapon weapon, UnitComponentWeaponDto dto)
    {
        weapon.Attack = ToDamageProfile(dto.Attack);
        weapon.MinimumRange = dto.MinimumRange;
        weapon.MaximumRange = dto.MaximumRange;
        weapon.DualWield = dto.DualWield;

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
