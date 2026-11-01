using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Geographies.Handlers;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Handlers.Geographies;

internal class AddCampaignMapGeographyTests
{
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(AddCampaignMapGeographyTests), new InMemoryDatabaseRoot())
        .Options;

    private AscendantEmpireContext _context = null!;
    private AddCampaignMapGeographyHandler _handler = null!;

    [SetUp]
    public async Task Setup()
    {
        _context = new AscendantEmpireContext(DbOptions);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        _handler = new AddCampaignMapGeographyHandler(new NullLogger<AddCampaignMapGeographyHandler>(), _context);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test, Description("Adding a valid Geography should be added to the db.")]
    public async Task ShouldAddValidGeographyAsync()
    {
        var request = new AddCampaignMapGeographyRequest(
        [
            new()
            {
                CampaignMapGeographyType = CampaignMapGeographyType.Mountainous,
                RelativeElevation = 1,
                Name = "Plain",
                MovementCost = 1.0m,
                IsWater = false,
                IsTraversable = true,
                MapEditorIcon = "plain-icon.png",
                IsPaintableOnCampaignMap = true
            }
        ]);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Campaign Map Geography addition failed, when it should have succeeded.");
            Assert.That(_context.CampaignGeographies.Count(), Is.GreaterThan(0), "No Campaign Map Geography were added to the context.");
        }
    }

    [Test, Description("Should fail if the Campaign Map Geography Type Id is already in the db.")]
    public async Task ShouldFailWhenDuplicateGeographyExists()
    {
        _context.CampaignGeographies.Add(new CampaignGeography 
        { 
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Mountainous,
            RelativeElevation = 1,
            Name = "Mountainous",
            MovementCost = 3,
            IsWater = false,
            IsTraversable = true,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        });
        await _context.SaveChangesAsync();

        var request = new AddCampaignMapGeographyRequest(
        [
            new()
            {
                CampaignMapGeographyType = CampaignMapGeographyType.Mountainous,
                RelativeElevation = 1,
                Name = "Mountainous",
                MovementCost = 3,
                IsWater = false,
                IsTraversable = true,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = true
            }
        ]);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Campaign Map Geography addition succeeded, when it wasn't expected to.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages when duplicate Campaign Map Geography exists.");
        }
    }

    [Test, Description("Should fail if the Campaign Map Geograpny Type Id is not valid.")]
    public async Task ShouldFailWhenInvalidCampaignMapGeographyTypeId()
    {
        var request = new AddCampaignMapGeographyRequest(
        [
            new()
            {
                CampaignMapGeographyType = (CampaignMapGeographyType)999, // Invalid Campaign Map Geography Type Id
                Name = "Invalid",
                MovementCost = 1.0m,
                IsWater = false,
                IsTraversable = true,
                RelativeElevation = 0,
                MapEditorIcon = "invalid-icon.png",
                IsPaintableOnCampaignMap = true
            }
        ]);
        
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Campaign Map Geography addition succeeded with an invalid Campaign Map Geography Type Id.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for invalid Campaign Map Geography Type Id.");
        }
    }

}
