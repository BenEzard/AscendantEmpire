using AscendantEmpire.Domain.Entities.UnitDesigns;

namespace AscendantEmpire.Application.Features.UnitComponents.Services;

/// <summary>
/// Validates that a template's fitted-component tree is structurally sound: every component sits in a
/// slot its owner actually exposes, satisfies that slot's acceptance rules, and no slot is filled twice
/// under the same parent.
/// </summary>
/// <remarks>
/// <para>The owner of a fitted component is the template's <b>body</b> when the component is fitted
/// directly (no parent), otherwise the <b>parent fitted-component's</b> component. Rule acceptance is
/// delegated to <see cref="SlotRuleEvaluator"/>, the single source of truth for "what fits where".</para>
/// <para>Pure and static (like <see cref="SlotRuleEvaluator"/>) so it can be reused by template
/// compilation and any future template-fitting writer, and unit-tested without a database.</para>
/// </remarks>
public static class SlotAssemblyValidator
{
    /// <summary>
    /// Validates the fitted-component tree. Returns an empty list when valid, otherwise one message per
    /// problem. Each fitted component must have its <see cref="UnitTemplateComponent.UnitComponentSlot"/>
    /// (with its rules) and its <see cref="UnitTemplateComponent.UnitComponent"/> (with tags) loaded.
    /// </summary>
    /// <param name="bodyComponentId">The template's body component id — the owner of root-fitted components.</param>
    /// <param name="fittedComponentsSource">Every fitted component in the template (the whole tree, flattened).</param>
    public static List<string> Validate(int bodyComponentId, IEnumerable<UnitTemplateComponent> fittedComponentsSource)
    {
        // Materialize once: the collection is walked several times below.
        List<UnitTemplateComponent> fittedComponents = [.. fittedComponentsSource];

        List<string> errors = [];

        // Map each fitted-component id to the component it carries, so a child can resolve which slots its
        // parent exposes (the parent's component is the owner of the child's slot).
        Dictionary<int, int> componentIdByFittedId = fittedComponents.ToDictionary(f => f.Id, f => f.UnitComponentId);

        foreach (UnitTemplateComponent fitted in fittedComponents)
        {
            UnitComponentSlot slot = fitted.UnitComponentSlot;

            // Determine the owner component the slot must belong to.
            int expectedOwnerComponentId;

            if (fitted.ParentTemplateComponentId is { } parentId)
            {
                if (!componentIdByFittedId.TryGetValue(parentId, out expectedOwnerComponentId))
                {
                    errors.Add($"Fitted component {fitted.Id} references a parent {parentId} that is not part of the template.");
                    continue;
                }
            }
            else
            {
                expectedOwnerComponentId = bodyComponentId;
            }

            // The slot must be exposed by the owner component (body when root, else the parent's component).
            if (slot.UnitComponentId != expectedOwnerComponentId)
            {
                errors.Add($"Fitted component {fitted.Id} uses slot {slot.Id}, which belongs to component {slot.UnitComponentId}, not its owner {expectedOwnerComponentId}.");
                continue;
            }

            // The fitted component must satisfy the slot's acceptance rules (reusing the shared evaluator).
            List<string> tagCodes = [.. fitted.UnitComponent.Tags.Select(t => t.Tag.Code)];

            if (!SlotRuleEvaluator.Accepts(fitted.UnitComponent.ComponentType, tagCodes, slot.Rules))
            {
                errors.Add($"Component '{fitted.UnitComponent.Code}' does not satisfy the acceptance rules of slot {slot.Id}.");
            }
        }

        // One component per slot per parent (mirrors the DB unique indexes). Group by (parent, slot).
        IEnumerable<IGrouping<(int? Parent, int Slot), UnitTemplateComponent>> duplicates = fittedComponents
            .GroupBy(f => (f.ParentTemplateComponentId, f.UnitComponentSlotId))
            .Where(g => g.Count() > 1);

        foreach (IGrouping<(int? Parent, int Slot), UnitTemplateComponent> group in duplicates)
        {
            errors.Add($"Slot {group.Key.Slot} is filled more than once under the same parent.");
        }

        return errors;
    }
}
