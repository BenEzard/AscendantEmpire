using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.Classes;
using AscendantEmpire.Application.Features.DatabaseSeeding.Handlers;
using AscendantEmpire.Application.Features.DatabaseSeeding.Steps;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests that initiating a seed persists a job carrying the fully expanded target list and enqueues it.
/// </summary>
/// <remarks>
/// <para>
/// In-memory database: the handler adds one row and saves it, which is ordinary EF work. It needs no constraint enforcement, no raw SQL and no transaction,
/// so the containerised tier would buy nothing.
/// </para>
/// <para>
/// The expansion is what is really being pinned. The row's <c>MetadataJson</c> is the only record of what a seed job will do, so a handler that stored the
/// raw selection would leave the executor to under-seed a database and the row claiming it had not.
/// </para>
/// </remarks>
[TestFixture]
internal class InitiateDatabaseSeedHandlerTests : InMemoryTestBase
{
    [Test, Description("A selection naming only a leaf target is expanded over its prerequisites before it is stored, so the job records what it will really seed.")]
    public async Task ShouldStoreTheExpandedTargetListAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var channel = new BackgroundProcessingChannel(Options.Create(new BackgroundProcessingSettings()));
        InitiateDatabaseSeedHandler handler = BuildHandler(context, channel);

        ServiceResult<int> result = await handler.Handle(
            new InitiateDatabaseSeedRequest([SeedTarget.StarterTemplates]), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, string.Join("; ", result.Errors));

        Processing processing = await context.Processing.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(processing.ProcessType, Is.EqualTo(ProcessType.SeedDatabase));
            Assert.That(processing.Status, Is.EqualTo(ProcessingStatus.Pending));
            Assert.That(DatabaseSeedMetadata.TryRead(processing.MetadataJson), Is.EquivalentTo(new[]
            {
                SeedTarget.TagTypes,
                SeedTarget.Tags,
                SeedTarget.UnitComponents,
                SeedTarget.StarterTemplates,
            }));
        });
    }

    [Test, Description("The new ProcessingId is both returned and queued, so the caller can poll the job the dispatcher is about to pick up.")]
    public async Task ShouldEnqueueTheProcessingIdItReturnsAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var channel = new BackgroundProcessingChannel(Options.Create(new BackgroundProcessingSettings()));
        InitiateDatabaseSeedHandler handler = BuildHandler(context, channel);

        ServiceResult<int> result = await handler.Handle(
            new InitiateDatabaseSeedRequest([SeedTarget.Biomes]), CancellationToken.None);

        Assert.That(channel.Reader.TryRead(out int queuedProcessingId), Is.True, "Nothing was queued.");
        Assert.That(queuedProcessingId, Is.EqualTo(result.SuccessValue));
    }

    [Test, Description("An empty selection is rejected rather than queueing a job that would seed nothing.")]
    public async Task ShouldRejectAnEmptySelectionAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var channel = new BackgroundProcessingChannel(Options.Create(new BackgroundProcessingSettings()));
        InitiateDatabaseSeedHandler handler = BuildHandler(context, channel);

        ServiceResult<int> result = await handler.Handle(
            new InitiateDatabaseSeedRequest([]), CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(await context.Processing.CountAsync(), Is.Zero, "A job was queued for an empty selection.");
        });
    }

    [Test, Description("A target with no registered step is refused up front, so a stale client gets an error rather than a job that quietly seeds less than it asked for.")]
    public async Task ShouldRejectATargetWithNoRegisteredStepAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var channel = new BackgroundProcessingChannel(Options.Create(new BackgroundProcessingSettings()));

        // Only the biome step is registered, so asking for geographies cannot be satisfied.
        InitiateDatabaseSeedHandler handler = BuildHandler(context, channel, new StubSeedStep(SeedTarget.Biomes, []));

        ServiceResult<int> result = await handler.Handle(
            new InitiateDatabaseSeedRequest([SeedTarget.Geographies]), CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(await context.Processing.CountAsync(), Is.Zero, "A job was queued for an unresolvable target.");
        });
    }

    /// <summary>
    /// Builds the handler over the given context and channel, with the unit-design chain registered unless specific steps are supplied.
    /// </summary>
    private static InitiateDatabaseSeedHandler BuildHandler(
        AscendantEmpireContext context,
        IBackgroundProcessingChannel channel,
        params ISeedStep[] steps)
    {
        ISeedStep[] registeredSteps = steps.Length > 0 ? steps : BuildDefaultSteps();

        return new InitiateDatabaseSeedHandler(
            new NullLogger<InitiateDatabaseSeedHandler>(),
            context,
            channel,
            registeredSteps);
    }

    /// <summary>
    /// The real graph's shape — an independent biome step plus the unit-design chain — without executing anything.
    /// </summary>
    private static ISeedStep[] BuildDefaultSteps()
    {
        return
        [
            new StubSeedStep(SeedTarget.Biomes, [], SeedTargetGroup.Map),
            new StubSeedStep(SeedTarget.TagTypes, []),
            new StubSeedStep(SeedTarget.Tags, [SeedTarget.TagTypes], sortOrder: 20),
            new StubSeedStep(SeedTarget.UnitComponents, [SeedTarget.Tags], sortOrder: 30),
            new StubSeedStep(SeedTarget.StarterTemplates, [SeedTarget.UnitComponents], sortOrder: 40),
        ];
    }

    /// <summary>
    /// A seed step that carries a place in the graph and refuses to run.
    /// </summary>
    /// <remarks>
    /// The initiating handler only resolves the graph; nothing it does executes a step, so running one would mean the handler had started doing the
    /// executor's job.
    /// </remarks>
    private sealed class StubSeedStep(
        SeedTarget target,
        IReadOnlyCollection<SeedTarget> dependsOn,
        SeedTargetGroup group = SeedTargetGroup.UnitDesign,
        int sortOrder = 10
    ) : ISeedStep
    {
        public SeedTarget Target => target;

        public SeedTargetGroup Group => group;

        public string DisplayName => target.ToString();

        public string Description => $"Stub step for {target}.";

        public IReadOnlyCollection<SeedTarget> DependsOn => dependsOn;

        public int SortOrder => sortOrder;

        public int ItemCount => 1;

        public Task<string> ExecuteAsync(IMediator mediator, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Initiating a seed never runs a step.");
        }
    }
}
