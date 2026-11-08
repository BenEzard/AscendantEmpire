using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.MapResources;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Pins which MapResource instances a Game's Players start out knowing about.
/// </summary>
/// <remarks>
/// In-memory: the seeder is ordinary EF Core — a query over the Game's instances and their types, then an <c>AddRange</c> — and leans on no database
/// constraint, so a container would buy nothing. See <c>docs/testing.md</c>.
/// </remarks>
[TestFixture]
internal sealed class PlayerMapResourceDiscoveryServiceTests : InMemoryTestBase
{
    private const int _GAME_ID = 7;
    private const int _OTHER_GAME_ID = 8;

    private const int _FIRST_PLAYER_ID = 101;
    private const int _SECOND_PLAYER_ID = 102;

    /// <summary>
    /// The Round the Game begins on, which anything known before the first Round must be stamped with.
    /// </summary>
    private const int _SETUP_ROUND = 0;

    /// <summary>
    /// The one layer of a stratified instance a Player can see without surveying, which is the switch default.
    /// </summary>
    private const int _SURFACE_LAYER = 1;

    #region Which instances are seeded

    [Test, Description("Every Player gets a row for every instance of a type that is discoverable by default, so a berry patch is visible from the first Round.")]
    public async Task ShouldSeedOneRowPerPlayerPerDefaultDiscoverableInstance()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        MapResourceTypeIds types = await SeedTypesAsync(context);

        await SeedInstanceAsync(context, _GAME_ID, types.BerryTypeId, campaignMapId: 500, slot: 1);
        await SeedInstanceAsync(context, _GAME_ID, types.BerryTypeId, campaignMapId: 501, slot: 1);

        await SeedAsync(context);

        List<PlayerKnownMapResource> known = await context.PlayerKnownMapResources.ToListAsync();

