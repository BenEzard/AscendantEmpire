using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Maps.MapGallery;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Handlers.MapGallery;

internal class AddCampaignMapGalleryTests
{
    private AscendantEmpireContext _context = null!;
    private AddCampaignMapHandler _handler = null!;
    private DbContextOptions<AscendantEmpireContext> _options;
    private IMediator _mediator = null!;

    [SetUp]
    public async Task Setup()
    {
        _options = new DbContextOptionsBuilder<AscendantEmpireContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AscendantEmpireContext(_options);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        _mediator = Substitute.For<IMediator>();
        _handler = new AddCampaignMapHandler(new NullLogger<AddCampaignMapHandler>(), _context);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test, Description("Valid update should insert a record.")]
    public async Task ValidRequestShouldInsertSuccessfullyAsync()
    {
        const string MAP_NAME = "Test Map";

        var updateRequest = new AddCampaignMapRequest(
            MapName: MAP_NAME,
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

        ServiceResultBase result = await _handler.Handle(updateRequest, CancellationToken.None);

        using var verifyContext = new AscendantEmpireContext(_options);
        var created = verifyContext.CampaignMapGalleryIndex.Single(m => m.MapName == MAP_NAME);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Id, Is.GreaterThan(0), $"{nameof(created.Id)} value is not what's expected.");
            Assert.That(created.MapName, Is.EqualTo("Test Map"), $"{nameof(created.MapName)} value is not what's expected.");
            Assert.That(created.Description, Is.EqualTo("A map for testing"), $"{nameof(created.Description)} value is not what's expected.");
            Assert.That(created.Width, Is.EqualTo(100), $"{nameof(created.Width)} value is not what's expected.");
            Assert.That(created.Height, Is.EqualTo(100), $"{nameof(created.Height)} value is not what's expected.");
            Assert.That(created.CreationStatus, Is.EqualTo(MapCreationStatus.Pending), $"{nameof(created.CreationStatus)} value is not what's expected.");
            Assert.That(created.CreationEngine, Is.EqualTo(MapCreationEngine.BlankMap), $"{nameof(created.CreationEngine)} value is not what's expected.");
            Assert.That(created.Status, Is.EqualTo("Pending creation (#1 in queue)"), $"{nameof(created.Status)} value is not what's expected.");
            Assert.That(created.PropertiesJson, Is.EqualTo("{\"Key1\":\"Value1\",\"Key2\":\"Value2\"}"), $"{nameof(created.PropertiesJson)} value is not what's expected.");
        }
    }

    [Test, Description("A non unique name should fail.")]
    public async Task NonUniqueNameShouldFailAsync()
    {
        const int ID = 1;
        const string EXISTING_NAME = "Existing Map";

        _context.CampaignMapGalleryIndex.Add(new CampaignMapGalleryIndex
        {
            Id = ID,
            MapName = EXISTING_NAME,
            Description = "Old Description",
            Width = 50,
            Height = 50,
            CreationEngine = MapCreationEngine.BlankMap,
            CreationStatus = MapCreationStatus.Pending,
            Status = "Pending",
            PropertiesJson = "{}"
        });
        await _context.SaveChangesAsync();

        var updateRequest = new AddCampaignMapRequest(
            EXISTING_NAME,
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

        ServiceResultBase result = await _handler.Handle(updateRequest, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Should have failed, but succeeded");
            Assert.That(result.Errors, Is.Not.Empty, "Should have errors, but none were found.");
        }
    }

}
