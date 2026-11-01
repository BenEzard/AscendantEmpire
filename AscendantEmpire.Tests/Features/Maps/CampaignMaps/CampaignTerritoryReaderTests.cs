using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Tests for <see cref="CampaignTerritoryReader"/>: what the per-Round pass is handed, and from which rows.
/// </summary>
/// <remarks>
/// On the in-memory tier. The reader is ordinary LINQ with no bulk operations, raw SQL or constraint dependency, so a container would buy nothing. What is pinned is the
/// shape of the projection, the Game boundary, and the two things this reader deliberately does differently from <see cref="CampaignOwnershipReader"/>: it reads the claim
/// junction by Game rather than by the <c>HasClaims</c> flag, and it hands back the Players in turn order.
/// </remarks>
[TestFixture]
internal class CampaignTerritoryReaderTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _OTHER_GAME_ID = 2;
    private const int _FIRST_PLAYER_ID = 10;
    private const int _SECOND_PLAYER_ID = 11;
    private const int _THIRD_PLAYER_ID = 12;

    private const int _MAP_WIDTH = 3;
    private const int _MAP_HEIGHT = 3;

    private readonly CampaignTerritoryReader _reader = new();

    private static int IdOf(int x, int y)
    {
        return x + (y * _MAP_WIDTH);
    }

    [Test, Description("Every tile of the Game comes back, because the pass is a graph problem over the whole map rather than over a handful of tiles in sight.")]
    public async Task ShouldReadEveryTileOfTheGameAsync()
    {
        await SeedMapAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyCollection<TerritoryTile> tiles = await _reader.GetTerritoryAsync(context, _GAME_ID);

        Assert.That(tiles, Has.Count.EqualTo(_MAP_WIDTH * _MAP_HEIGHT));
    }

    [Test, Description("Another Game's tiles are excluded, since ownership is resolved per Game and the world map holds every Game's block.")]
    public async Task ShouldExcludeAnotherGamesTilesAsync()
    {
        await SeedMapAsync(alsoSeedOtherGame: true);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyCollection<TerritoryTile> tiles = await _reader.GetTerritoryAsync(context, _GAME_ID);

        Assert.That(tiles, Has.Count.EqualTo(_MAP_WIDTH * _MAP_HEIGHT));
    }

    [Test, Description("The projection brings back the terrain, river and ownership columns the rules read, because a missing one would silently change a border.")]
    public async Task ShouldProjectEveryColumnTheRulesReadAsync()
    {
        await SeedMapAsync();
        await PaintAsync(1, 1, biomeType: BiomeType.Void, geographyType: CampaignMapGeographyType.Mountainous, ownedByPlayerId: _FIRST_PLAYER_ID, southRiver: RiverWidth.Large, eastRiver: RiverWidth.Medium);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        TerritoryTile tile = (await _reader.GetTerritoryAsync(context, _GAME_ID)).Single(t => t.CampaignMapId == IdOf(1, 1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tile.X, Is.EqualTo(1));
            Assert.That(tile.Y, Is.EqualTo(1));
            Assert.That(tile.OwnedByPlayerId, Is.EqualTo(_FIRST_PLAYER_ID));
            Assert.That(tile.BiomeTypeId, Is.EqualTo(BiomeType.Void));
            Assert.That(tile.BiomeIsTraversable, Is.False);
            Assert.That(tile.GeographyIsTraversable, Is.False);
            Assert.That(tile.Rivers.South, Is.EqualTo(RiverWidth.Large));
            Assert.That(tile.Rivers.East, Is.EqualTo(RiverWidth.Medium));
            Assert.That(tile.IsVoid, Is.True);
            Assert.That(tile.IsImpassable, Is.True);
        }
    }

    [Test, Description("Claimants come back against their tile, in Player id order, so a ring of claims reads the same whichever order the rows were written in.")]
    public async Task ShouldReadClaimantsAgainstTheirTileAsync()
    {
        await SeedMapAsync();
        await SeedClaimAsync(2, 2, _SECOND_PLAYER_ID, setFlag: true);
        await SeedClaimAsync(2, 2, _FIRST_PLAYER_ID, setFlag: true);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        TerritoryTile tile = (await _reader.GetTerritoryAsync(context, _GAME_ID)).Single(t => t.CampaignMapId == IdOf(2, 2));

        Assert.That(tile.ClaimantPlayerIds, Is.EqualTo([_FIRST_PLAYER_ID, _SECOND_PLAYER_ID]).AsCollection);
    }

    [Test, Description("A claim row on a tile whose HasClaims flag is false is still read, because the junction is the truth and the flag is only a cache the pass repairs.")]
    public async Task ShouldReadAClaimOnATileWhoseFlagIsFalseAsync()
    {
        await SeedMapAsync();
        await SeedClaimAsync(2, 2, _FIRST_PLAYER_ID, setFlag: false);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        TerritoryTile tile = (await _reader.GetTerritoryAsync(context, _GAME_ID)).Single(t => t.CampaignMapId == IdOf(2, 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tile.ClaimantPlayerIds, Is.EquivalentTo(new[] { _FIRST_PLAYER_ID }));
            Assert.That(tile.HasClaims, Is.False, "The stale flag is carried through as it stands, so the pass can put it right.");
        }
    }

    [Test, Description("Only the tiles asked for come back from the narrowed read, which is what lets a Structure's assertion load nine tiles instead of a whole map.")]
    public async Task ShouldReadOnlyTheTilesAskedForAsync()
    {
        await SeedMapAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyCollection<TerritoryTile> tiles = await _reader.GetTilesAsync(context, _GAME_ID, [IdOf(0, 0), IdOf(1, 0)]);

        Assert.That(tiles.Select(tile => tile.CampaignMapId), Is.EquivalentTo(new[] { IdOf(0, 0), IdOf(1, 0) }));
    }

    [Test, Description("Players come back in turn queue order, because that is the tie-break wherever two of them could want one tile.")]
    public async Task ShouldReadPlayersInTurnOrderAsync()
    {
        await SeedMapAsync();
        await SeedTurnQueueAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyCollection<int> playerIds = await _reader.GetPlayerIdsInTurnOrderAsync(context, _GAME_ID);

        Assert.That(playerIds, Is.EqualTo([_SECOND_PLAYER_ID, _FIRST_PLAYER_ID]).AsCollection, "Seeded with the second Player first in the queue.");
    }

    [Test, Description("A Player who has dropped out of the lane is left out, since a pass that granted them ground would be resolving a Player who is no longer playing.")]
    public async Task ShouldExcludeAPlayerNoLongerActiveInTheLaneAsync()
    {
        await SeedMapAsync();
        await SeedTurnQueueAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.That(await _reader.GetPlayerIdsInTurnOrderAsync(context, _GAME_ID), Does.Not.Contain(_THIRD_PLAYER_ID));
    }

    /// <summary>A 3 x 3 Flat Grassland map, with the terrain rows the projection reads through.</summary>
    private async Task SeedMapAsync(bool alsoSeedOtherGame = false)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var flat = new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Name = "Flat",
            MovementCost = 1m,
            IsWater = false,
            IsTraversable = true,
            RelativeElevation = 0,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        };

        var mountainous = new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Mountainous,
            Name = "Mountainous",
            MovementCost = CampaignGeography.IMPASSABLE_MOVEMENT_COST,
            IsWater = false,
            IsTraversable = false,
            RelativeElevation = 3,
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

        var theVoid = new CampaignBiome
        {
            BiomeTypeId = BiomeType.Void,
            Name = "Void",
            MovementCost = 1m,
            IsWater = false,
            IsTraversable = false,
            MapEditorHtmlColor = "#000000"
        };

        context.CampaignGeographies.AddRange(flat, mountainous);
        context.CampaignBiomes.AddRange(grassland, theVoid);

        for (int y = 0; y < _MAP_HEIGHT; y++)
        {
            for (int x = 0; x < _MAP_WIDTH; x++)
            {
                context.CampaignMaps.Add(NewTile(IdOf(x, y), _GAME_ID, x, y, grassland, flat));
            }
        }

        if (alsoSeedOtherGame)
        {
            // Another Game's block, starting past this one's, which the Game filter has to leave behind.
            context.CampaignMaps.Add(NewTile(500, _OTHER_GAME_ID, 0, 0, grassland, flat));
        }

        await context.SaveChangesAsync();
    }

    private static CampaignMap NewTile(int id, int gameId, int x, int y, CampaignBiome biome, CampaignGeography geography)
    {
        return new CampaignMap
        {
            CampaignMapId = id,
            GameId = gameId,
            XCoord = x,
            YCoord = y,
            BiomeTypeId = biome.BiomeTypeId,
            CampaignMapGeographyTypeId = geography.CampaignMapGeographyTypeId,
            Biome = biome,
            Geography = geography,
            // The navigations the tile declares required but nothing here reads.
            Game = null!,
            FirstVisitor = null,
            LastVisitor = null,
            OwnedBy = null
        };
    }

    private async Task PaintAsync(
        int x,
        int y,
        BiomeType? biomeType = null,
        CampaignMapGeographyType? geographyType = null,
        int? ownedByPlayerId = null,
        RiverWidth southRiver = RiverWidth.None,
        RiverWidth eastRiver = RiverWidth.None)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        CampaignMap tile = await context.CampaignMaps.SingleAsync(t => t.CampaignMapId == IdOf(x, y));

        if (biomeType is BiomeType biome)
        {
            tile.BiomeTypeId = biome;
        }

        if (geographyType is CampaignMapGeographyType geography)
        {
            tile.CampaignMapGeographyTypeId = geography;
        }

        tile.OwnedByPlayerId = ownedByPlayerId;
        tile.SouthRiver = southRiver;
        tile.EastRiver = eastRiver;

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

    /// <summary>A campaign queue with the second Player taking their Turn first, plus one Player who has left the lane and a Battle queue that must be ignored.</summary>
    private async Task SeedTurnQueueAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var campaignQueue = new TurnQueue
        {
            Id = 1,
            GameId = _GAME_ID,
            QueueType = TurnQueueType.Campaign,
            CurrentRoundNumber = 1,
            Game = null!
        };

        var battleQueue = new TurnQueue
        {
            Id = 2,
            GameId = _GAME_ID,
            QueueType = TurnQueueType.Battle,
            BattleId = 99,
            CurrentRoundNumber = 1,
            Game = null!
        };

        context.TurnQueues.AddRange(campaignQueue, battleQueue);

        context.TurnQueueParticipants.AddRange(
            NewParticipant(1, campaignQueue, _SECOND_PLAYER_ID, turnOrder: 1, isActiveInLane: true),
            NewParticipant(2, campaignQueue, _FIRST_PLAYER_ID, turnOrder: 2, isActiveInLane: true),
            NewParticipant(3, campaignQueue, _THIRD_PLAYER_ID, turnOrder: 3, isActiveInLane: false),
            NewParticipant(4, battleQueue, _THIRD_PLAYER_ID, turnOrder: 1, isActiveInLane: true));

        await context.SaveChangesAsync();
    }

    private static TurnQueueParticipant NewParticipant(int id, TurnQueue queue, int playerId, int turnOrder, bool isActiveInLane)
    {
        return new TurnQueueParticipant
        {
            Id = id,
            TurnQueueId = queue.Id,
            TurnQueue = queue,
            PlayerId = playerId,
            TurnOrder = turnOrder,
            Status = TurnQueueParticipantStatus.Pending,
            IsActiveInLane = isActiveInLane,
            Player = null!
        };
    }
}
