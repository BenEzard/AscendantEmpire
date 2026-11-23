using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Modifiers;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests that the database enforces the rules on <c>game.PlayerExtractionEfficiencyModifier</c>: one row per Player, source and Asset, and no row that changes
/// nothing.
/// </summary>
/// <remarks>
/// Containerised because it is the constraints themselves under test, which <c>testing.md</c> says the in-memory provider cannot reproduce. The per-test database is
/// restored from the migrated template, so this also proves the migration created them.
/// </remarks>
[TestFixture]
internal class PlayerExtractionEfficiencyModifierConstraintTests : SqlServerTestBase
{
    private const int _SOURCE_ID = 10;

    private int _gameId;
    private int _playerId;
    private int _assetId;

    #region Setup

    [SetUp]
    public async Task SeedPlayerAndAssetAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var metaCategory = new AssetMetaCategory { Code = "TestMeta", DisplayName = "Test Meta", SortOrder = 1, IconPath = "/resources/none.svg" };
        var storageClass = new StorageClass { Code = "TestStore", DisplayName = "Test Store" };

        var game = new GameIndex
        {
            CampaignMapIndexId = 1,
            GameName = "Efficiency Game",
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = TestGameSettings.MINIMUM_BATTLE_MAP_SIZE,
            MapWidth = 10,
            MapHeight = 10,
            CampaignMapOffsetId = 0,
        };

        context.AssetMetaCategories.Add(metaCategory);
        context.StorageClasses.Add(storageClass);
        context.GameIndex.Add(game);
        await context.SaveChangesAsync();

        var category = new AssetCategory
        {
            Code = "TestMeta.Test",
            DisplayName = "Test",
            CategoryType = AssetCategoryType.StandaloneAssets,
            SortOrder = 1,
            AssetMetaCategoryId = metaCategory.Id
        };

        var player = new Player
        {
            GameId = game.Id,
            RulerTitle = Player.DEFAULT_RULER_TITLE,
            RulerName = "Ruler",
            FactionName = "Faction",
            PeopleName = "People",
            Demonym = "Person",
            Language = "Language",
            RulerPortraitPath = "/portraits/01_western_king.png",
            Status = PlayerStatus.Active,
            CampaignOffsetId = 1,
        };

        context.AssetCategories.Add(category);
        context.Players.Add(player);
        await context.SaveChangesAsync();

        var asset = new Asset
        {
            Code = "TestMeta.Rock",
            DisplayName = "Rock",
            IconPath = "/resources/none.svg",
            IsGradedAsset = false,
            IsNaturallyOccurring = true,
            SortOrder = 1,
            AssetMetaCategoryId = metaCategory.Id,
            AssetCategoryId = category.Id,
            StorageClassId = storageClass.Id
        };

        context.Assets.Add(asset);
        await context.SaveChangesAsync();

        _gameId = game.Id;
        _playerId = player.Id;
        _assetId = asset.Id;
    }

    #endregion

    #region Unique index

    [Test, Description("A source cannot give one Player two rows for the same Asset, because a second row would be a second opinion on one figure.")]
    public async Task ShouldRejectWhenSamePlayerSourceAndAssetInsertedTwiceAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerExtractionEfficiencyModifiers.Add(BuildModifier(_SOURCE_ID, flatValue: 950));
        await context.SaveChangesAsync();

        context.PlayerExtractionEfficiencyModifiers.Add(BuildModifier(_SOURCE_ID, flatValue: 200));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("Two sources may each modify the same Asset for one Player, which is how two technologies' grants stack.")]
    public async Task ShouldAllowWhenSameAssetFromDifferentSourcesAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerExtractionEfficiencyModifiers.AddRange(BuildModifier(_SOURCE_ID, flatValue: 950), BuildModifier(_SOURCE_ID + 1, flatValue: 200));

        await context.SaveChangesAsync();

        Assert.That(await context.PlayerExtractionEfficiencyModifiers.CountAsync(modifier => modifier.PlayerId == _playerId), Is.EqualTo(2));
    }

    #endregion

    #region Check constraint

    [Test, Description("A row with neither a flat nor a percentage part is rejected, because it would change nothing while still using up the source's one row.")]
    public async Task ShouldRejectWhenNeitherFlatNorPercentIsSetAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerExtractionEfficiencyModifiers.Add(BuildModifier(_SOURCE_ID, flatValue: 0));

        Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test, Description("Negative values are accepted, because a source that makes an Asset harder to extract is a penalty rather than invalid data.")]
    public async Task ShouldAllowWhenValuesAreNegativeAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerExtractionEfficiencyModifiers.Add(BuildModifier(_SOURCE_ID, flatValue: -200, percentValue: -500));

        await context.SaveChangesAsync();

        PlayerExtractionEfficiencyModifier stored = await context.PlayerExtractionEfficiencyModifiers.SingleAsync(modifier => modifier.PlayerId == _playerId);

        Assert.Multiple(() =>
        {
            Assert.That(stored.FlatValue, Is.EqualTo(-200));
            Assert.That(stored.PercentValue, Is.EqualTo(-500));
        });
    }

    #endregion

    #region Helpers

    private PlayerExtractionEfficiencyModifier BuildModifier(int sourceId, long flatValue, long percentValue = 0)
    {
        return new PlayerExtractionEfficiencyModifier
        {
            GameId = _gameId,
            PlayerId = _playerId,
            SourceType = ModifierSourceType.Technology,
            SourceId = sourceId,
            AssetId = _assetId,
            FlatValue = flatValue,
            PercentValue = percentValue
        };
    }

    #endregion
}
