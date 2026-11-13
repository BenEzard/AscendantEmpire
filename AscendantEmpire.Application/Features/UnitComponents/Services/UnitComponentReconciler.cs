using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.UnitComponents.Services;

/// <summary>
/// Builds and reconciles the full <see cref="UnitComponent"/> entity graph from a payload DTO.
/// </summary>
/// <remarks>
/// <para>Shared by the catalogue bulk importer and the single-component create/update endpoints so every
/// writer applies identical rules. Keeping one implementation matters: the FK crash this class was extracted
/// to fix came from slots being reconciled differently to every other child collection.</para>
/// <para>Every child collection is merged on its natural key rather than deleted and re-added, so a row that
/// the payload still describes keeps its database id — and with it anything referencing that id. Slots are
/// keyed by <see cref="UnitComponentSlot.Id"/>, tag joins by tag code, and weapon domain modifiers by
/// <c>Domain</c>. Acceptance rules are the one exception: they carry no id on the wire and nothing references
/// them, so there is no identity worth preserving.</para>
/// <para>Static, taking the context per call, matching <c>UnitTemplateAssembly</c> and
/// <c>SlotAssemblyValidator</c> in this same feature.</para>
/// </remarks>
internal static class UnitComponentReconciler
{
    /// <summary>
    /// Adds every include a full component graph needs, so the reconciler can replace children without
    /// tripping lazy-load or leaving an untracked collection behind.
    /// </summary>
    public static IQueryable<UnitComponent> WithFullGraph(IQueryable<UnitComponent> components)
    {
        return components
            .Include(component => component.Body!).ThenInclude(body => body.Slots).ThenInclude(slot => slot.Rules)
            .Include(component => component.Weapon!).ThenInclude(weapon => weapon.DomainModifiers)
            .Include(component => component.Weapon!).ThenInclude(weapon => weapon.Ammunition).ThenInclude(option => option.Asset)
            .Include(component => component.Armour)
            .Include(component => component.Tags).ThenInclude(join => join.Tag)
            .Include(component => component.SupplyRequirements).ThenInclude(requirement => requirement.SupplySet);
    }

    /// <summary>
    /// A component's slot ids in the canonical order — ascending id, which is how every other read in this
    /// feature orders slots. Empty for anything that isn't a body, since only bodies expose slots.
    /// </summary>
    public static IReadOnlyCollection<int> OrderedSlotIds(UnitComponent component)
    {
        if (component.Body is not { } body)
        {
            return [];
        }

        return [.. body.Slots.Select(slot => slot.Id).Order()];
    }

