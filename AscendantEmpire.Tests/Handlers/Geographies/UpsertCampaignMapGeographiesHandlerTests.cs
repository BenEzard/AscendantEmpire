using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Geographies.Handlers;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Handlers.Geographies;

/// <summary>
/// Tests for <see cref="UpsertCampaignMapGeographiesHandler"/>, whose update path is a separate block of assignments from the insert path it
/// delegates to, so the two can fall out of step as columns are added.
/// </summary>
internal class UpsertCampaignMapGeographiesHandlerTests
{
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(UpsertCampaignMapGeographiesHandlerTests), new InMemoryDatabaseRoot())
        .Options;

    private AscendantEmpireContext _context = null!;
    private UpsertCampaignMapGeographiesHandler _handler = null!;
    private IMediator _mediator = null!;

    [SetUp]
    public async Task Setup()
    {
        _context = new AscendantEmpireContext(DbOptions);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        _mediator = Substitute.For<IMediator>();
        _handler = new UpsertCampaignMapGeographiesHandler(new NullLogger<UpsertCampaignMapGeographiesHandler>(), _context, _mediator);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test, Description("Pins that an upsert over an existing row carries the terrain flags, because the handler assigns them in a separate place from the insert and the two can drift.")]
    public async Task ShouldKeepTheTerrainFlagsWhenUpdatingAnExistingGeographyAsync()
    {
        _context.CampaignGeographies.Add(new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.ShallowWater,
            RelativeElevation = -1,
            Name = "Shallow Water",
            MovementCost = 1.0m,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true,
            IsWater = false,
            IsTraversable = true
        });

        await _context.SaveChangesAsync();

        var request = new UpsertCampaignMapGeographiesRequest(
        [
            new()
            {
                CampaignMapGeographyType = CampaignMapGeographyType.ShallowWater,
                RelativeElevation = -1,
                Name = "Shallow Water",
                MovementCost = 1.0m,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = true,
                IsWater = true,
                IsTraversable = false
            }
        ]);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        CampaignGeography updated = await _context.CampaignGeographies
            .SingleAsync(g => g.CampaignMapGeographyTypeId == CampaignMapGeographyType.ShallowWater);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "The upsert failed, when it should have succeeded.");
            Assert.That(updated.IsWater, Is.True, "Is Water was not carried onto the existing row.");
            Assert.That(updated.IsTraversable, Is.False, "Is Traversable was not carried onto the existing row.");
        }
    }
}
