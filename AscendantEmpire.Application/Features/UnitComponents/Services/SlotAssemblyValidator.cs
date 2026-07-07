using AscendantEmpire.Domain.Entities.UnitDesigns;

namespace AscendantEmpire.Application.Features.UnitComponents.Services;

/// <summary>
/// Validates that a template's fitted components are structurally sound: every component sits in a slot
/// the template's body actually exposes, satisfies that slot's acceptance rules, and no slot is filled twice.
/// </summary>
/// <remarks>
/// <para>Only bodies expose slots, so every fitted component sits directly in one of the body's slots.
/// Rule acceptance is delegated to <see cref="SlotRuleEvaluator"/>, the single source of truth for
/// "what fits where".</para>
/// <para>Pure and static (like <see cref="SlotRuleEvaluator"/>) so it can be reused by template
/// compilation and any future template-fitting writer, and unit-tested without a database.</para>
/// </remarks>
public static class SlotAssemblyValidator
{
    /// <summary>
    /// Validates the fitted components. Returns an empty list when valid, otherwise one message per
    /// problem. Each fitted component must have its <see cref="UnitTemplateComponent.UnitComponentSlot"/>
    /// (with its rules) and its <see cref="UnitTemplateComponent.UnitComponent"/> (with tags) loaded.
    /// </summary>
    /// <param name="bodyComponentId">The template's body component id — the owner of every fitted component's slot.</param>
    /// <param name="fittedComponentsSource">Every fitted component in the template.</param>
    public static List<string> Validate(int bodyComponentId, IEnumerable<UnitTemplateComponent> fittedComponentsSource)
    {
        // Materialize once: the collection is walked several times below.
        List<UnitTemplateComponent> fittedComponents = [.. fittedComponentsSource];

        List<string> errors = [];

        foreach (UnitTemplateComponent fitted in fittedComponents)
        {
            UnitComponentSlot slot = fitted.UnitComponentSlot;

            // The slot must be exposed by the template's body.
            if (slot.UnitComponentBodyId != bodyComponentId)
            {
                errors.Add($"Fitted component {fitted.Id} uses slot {slot.Id}, which belongs to body {slot.UnitComponentBodyId}, not the template's body {bodyComponentId}.");
                continue;
            }

            // The fitted component must satisfy the slot's acceptance rules (reusing the shared evaluator).
            List<string> tagCodes = [.. fitted.UnitComponent.Tags.Select(t => t.Tag.Code)];

            if (!SlotRuleEvaluator.Accepts(fitted.UnitComponent.ComponentType, tagCodes, slot.Rules))
            {
                errors.Add($"Component '{fitted.UnitComponent.Code}' does not satisfy the acceptance rules of slot {slot.Id}.");
            }
        }

        // One component per body slot (mirrors the DB unique index).
        IEnumerable<IGrouping<int, UnitTemplateComponent>> duplicates = fittedComponents
            .GroupBy(f => f.UnitComponentSlotId)
            .Where(g => g.Count() > 1);

        foreach (IGrouping<int, UnitTemplateComponent> group in duplicates)
        {
            errors.Add($"Slot {group.Key} is filled more than once.");
        }

        return errors;
    }
}
