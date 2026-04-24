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

    [Test, Description("Valid update should succeed.")]
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
            Description: "A map for testing",
            Width: 100,
            Height: 100,
            CreationEngine: MapCreationEngine.BlankMap,
            Parameters: new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            }
        );

        ServiceResultBase result = await _updateHandler.Handle(updateRequest, CancellationToken.None);

        using var verifyContext = new AscendantEmpireContext(DbOptions);
        var created = verifyContext.CampaignMapGalleryIndex.Single(m => m.Id == ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Id, Is.GreaterThan(0), $"{nameof(created.Id)} value is not what's expected.");
            Assert.That(created.MapName, Is.EqualTo("New Map Name"), $"{nameof(created.MapName)} value is not what's expected.");
            Assert.That(created.Description, Is.EqualTo("A map for testing"), $"{nameof(created.Description)} value is not what's expected.");
            Assert.That(created.Width, Is.EqualTo(100), $"{nameof(created.Width)} value is not what's expected.");
            Assert.That(created.Height, Is.EqualTo(100), $"{nameof(created.Height)} value is not what's expected.");
            Assert.That(created.CreationEngine, Is.EqualTo(MapCreationEngine.BlankMap), $"{nameof(created.CreationEngine)} value is not what's expected.");
            Assert.That(created.CreationStatus, Is.EqualTo(MapCreationStatus.Pending), $"{nameof(created.CreationStatus)} value is not what's expected.");
            Assert.That(created.PropertiesJson, Is.EqualTo("{\"Key1\":\"Value1\",\"Key2\":\"Value2\"}"), $"{nameof(created.PropertiesJson)} value is not what's expected.");
        }
    }


    [Test, Description("Update should fail if the map isn't Pending.")]
    public async Task UpdateShouldFailOnNonPendingMapAsync()
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
            Status = "Pending",
            PropertiesJson = "{}"
        });
        await _context.SaveChangesAsync();

        var updateRequest = new UpdateCampaignMapIndexRequest(
            Id: ID,
            "New Map Name",
            "A map for testing",
            100,
            100,
            MapCreationEngine.BlankMap,
            new Dictionary<string, string>
            {
                { "Key1", "Value1" },
                { "Key2", "Value2" }
            }
        );

        ServiceResultBase result = await _updateHandler.Handle(updateRequest, CancellationToken.None);

        using var verifyContext = new AscendantEmpireContext(DbOptions);
        var created = verifyContext.CampaignMapGalleryIndex.Single(m => m.Id == ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Should have failed, but succeeded");
            Assert.That(result.Errors, Is.Not.Empty, "Should have errors, but none were found.");
        }
    }
}
