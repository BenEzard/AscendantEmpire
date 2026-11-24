using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Turns.Services;

/// <summary>
/// Tests how the campaign and battle movement checks word a Force that still has movement, which is what the notifications panel shows for it.
/// </summary>
/// <remarks>
/// In-memory: both checks are a single LINQ read that projects only scalars off <see cref="CompiledUnitTemplate"/>, never its damage profiles.
/// </remarks>
[TestFixture]
internal class RemainingMovementCheckTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 1;
    private const int _TILE_ID = 40;
    private const int _TEMPLATE_ID = 5;
    private const long _CAMPAIGN_MOVEMENT_POINTS = 1_000;
    private const long _BATTLEFIELD_MOVEMENT_POINTS = 3_000;

    [SetUp]
    public async Task SetUpFixture()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.UnitTemplates.Add(new UnitTemplate
        {
            Id = _TEMPLATE_ID,
            DisplayName = "Settler",
            MapImagePath = "settler.png",
            BodyComponentId = 1
        });

        context.CompiledUnitTemplates.Add(new CompiledUnitTemplate
        {
            UnitTemplateId = _TEMPLATE_ID,
            DisplayName = "Settler",
            MapImagePath = "settler.png",
            BodyComponentId = 1,
            CampaignMovementPoints = _CAMPAIGN_MOVEMENT_POINTS,
            BattlefieldMovementPoints = _BATTLEFIELD_MOVEMENT_POINTS
        });

        context.CampaignMaps.Add(new CampaignMap
        {
            CampaignMapId = _TILE_ID,
            GameId = _GAME_ID,
            XCoord = 3,
            YCoord = 4,
            BiomeTypeId = BiomeType.Grassland,
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Biome = null!,
            Geography = null!,
            Game = null!,
            FirstVisitor = null,
            LastVisitor = null,
            OwnedBy = null
        });

        await context.SaveChangesAsync();
    }

    #region Helpers

    private async Task AddForceAsync(int id, long campaignMovementRemaining = 0, long battleMovementRemaining = 0, int? battleMapTileId = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.UnitInstances.Add(new UnitInstance
        {
            Id = id,
            GameId = _GAME_ID,
            PlayerId = _PLAYER_ID,
            CampaignMapTileId = _TILE_ID,
            BattleMapTileId = battleMapTileId,
            UnitTemplateId = _TEMPLATE_ID,
            Name = "Alpha",
            InitialStrength = 100,
            HealthyCount = 100,
            CampaignMovementRemaining = campaignMovementRemaining,
            BattleMovementRemaining = battleMovementRemaining
        });

        await context.SaveChangesAsync();
    }

    private static PossibleActionContext Context()
    {
        return new PossibleActionContext(_GAME_ID, _PLAYER_ID, currentRoundNumber: 1);
    }

    #endregion

    [Test, Description("A Force with campaign movement left is named for its template, not its own name, with what it has left out of its allowance.")]
    public async Task ShouldDescribeCampaignForceByTemplateAndMovement()
    {
        await AddForceAsync(7, campaignMovementRemaining: 500);

        PossibleAction action = (await new CampaignRemainingMovementCheck(DbFactory).EvaluateAsync(Context(), CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(action.Description, Is.EqualTo("Settler (0.5/1)"));
            Assert.That(action.Id, Is.EqualTo(7));
            Assert.That(action.UnitTemplateId, Is.EqualTo(_TEMPLATE_ID));
            Assert.That(action.RecordTypeId, Is.EqualTo((int)RecordType.Unit));
            Assert.That(action.XCoord, Is.EqualTo(3));
            Assert.That(action.YCoord, Is.EqualTo(4));
        });
    }

    [Test, Description("A Force in battle is measured against its battlefield allowance, since campaign movement means nothing on a Battle Map.")]
    public async Task ShouldDescribeBattleForceAgainstBattlefieldMovement()
    {
        await AddForceAsync(8, campaignMovementRemaining: 500, battleMovementRemaining: 2_000, battleMapTileId: 900);

        PossibleAction action = (await new BattleRemainingMovementCheck(DbFactory).EvaluateAsync(Context(), CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(action.Description, Is.EqualTo("Settler (2/3)"));
            Assert.That(action.UnitTemplateId, Is.EqualTo(_TEMPLATE_ID));
        });
    }
}