    /// <summary>
    /// Loads the tags for the given codes, keyed by code.
    /// </summary>
    public static async Task<Dictionary<string, Tag>> LoadTagsByCodeAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<string> tagCodes,
        CancellationToken cancelToken)
    {
        if (tagCodes.Count == 0)
        {
            return [];
        }

        string[] distinctCodes = [.. tagCodes.Distinct()];

        return await context.Tags
            .Where(tag => distinctCodes.Contains(tag.Code))
            .ToDictionaryAsync(tag => tag.Code, cancelToken);
    }

    /// <summary>
    /// Loads the supply sets for the given codes, keyed by code.
    /// </summary>
    public static async Task<Dictionary<string, SupplySet>> LoadSupplySetsByCodeAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<string> supplySetCodes,
        CancellationToken cancelToken)
    {
        if (supplySetCodes.Count == 0)
        {
            return [];
        }

        string[] distinctCodes = [.. supplySetCodes.Distinct()];

        return await context.SupplySets
            .Where(set => distinctCodes.Contains(set.Code))
            .ToDictionaryAsync(set => set.Code, cancelToken);
    }

    /// <summary>
    /// Loads the Assets for the given codes, keyed by code.
    /// </summary>
    public static async Task<Dictionary<string, Asset>> LoadAssetsByCodeAsync(
        AscendantEmpireContext context,
        IReadOnlyCollection<string> assetCodes,
        CancellationToken cancelToken)
    {
        if (assetCodes.Count == 0)
        {
            return [];
        }

        string[] distinctCodes = [.. assetCodes.Distinct()];

        return await context.Assets
            .Where(asset => distinctCodes.Contains(asset.Code))
            .ToDictionaryAsync(asset => asset.Code, cancelToken);
    }

    // --- Validation -----------------------------------------------------------------------------

    /// <summary>
    /// Validates a single component's fields and that its populated extension matches its
    /// <see cref="UnitComponentDto.ComponentType"/>.
    /// </summary>
    public static void ValidateComponent(UnitComponentDto component, List<string> errors)
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

        // Only bodies expose slots, and a body's slots live on its Body extension. A non-body component
        // therefore cannot carry any (its Body is null), which is how "Body-only slots" is enforced.
        foreach (UnitComponentSlotDto slot in component.Body?.Slots ?? [])
        {
            // `required` on the DTO only makes the caller send the property; it does not stop them sending
            // null. Check it here so a nameless slot fails with a usable message rather than a NOT NULL
            // violation from the database.
            if (string.IsNullOrWhiteSpace(slot.DisplayName))
            {
                errors.Add($"Component '{component.Code}' has a slot with no DisplayName.");
            }
            else if (slot.DisplayName.Length > UnitComponentSlot.DISPLAY_NAME_MAX_LENGTH)
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
    /// Validates the payload's slot identities: every code is present and unique within the body, and every
    /// id names a slot the target component actually owns.
    /// </summary>
    /// <remarks>
    /// An id belonging to another component — or to nothing at all — almost always means the caller mixed up
    /// payloads. Silently treating it as a brand-new slot would orphan the real one and drop its fittings, so
    /// it is rejected instead. A component that doesn't exist yet owns no slots, so any id on it is bogus.
    /// </remarks>
    public static void ValidateSlots(UnitComponentDto component, UnitComponent? existing, List<string> errors)
    {
        if (component.Body is not { } bodyDto)
        {
            return;
        }

        HashSet<int> ownedSlotIds = [.. existing?.Body?.Slots.Select(slot => slot.Id) ?? []];

        foreach (UnitComponentSlotDto slotDto in bodyDto.Slots)
        {
            if (string.IsNullOrWhiteSpace(slotDto.Code))
            {
                errors.Add($"Component '{component.Code}' has a slot with no Code.");
            }
            else if (slotDto.Code.Length > UnitComponentSlot.CODE_MAX_LENGTH)
            {
                errors.Add($"Component '{component.Code}' has a slot Code exceeding {UnitComponentSlot.CODE_MAX_LENGTH} characters.");
            }

            if (slotDto.Id is { } slotId && !ownedSlotIds.Contains(slotId))
            {
                errors.Add($"Component '{component.Code}' references slot id {slotId}, which does not belong to it.");
            }
        }

        // Codes address slots on the wire, so a duplicate would make "which slot?" ambiguous — and the unique
        // index would reject it anyway, as an opaque SQL error rather than a usable message.
        string[] duplicateCodes = [.. bodyDto.Slots
            .Where(slot => !string.IsNullOrWhiteSpace(slot.Code))
            .GroupBy(slot => slot.Code)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];

        if (duplicateCodes.Length > 0)
        {
            errors.Add($"Component '{component.Code}' has duplicate slot code(s): {string.Join(", ", duplicateCodes)}");
        }
    }

    /// <summary>
    /// Validates that an existing component isn't being re-submitted with a different
    /// <see cref="UnitComponentDto.ComponentType"/>, which the upsert treats as immutable.
    /// </summary>
    public static void ValidateTypeUnchanged(UnitComponentDto component, UnitComponent existing, List<string> errors)
    {
        if (existing.ComponentType != component.ComponentType)
        {
            errors.Add($"Component '{component.Code}' already exists as {existing.ComponentType} and cannot be changed to {component.ComponentType}.");
        }
    }

    /// <summary>
    /// Validates that every tag code the component references resolves to a known tag.
    /// </summary>
    public static void ValidateTagReferences(UnitComponentDto component, IReadOnlyCollection<string> knownTagCodes, List<string> errors)
    {
        foreach (string tagCode in component.TagCodes)
        {
            if (!knownTagCodes.Contains(tagCode))
            {
                errors.Add($"Component '{component.Code}' references unknown tag code '{tagCode}'.");
            }
        }
    }

    /// <summary>
    /// Validates a component's supply requirements:
    /// <list type="bullet">
    /// <item>where they may sit</item>
    /// <item>that each set resolves</item>
    /// <item>that each quantity is real</item>
    /// <item>and that no supply type is named more than once</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a Body may carry a requirement. That is enforced here rather than by the schema, whose foreign key is on the component, so letting another
    /// component type consume something later is a change to this method.
    /// </para>
    /// <para>
    /// A Component carries at most one requirement per <see cref="SupplyType"/>.
    /// </para>
    /// <para>
    /// The sets are passed whole rather than as codes because the type rule needs each one's <see cref="SupplySet.SupplyType"/>; every caller already
    /// loads them, so this costs no extra query.
    /// </para>
    /// </remarks>
    public static void ValidateSupplyRequirements(
        UnitComponentDto component,
        IReadOnlyDictionary<string, SupplySet> supplySetsByCode,
        List<string> errors)
    {
        if (component.SupplyRequirements.Count == 0)
        {
            return;
        }

        if (component.ComponentType != ComponentType.Body)
        {
            errors.Add($"Component '{component.Code}' is {component.ComponentType} but carries supply requirements, which only a Body may have.");

            return;
        }

        foreach (UnitComponentSupplyRequirementDto requirement in component.SupplyRequirements)
        {
            if (string.IsNullOrWhiteSpace(requirement.SupplySetCode))
            {
                errors.Add($"Component '{component.Code}' has a supply requirement with no SupplySetCode.");

                continue;
            }

            if (!supplySetsByCode.ContainsKey(requirement.SupplySetCode))
            {
                errors.Add($"Component '{component.Code}' references unknown supply set code '{requirement.SupplySetCode}'.");
            }

            if (requirement.Quantity <= 0)
            {
                errors.Add($"Component '{component.Code}' needs a Quantity above 0 for supply set '{requirement.SupplySetCode}'.");
            }
        }

        // Rows on one component are cumulative, so a second set of a type would double the need rather than offer a choice between them. Only a code that
        // resolved has a type to group by; one that did not is already an error of its own.
        IOrderedEnumerable<IGrouping<SupplyType, UnitComponentSupplyRequirementDto>> repeatedTypes = component.SupplyRequirements
            .Where(requirement => !string.IsNullOrWhiteSpace(requirement.SupplySetCode) && supplySetsByCode.ContainsKey(requirement.SupplySetCode))
            .GroupBy(requirement => supplySetsByCode[requirement.SupplySetCode].SupplyType)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key);

        foreach (IGrouping<SupplyType, UnitComponentSupplyRequirementDto> repeatedType in repeatedTypes)
        {
            string codes = string.Join("', '", repeatedType.Select(requirement => requirement.SupplySetCode).Order());

            errors.Add($"Component '{component.Code}' carries {repeatedType.Count()} {repeatedType.Key} supply requirements ('{codes}'), and may carry at most one of each supply type.");
        }
    }

    /// <summary>
    /// Validates a weapon's ammunition options: how many there may be, and that each Asset resolves.
    /// </summary>
    /// <remarks>
    /// Options hang off the weapon, so a Body's inherent attack is validated on the same terms as a Weapon component and there is no placement rule to
    /// apply. A component with no weapon data has nothing to check.
    /// </remarks>
    public static void ValidateWeaponAmmunition(
        UnitComponentDto component,
        IReadOnlyCollection<string> knownAssetCodes,
        List<string> errors)
    {
        if (component.Weapon is not { } weapon || weapon.Ammunition.Count == 0)
        {
            return;
        }

        if (weapon.Ammunition.Count > UnitComponentWeaponAmmunition.MAXIMUM_OPTIONS_PER_WEAPON)
        {
            errors.Add(
                $"Component '{component.Code}' names {weapon.Ammunition.Count} kinds of ammunition, but a weapon accepts at most "
                + $"{UnitComponentWeaponAmmunition.MAXIMUM_OPTIONS_PER_WEAPON}.");
        }

        foreach (UnitComponentWeaponAmmunitionDto option in weapon.Ammunition)
        {
            if (string.IsNullOrWhiteSpace(option.AssetCode))
            {
                errors.Add($"Component '{component.Code}' has an ammunition option with no AssetCode.");

                continue;
            }

            if (!knownAssetCodes.Contains(option.AssetCode))
            {
                errors.Add($"Component '{component.Code}' references unknown ammunition Asset code '{option.AssetCode}'.");
            }
        }

        // The merge keys on the Asset, so a second row for one Asset would be a silent overwrite before the unique index ever saw it.
        string[] duplicateCodes =
        [
            .. weapon.Ammunition
                .Where(option => !string.IsNullOrWhiteSpace(option.AssetCode))
                .GroupBy(option => option.AssetCode)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .Order()
        ];

        foreach (string duplicateCode in duplicateCodes)
        {
            errors.Add($"Component '{component.Code}' names ammunition Asset '{duplicateCode}' more than once.");
        }
    }

    // --- Insert ---------------------------------------------------------------------------------

    /// <summary>
    /// Builds the full <see cref="UnitComponent"/> entity graph (parent + type-specific extension +
    /// tag joins) from a DTO, wiring everything via navigation properties so EF assigns the shared
    /// keys and foreign keys on save.
    /// </summary>
    public static UnitComponent Build(
        UnitComponentDto dto,
        IReadOnlyDictionary<string, Tag> tagsByCode,
        IReadOnlyDictionary<string, SupplySet> supplySetsByCode,
        IReadOnlyDictionary<string, Asset> assetsByCode)
    {
        return new UnitComponent
        {
            Code = dto.Code,
            DisplayName = dto.DisplayName,
            Description = dto.Description,
            DesignerImagePath = dto.DesignerImagePath,
            ComponentType = dto.ComponentType,
            Weight = dto.Weight,
            Sight = dto.Sight,
            Detection = dto.Detection,
            Visibility = dto.Visibility,
            Body = dto.Body is { } bodyDto ? BuildBody(bodyDto) : null,
            Weapon = dto.Weapon is { } weaponDto ? BuildWeapon(weaponDto, assetsByCode) : null,
            Armour = dto.Armour is { } armourDto ? BuildArmour(armourDto) : null,
            Tags = BuildTagJoins(dto.TagCodes, tagsByCode),
            SupplyRequirements = BuildSupplyRequirements(dto.SupplyRequirements, supplySetsByCode),
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
            Slots = [.. dto.Slots.Select(BuildSlot)],
        };
    }

    private static UnitComponentSlot BuildSlot(UnitComponentSlotDto dto)
    {
        return new UnitComponentSlot
        {
            Code = dto.Code,
            DisplayName = dto.DisplayName,
            IsVisible = dto.IsVisible,
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

    private static UnitComponentWeapon BuildWeapon(UnitComponentWeaponDto dto, IReadOnlyDictionary<string, Asset> assetsByCode)
    {
        return new UnitComponentWeapon
        {
            Attack = ToDamageProfile(dto.Attack),
            MinimumRange = dto.MinimumRange,
            MaximumRange = dto.MaximumRange,
            DualWield = dto.DualWield,
            DomainModifiers = [.. dto.DomainModifiers.Select(BuildDomainModifier)],
            Ammunition = BuildAmmunition(dto.Ammunition, assetsByCode),
        };
    }

    /// <summary>
    /// Builds a weapon's ammunition rows, resolving each asset code to its <see cref="Asset"/> entity via navigation.
    /// </summary>
    private static ICollection<UnitComponentWeaponAmmunition> BuildAmmunition(
        IReadOnlyCollection<UnitComponentWeaponAmmunitionDto> ammunition,
        IReadOnlyDictionary<string, Asset> assetsByCode)
    {
        return
        [
            .. ammunition.Select(option => new UnitComponentWeaponAmmunition
            {
                Asset = assetsByCode[option.AssetCode],
            })
        ];
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
    /// Builds a component's supply-requirement rows, resolving each set code to its <see cref="SupplySet"/> entity via navigation.
    /// </summary>
    private static ICollection<UnitComponentSupplyRequirement> BuildSupplyRequirements(
        IReadOnlyCollection<UnitComponentSupplyRequirementDto> requirements,
        IReadOnlyDictionary<string, SupplySet> supplySetsByCode)
    {
        return
        [
            .. requirements.Select(requirement => new UnitComponentSupplyRequirement
            {
                SupplySet = supplySetsByCode[requirement.SupplySetCode],
                Quantity = requirement.Quantity,
            })
        ];
    }

    // --- Update ---------------------------------------------------------------------------------

    /// <summary>
    /// Overwrites an existing tracked component from the DTO: scalar fields, the type-specific
    /// extension (its type is guaranteed unchanged by validation) and every child collection.
    /// </summary>
    /// <param name="removedSlots">
    /// Accumulates the slots this component no longer exposes. The caller deletes them in one batch via
    /// <see cref="RemoveSlotsAndDependentsAsync"/> once every component has been reconciled, so finding their
    /// dependent rows costs a single query rather than one per body.
    /// </param>
    public static void Update(
        AscendantEmpireContext context,
        UnitComponent existing,
        UnitComponentDto dto,
        IReadOnlyDictionary<string, Tag> tagsByCode,
        IReadOnlyDictionary<string, SupplySet> supplySetsByCode,
        IReadOnlyDictionary<string, Asset> assetsByCode,
        List<UnitComponentSlot> removedSlots)
    {
        existing.DisplayName = dto.DisplayName;
        existing.Description = dto.Description;
        existing.DesignerImagePath = dto.DesignerImagePath;
        existing.Weight = dto.Weight;
        existing.Sight = dto.Sight;
        existing.Detection = dto.Detection;
        existing.Visibility = dto.Visibility;

        switch (dto.ComponentType)
        {
            case ComponentType.Body:
                UpdateBody(existing.Body!, dto.Body!);
                // Only bodies expose slots; reconcile them against the body extension.
                UpdateSlots(context, existing.Body!, dto.Body!.Slots, removedSlots);
                // A body may additionally carry an inherent weapon and/or armour; reconcile each since the
                // payload can add, update or clear them between upserts.
                ReconcileOptionalWeapon(context, existing, dto.Weapon, assetsByCode);
                ReconcileOptionalArmour(context, existing, dto.Armour);
                break;

            case ComponentType.Weapon:
                UpdateWeapon(context, existing.Weapon!, dto.Weapon!, assetsByCode);
                break;

            case ComponentType.Armour:
                existing.Armour!.Resistance = ToDamageProfile(dto.Armour!.Resistance);
                break;
        }

        MergeTagJoins(context, existing, dto.TagCodes, tagsByCode);
        MergeSupplyRequirements(context, existing, dto.SupplyRequirements, supplySetsByCode);
    }

    /// <summary>
    /// Reconciles a body's slots against the payload, matching on <see cref="UnitComponentSlot.Id"/> when the
    /// caller supplied one and falling back to <see cref="UnitComponentSlot.Code"/> when it didn't.
    /// </summary>
    /// <remarks>
    /// <para>The two keys serve different callers. The Entity Setup Tool round-trips real ids, which is the
    /// only way to tell "this slot's code was renamed" apart from "that slot went and a different one
    /// arrived". A catalogue imported from another database has no meaningful ids, so it matches on the code —
    /// the identity that survives the move.</para>
    /// <para>Either way a matched slot is updated in place and keeps its row, which is what every saved
    /// template's fitted component references. An unmatched payload slot is newly authored and is appended.
    /// Slots the payload no longer lists have genuinely been removed, so they are handed back to the caller
    /// via <paramref name="removedSlots"/> to be deleted in one batch.</para>
    /// <para>Merging rather than delete-all + re-add is what makes an edit non-destructive: adding a slot no
    /// longer disturbs any other slot's row, and no longer trips
    /// <c>FK_UnitTemplateComponent_UnitComponentSlot_UnitComponentSlotId</c>.</para>
    /// <para>Acceptance rules are still replaced wholesale per slot: they carry no id on the wire and nothing
    /// references them, so there is no identity worth preserving.</para>
    /// </remarks>
    private static void UpdateSlots(
        AscendantEmpireContext context,
        UnitComponentBody body,
        IReadOnlyCollection<UnitComponentSlotDto> slots,
        List<UnitComponentSlot> removedSlots)
    {
        Dictionary<int, UnitComponentSlot> existingById = body.Slots.ToDictionary(slot => slot.Id);
        Dictionary<string, UnitComponentSlot> existingByCode = body.Slots.ToDictionary(slot => slot.Code);
        HashSet<int> keptSlotIds = [];

        foreach (UnitComponentSlotDto slotDto in slots)
        {
            // Ids were validated against this body up front, so a non-null id always resolves. Falling back to
            // the code covers the id-less import; an unmatched code is simply a new slot.
            UnitComponentSlot? existingSlot = slotDto.Id is { } slotId
                ? existingById[slotId]
                : existingByCode.GetValueOrDefault(slotDto.Code);

            if (existingSlot is not null)
            {
                existingSlot.Code = slotDto.Code;
                existingSlot.DisplayName = slotDto.DisplayName;
                existingSlot.IsVisible = slotDto.IsVisible;
                existingSlot.UIXPosition = slotDto.UIXPosition;
                existingSlot.UIYPosition = slotDto.UIYPosition;

                context.UnitComponentSlotRules.RemoveRange(existingSlot.Rules);
                existingSlot.Rules = [.. slotDto.Rules.Select(BuildSlotRule)];

                keptSlotIds.Add(existingSlot.Id);
            }
            else
            {
                body.Slots.Add(BuildSlot(slotDto));
            }
        }

        // Anything the payload didn't mention is a genuine removal. Detach it from the body here, but leave
        // the deletion itself to the batched pass so the dependent rows can be found in a single query.
        List<UnitComponentSlot> slotsToRemove = [.. existingById.Values.Where(slot => !keptSlotIds.Contains(slot.Id))];

        foreach (UnitComponentSlot slotToRemove in slotsToRemove)
        {
            body.Slots.Remove(slotToRemove);
        }

        removedSlots.AddRange(slotsToRemove);
    }

    /// <summary>
    /// Deletes the slots the payload dropped, along with everything that references them.
    /// </summary>
    /// <remarks>
    /// <para>Every foreign key in this area is <c>NoAction</c>, so nothing cascades: each dependent row has to
    /// be removed explicitly and in FK order — the fitted template rows first, then the slots' acceptance
    /// rules, then the slots themselves.</para>
    /// <para>Removing a slot that a saved template still fits a component into is destructive by design: the
    /// caller asked for the slot to go, and the fitting cannot outlive it. It must not be silent though, so
    /// the affected templates are returned for the caller to log and report.</para>
    /// </remarks>
    /// <returns>The distinct display names of the templates that lost a fitted component.</returns>
    public static async Task<List<string>> RemoveSlotsAndDependentsAsync(
        AscendantEmpireContext context,
        List<UnitComponentSlot> removedSlots,
        CancellationToken cancelToken)
    {
        if (removedSlots.Count == 0)
        {
            return [];
        }

        int[] removedSlotIds = [.. removedSlots.Select(slot => slot.Id)];

        // The template is included so the warning and the response can name it rather than quote a bare id.
        List<UnitTemplateComponent> fittedComponents = await context.UnitTemplateComponents
            .Include(fitted => fitted.UnitTemplate)
            .Where(fitted => removedSlotIds.Contains(fitted.UnitComponentSlotId))
            .ToListAsync(cancelToken);

        context.UnitTemplateComponents.RemoveRange(fittedComponents);

        foreach (UnitComponentSlot removedSlot in removedSlots)
        {
            context.UnitComponentSlotRules.RemoveRange(removedSlot.Rules);
        }

        context.UnitComponentSlots.RemoveRange(removedSlots);

        return [.. fittedComponents
            .Select(fitted => fitted.UnitTemplate.DisplayName)
            .Distinct()
            .Order()];
    }

    /// <summary>
    /// Reconciles a component's tag joins to exactly the requested codes by adding the missing joins
    /// and deleting the surplus ones. Merging (rather than delete-all + re-add) avoids deleting and
    /// re-inserting the same composite-key join row in one transaction.
    /// </summary>
    private static void MergeTagJoins(
        AscendantEmpireContext context,
        UnitComponent existing,
        IReadOnlyCollection<string> desiredTagCodes,
        IReadOnlyDictionary<string, Tag> tagsByCode)
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
    /// Reconciles a component's supply requirements to exactly those requested, keyed by supply set, so a need whose quantity merely changed keeps its row.
    /// </summary>
    private static void MergeSupplyRequirements(
        AscendantEmpireContext context,
        UnitComponent existing,
        IReadOnlyCollection<UnitComponentSupplyRequirementDto> desiredRequirements,
        IReadOnlyDictionary<string, SupplySet> supplySetsByCode)
    {
        Dictionary<int, UnitComponentSupplyRequirementDto> desiredBySetId = desiredRequirements
            .ToDictionary(requirement => supplySetsByCode[requirement.SupplySetCode].Id);

        Dictionary<int, UnitComponentSupplyRequirement> currentBySetId = existing.SupplyRequirements
            .ToDictionary(requirement => requirement.SupplySetId);

        // NoAction FK => a dropped need has to be deleted explicitly.
        List<UnitComponentSupplyRequirement> removedRequirements =
            [.. existing.SupplyRequirements.Where(requirement => !desiredBySetId.ContainsKey(requirement.SupplySetId))];

        context.UnitComponentSupplyRequirements.RemoveRange(removedRequirements);

        foreach (UnitComponentSupplyRequirement removedRequirement in removedRequirements)
        {
            existing.SupplyRequirements.Remove(removedRequirement);
        }

        foreach (UnitComponentSupplyRequirementDto desired in desiredRequirements)
        {
            SupplySet supplySet = supplySetsByCode[desired.SupplySetCode];

            if (currentBySetId.TryGetValue(supplySet.Id, out UnitComponentSupplyRequirement? current))
            {
                current.Quantity = desired.Quantity;
            }
            else
            {
                existing.SupplyRequirements.Add(new UnitComponentSupplyRequirement
                {
                    SupplySet = supplySet,
                    Quantity = desired.Quantity,
                });
            }
        }
    }

    /// <summary>
    /// Overwrites a body extension's scalars. Its slots are reconciled separately (see <see cref="UpdateSlots"/>).
    /// </summary>
    private static void UpdateBody(UnitComponentBody body, UnitComponentBodyDto dto)
    {
        body.MovementDomain = dto.MovementDomain;
        body.CampaignMovementPoints = dto.CampaignMovementPoints;
        body.BattlefieldMovementPoints = dto.BattlefieldMovementPoints;
        body.Power = dto.Power;
        body.Hitpoints = dto.Hitpoints;
    }

    /// <summary>
    /// Reconciles a component's optional weapon extension against the payload: builds it when newly
    /// present, updates it in place when it stays, and deletes it (with its domain modifiers) when the
    /// payload drops it. Used for a body's inherent attack, which may be added or removed between upserts.
    /// </summary>
    private static void ReconcileOptionalWeapon(
        AscendantEmpireContext context,
        UnitComponent existing,
        UnitComponentWeaponDto? dto,
        IReadOnlyDictionary<string, Asset> assetsByCode)
    {
        if (dto is null)
        {
            if (existing.Weapon is { } weapon)
            {
                // NoAction FKs => delete the modifiers and ammunition first, then the weapon row, explicitly.
                context.RemoveRange(weapon.DomainModifiers);
                context.RemoveRange(weapon.Ammunition);
                context.Remove(weapon);
                existing.Weapon = null;
            }

            return;
        }

        if (existing.Weapon is { } existingWeapon)
        {
            UpdateWeapon(context, existingWeapon, dto, assetsByCode);
        }
        else
        {
            existing.Weapon = BuildWeapon(dto, assetsByCode);
        }
    }

    /// <summary>
    /// Reconciles a component's optional armour extension against the payload: builds it when newly
    /// present, updates it in place when it stays, and deletes it when the payload drops it.
    /// </summary>
    private static void ReconcileOptionalArmour(AscendantEmpireContext context, UnitComponent existing, UnitComponentArmourDto? dto)
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
    /// Overwrites a weapon extension's scalars and reconciles its domain modifiers and ammunition.
    /// </summary>
    private static void UpdateWeapon(
        AscendantEmpireContext context,
        UnitComponentWeapon weapon,
        UnitComponentWeaponDto dto,
        IReadOnlyDictionary<string, Asset> assetsByCode)
    {
        weapon.Attack = ToDamageProfile(dto.Attack);
        weapon.MinimumRange = dto.MinimumRange;
        weapon.MaximumRange = dto.MaximumRange;
        weapon.DualWield = dto.DualWield;

        MergeDomainModifiers(context, weapon, dto.DomainModifiers);
        MergeAmmunition(context, weapon, dto.Ammunition, assetsByCode);
    }

    /// <summary>
    /// Reconciles a weapon's ammunition to exactly the options requested: new Assets are added and surplus ones deleted.
    /// </summary>
    /// <remarks>
    /// An option carries nothing but its Asset, so a row that is still wanted is left alone rather than rewritten. Keying on the Asset — the natural key
    /// behind the unique index — also avoids deleting and re-inserting the same (weapon, Asset) row in one transaction, which would trip that index.
    /// </remarks>
    private static void MergeAmmunition(
        AscendantEmpireContext context,
        UnitComponentWeapon weapon,
        IReadOnlyCollection<UnitComponentWeaponAmmunitionDto> desiredAmmunition,
        IReadOnlyDictionary<string, Asset> assetsByCode)
    {
        HashSet<int> desiredAssetIds = [.. desiredAmmunition.Select(option => assetsByCode[option.AssetCode].Id)];
        HashSet<int> currentAssetIds = [.. weapon.Ammunition.Select(option => option.AssetId)];

        // Delete options whose Asset is no longer requested (NoAction FK => delete explicitly).
        List<UnitComponentWeaponAmmunition> removedOptions = [.. weapon.Ammunition.Where(option => !desiredAssetIds.Contains(option.AssetId))];
        context.UnitComponentWeaponAmmunitions.RemoveRange(removedOptions);

        foreach (UnitComponentWeaponAmmunition removedOption in removedOptions)
        {
            weapon.Ammunition.Remove(removedOption);
        }

        foreach (UnitComponentWeaponAmmunitionDto desired in desiredAmmunition)
        {
            Asset asset = assetsByCode[desired.AssetCode];

            if (!currentAssetIds.Contains(asset.Id))
            {
                weapon.Ammunition.Add(new UnitComponentWeaponAmmunition { Asset = asset });
            }
        }
    }

    /// <summary>
    /// Reconciles a weapon's domain modifiers to exactly the requested domains: existing rows are
    /// updated, missing domains added and surplus domains deleted. Merging by <c>Domain</c> (the
    /// natural key behind the unique index) avoids deleting and re-inserting the same (weapon, domain)
    /// row in one transaction, which would trip the unique index.
    /// </summary>
    private static void MergeDomainModifiers(
        AscendantEmpireContext context,
        UnitComponentWeapon weapon,
        IReadOnlyCollection<UnitComponentWeaponDomainModifierDto> desiredModifiers)
    {
        Dictionary<DomainType, UnitComponentWeaponDomainModifierDto> desiredByDomain = desiredModifiers.ToDictionary(modifier => modifier.Domain);
        Dictionary<DomainType, UnitComponentWeaponDomainModifier> currentByDomain = weapon.DomainModifiers.ToDictionary(modifier => modifier.Domain);

        // Delete modifiers whose domain is no longer requested (NoAction FK => delete explicitly).
        List<UnitComponentWeaponDomainModifier> removedModifiers = [.. weapon.DomainModifiers.Where(modifier => !desiredByDomain.ContainsKey(modifier.Domain))];
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

    // --- Supersession links ---------------------------------------------------------------------

    /// <summary>
    /// Validates one predecessor's outgoing supersession links: successors resolve, none is the predecessor
    /// itself, no successor is listed twice, and at most one is flagged as the recommended upgrade.
    /// </summary>
    public static void ValidateSupersessionsForPredecessor(
        string predecessorCode,
        IReadOnlyCollection<ComponentSupersessionDto> links,
        IReadOnlyDictionary<string, UnitComponent> componentsByCode,
        List<string> errors)
    {
        string[] duplicateSuccessors = [.. links
            .GroupBy(link => link.SuccessorCode)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];

        if (duplicateSuccessors.Length > 0)
        {
            errors.Add($"Duplicate component supersession pair(s): {string.Join(", ", duplicateSuccessors.Select(code => $"{predecessorCode} -> {code}"))}");
        }

        foreach (ComponentSupersessionDto link in links)
        {
            if (string.IsNullOrWhiteSpace(link.SuccessorCode))
            {
                errors.Add("A component supersession is missing its SuccessorCode.");
                continue;
            }

            if (link.SuccessorCode == predecessorCode)
            {
                errors.Add($"Component '{predecessorCode}' cannot supersede itself.");
            }
            else if (!componentsByCode.ContainsKey(link.SuccessorCode))
            {
                errors.Add($"Component supersession references unknown successor code '{link.SuccessorCode}'.");
            }
        }

        if (links.Count(link => link.IsRecommended) > 1)
        {
            errors.Add($"More than one recommended successor for predecessor(s): {predecessorCode}");
        }
    }

    /// <summary>
    /// Reconciles one predecessor's outgoing supersession links to exactly those requested — updating
    /// <c>IsRecommended</c> on kept links, adding new ones and deleting the surplus.
    /// </summary>
    /// <remarks>
    /// New links are wired by navigation rather than by id so EF resolves the foreign keys even when either
    /// end is a component being inserted in this same transaction.
    /// </remarks>
    public static async Task ReconcileSupersessionsForPredecessorAsync(
        AscendantEmpireContext context,
        UnitComponent predecessor,
        IReadOnlyCollection<ComponentSupersessionDto> desiredLinks,
        IReadOnlyDictionary<string, UnitComponent> componentsByCode,
        CancellationToken cancelToken)
    {
        // Existing links only exist for an already-persisted predecessor (a brand-new component has none).
        List<ComponentSupersession> existingLinks = predecessor.Id == 0
            ? []
            : await context.ComponentSupersessions
                .Where(link => link.PredecessorComponentId == predecessor.Id)
                .ToListAsync(cancelToken);

        Dictionary<int, ComponentSupersession> existingBySuccessorId = existingLinks.ToDictionary(link => link.SuccessorComponentId);
        HashSet<int> keptSuccessorIds = [];

        foreach (ComponentSupersessionDto desired in desiredLinks)
        {
            UnitComponent successor = componentsByCode[desired.SuccessorCode];

            // A persisted successor with an existing link is updated in place; everything else (a new
            // successor, or one not yet linked) is added.
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
        List<ComponentSupersession> removedLinks = [.. existingLinks.Where(link => !keptSuccessorIds.Contains(link.SuccessorComponentId))];
        context.ComponentSupersessions.RemoveRange(removedLinks);
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
