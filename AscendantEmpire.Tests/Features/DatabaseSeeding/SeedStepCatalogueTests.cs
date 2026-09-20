using AscendantEmpire.Application;
using AscendantEmpire.Application.Features.DatabaseSeeding.Classes;
using AscendantEmpire.Application.Features.DatabaseSeeding.Steps;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.Extensions.DependencyInjection;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests that the registered seed steps cover the <see cref="SeedTarget"/> enum exactly and form a graph the resolver can walk.
/// </summary>
/// <remarks>
/// <para>
/// No database: the steps are resolved from a container built by the real registration method, so what is pinned here is the registration itself rather
/// than a list restated in the test. Nothing is executed — every check is over the steps' metadata.
/// </para>
/// <para>
/// This is the guard against the one mistake the design makes easy: adding a seed-data class and its enum member, and forgetting the step that ties them together. 
/// Without it the target would simply never appear in the dialog and never be seeded, with nothing to say so.
/// </para>
/// </remarks>
[TestFixture]
internal class SeedStepCatalogueTests
{
    private ISeedStep[] _steps = [];

    [OneTimeSetUp]
    public void ResolveRegisteredSteps()
    {
        // The registration method pulls in the whole application graph, so the steps are read out of the
        // descriptors rather than built — nothing here needs a live DbContext or a configured mediator.
        var services = new ServiceCollection();
        services.AddApplicationServices();

        _steps =
        [
            .. services
                .Where(descriptor => descriptor.ServiceType == typeof(ISeedStep))
                .Select(descriptor => (ISeedStep)Activator.CreateInstance(descriptor.ImplementationType!)!)
        ];
    }

    [Test, Description("Every SeedTarget has a registered step, so adding an enum member without its step fails here rather than silently never being seeded.")]
    public void ShouldRegisterAStepForEverySeedTarget()
    {
        IEnumerable<SeedTarget> registeredTargets = _steps.Select(step => step.Target);

        SeedTarget[] missingTargets = [.. Enum.GetValues<SeedTarget>().Except(registeredTargets)];

        Assert.That(missingTargets, Is.Empty, $"Seed targets with no registered step: {string.Join(", ", missingTargets)}");
    }

    [Test, Description("No two steps claim the same target, because which one ran would otherwise be decided by registration order alone.")]
    public void ShouldRegisterAtMostOneStepPerSeedTarget()
    {
        Assert.That(_steps.Select(step => step.Target), Is.Unique);
    }

    [Test, Description("Every declared dependency names a registered target, so a step cannot wait on a prerequisite nothing can satisfy.")]
    public void ShouldResolveEveryDeclaredDependency()
    {
        HashSet<SeedTarget> registeredTargets = [.. _steps.Select(step => step.Target)];

        foreach (ISeedStep step in _steps)
        {
            SeedTarget[] unresolved = [.. step.DependsOn.Where(dependency => registeredTargets.Contains(dependency) == false)];

            Assert.That(unresolved, Is.Empty, $"Step '{step.Target}' depends on unregistered target(s): {string.Join(", ", unresolved)}");
        }
    }

    [Test, Description("The whole graph resolves, which is what proves it is acyclic — a cycle would make a full seed impossible to order.")]
    public void ShouldResolveEverySeedTargetTogether()
    {
        SeedTarget[] everyTarget = Enum.GetValues<SeedTarget>();

        IReadOnlyList<ISeedStep> resolved = SeedTargetResolver.Resolve(_steps, everyTarget);

        Assert.That(resolved.Select(step => step.Target), Is.EquivalentTo(everyTarget));
    }

    [Test, Description("No step declares itself as a dependency, which would deadlock the resolver on a single node.")]
    public void ShouldNotLetAStepDependOnItself()
    {
        foreach (ISeedStep step in _steps)
        {
            Assert.That(step.DependsOn, Does.Not.Contain(step.Target), $"Step '{step.Target}' depends on itself.");
        }
    }

    [Test, Description("Every step carries the label, description and row count the selection dialog renders, so no target shows up in the UI as a blank row.")]
    public void ShouldGiveEveryStepTheMetadataTheDialogNeeds()
    {
        foreach (ISeedStep step in _steps)
        {
            Assert.Multiple(() =>
            {
                Assert.That(step.DisplayName, Is.Not.Null.And.Not.Empty, $"Step '{step.Target}' has no DisplayName.");
                Assert.That(step.Description, Is.Not.Null.And.Not.Empty, $"Step '{step.Target}' has no Description.");
                Assert.That(step.ItemCount, Is.GreaterThan(0), $"Step '{step.Target}' would seed nothing.");
            });
        }
    }

    [Test, Description("Sort orders are unique within a group, so two rows never have an order between them decided arbitrarily.")]
    public void ShouldGiveEveryStepADistinctSortOrderWithinItsGroup()
    {
        foreach (IGrouping<SeedTargetGroup, ISeedStep> group in _steps.GroupBy(step => step.Group))
        {
            Assert.That(group.Select(step => step.SortOrder), Is.Unique, $"Group '{group.Key}' has duplicate sort orders.");
        }
    }
}
