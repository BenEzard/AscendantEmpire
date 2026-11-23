using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Modifiers;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="PlayerExtractionEfficiencyReader"/>: that a Player's efficiency for an Asset is worked out from their own modifier rows and the start the caller supplies.
/// </summary>
/// <remarks>
/// On the in-memory tier: a filtered read and an in-memory sum, with no SQL-specific behaviour. The provider does not enforce the Player or Asset foreign keys, so
/// both are bare numbers.
/// </remarks>
[TestFixture]
internal class PlayerExtractionEfficiencyReaderTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 11;
    private const int _OTHER_PLAYER_ID = 12;
    private const int _ROCK_ASSET_ID = 501;
    private const int _LOG_ASSET_ID = 502;

    private static readonly ScaledPercent _FIVE_PERCENT = ScaledPercent.FromWholePercent(5);

    private readonly PlayerExtractionEfficiencyReader _reader = new();

    [Test, Description("Only the asked-for Player's rows count, so one Player's crafting technology does not lift another's harvest.")]
    public async Task ShouldSumOnlyThatPlayersRowsWhenSeveralPlayersHoldModifiers()
    {
        await AddModifierAsync(_PLAYER_ID, sourceId: 1, _ROCK_ASSET_ID, flatPoints: 95);
        await AddModifierAsync(_OTHER_PLAYER_ID, sourceId: 1, _ROCK_ASSET_ID, flatPoints: 50);

        PlayerExtractionEfficiencies efficiencies = await ReadAsync(_PLAYER_ID);

        Assert.That(efficiencies.GetEfficiency(_ROCK_ASSET_ID, _FIVE_PERCENT), Is.EqualTo(ScaledPercent.FromWholePercent(100)));
    }

    [Test, Description("An Asset the Player holds no modifier for is extracted at the start alone, which is the before-PrimitiveCrafting1 harvest.")]
    public async Task ShouldReturnStartWhenAssetHasNoModifiers()
    {
        await AddModifierAsync(_PLAYER_ID, sourceId: 1, _ROCK_ASSET_ID, flatPoints: 95);

        PlayerExtractionEfficiencies efficiencies = await ReadAsync(_PLAYER_ID);

        Assert.That(efficiencies.GetEfficiency(_LOG_ASSET_ID, _FIVE_PERCENT), Is.EqualTo(_FIVE_PERCENT));
    }

    [Test, Description("Rows from several sources for one Asset sum, so two technologies both raising stone extraction both count.")]
    public async Task ShouldSumAcrossSourcesWhenAssetHasSeveralRows()
    {
        await AddModifierAsync(_PLAYER_ID, sourceId: 1, _ROCK_ASSET_ID, flatPoints: 95);
        await AddModifierAsync(_PLAYER_ID, sourceId: 2, _ROCK_ASSET_ID, flatPoints: 20);

        PlayerExtractionEfficiencies efficiencies = await ReadAsync(_PLAYER_ID);

        Assert.That(efficiencies.GetEfficiency(_ROCK_ASSET_ID, _FIVE_PERCENT), Is.EqualTo(ScaledPercent.FromWholePercent(120)));
    }

    private async Task AddModifierAsync(int playerId, int sourceId, int assetId, int flatPoints)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.PlayerExtractionEfficiencyModifiers.Add(new PlayerExtractionEfficiencyModifier
        {
            GameId = _GAME_ID,
            PlayerId = playerId,
            SourceType = ModifierSourceType.Technology,
            SourceId = sourceId,
            AssetId = assetId,
            FlatValue = ScaledPercent.FromWholePercent(flatPoints).RawValue
        });

        await context.SaveChangesAsync();
    }

    private async Task<PlayerExtractionEfficiencies> ReadAsync(int playerId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await _reader.GetEfficienciesAsync(context, playerId);
    }
}
