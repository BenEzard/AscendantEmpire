using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Application.Features.UnitComponents.Services;

/// <summary>
/// Validates that a template carries an unambiguous tag for every <em>inheriting</em> tag type — one whose
/// <see cref="TagTypeInfo.IsInheritedToBase"/> flag is set.
/// </summary>
/// <remarks>
/// <para>
/// An inheriting tag type is one where a tag fitted onto the design overrides the equivalent tag on the base
/// (body) component, rather than sitting alongside it. <see cref="TagType.MilitaryOrganisationalGroup"/> is the
/// only such type today: a human body defaults to <c>infantry</c> (Commissioned), but fitting
/// <c>camping-goods</c> — which carries <c>settler</c> — makes the whole design Unled.
/// </para>
/// <para>
/// Because the fitted tag wins outright, a design may offer only one candidate to win with. Two components each
/// proposing a different organisation would leave no principled way to pick between them, so this is a hard
/// error at save time rather than a warning: a template with an ambiguous organisation cannot be compiled, and
/// therefore cannot be fielded, so deferring the failure would only move it somewhere less useful.
/// </para>
/// <para>
/// Pure and static (like <see cref="SlotAssemblyValidator"/>) so it can be unit-tested without a database. The
/// flags themselves are catalogue data, so the caller reads them and passes the resulting enum set in.
/// </para>
/// </remarks>
public static class InheritedTagValidator
{
    /// <summary>
    /// Validates the base and fitted components against the inheriting tag types. Returns an empty list when
    /// valid, otherwise one message per problem.
    /// </summary>
    /// <remarks>
    /// Every component passed in must have its <see cref="UnitComponent.Tags"/> collection loaded, each with its
    /// <see cref="UnitComponentTag.Tag"/>. Two rules are applied per inheriting tag type:
    /// <list type="number">
    /// <item>At most one <em>fitted</em> component may carry a tag of that type. The base is excluded — it is the
    /// value being overridden, not a competitor for the override.</item>
    /// <item>No single component — base included — may carry more than one tag of that type, since it would then
    /// be ambiguous on its own.</item>
    /// </list>
    /// The budget in the first rule is per tag type, not shared: one component may contribute the organisation
    /// tag while another contributes a tag of some future inheriting type.
    /// </remarks>
    /// <param name="baseComponent">The template's base (body) component, whose tags are the defaults being overridden.</param>
    /// <param name="fittedComponentsSource">Every component fitted into the base's slots.</param>
    /// <param name="inheritedTagTypes">The tag types flagged <see cref="TagTypeInfo.IsInheritedToBase"/>; an empty set disables every check.</param>
    public static List<string> Validate(
        UnitComponent baseComponent,
        IEnumerable<UnitTemplateComponent> fittedComponentsSource,
        IReadOnlySet<TagType> inheritedTagTypes)
    {
        List<string> errors = [];

        if (inheritedTagTypes.Count == 0)
        {
            return errors;
        }

        // Materialize once: the collection is walked once per inheriting tag type below.
        List<UnitComponent> fittedComponents = [.. fittedComponentsSource.Select(fitted => fitted.UnitComponent)];

        foreach (TagType inheritedTagType in inheritedTagTypes)
        {
            // Rule 2 first, so that a component that is ambiguous by itself is reported as such rather than
            // being counted towards — and blamed for — the design-wide limit below.
            ValidateSingleTagPerComponent(baseComponent, fittedComponents, inheritedTagType, errors);
            ValidateSingleFittedComponent(fittedComponents, inheritedTagType, errors);
        }

        return errors;
    }

    /// <summary>
    /// Adds an error for each component carrying more than one tag of the given inheriting type.
    /// </summary>
    private static void ValidateSingleTagPerComponent(
        UnitComponent baseComponent,
        List<UnitComponent> fittedComponents,
        TagType inheritedTagType,
        List<string> errors)
    {
        // The base is checked on the same terms as the fitted components: overriding an ambiguous default would
        // not make it any less ambiguous when nothing overrides it.
        foreach (UnitComponent component in fittedComponents.Prepend(baseComponent))
        {
            List<Tag> tagsOfType = TagsOfType(component, inheritedTagType);

            if (tagsOfType.Count > 1)
            {
                errors.Add(
                    $"Component '{component.Code}' carries more than one {inheritedTagType} tag ({FormatCodes(tagsOfType)}) — a component may carry at most one tag of an inherited type.");
            }
        }
    }

    /// <summary>
    /// Adds an error when more than one fitted component carries a tag of the given inheriting type.
    /// </summary>
    private static void ValidateSingleFittedComponent(
        List<UnitComponent> fittedComponents,
        TagType inheritedTagType,
        List<string> errors)
    {
        // Counted by component, not by distinct tag: two components carrying the *same* organisation tag is still
        // two components competing to override the base, and the limit is deliberately on the component.
        List<UnitComponent> carriers = [.. fittedComponents.Where(component => TagsOfType(component, inheritedTagType).Count > 0)];

        if (carriers.Count > 1)
        {
            errors.Add(
                $"{carriers.Count} fitted components carry a {inheritedTagType} tag ({FormatCodes(carriers)}) — only one fitted component may override the base component's tag of an inherited type.");
        }
    }

    /// <summary>
    /// The component's tags of one type, de-duplicated by tag id.
    /// </summary>
    /// <remarks>
    /// The same tag row joined twice to one component is a data duplicate, not a second opinion, so it must not
    /// read as ambiguity.
    /// </remarks>
    private static List<Tag> TagsOfType(UnitComponent component, TagType tagType)
    {
        return
        [
            .. component.Tags
                .Select(componentTag => componentTag.Tag)
                .Where(tag => tag.TagType == tagType)
                .DistinctBy(tag => tag.Id)
        ];
    }

    private static string FormatCodes(IEnumerable<Tag> tags)
    {
        return string.Join(", ", tags.Select(tag => $"'{tag.Code}'"));
    }

    private static string FormatCodes(IEnumerable<UnitComponent> components)
    {
        return string.Join(", ", components.Select(component => $"'{component.Code}'"));
    }
}
