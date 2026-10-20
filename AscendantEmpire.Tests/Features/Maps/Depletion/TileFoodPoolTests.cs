using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="TileFoodPool"/>: which resources make up a tile's pool, and how much of each is available to the Player.
/// </summary>
/// <remarks>
/// <para>
/// No database - the pool is pure. The Player's values are built directly rather than read, since the formula behind them is covered in <c>SustenanceValueCalculatorTests</c>.
/// </para>
/// <para>
/// Grassland has Forage 400, Hunt 200, no Fish and Fodder 1,000. Ocean has Fish 300 only.
/// </para>
/// </remarks>
[TestFixture]
internal class TileFoodPoolTests
{
    private static readonly PlayerSustenanceValues _VALUES = new(playerId: 1, new Dictionary<(BiomeType Biome, SustenanceResource Resource), ScaledAmount?>
    {
        [(BiomeType.Grassland, SustenanceResource.Forage)] = ScaledAmount.FromWhole(400),
        [(BiomeType.Grassland, SustenanceResource.Hunt)] = ScaledAmount.FromWhole(200),
        [(BiomeType.Grassland, SustenanceResource.Fish)] = null,
        [(BiomeType.Grassland, SustenanceResource.Fodder)] = ScaledAmount.FromWhole(1_000),
        [(BiomeType.Ocean, SustenanceResource.Forage)] = null,
        [(BiomeType.Ocean, SustenanceResource.Hunt)] = null,
        [(BiomeType.Ocean, SustenanceResource.Fish)] = ScaledAmount.FromWhole(300),
        [(BiomeType.Ocean, SustenanceResource.Fodder)] = null
    });

    [Test, Description("A land tile offers Forage and Hunt only; Fodder is for animals and stays out of the pool even though the biome has it.")]
    public void ShouldOfferForageAndHuntOnLand()
    {
        Dictionary<SustenanceResource, ScaledAmount> available = TileFoodPool.GetAvailable(isWater: false, _VALUES, BiomeType.Grassland, depletion: null);

        Assert.That(available, Is.EquivalentTo(new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Forage] = ScaledAmount.FromWhole(400),
            [SustenanceResource.Hunt] = ScaledAmount.FromWhole(200)
        }));
    }

    [Test, Description("A land tile with a river offers Fish as well, at the river's flat amount, with Forage and Hunt both raised 20% by the edge.")]
    public void ShouldOfferFishAndARicherLandPoolOnARiverbank()
    {
        var rivers = new TileRivers { East = RiverWidth.Large };

        Dictionary<SustenanceResource, ScaledAmount> available = TileFoodPool.GetAvailable(isWater: false, _VALUES, BiomeType.Grassland, depletion: null, rivers);

        Assert.That(available, Is.EquivalentTo(new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Forage] = ScaledAmount.FromWhole(480),
            [SustenanceResource.Hunt] = ScaledAmount.FromWhole(240),
            [SustenanceResource.Fish] = ScaledAmount.FromWhole(30)
        }));
    }

    [Test, Description("A water tile at a river mouth is unchanged: the river raises its banks, not the sea it runs into.")]
    public void ShouldLeaveAWaterTilesPoolAloneWhenARiverMeetsIt()
    {
        var rivers = new TileRivers { North = RiverWidth.Large };

        Dictionary<SustenanceResource, ScaledAmount> available = TileFoodPool.GetAvailable(isWater: true, _VALUES, BiomeType.Ocean, depletion: null, rivers);

        Assert.That(available, Is.EquivalentTo(new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Fish] = ScaledAmount.FromWhole(300)
        }));
    }

    [Test, Description("A water tile offers Fish only.")]
    public void ShouldOfferOnlyFishOnWater()
    {
        Dictionary<SustenanceResource, ScaledAmount> available = TileFoodPool.GetAvailable(isWater: true, _VALUES, BiomeType.Ocean, depletion: null);

        Assert.That(available, Is.EquivalentTo(new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Fish] = ScaledAmount.FromWhole(300)
        }));
    }

    [Test, Description("A resource the biome does not have is left out of the pool altogether, rather than being in it at 0 like a stripped resource.")]
    public void ShouldLeaveOutAResourceTheBiomeDoesNotHave()
    {
        // Grassland has no Fish, so a Grassland tile marked as water has nothing in its pool at all.
        Dictionary<SustenanceResource, ScaledAmount> available = TileFoodPool.GetAvailable(isWater: true, _VALUES, BiomeType.Grassland, depletion: null);

        Assert.That(available, Is.Empty);
    }

    [Test, Description("What has been taken is subtracted per resource and floored at 0, so a stripped resource stays in the pool at 0.")]
    public void ShouldSubtractWhatHasBeenTaken()
    {
        var depletion = new CampaignMapDepletion
        {
            CampaignMapId = 1,
            GameId = 1,
            ForageTaken = ScaledAmount.FromWhole(150).RawValue,
            HuntTaken = ScaledAmount.FromWhole(250).RawValue
        };

        Dictionary<SustenanceResource, ScaledAmount> available = TileFoodPool.GetAvailable(isWater: false, _VALUES, BiomeType.Grassland, depletion);

        Assert.That(available, Is.EquivalentTo(new Dictionary<SustenanceResource, ScaledAmount>
        {
            [SustenanceResource.Forage] = ScaledAmount.FromWhole(250),
            [SustenanceResource.Hunt] = ScaledAmount.Zero
        }));
    }
}
