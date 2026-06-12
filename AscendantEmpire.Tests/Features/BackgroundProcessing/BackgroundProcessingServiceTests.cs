using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AscendantEmpire.Tests.Features.BackgroundProcessing;

/// <summary>
/// Verifies the generic dispatcher routes a queued ProcessingId to the IProcessExecutor whose ProcessType matches, and that a throwing executor is backstopped to a Failed record.
/// </summary>
/// <remarks>
/// Uses an in-memory database and fake executors — no SQL Server / Docker required.
/// </remarks>
internal class BackgroundProcessingServiceTests
{
    private InMemoryDatabaseRoot _dbRoot = null!;
    private string _storeName = null!;
    private DbContextOptions<AscendantEmpireContext> _dbOptions = null!;

    [SetUp]
    public async Task Setup()
    {
        // A fresh root + unique store name per test keeps the in-memory store isolated.
        _dbRoot = new InMemoryDatabaseRoot();
        _storeName = $"BackgroundProcessingServiceTests_{Guid.NewGuid():N}";
        _dbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseInMemoryDatabase(_storeName, _dbRoot)
            .Options;

        await using var ctx = new AscendantEmpireContext(_dbOptions);
        await ctx.Database.EnsureCreatedAsync();
    }

    [Test, Description("Dispatches a queued job to the executor whose ProcessType matches, and not to others.")]
    public async Task DispatchesToMatchingExecutorAsync()
    {
        // Arrange — two executors registered; only the GenerateCampaignMap one should run.
        var generateExecutor = new FakeProcessExecutor(ProcessType.GenerateCampaignMap);
        var exportExecutor = new FakeProcessExecutor(ProcessType.ExportGalleryMap);

        int processingId = await SeedProcessingAsync(ProcessType.GenerateCampaignMap);

        var channel = new BackgroundProcessingChannel(Options.Create(new BackgroundProcessingSettings()));
        ServiceProvider provider = BuildProvider(channel, generateExecutor, exportExecutor);

        var service = new BackgroundProcessingService(
            channel,
            provider.GetRequiredService<IServiceScopeFactory>(),
            new NullLogger<BackgroundProcessingService>());

        // Act
        await service.StartAsync(CancellationToken.None);
        await channel.Writer.WriteAsync(processingId);

        bool ran = await generateExecutor.InvokedTask.WaitAsync(TimeSpan.FromSeconds(10)).ContinueWith(t => t.IsCompletedSuccessfully);

        await service.StopAsync(CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ran, Is.True, "The matching (GenerateCampaignMap) executor should have run.");
            Assert.That(generateExecutor.ReceivedProcessingId, Is.EqualTo(processingId), "The matching executor should receive the queued processing id.");
            Assert.That(exportExecutor.WasInvoked, Is.False, "The non-matching (ExportGalleryMap) executor should not have run.");
        }

        await provider.DisposeAsync();
    }

    [Test, Description("A throwing executor is backstopped: the processing record is marked Failed with the error message.")]
    public async Task ThrowingExecutorMarksRecordFailedAsync()
    {
        // Arrange — the executor throws instead of recording its own status.
        const string ErrorText = "boom from executor";
        var throwingExecutor = new FakeProcessExecutor(ProcessType.GenerateCampaignMap, throwMessage: ErrorText);

        int processingId = await SeedProcessingAsync(ProcessType.GenerateCampaignMap);

        var channel = new BackgroundProcessingChannel(Options.Create(new BackgroundProcessingSettings()));
        ServiceProvider provider = BuildProvider(channel, throwingExecutor);

        var service = new BackgroundProcessingService(
            channel,
            provider.GetRequiredService<IServiceScopeFactory>(),
            new NullLogger<BackgroundProcessingService>());

        // Act
        await service.StartAsync(CancellationToken.None);
        await channel.Writer.WriteAsync(processingId);

        Processing? persisted = await WaitForStatusAsync(processingId, ProcessingStatus.Failed, TimeSpan.FromSeconds(10));

        await service.StopAsync(CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(persisted, Is.Not.Null, "The processing record should have been marked Failed by the dispatcher backstop.");
            Assert.That(persisted!.Status, Is.EqualTo(ProcessingStatus.Failed));
            Assert.That(persisted.ErrorMessage, Is.EqualTo(ErrorText));
            Assert.That(persisted.CompletedAt, Is.Not.Null);
        }

        await provider.DisposeAsync();
    }

    // ---------------------------------------------------------------------

    private ServiceProvider BuildProvider(BackgroundProcessingChannel channel, params FakeProcessExecutor[] executors)
    {
        var services = new ServiceCollection();

        // Scoped context backed by the same in-memory store as the seed context.
        services.AddDbContext<AscendantEmpireContext>(o => o.UseInMemoryDatabase(_storeName, _dbRoot));

        services.AddSingleton<IBackgroundProcessingChannel>(channel);

        foreach (FakeProcessExecutor executor in executors)
        {
            services.AddSingleton<IProcessExecutor>(executor);
        }

        return services.BuildServiceProvider();
    }

    private async Task<int> SeedProcessingAsync(ProcessType processType)
    {
        await using var ctx = new AscendantEmpireContext(_dbOptions);
        var processing = new Processing
        {
            ProcessType = processType,
            Status = ProcessingStatus.Pending,
            StartedAt = DateTime.UtcNow,
            MetadataJson = "{}"
        };
        ctx.Processing.Add(processing);
        await ctx.SaveChangesAsync();
        return processing.ProcessingId;
    }

    private async Task<Processing?> WaitForStatusAsync(int processingId, ProcessingStatus status, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await using var ctx = new AscendantEmpireContext(_dbOptions);
            Processing? processing = await ctx.Processing.AsNoTracking().FirstOrDefaultAsync(p => p.ProcessingId == processingId);
            if (processing is not null && processing.Status == status)
            {
                return processing;
            }

            await Task.Delay(50);
        }

        return null;
    }

    /// <summary>
    /// Test double for <see cref="IProcessExecutor"/>.
    /// </summary>
    /// <remarks>
    /// Records the processing id it was called with (signalling via <see cref="InvokedTask"/>), and optionally throws to exercise the backstop.
    /// </remarks>
    private sealed class FakeProcessExecutor(ProcessType processType, string? throwMessage = null) : IProcessExecutor
    {
        private readonly TaskCompletionSource _invoked = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ProcessType ProcessType { get; } = processType;
        public bool WasInvoked { get; private set; }
        public int? ReceivedProcessingId { get; private set; }

        /// <summary>Completes when ExecuteAsync is first called.</summary>
        public Task InvokedTask => _invoked.Task;

        public Task ExecuteAsync(Processing processing, CancellationToken cancellationToken)
        {
            WasInvoked = true;
            ReceivedProcessingId = processing.ProcessingId;
            _invoked.TrySetResult();

            if (throwMessage is not null)
            {
                throw new InvalidOperationException(throwMessage);
            }

            return Task.CompletedTask;
        }
    }
}