        Assert.That(known, Has.Count.EqualTo(4));
    }

    [Test, Description("An instance of a type that is not discoverable by default is seeded for nobody, so uranium stays hidden until a Player finds it.")]
    public async Task ShouldNotSeedAnInstanceOfATypeThatIsNotDiscoverableByDefault()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        MapResourceTypeIds types = await SeedTypesAsync(context);

        await SeedInstanceAsync(context, _GAME_ID, types.HiddenTypeId, campaignMapId: 500, slot: 1);

        await SeedAsync(context);

        Assert.That(await context.PlayerKnownMapResources.AnyAsync(), Is.False);
    }

    [Test, Description("Instances belonging to another Game are left alone, so seeding one Game cannot hand its resources to another's Players.")]
    public async Task ShouldIgnoreInstancesOfAnotherGame()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        MapResourceTypeIds types = await SeedTypesAsync(context);

        await SeedInstanceAsync(context, _OTHER_GAME_ID, types.BerryTypeId, campaignMapId: 900, slot: 1);

        await SeedAsync(context);

        Assert.That(await context.PlayerKnownMapResources.AnyAsync(), Is.False);
    }

    [Test, Description("The seeder reports how many rows it wrote, so Game setup can log a shortfall rather than finding one later.")]
    public async Task ShouldReportHowManyRowsItWrote()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        MapResourceTypeIds types = await SeedTypesAsync(context);

        await SeedInstanceAsync(context, _GAME_ID, types.BerryTypeId, campaignMapId: 500, slot: 1);

        ServiceResult<int> result = await SeedAsync(context);

        Assert.That(result.SuccessValue, Is.EqualTo(2));
    }
    #endregion

    #region What each row carries

    [Test, Description("A seeded row is stamped with Round 0, matching the Round the instances themselves were created on.")]
    public async Task ShouldStampSeededRowsWithTheSetupRound()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        MapResourceTypeIds types = await SeedTypesAsync(context);

        await SeedInstanceAsync(context, _GAME_ID, types.BerryTypeId, campaignMapId: 500, slot: 1);

        await SeedAsync(context);

        PlayerKnownMapResource known = await context.PlayerKnownMapResources.FirstAsync();

        Assert.That(known.DiscoveredOnRound, Is.EqualTo(_SETUP_ROUND));
    }

    [Test, Description("A non-stratified instance records no surveyed layer, because only a stratified one has layers to survey.")]
    public async Task ShouldRecordNoSurveyedLayerForANonStratifiedInstance()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        MapResourceTypeIds types = await SeedTypesAsync(context);

        await SeedInstanceAsync(context, _GAME_ID, types.BerryTypeId, campaignMapId: 500, slot: 1);

        await SeedAsync(context);

        PlayerKnownMapResource known = await context.PlayerKnownMapResources.FirstAsync();

        Assert.That(known.DeepestLayerSurveyed, Is.Null);
    }

    [Test, Description("A stratified instance records layer 1 as surveyed, which is the switch default for its surface layer.")]
    public async Task ShouldRecordTheSurfaceLayerAsSurveyedForAStratifiedInstance()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        MapResourceTypeIds types = await SeedTypesAsync(context);

        await SeedInstanceAsync(context, _GAME_ID, types.CoalTypeId, campaignMapId: 500, slot: 1);

        await SeedAsync(context);

        PlayerKnownMapResource known = await context.PlayerKnownMapResources.FirstAsync();

        Assert.That(known.DeepestLayerSurveyed, Is.EqualTo(_SURFACE_LAYER));
    }

    [Test, Description("A seeded row names the Game, so the Game delete can purge these rows without walking to the instance.")]
    public async Task ShouldCarryTheGameOnEverySeededRow()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        MapResourceTypeIds types = await SeedTypesAsync(context);

        await SeedInstanceAsync(context, _GAME_ID, types.BerryTypeId, campaignMapId: 500, slot: 1);

        await SeedAsync(context);

        Assert.That(await context.PlayerKnownMapResources.AllAsync(known => known.GameId == _GAME_ID), Is.True);
    }
    #endregion

    #region Helpers

    /// <summary>
    /// The types <see cref="SeedTypesAsync"/> writes, one per discovery behaviour the seeder has to tell apart.
    /// </summary>
    private sealed record MapResourceTypeIds(int BerryTypeId, int CoalTypeId, int HiddenTypeId);

    private async Task<ServiceResult<int>> SeedAsync(AscendantEmpireContext context)
    {
        var service = new PlayerMapResourceDiscoveryService();

        return await service.SeedDefaultDiscoveriesAsync(context, _GAME_ID, [_FIRST_PLAYER_ID, _SECOND_PLAYER_ID], CancellationToken.None);
    }

    /// <summary>
    /// Three MapResourceTypes: one discoverable by default, one stratified and also discoverable, and one that must be found.
    /// </summary>
    private static async Task<MapResourceTypeIds> SeedTypesAsync(AscendantEmpireContext context)
    {
        var berry = new MapResourceType
        {
            Code = "TestBerry",
            DisplayName = "Test Berry",
            MapResourceCategoryId = 1,
            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 1m
        };

        var coal = new MapResourceType
        {
            Code = "TestCoal",
            DisplayName = "Test Coal",
            MapResourceCategoryId = 1,
            IsStratified = true,
            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 1m
        };

        var hidden = new MapResourceType
        {
            Code = "TestUranium",
            DisplayName = "Test Uranium",
            MapResourceCategoryId = 1,
            IsDiscoverableByDefault = false,
            DiscoveryLikelihood = 0.1m
        };

        context.MapResourceTypes.AddRange(berry, coal, hidden);
        await context.SaveChangesAsync();

        return new MapResourceTypeIds(berry.Id, coal.Id, hidden.Id);
    }

    private static async Task SeedInstanceAsync(AscendantEmpireContext context, int gameId, int mapResourceTypeId, int campaignMapId, byte slot)
    {
        context.MapResourceInstances.Add(new MapResourceInstance
        {
            GameId = gameId,
            CampaignMapId = campaignMapId,
            MapResourceTypeId = mapResourceTypeId,
            Slot = slot,
            Quantity = 250_000,
            EstimationFuzzyFactor = 0.100m,
            CreatedOnRound = 0
        });

        await context.SaveChangesAsync();
    }
    #endregion
}
