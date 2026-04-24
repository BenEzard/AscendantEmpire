using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Handlers;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Handlers.Biomes;
internal class AddCampaignBiomesHandlerTests
{
    private static readonly DbContextOptions<AscendantEmpireContext> DbOptions = new DbContextOptionsBuilder<AscendantEmpireContext>()
        .UseInMemoryDatabase(nameof(AddCampaignBiomesHandlerTests), new InMemoryDatabaseRoot())
        .Options;

    private AscendantEmpireContext _context = null!;
    private AddCampaignMapBiomesHandler _handler = null!;

    [SetUp]
    public async Task Setup()
    {
        _context = new AscendantEmpireContext(DbOptions);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        _handler = new AddCampaignMapBiomesHandler(new NullLogger<AddCampaignMapBiomesHandler>(), _context);
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
    }

    [Test, Description("Adding a valid Biome should be added to the db.")]
    public async Task ShouldAddValidBiomesAsync()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                IsPaintableOnCampaignMap = true
            }
        ]);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Biome addition failed, when it should have succeeded.");
            Assert.That(_context.CampaignBiomes.Count(), Is.GreaterThan(0), "No biomes were added to the context.");
        }
    }

    [Test, Description("Should fail if the Biome Type Id is already in the db.")]
    public async Task ShouldFailWhenDuplicateBiomeExists()
    {
        _context.CampaignBiomes.Add(new CampaignBiome { BiomeTypeId = BiomeType.Arid, Name = "Arid", MapEditorHtmlColor = "#c0c0c0" });
        await _context.SaveChangesAsync();

        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid Duplicate",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                MapEditorHtmlColor = "#000000",
                CanBeElevated = false,
                IsPaintableOnCampaignMap = true
            }
        ]);

        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded, when it wasn't expected to.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages when duplicate biome exists.");
        }
    }

    [Test, Description("Should fail if the Biome Type Id is not valid.")]
    public async Task ShouldFailWhenInvalidBiomeTypeId()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = (BiomeType)999, // Invalid Biome Type Id
                Name = "Invalid Biome",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                MapEditorHtmlColor = "#000000",
                CanBeElevated = false,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with an invalid Biome Type Id.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for invalid Biome Type Id.");
        }
    }

    [Test, Description("Should fail if the Biome Name is empty.")]
    public async Task ShouldFailWhenBiomeNameIsEmpty()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = string.Empty, // Empty Name
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                MapEditorHtmlColor = "#000000",
                CanBeElevated = false,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with an empty Name.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for empty Biome Name.");
        }
    }

    [Test, Description("Should fail if the same Biome Type Id is in the request multiple times.")]
    public async Task ShouldFailWhenDuplicateBiomeTypeInRequest()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid 1",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                IsPaintableOnCampaignMap = true
            },
            new()
            {
                BiomeType = BiomeType.Arid, // Duplicate Biome Type Id
                Name = "Arid 2",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                MapEditorHtmlColor = "#000000",
                CanBeElevated = false,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with duplicate Biome Type Id in request.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for duplicate Biome Type Id in request.");
        }
    }

    [Test, Description("Should fail if the MapEditorHtmlColor is not a valid color.")]
    public async Task ShouldFailWhenInvalidMapEditorHtmlColor()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                MapEditorHtmlColor = "invalid-color", // Invalid Color
                CanBeElevated = true,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with an invalid MapEditorHtmlColor.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for invalid MapEditorHtmlColor.");
        }
    }

    [Test, Description("Should fail if Movement Cost is less than or equal to zero.")]
    [TestCase(0.0, Description = "Movement Cost is zero")]
    [TestCase(-1.0, Description = "Movement Cost is negative")]
    public async Task ShouldFailWhenMovementCostIsZeroOrNegative(decimal movementCost)
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = movementCost, // Invalid Movement Cost
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                MapEditorHtmlColor = "#000000",
                CanBeElevated = false,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with an invalid Movement Cost.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for invalid Movement Cost.");
        }
    }

    [Test, Description("Should fail if Biome IsWater is true and DoesBlockSight is true.")]
    public async Task ShouldFailWhenWaterBiomeBlocksSight()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Ocean,
                Name = "Ocean",
                MovementCost = 1.0m,
                DoesBlockSight = true, // Water Biomes should not block sight
                IsWater = true,
                IsTraversable = false,
                RegenerationRate = 1.15m,
                MapEditorHtmlColor = "#0000ff",
                CanBeElevated = false,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with a water biome that blocks sight.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for water biome blocking sight.");
        }
    }

    [Test, Description("Should fail if Regeneration Rate is 1 or less.")]
    [TestCase(1.0, Description = "Regeneration Rate is 1.0")]
    [TestCase(0.5, Description = "Regeneration Rate is 0.5")]
    public async Task ShouldFailWhenRegenerationRateIsOneOrLess(decimal regenerationRate)
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = regenerationRate, // Invalid Regeneration Rate
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with an invalid Regeneration Rate.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for invalid Regeneration Rate.");
        }
    }

    [Test, Description("Should fail if BaseForageValue is specified, but less than 0")]
    public async Task ShouldFailWhenBaseForageValueIsNegative()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                BaseForageValue = -10, // Invalid Base Forage Value
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with a negative Base Forage Value.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for negative Base Forage Value.");
        }
    }

    // BaseForageValue, BaseHuntValue, BaseFishValue, BaseFodderValue, BaseBoneValue, BaseWoodValue, BaseStoneValue, and BaseSpecialValue must be greater than or equal to 0, if specified

    [Test, Description("Should fail if BaseHuntValue is specified, but less than 0")]
    public async Task ShouldFailWhenBaseHuntValueIsNegative()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                BaseHuntValue = -5, // Invalid Base Hunt Value
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with a negative Base Hunt Value.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for negative Base Hunt Value.");
        }
    }

    [Test, Description("Should fail if BaseFishValue is specified, but less than 0")]
    public async Task ShouldFailWhenBaseFishValueIsNegative()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                BaseFishValue = -3, // Invalid Base Fish Value
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with a negative Base Fish Value.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for negative Base Fish Value.");
        }
    }

    [Test, Description("Should fail if BaseFodderValue is specified, but less than 0")]
    public async Task ShouldFailWhenBaseFodderValueIsNegative()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                BaseFodderValue = -2, // Invalid Base Fodder Value
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with a negative Base Fodder Value.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for negative Base Fodder Value.");
        }
    }

    [Test, Description("Should fail if BaseBoneValue is specified, but less than 0")]
    public async Task ShouldFailWhenBaseBoneValueIsNegative()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                BaseBoneValue = -1, // Invalid Base Bone Value
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with a negative Base Bone Value.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for negative Base Bone Value.");
        }
    }

    [Test, Description("Should fail if BaseWoodValue is specified, but less than 0")]
    public async Task ShouldFailWhenBaseWoodValueIsNegative()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                BaseWoodValue = -4, // Invalid Base Wood Value
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with a negative Base Wood Value.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for negative Base Wood Value.");
        }
    }

    [Test, Description("Should fail if BaseStoneValue is specified, but less than 0")]
    public async Task ShouldFailWhenBaseStoneValueIsNegative()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                BaseStoneValue = -6, // Invalid Base Stone Value
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with a negative Base Stone Value.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for negative Base Stone Value.");
        }
    }

    [Test, Description("Should fail if BaseSpecialValue is specified, but less than 0")]
    public async Task ShouldFailWhenBaseSpecialValueIsNegative()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Arid,
                Name = "Arid",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.15m,
                BaseSpecialValue = -8, // Invalid Base Special Value
                MapEditorHtmlColor = "#fcfda4",
                CanBeElevated = true,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with a negative Base Special Value.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for negative Base Special Value.");
        }
    }

    [Test, Description("Should fail if IsWater is true and has a Hunt value specified.")]
    public async Task ShouldFailWhenWaterBiomeHasHuntValue()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Ocean,
                Name = "Ocean",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = true, // Water Biome
                IsTraversable = false,
                RegenerationRate = 1.15m,
                BaseHuntValue = 5, // Invalid Hunt Value for Water Biome
                MapEditorHtmlColor = "#0000ff",
                CanBeElevated = false,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with a water biome that has a Hunt value.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for water biome having a Hunt value.");
        }
    }

    [Test, Description("Should fail if IsWater is true and has a Wood value specified.")]
    public async Task ShouldFailWhenWaterBiomeHasWoodValue()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Ocean,
                Name = "Ocean",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = true, // Water Biome
                IsTraversable = false,
                RegenerationRate = 1.15m,
                BaseWoodValue = 10, // Invalid Wood Value for Water Biome
                MapEditorHtmlColor = "#0000ff",
                CanBeElevated = false,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with a water biome that has a Wood value.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for water biome having a Wood value.");
        }
    }

    [Test, Description("Should fail if IsWater is true and has a Stone value specified.")]
    public async Task ShouldFailWhenWaterBiomeHasStoneValue()
    {
        var request = new AddCampaignMapBiomesRequest(
        [
            new()
            {
                BiomeType = BiomeType.Ocean,
                Name = "Ocean",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = true, // Water Biome
                IsTraversable = false,
                RegenerationRate = 1.15m,
                BaseStoneValue = 15, // Invalid Stone Value for Water Biome
                MapEditorHtmlColor = "#0000ff",
                CanBeElevated = false,
                IsPaintableOnCampaignMap = true
            }
        ]);
        ServiceResultBase result = await _handler.Handle(request, CancellationToken.None);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False, "Biome addition succeeded with a water biome that has a Stone value.");
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty, "Expected error messages for water biome having a Stone value.");
        }
    }
}

