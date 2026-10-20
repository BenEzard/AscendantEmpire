using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Tests for <see cref="TileTerritoryService"/>: what reaches the database when the pass decides a border has moved.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The writer is deliberately tracked entity writes plus one save - no bulk update, no raw SQL, no transaction - so a container would buy nothing
/// here, and that is itself part of what this fixture holds in place.
/// </para>
/// <para>
/// The geometry is substituted, because <c>IGameContextService</c> reads the Game and its Players and none of that is what is under test. Everything else is real.
/// </para>
/// </remarks>
[TestFixture]
internal class TileTerritoryServiceTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 10;
    private const int _RIVAL_ID = 11;

    private const int _MAP_WIDTH = 5;
    private const int _MAP_HEIGHT = 5;

    private TileTerritoryService _service = null!;

    private static int IdOf(int x, int y)
    {
        return x + (y * _MAP_WIDTH);
    }

    [SetUp]
    public void SetUpService()
    {
        IGameContextService gameContextService = Substitute.For<IGameContextService>();

        gameContextService
            .GetGameContexts(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResultBase.ServiceResult<IReadOnlyCollection<GameContext>>.Succeed(
            [
                new GameContext
                {
                    GameId = _GAME_ID,
                    GameName = "Territory",
                    NumberOfPlayers = 2,
                    MapWidth = _MAP_WIDTH,
                    MapHeight = _MAP_HEIGHT,
                    CampaignMapOffsetId = 0,
                    HasHorizontalWrapping = false,
                    HasVerticalWrapping = false,
                    Players = []
                }
            ]));

        _service = new TileTerritoryService(
            NullLogger<TileTerritoryService>.Instance,
            DbFactory,
            new CampaignTerritoryReader(),
            gameContextService);
    }

    [Test, Description("A ringed interior is absorbed and written, which is the end-to-end proof that the pure pass and the writer agree about what a grant is.")]
    public async Task ShouldWriteAnAbsorbedInteriorAsync()
    {
        await SeedMapAsync();
        await RingAsync(2, 2, _PLAYER_ID);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase.ServiceResult<TerritoryChanges> result = await _service.ResolveGameTerritoryAsync(context, _GAME_ID);
        await context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That((await ReloadTileAsync(2, 2)).OwnedByPlayerId, Is.EqualTo(_PLAYER_ID));
        }
    }

    [Test, Description("A claim out of supply range is deleted and its tile's HasClaims cleared in the same save, so the junction and the flag are never observably out of step.")]
    public async Task ShouldDeleteALapsedClaimAndClearItsFlagAsync()
    {
        await SeedMapAsync();

        // No owned tile anywhere, so the claim can reach nothing and lapses.
        await SeedClaimAsync(4, 4, _PLAYER_ID, setFlag: true);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.ResolveGameTerritoryAsync(context, _GAME_ID);
        await context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await ReloadClaimCountAsync(), Is.Zero);
            Assert.That((await ReloadTileAsync(4, 4)).HasClaims, Is.False);
        }
    }

    [Test, Description("A stale HasClaims flag is repaired even though nothing else about the tile moved, because the pass holds the whole claim set and the flag is only a cache of it.")]
    public async Task ShouldRepairAStaleHasClaimsFlagAsync()
    {
        await SeedMapAsync();
        await SetHasClaimsAsync(1, 1, hasClaims: true);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.ResolveGameTerritoryAsync(context, _GAME_ID);
        await context.SaveChangesAsync();

        Assert.That((await ReloadTileAsync(1, 1)).HasClaims, Is.False);
    }

    [Test, Description("A Game whose borders have not moved is written to not at all, which is the commonest outcome of a Round and must not cost a statement.")]
    public async Task ShouldWriteNothingWhenNoBorderMovedAsync()
    {
        await SeedMapAsync();

        // Both Players on the open region's boundary, so neither rings it and nothing is absorbed.
        await PaintAsync(0, 0, ownedByPlayerId: _PLAYER_ID);
        await PaintAsync(4, 4, ownedByPlayerId: _RIVAL_ID);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase.ServiceResult<TerritoryChanges> result = await _service.ResolveGameTerritoryAsync(context, _GAME_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.IsEmpty, Is.True);
        }
    }

    [Test, Description("Running the pass a second time writes nothing, which is what stands in for the turn pipeline having no transaction and this process no per-Round stamp.")]
    public async Task ShouldWriteNothingOnASecondRunAsync()
    {
        await SeedMapAsync();
        await RingAsync(2, 2, _PLAYER_ID);
        await PaintAsync(4, 4, ownedByPlayerId: _RIVAL_ID);

        await _service.ResolveGameTerritoryAsync(_GAME_ID);

        ServiceResultBase.ServiceResult<TerritoryChanges> second = await _service.ResolveGameTerritoryAsync(_GAME_ID);

        Assert.That(second.SuccessValue!.IsEmpty, Is.True);
    }

    [Test, Description("A grant that says so clears the Abandoned stamps, because a transfer extinguishes the former ownership rather than leaving the ground remembering it.")]
    public async Task ShouldClearAbandonmentWhenAGrantSaysSoAsync()
    {
        await SeedMapAsync();
        await PaintAsync(2, 2, lastOwnedByPlayerId: _RIVAL_ID, abandonedOnTurnId: 3);

        // Handed straight to the writer, because enclosure never offers abandoned ground: a tile with a former owner is not wilderness. The route that does reach it is a
        // Settlement's radius, which is BuildingStructure's business.
        TerritoryChanges changes = new()
        {
            OwnershipWrites = new Dictionary<int, TileOwnershipWrite>
            {
                [IdOf(2, 2)] = new TileOwnershipWrite(IdOf(2, 2), _PLAYER_ID, OwnedBySettlementId: null, ClearsAbandonment: true)
            },
            ClaimsToAdd = new HashSet<(int, int)>(),
            ClaimsToRemove = new HashSet<(int, int)>(),
            HasClaimsWrites = new Dictionary<int, bool>()
        };

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.ApplyAsync(context, _GAME_ID, changes);
        await context.SaveChangesAsync();

        CampaignMap tile = await ReloadTileAsync(2, 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tile.OwnedByPlayerId, Is.EqualTo(_PLAYER_ID));
            Assert.That(tile.LastOwnedByPlayerId, Is.Null);
            Assert.That(tile.AbandonedOnTurnId, Is.Null);
        }
    }

    [Test, Description("Enclosure leaves abandoned ground inside a ring alone, because a tile still remembering a former owner is not wilderness to absorb.")]
    public async Task ShouldNotAbsorbAbandonedGroundInsideARingAsync()
    {
        await SeedMapAsync();
        await RingAsync(2, 2, _PLAYER_ID);
        await PaintAsync(2, 2, lastOwnedByPlayerId: _RIVAL_ID, abandonedOnTurnId: 3);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await _service.ResolveGameTerritoryAsync(context, _GAME_ID);
        await context.SaveChangesAsync();

        CampaignMap tile = await ReloadTileAsync(2, 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tile.OwnedByPlayerId, Is.Null);
            Assert.That(tile.LastOwnedByPlayerId, Is.EqualTo(_RIVAL_ID), "It keeps remembering until it reverts to Unclaimed of its own accord.");
        }
    }

    [Test, Description("A Game with no tiles resolves to nothing rather than failing, so a Game mid-setup cannot stop a Round from ending.")]
    public async Task ShouldSucceedWithNothingToDoForAGameWithNoTilesAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        ServiceResultBase.ServiceResult<TerritoryChanges> result = await _service.ResolveGameTerritoryAsync(context, _GAME_ID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.IsEmpty, Is.True);
        }
    }

    /// <summary>A 5 x 5 Flat Grassland map with a campaign queue holding both Players.</summary>
    private async Task SeedMapAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

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

        for (int y = 0; y < _MAP_HEIGHT; y++)
        {
            for (int x = 0; x < _MAP_WIDTH; x++)
            {
                context.CampaignMaps.Add(new CampaignMap
                {
                    CampaignMapId = IdOf(x, y),
                    GameId = _GAME_ID,
                    XCoord = x,
                    YCoord = y,
                    BiomeTypeId = grassland.BiomeTypeId,
                    CampaignMapGeographyTypeId = flat.CampaignMapGeographyTypeId,
                    Biome = grassland,
                    Geography = flat,
                    // The navigations the tile declares required but nothing here reads.
                    Game = null!,
                    FirstVisitor = null,
                    LastVisitor = null,
                    OwnedBy = null
                });
            }
        }

        var queue = new TurnQueue
        {
            Id = 1,
            GameId = _GAME_ID,
            QueueType = TurnQueueType.Campaign,
            CurrentRoundNumber = 1,
            Game = null!
        };

        context.TurnQueues.Add(queue);

        context.TurnQueueParticipants.AddRange(
            new TurnQueueParticipant { Id = 1, TurnQueueId = queue.Id, TurnQueue = queue, PlayerId = _PLAYER_ID, TurnOrder = 1, Status = TurnQueueParticipantStatus.Pending, IsActiveInLane = true, Player = null! },
            new TurnQueueParticipant { Id = 2, TurnQueueId = queue.Id, TurnQueue = queue, PlayerId = _RIVAL_ID, TurnOrder = 2, Status = TurnQueueParticipantStatus.Pending, IsActiveInLane = true, Player = null! });

        await context.SaveChangesAsync();
    }

    /// <summary>Gives one Player all eight neighbours of a tile, so its interior is ringed.</summary>
    private async Task RingAsync(int x, int y, int playerId)
    {
        for (int ringY = y - 1; ringY <= y + 1; ringY++)
        {
            for (int ringX = x - 1; ringX <= x + 1; ringX++)
            {
                if ((ringX, ringY) != (x, y))
                {
                    await PaintAsync(ringX, ringY, ownedByPlayerId: playerId);
                }
            }
        }
    }

    private async Task PaintAsync(int x, int y, int? ownedByPlayerId = null, int? lastOwnedByPlayerId = null, int? abandonedOnTurnId = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        CampaignMap tile = await context.CampaignMaps.SingleAsync(t => t.CampaignMapId == IdOf(x, y));

        if (ownedByPlayerId is int owner)
        {
            tile.OwnedByPlayerId = owner;
        }

        if (lastOwnedByPlayerId is int lastOwner)
        {
            tile.LastOwnedByPlayerId = lastOwner;
        }

        if (abandonedOnTurnId is int abandonedOn)
        {
            tile.AbandonedOnTurnId = abandonedOn;
        }

        await context.SaveChangesAsync();
    }

    private async Task SeedClaimAsync(int x, int y, int playerId, bool setFlag)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMapClaims.Add(new CampaignMapClaim
        {
            CampaignMapId = IdOf(x, y),
            PlayerId = playerId,
            GameId = _GAME_ID
        });

        if (setFlag)
        {
            CampaignMap tile = await context.CampaignMaps.SingleAsync(t => t.CampaignMapId == IdOf(x, y));
            tile.HasClaims = true;
        }

        await context.SaveChangesAsync();
    }

    private async Task SetHasClaimsAsync(int x, int y, bool hasClaims)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        CampaignMap tile = await context.CampaignMaps.SingleAsync(t => t.CampaignMapId == IdOf(x, y));
        tile.HasClaims = hasClaims;

        await context.SaveChangesAsync();
    }

    private async Task<CampaignMap> ReloadTileAsync(int x, int y)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.CampaignMaps.AsNoTracking().SingleAsync(t => t.CampaignMapId == IdOf(x, y));
    }

    private async Task<int> ReloadClaimCountAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.CampaignMapClaims.AsNoTracking().CountAsync();
    }
}
