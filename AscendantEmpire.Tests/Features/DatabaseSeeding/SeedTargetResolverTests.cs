using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.Classes;
using AscendantEmpire.Application.Features.DatabaseSeeding.Steps;
using AscendantEmpire.Domain.Enums.System;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests that a chosen set of seed targets is expanded over its prerequisites and ordered so no step runs before something it depends on.
/// </summary>
/// <remarks>
/// <para>
/// No database and no mediator — the resolver is pure, taking nothing but the registered steps and the requested targets, so the graph rules can be pinned
/// on their own. Fake steps are used rather than the real ones so the shapes under test (a diamond, a cycle, a missing node) can be built deliberately.
/// </para>
/// <para>
/// This is the half of the feature the UI cannot be trusted with: the selection dialog ticks prerequisites for the user, but a request naming only a leaf
/// target must still seed everything underneath it.
/// </para>
/// </remarks>
[TestFixture]
internal class SeedTargetResolverTests
{
    [Test, Description("A leaf target alone pulls in its whole ancestry, so a selection naming only the starter templates still seeds the components, tags and tag types beneath them.")]
    public void ShouldExpandALeafTargetOverItsWholeAncestry()
    {
        IReadOnlyList<SeedTarget> expanded = SeedTargetResolver.ExpandTargets(BuildChain(), [SeedTarget.StarterTemplates]);

        Assert.That(expanded, Is.EquivalentTo(new[]
        {
            SeedTarget.TagTypes,
            SeedTarget.Tags,
            SeedTarget.UnitComponents,
            SeedTarget.StarterTemplates,
        }));
    }

    [Test, Description("A step is ordered after everything it depends on, so the seed never writes a row whose foreign key or code reference has not been created yet.")]
    public void ShouldOrderEveryStepAfterItsDependencies()
    {
        IReadOnlyList<ISeedStep> resolved = SeedTargetResolver.Resolve(BuildChain(), [SeedTarget.StarterTemplates]);

        List<SeedTarget> order = [.. resolved.Select(step => step.Target)];

        foreach (ISeedStep step in resolved)
        {
            foreach (SeedTarget dependency in step.DependsOn)
            {
                Assert.That(order.IndexOf(dependency), Is.LessThan(order.IndexOf(step.Target)),
                    $"'{step.Target}' is ordered before its dependency '{dependency}'.");
            }
        }
    }

    [Test, Description("A target reached by two different paths is emitted once, so a diamond in the graph does not run the shared step twice.")]
    public void ShouldEmitASharedDependencyOnlyOnce()
    {
        // Both UnitComponents and StarterTemplates depend on Tags directly, so Tags is reachable twice.
        ISeedStep[] steps =
        [
            new FakeSeedStep(SeedTarget.TagTypes, []),
            new FakeSeedStep(SeedTarget.Tags, [SeedTarget.TagTypes]),
            new FakeSeedStep(SeedTarget.UnitComponents, [SeedTarget.Tags]),
            new FakeSeedStep(SeedTarget.StarterTemplates, [SeedTarget.UnitComponents, SeedTarget.Tags]),
        ];

        IReadOnlyList<SeedTarget> expanded = SeedTargetResolver.ExpandTargets(steps, [SeedTarget.StarterTemplates]);

        Assert.That(expanded, Is.Unique);
        Assert.That(expanded, Has.Count.EqualTo(4));
    }

    [Test, Description("Naming a prerequisite explicitly alongside its dependent changes nothing, so the dialog ticking prerequisites cannot produce a different seed from naming the leaf alone.")]
    public void ShouldResolveTheSameWhetherOrNotPrerequisitesAreNamed()
    {
        IReadOnlyList<SeedTarget> fromLeafAlone = SeedTargetResolver.ExpandTargets(BuildChain(), [SeedTarget.StarterTemplates]);

        IReadOnlyList<SeedTarget> fromFullSelection = SeedTargetResolver.ExpandTargets(
            BuildChain(),
            [SeedTarget.StarterTemplates, SeedTarget.UnitComponents, SeedTarget.Tags, SeedTarget.TagTypes]);

        Assert.That(fromFullSelection, Is.EqualTo(fromLeafAlone));
    }

