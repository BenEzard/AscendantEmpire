using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Structures.Services;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.Domain.Enums.Structures;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.Turns.Services.TurnPipeline;

/// <summary>
/// Tests for <see cref="BuildingStructure"/>: which Structures a Turn advances, by how much, and what finishing one does to the tile under it.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The process itself only queries, updates and saves; the writing of progress and its history row is <c>IStructureLifecycleWriter</c>'s, which is
/// substituted here - it allocates its sequence with raw SQL, and what it writes is covered by its own fixtures. What this pins is the choosing and the arithmetic: which
/// Structures are picked up, what target each is advanced to, and the ownership written when one completes.
/// </para>
/// <para>
/// The substitute applies the progress itself, exactly as the real writer does, so the completion the process reads back afterwards is the one a real run would see.
/// </para>
/// </remarks>
[TestFixture]
internal class BuildingStructureTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 2;
    private const int _OTHER_PLAYER_ID = 3;
    private const int _PLAYER_TURN_ID = 4;
    private const int _ROUND_NUMBER = 7;

    /// <summary>A kind taking twice the fixture's per-Turn rate, so the Forces below build it in two Turns.</summary>
    private const int _TWO_TURN_STRUCTURE_ID = 10;

    /// <summary>A kind taking exactly the per-Turn rate, so it goes up in one Turn.</summary>
    private const int _ONE_TURN_STRUCTURE_ID = 11;

    /// <summary>A kind taking three times the rate, so a build lasting several Turns is exercised.</summary>
    private const int _THREE_TURN_STRUCTURE_ID = 12;

    /// <summary>A kind taking two and a half times the rate, so the last Turn of its build is a part Turn.</summary>
    private const int _UNEVEN_STRUCTURE_ID = 14;

    /// <summary>A kind that needs no building at all - a marker, like the totem pole - which finishes the Turn it is founded in.</summary>
    private const int _NO_WORK_STRUCTURE_ID = 13;

    /// <summary>
    /// The work one Turn of the fixture's Force does, raw Scale-1,000.
    /// </summary>
    /// <remarks>
    /// An arbitrary round number: this fixture is about the arithmetic of advancing, not about the rate, which <c>ConstructionRateCalculatorTests</c> owns. Every seeded
    /// Structure carries it as its <c>BuildPointsPerTurn</c>, so a kind's work divided by this is how many Turns it takes.
    /// </remarks>
    private const long _POINTS_PER_TURN = 1_000_000;

    /// <summary>Some work done but not all of it, for the Structures a Turn is supposed to leave exactly as it found them.</summary>
    private const long _PART_BUILT = _POINTS_PER_TURN / 5;

    private const int _TILE_ID = 100;

    private IStructureLifecycleWriter _lifecycleWriter = null!;
    private BuildingStructure _process = null!;

    [SetUp]
    public void SetUpProcess()
    {
        _lifecycleWriter = Substitute.For<IStructureLifecycleWriter>();

        // Stands in for the real writer: applies the progress and stamps the completion Turn, which is what the process reads back to decide whether to claim the tile.
        // Written through the tracker because both properties are init-only, exactly as the real writer does it.
        _lifecycleWriter
            .ProgressAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<StructureInstance>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var context = call.ArgAt<AscendantEmpireContext>(0);
                var structure = call.ArgAt<StructureInstance>(1);
                long target = call.ArgAt<long>(2);
                int playerTurnId = call.ArgAt<int>(3);

                // The kind comes from the process's own Include, which is what the real writer reads the total from too.
                long buildPointsRequired = structure.Structure?.BuildPointsRequired ?? 0;

                var entry = context.Entry(structure);
                entry.Property(s => s.BuildPointsProgress).CurrentValue = Math.Min(target, buildPointsRequired);

                if (target >= buildPointsRequired)
                {
                    entry.Property(s => s.CompletedOnTurnId).CurrentValue = playerTurnId;
                }

                return Task.FromResult(ServiceResultBase.Succeed());
            });

        _process = new BuildingStructure(DbFactory, _lifecycleWriter, NullLogger<BuildingStructure>.Instance);
    }

    [Test, Description("A two-turn structure gains half its build each turn, so a village founded this turn is half built when the turn ends.")]
    public async Task ShouldAdvanceATwoTurnStructureToHalfBuiltAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: 0);

        await _process.ProcessAsync(Context());

        StructureInstance structure = await ReloadAsync(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(structure.BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN), "One Turn of work, which is half of this kind.");
            Assert.That(structure.CompletedOnTurnId, Is.Null, "Still being built.");
        }
    }

    [Test, Description("A second turn on a half-built two-turn structure finishes it, which is what makes a village founded on turn 4 complete during turn 6.")]
    public async Task ShouldCompleteATwoTurnStructureOnItsSecondTurnAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: _POINTS_PER_TURN);

        await _process.ProcessAsync(Context());

        StructureInstance structure = await ReloadAsync(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(structure.BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN * 2), "All of this kind's work.");
            Assert.That(structure.CompletedOnTurnId, Is.EqualTo(_PLAYER_TURN_ID));
        }
    }

    [Test, Description("A one-turn structure finishes at the end of the turn it was founded in, which is what makes a camp complete a turn after founding.")]
    public async Task ShouldCompleteAOneTurnStructureInOneTurnAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _ONE_TURN_STRUCTURE_ID, buildPointsProgress: 0);

        await _process.ProcessAsync(Context());

        Assert.That((await ReloadAsync(1)).BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN), "All of this kind's work, done in its one Turn.");
    }

    [Test, Description("A multi-turn build gains one turn of work per turn and finishes on the turn its total is met, no sooner.")]
    public async Task ShouldAdvanceAThreeTurnStructureOneTurnOfWorkAtATimeAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _THREE_TURN_STRUCTURE_ID, buildPointsProgress: 0);

        await _process.ProcessAsync(Context());
        Assert.That((await ReloadAsync(1)).BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN));

        await _process.ProcessAsync(Context());
        Assert.That((await ReloadAsync(1)).BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN * 2));

        await _process.ProcessAsync(Context());

        StructureInstance structure = await ReloadAsync(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(structure.BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN * 3));
            Assert.That(structure.CompletedOnTurnId, Is.EqualTo(_PLAYER_TURN_ID));
        }
    }

    /// <summary>
    /// Two and a half Turns of work takes three Turns, because the last part Turn is still a whole Turn of waiting - which is what
    /// <c>ConstructionRateCalculator.CalculateTurnsToComplete</c> rounds up to and tells the player when they found.
    /// </summary>
    /// <remarks>
    /// The regression this pins: while progress was a percentage, the step looked ahead and finished anything within one step of the end, so this build completed on its
    /// second Turn and beat the figure the settle dialog had promised.
    /// </remarks>
    [Test, Description("A build whose work does not divide evenly into its rate takes the whole extra turn rather than finishing early on a part turn.")]
    public async Task ShouldNotFinishAnUnevenlyDividedBuildEarlyAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _UNEVEN_STRUCTURE_ID, buildPointsProgress: 0);

        await _process.ProcessAsync(Context());
        await _process.ProcessAsync(Context());

        StructureInstance afterTwoTurns = await ReloadAsync(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterTwoTurns.BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN * 2), "Two turns of work, with half a turn still owed.");
            Assert.That(afterTwoTurns.CompletedOnTurnId, Is.Null, "Half a turn of work short is not finished.");
        }

        await _process.ProcessAsync(Context());

        StructureInstance afterThreeTurns = await ReloadAsync(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterThreeTurns.BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN * 5 / 2), "Clamped to the total rather than overshooting it.");
            Assert.That(afterThreeTurns.CompletedOnTurnId, Is.EqualTo(_PLAYER_TURN_ID));
            Assert.That(ConstructionRateCalculator.CalculateTurnsToComplete(_POINTS_PER_TURN * 5 / 2, _POINTS_PER_TURN), Is.EqualTo(3),
                "The same three turns the player was told at founding.");
        }
    }

    /// <summary>
    /// The whole point of the points model: the same kind of Structure takes longer for a weaker founder. Two villages, one founded by a Force building at half the rate,
    /// and the slower one is half as far along after the same Turn.
    /// </summary>
    [Test, Description("Two structures of the same kind advance at their own founders' rates, so a village founded by a smaller force is further behind after the same turn.")]
    public async Task ShouldAdvanceEachStructureAtItsOwnFoundersRateAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: 0);
        await SeedStructureAsync(id: 2, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: 0, buildPointsPerTurn: _POINTS_PER_TURN / 2);

        await _process.ProcessAsync(Context());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await ReloadAsync(1)).BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN));
            Assert.That((await ReloadAsync(2)).BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN / 2), "Half the rate is half the progress.");
        }
    }

    [Test, Description("A kind that needs no work is finished outright on its first turn, which is how a planted marker differs from a raised building.")]
    public async Task ShouldFinishAStructureThatNeedsNoWorkAtOnceAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _NO_WORK_STRUCTURE_ID, buildPointsProgress: 0);

        await _process.ProcessAsync(Context());

        StructureInstance structure = await ReloadAsync(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(structure.BuildPointsProgress, Is.Zero, "There was no work to do, so none was done - the stamp is what says it is finished.");
            Assert.That(structure.CompletedOnTurnId, Is.EqualTo(_PLAYER_TURN_ID));
        }
    }

    /// <summary>
    /// A Structure placed rather than founded by anybody carries no rate. The floor means it creeps rather than stalling for ever, which is a bug that would otherwise leave
    /// a permanent hole in the map with nothing to say so.
    /// </summary>
    [Test, Description("A structure carrying no build rate still advances, by the one-point floor, rather than stalling unfinished for ever.")]
    public async Task ShouldStillAdvanceAStructureWithNoRateAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: 0, buildPointsPerTurn: 0);

        await _process.ProcessAsync(Context());

        Assert.That((await ReloadAsync(1)).BuildPointsProgress, Is.GreaterThan(0));
    }

    [Test, Description("A finished structure's own tile becomes owned by its player, which is the design's rule that finishing a structure asserts ownership.")]
    public async Task ShouldClaimTheTileWhenAStructureCompletesAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _ONE_TURN_STRUCTURE_ID, buildPointsProgress: 0);

        await _process.ProcessAsync(Context());

        CampaignMap tile = await ReloadTileAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tile.OwnedByPlayerId, Is.EqualTo(_PLAYER_ID));
            Assert.That(tile.OwnedBySettlementId, Is.Null, "A structure that is nobody's settlement carries none onto the tile.");
        }
    }

    [Test, Description("A completed settlement's tile is owned through that settlement, which is what lets settlement-scoped modifiers reach the ground it holds.")]
    public async Task ShouldCarryTheSettlementOntoItsTileWhenItCompletesAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _ONE_TURN_STRUCTURE_ID, buildPointsProgress: 0);
        await SeedSettlementAsync(settlementId: 50, structureInstanceId: 1);

        await _process.ProcessAsync(Context());

        CampaignMap tile = await ReloadTileAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tile.OwnedByPlayerId, Is.EqualTo(_PLAYER_ID));
            Assert.That(tile.OwnedBySettlementId, Is.EqualTo(50));
        }
    }

    [Test, Description("A structure still under construction claims nothing, so a half-built village gives its owner no ground until it is finished.")]
    public async Task ShouldNotClaimTheTileWhileStillUnderConstructionAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: 0);
        await SeedSettlementAsync(settlementId: 50, structureInstanceId: 1);

        await _process.ProcessAsync(Context());

        CampaignMap tile = await ReloadTileAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tile.OwnedByPlayerId, Is.Null);
            Assert.That(tile.OwnedBySettlementId, Is.Null);
        }
    }

    [Test, Description("Only the acting player's structures advance, because the process runs at the end of one player's turn and construction is paid for in that player's turns.")]
    public async Task ShouldAdvanceOnlyTheActingPlayersStructuresAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: 0);
        await SeedStructureAsync(id: 2, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: 0, ownedByPlayerId: _OTHER_PLAYER_ID);

        await _process.ProcessAsync(Context());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await ReloadAsync(1)).BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN));
            Assert.That((await ReloadAsync(2)).BuildPointsProgress, Is.EqualTo(0), "Another player's construction is theirs to advance on their own turn.");
        }
    }

    [Test, Description("A finished, a razed and a battle-map structure are all left alone, so a turn does no work on anything that is not under construction on the campaign map.")]
    public async Task ShouldLeaveStructuresThatAreNotUnderConstructionAloneAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: _POINTS_PER_TURN * 2, completedOnTurnId: 3);
        await SeedStructureAsync(id: 2, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: _PART_BUILT, removedOnTurnId: 3);
        await SeedStructureAsync(id: 3, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: _PART_BUILT, battleMapTileId: 900);

        string summary = await _process.ProcessAsync(Context());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary, Is.Empty, "Nothing was under construction, so the turn reports nothing.");
            Assert.That((await ReloadAsync(1)).BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN * 2));
            Assert.That((await ReloadAsync(2)).BuildPointsProgress, Is.EqualTo(_PART_BUILT));
            Assert.That((await ReloadAsync(3)).BuildPointsProgress, Is.EqualTo(_PART_BUILT));
        }
    }

    [Test, Description("One structure failing to advance does not stop the rest, because a single refusal must not cost the player the whole turn's construction.")]
    public async Task ShouldKeepAdvancingTheRestWhenOneStructureFailsAsync()
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: 0);
        await SeedStructureAsync(id: 2, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: 0);

        // Only the first structure's advance is refused.
        _lifecycleWriter
            .ProgressAsync(Arg.Any<AscendantEmpireContext>(), Arg.Is<StructureInstance>(s => s.Id == 1), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ServiceResultBase.Fail("Refused for the test.")));

        string summary = await _process.ProcessAsync(Context());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await ReloadAsync(1)).BuildPointsProgress, Is.EqualTo(0));
            Assert.That((await ReloadAsync(2)).BuildPointsProgress, Is.EqualTo(_POINTS_PER_TURN), "The second structure's turn is not lost to the first's refusal.");
            Assert.That(summary, Does.Contain("1 structure(s) could not be advanced"));
        }
    }

    [Test, Description("An end-of-turn context with no player or no open turn is refused outright, since advancing every player's construction would be worse than doing none.")]
    [TestCase(false, true, Description = "No Player Id.")]
    [TestCase(true, false, Description = "No Player Turn Id.")]
    public async Task ShouldRefuseAContextItCannotActOnAsync(bool hasPlayerId, bool hasPlayerTurnId)
    {
        await SeedCatalogueAsync();
        await SeedStructureAsync(id: 1, structureId: _TWO_TURN_STRUCTURE_ID, buildPointsProgress: 0);

        var context = hasPlayerId
            ? new TurnPipelineContextData(TurnPipelineType.EndCampaignTurn, _GAME_ID, _PLAYER_ID, _ROUND_NUMBER)
            {
                PlayerTurnId = hasPlayerTurnId ? _PLAYER_TURN_ID : null
            }
            : new TurnPipelineContextData(TurnPipelineType.EndCampaignTurn, _GAME_ID, _ROUND_NUMBER)
            {
                PlayerTurnId = hasPlayerTurnId ? _PLAYER_TURN_ID : null
            };

        string summary = await _process.ProcessAsync(context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary, Is.Not.Empty, "A refusal reports why.");
            Assert.That((await ReloadAsync(1)).BuildPointsProgress, Is.EqualTo(0), "Nothing was advanced.");
        }
    }

    // ── fixture helpers ───────────────────────────────────────────────────────

    private static TurnPipelineContextData Context()
    {
        return new TurnPipelineContextData(TurnPipelineType.EndCampaignTurn, _GAME_ID, _PLAYER_ID, _ROUND_NUMBER)
        {
            PlayerTurnId = _PLAYER_TURN_ID
        };
    }

    /// <summary>The three kinds the tests build, and the one tile they all stand on.</summary>
    private async Task SeedCatalogueAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Structures.AddRange(
            BuildStructureKind(_TWO_TURN_STRUCTURE_ID, "village", buildPointsRequired: _POINTS_PER_TURN * 2),
            BuildStructureKind(_ONE_TURN_STRUCTURE_ID, "camp", buildPointsRequired: _POINTS_PER_TURN),
            BuildStructureKind(_THREE_TURN_STRUCTURE_ID, "three-turn-thing", buildPointsRequired: _POINTS_PER_TURN * 3),
            BuildStructureKind(_NO_WORK_STRUCTURE_ID, "totem-pole", buildPointsRequired: 0),
            BuildStructureKind(_UNEVEN_STRUCTURE_ID, "uneven-thing", buildPointsRequired: _POINTS_PER_TURN * 5 / 2));

        // The terrain is incidental here - construction does not care what it is built on - but a tile needs a Biome and a Geography to exist at all.
        var flat = new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Name = "Flat",
            MovementCost = 1m,
            RelativeElevation = 0,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        };

        var grassland = new CampaignBiome
        {
            BiomeTypeId = BiomeType.Grassland,
            Name = "Grassland",
            MovementCost = 1m,
            IsWater = false,
            IsTraversable = true,
            MapEditorHtmlColor = "#7cb342"
        };

        context.CampaignGeographies.Add(flat);
        context.CampaignBiomes.Add(grassland);

        context.CampaignMaps.Add(new CampaignMap
        {
            CampaignMapId = _TILE_ID,
            GameId = _GAME_ID,
            XCoord = 4,
            YCoord = 1,
            BiomeTypeId = grassland.BiomeTypeId,
            CampaignMapGeographyTypeId = flat.CampaignMapGeographyTypeId,
            Biome = grassland,
            Geography = flat,
            // The navigations the tile declares required but nothing here reads. Left unset rather than stood up, as the other in-memory tile fixtures do.
            Game = null!,
            FirstVisitor = null,
            LastVisitor = null,
            OwnedBy = null
        });

        await context.SaveChangesAsync();
    }

    private static Structure BuildStructureKind(int id, string code, long buildPointsRequired)
    {
        return new Structure
        {
            Id = id,
            Code = code,
            Name = code,
            RecordTypeId = (int)Domain.Enums.RecordType.Structure,
            RequiresSlotType = TileSlotType.Primary,
            AllowedPositions = PositionOnTile.WholeTile,
            CanBeBuiltOnCampaignMap = true,
            CanBeBuiltOnBattleMap = false,
            DualMapBuildTrigger = false,
            BuildPointsRequired = buildPointsRequired,
            SightRange = 1,
            SightDirection = Domain.Enums.Maps.DetectionDirection.FullRadius,
            MapImagePath = $"{code}.png",
            FullImagePath = $"{code}.png"
        };
    }

    private async Task SeedStructureAsync(
        int id,
        int structureId,
        long buildPointsProgress,
        int? ownedByPlayerId = null,
        long? buildPointsPerTurn = null,
        int? completedOnTurnId = null,
        int? removedOnTurnId = null,
        int? battleMapTileId = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.StructureInstances.Add(new StructureInstance
        {
            Id = id,
            GameId = _GAME_ID,
            CampaignMapTileId = _TILE_ID,
            BattleMapTileId = battleMapTileId,
            StructureId = structureId,
            SlotType = TileSlotType.Primary,
            Position = PositionOnTile.WholeTile,
            OwnedByPlayerId = ownedByPlayerId ?? _PLAYER_ID,
            CreatedOnTurnId = 1,
            BuildPointsProgress = buildPointsProgress,
            BuildPointsPerTurn = buildPointsPerTurn ?? _POINTS_PER_TURN,
            CompletedOnTurnId = completedOnTurnId,
            RemovedOnTurnId = removedOnTurnId
        });

        await context.SaveChangesAsync();
    }

    private async Task SeedSettlementAsync(int settlementId, int structureInstanceId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.Settlements.Add(new Settlement
        {
            Id = settlementId,
            GameId = _GAME_ID,
            StructureInstanceId = structureInstanceId,
            FoundingPlayerId = _PLAYER_ID,
            Name = "Riverhold",
            Tier = SettlementTier.Village,
            Population = 100,
            FoundedOnRoundNumber = _ROUND_NUMBER,
            FoundedOnTurnId = 1
        });

        await context.SaveChangesAsync();
    }

    private async Task<StructureInstance> ReloadAsync(int id)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.StructureInstances.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    private async Task<CampaignMap> ReloadTileAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.CampaignMaps.AsNoTracking().SingleAsync(t => t.CampaignMapId == _TILE_ID);
    }
}
