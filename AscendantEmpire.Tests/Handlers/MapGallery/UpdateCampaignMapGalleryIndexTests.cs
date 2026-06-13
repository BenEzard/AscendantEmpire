using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Handlers.MapGallery;

internal class UpdateCampaignMapGalleryIndexTests
{
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(UpdateCampaignMapGalleryIndexTests), new InMemoryDatabaseRoot())
        .Options;

    private AscendantEmpireContext _context = null!;
    private UpdateCampaignMapIndexHandler _updateHandler = null!;

    [SetUp]
    public async Task Setup()
    {
        _context = new AscendantEmpireContext(DbOptions);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        _updateHandler = new UpdateCampaignMapIndexHandler(new NullLogger<UpdateCampaignMapIndexHandler>(), _context);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test, Description("Valid update should succeed and persist the new name and description.")]
    public async Task ValidUpdateShouldSucceedAsync()
    {
        const int ID = 1;

        _context.CampaignMapGalleryIndex.Add(new CampaignMapGalleryIndex
        {
            Id = ID,
            MapName = "Test Map",
            Description = "Old Description",
            Width = 50,
            Height = 50,
            CreationEngine = MapCreationEngine.BlankMap,
            CreationStatus = MapCreationStatus.Pending,
            Status = "Pending",
            PropertiesJson = "{}"
        });
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateCampaignMapIndexRequest(
            Id: ID,
            MapName: "New Map Name",
            Description: "A map for testing"
        );

        ServiceResultBase result = await _updateHandler.Handle(updateRequest, CancellationToken.None);

        using var verifyContext = new AscendantEmpireContext(DbOptions);
        var updated = verifyContext.CampaignMapGalleryIndex.Single(m => m.Id == ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Expected success.");
            Assert.That(updated.MapName, Is.EqualTo("New Map Name"), $"{nameof(updated.MapName)} value is not what's expected.");
            Assert.That(updated.Description, Is.EqualTo("A map for testing"), $"{nameof(updated.Description)} value is not what's expected.");

            // Immutable fields should be unchanged.
            Assert.That(updated.Width, Is.EqualTo(50), "Width should not have changed.");
            Assert.That(updated.Height, Is.EqualTo(50), "Height should not have changed.");
            Assert.That(updated.CreationEngine, Is.EqualTo(MapCreationEngine.BlankMap), "CreationEngine should not have changed.");
            Assert.That(updated.PropertiesJson, Is.EqualTo("{}"), "PropertiesJson should not have changed.");
        }
    }

    [Test, Description("Update should succeed even for non-Pending maps (name/description are always editable).")]
    public async Task UpdateShouldSucceedOnNonPendingMapAsync()
    {
        const int ID = 1;

        _context.CampaignMapGalleryIndex.Add(new CampaignMapGalleryIndex
        {
            Id = ID,
            MapName = "Test Map",
            Description = "Old Description",
            Width = 50,
            Height = 50,
            CreationEngine = MapCreationEngine.BlankMap,
            CreationStatus = MapCreationStatus.Complete, // non-pending status
            Status = "Complete",
            PropertiesJson = "{}"
        });
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateCampaignMapIndexRequest(
            Id: ID,
            MapName: "New Map Name",
            Description: "Updated description"
        );

        ServiceResultBase result = await _updateHandler.Handle(updateRequest, CancellationToken.None);

        using var verifyContext = new AscendantEmpireContext(DbOptions);
        var updated = verifyContext.CampaignMapGalleryIndex.Single(m => m.Id == ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Expected success on a non-Pending map.");
            Assert.That(updated.MapName, Is.EqualTo("New Map Name"), "Name should have been updated.");
        }
    }

    [Test, Description("Update should return NotFound when no matching map exists.")]
    public async Task UpdateShouldReturnNotFoundForMissingMapAsync()
    {
        var updateRequest = new UpdateCampaignMapIndexRequest(
            Id: 999,
            MapName: "Ghost Map",
            Description: null
        );

        ServiceResultBase result = await _updateHandler.Handle(updateRequest, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False, "Expected failure for a missing map.");
    }
}