    [Test, Description("Independent targets keep a stable group-then-sort-order sequence, so a full seed runs the same way round however the container listed the steps.")]
    public void ShouldOrderIndependentStepsByGroupThenSortOrder()
    {
        // Registered back to front, and across both groups, so container order cannot be what produces the result.
        ISeedStep[] steps =
        [
            new FakeSeedStep(SeedTarget.Tags, [], SeedTargetGroup.UnitDesign, sortOrder: 20),
            new FakeSeedStep(SeedTarget.Biomes, [], SeedTargetGroup.Map, sortOrder: 20),
            new FakeSeedStep(SeedTarget.TagTypes, [], SeedTargetGroup.UnitDesign, sortOrder: 10),
            new FakeSeedStep(SeedTarget.Geographies, [], SeedTargetGroup.Map, sortOrder: 10),
        ];

        IReadOnlyList<SeedTarget> expanded = SeedTargetResolver.ExpandTargets(
            steps,
            [SeedTarget.Tags, SeedTarget.TagTypes, SeedTarget.Biomes, SeedTarget.Geographies]);

        Assert.That(expanded, Is.EqualTo(new[]
        {
            SeedTarget.Geographies,
            SeedTarget.Biomes,
            SeedTarget.TagTypes,
            SeedTarget.Tags,
        }));
    }

    [Test, Description("A requested target with no registered step is rejected rather than silently skipped, so a stale client cannot quietly seed less than it asked for.")]
    public void ShouldThrowWhenARequestedTargetHasNoStep()
    {
        ISeedStep[] steps = [new FakeSeedStep(SeedTarget.Biomes, [])];

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(
            () => SeedTargetResolver.ExpandTargets(steps, [SeedTarget.Geographies]));

        Assert.That(exception!.Message, Does.Contain(nameof(SeedTarget.Geographies)));
    }

    [Test, Description("A dependency that is not registered is rejected too, so a step cannot declare a prerequisite nothing can satisfy.")]
    public void ShouldThrowWhenADependencyHasNoStep()
    {
        ISeedStep[] steps = [new FakeSeedStep(SeedTarget.UnitComponents, [SeedTarget.Tags])];

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(
            () => SeedTargetResolver.ExpandTargets(steps, [SeedTarget.UnitComponents]));

        Assert.That(exception!.Message, Does.Contain(nameof(SeedTarget.Tags)));
    }

    [Test, Description("A cycle is reported as a cycle rather than recursing until the stack overflows, so a mis-declared dependency fails legibly.")]
    public void ShouldThrowWhenTheGraphContainsACycle()
    {
        ISeedStep[] steps =
        [
            new FakeSeedStep(SeedTarget.Tags, [SeedTarget.UnitComponents]),
            new FakeSeedStep(SeedTarget.UnitComponents, [SeedTarget.Tags]),
        ];

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(
            () => SeedTargetResolver.ExpandTargets(steps, [SeedTarget.Tags]));

        Assert.That(exception!.Message, Does.Contain("cycle"));
    }

    [Test, Description("Two steps claiming the same target are rejected, because which one would run is otherwise decided by registration order alone.")]
    public void ShouldThrowWhenTwoStepsClaimTheSameTarget()
    {
        ISeedStep[] steps =
        [
            new FakeSeedStep(SeedTarget.Biomes, []),
            new FakeSeedStep(SeedTarget.Biomes, []),
        ];

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(
            () => SeedTargetResolver.ExpandTargets(steps, [SeedTarget.Biomes]));

        Assert.That(exception!.Message, Does.Contain("More than one seed step"));
    }

    /// <summary>
    /// The unit-design chain: tag types, then tags, then components, then the starter templates.
    /// </summary>
    private static ISeedStep[] BuildChain()
    {
        return
        [
            new FakeSeedStep(SeedTarget.TagTypes, [], SeedTargetGroup.UnitDesign, sortOrder: 10),
            new FakeSeedStep(SeedTarget.Tags, [SeedTarget.TagTypes], SeedTargetGroup.UnitDesign, sortOrder: 20),
            new FakeSeedStep(SeedTarget.UnitComponents, [SeedTarget.Tags], SeedTargetGroup.UnitDesign, sortOrder: 30),
            new FakeSeedStep(SeedTarget.StarterTemplates, [SeedTarget.UnitComponents], SeedTargetGroup.UnitDesign, sortOrder: 40),
        ];
    }

    /// <summary>
    /// A seed step that records its place in the graph and does nothing when run.
    /// </summary>
    /// <remarks>
    /// The resolver never executes a step, so <see cref="ExecuteAsync"/> is unreachable from these tests — it throws rather than returning quietly, so a
    /// future test that does run one fails loudly instead of silently passing over a no-op.
    /// </remarks>
    private sealed class FakeSeedStep(
        SeedTarget target,
        IReadOnlyCollection<SeedTarget> dependsOn,
        SeedTargetGroup group = SeedTargetGroup.UnitDesign,
        int sortOrder = 10
    ) : ISeedStep
    {
        public SeedTarget Target => target;

        public SeedTargetGroup Group => group;

        public string DisplayName => target.ToString();

        public string Description => $"Fake step for {target}.";

        public IReadOnlyCollection<SeedTarget> DependsOn => dependsOn;

        public int SortOrder => sortOrder;

        public int ItemCount => 0;

        public Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The resolver tests never run a step.");
        }
    }
}
