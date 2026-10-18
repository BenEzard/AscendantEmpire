using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Tests for <see cref="CampaignOwnershipReader"/>: that the ownership columns, the claim junction and the battles on the map are read off the right rows for the right tiles.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The reader is three projected LINQ queries with no raw SQL, no bulk update and no complex type in its shape, so nothing here needs a container.
/// <c>TileOwnershipDerivationTests</c> pins the precedence between the states; what is pinned here is which rows feed it.
/// </para>
/// <para>
/// Two Games on 4 x 3 maps that wrap on neither axis. Game 1 carries the interesting ground: (0, 0) owned by Player 1, (1, 0) owned by Player 1 and occupied by Player 2,
/// (2, 0) claimed by both Players, (3, 0) abandoned by Player 2, (0, 1) owned by Player 1 with a battle on it, (1, 1) flagged as claimed with no rows behind it, and (2, 1)
/// owned and rebelling. Game 2 is owned by Player 1 throughout, so reading one Game can be shown not to pick up the other's ownership.
/// </para>
/// </remarks>
[TestFixture]
internal class CampaignOwnershipReaderTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _OTHER_GAME_ID = 2;

    private const int _MAP_OFFSET = 100;
    private const int _OTHER_MAP_OFFSET = 500;

    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 3;

    private const int _VIEWER = 1;
    private const int _RIVAL = 2;

    private readonly CampaignOwnershipReader _reader = new();

    /// <summary>The world-map id of a tile in the Game under test, which is the key the reader is asked about and answers with.</summary>
    private static int TileId(int x, int y)
    {
        return _MAP_OFFSET + x + (y * _MAP_WIDTH);
    }

    [SetUp]
    public async Task SeedMapsAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var grassland = new CampaignBiome
        {
            BiomeTypeId = BiomeType.Grassland,
            Name = "Grassland",
            MovementCost = 1m,
            IsTraversable = true,
            MapEditorHtmlColor = "#7cb342"
        };

        var flat = new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Name = "Flat",
            RelativeElevation = 0,
            MovementCost = 1m,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        };

        context.CampaignBiomes.Add(grassland);
        context.CampaignGeographies.Add(flat);

        context.GameIndex.AddRange(NewGame(_GAME_ID, _MAP_OFFSET), NewGame(_OTHER_GAME_ID, _OTHER_MAP_OFFSET));

        for (int y = 0; y < _MAP_HEIGHT; y++)
        {
            for (int x = 0; x < _MAP_WIDTH; x++)
            {
                CampaignMap tile = NewTile(_GAME_ID, _MAP_OFFSET, x, y, grassland, flat);

                switch ((x, y))
                {
                    case (0, 0):
                        tile.OwnedByPlayerId = _VIEWER;
                        break;

                    // Owned by the viewer and occupied by the rival, so the two layers can be told apart.
                    case (1, 0):
                        tile.OwnedByPlayerId = _VIEWER;
                        tile.OccupiedByPlayerId = _RIVAL;
                        break;

                    // Unowned and claimed by both, which is the case a column on the tile could not express.
                    case (2, 0):
                        tile.HasClaims = true;
                        break;

                    case (3, 0):
                        tile.LastOwnedByPlayerId = _RIVAL;
                        tile.AbandonedOnTurnId = 1;
                        break;

                    // Owned, with a battle on it, so Disputed can be shown to come from the Battle rather than the tile.
                    case (0, 1):
                        tile.OwnedByPlayerId = _VIEWER;
                        break;

                    // A claim flag left set over no rows at all, which must not resolve as claimed.
                    case (1, 1):
                        tile.HasClaims = true;
                        break;

                    // Owned and still rebelling, so the modifier can be shown to ride alongside the status.
                    case (2, 1):
                        tile.OwnedByPlayerId = _VIEWER;
                        tile.RebellionPercentRemaining = 60;
                        break;
                }

                context.CampaignMaps.Add(tile);

                // The other Game is owned throughout, so any leak across Games shows up as ownership where this Game has none.
                CampaignMap otherTile = NewTile(_OTHER_GAME_ID, _OTHER_MAP_OFFSET, x, y, grassland, flat);
                otherTile.OwnedByPlayerId = _VIEWER;
                context.CampaignMaps.Add(otherTile);
            }
        }

        // Both Players claim (2, 0). No Player rows are seeded: the reader never joins them, and the in-memory provider enforces no foreign keys.
        context.CampaignMapClaims.AddRange(
            new CampaignMapClaim { CampaignMapId = TileId(2, 0), PlayerId = _VIEWER, GameId = _GAME_ID },
            new CampaignMapClaim { CampaignMapId = TileId(2, 0), PlayerId = _RIVAL, GameId = _GAME_ID });

        // One battle still being fought, on (0, 1), and one already concluded on (0, 0) - which must not make that tile Disputed.
        context.Battles.AddRange(
            NewBattle(battleId: 1, TileId(0, 1), concludedAtRoundNumber: null),
            NewBattle(battleId: 2, TileId(0, 0), concludedAtRoundNumber: 4));

        await context.SaveChangesAsync();
    }

    private static GameIndex NewGame(int gameId, int mapOffset)
    {
        return new GameIndex
        {
            Id = gameId,
            GameName = $"Ownership Game {gameId}",
            NumberOfPlayers = 2,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = GameIndex.MIN_BATTLE_MAP_SIZE,
            CampaignMapOffsetId = mapOffset,
            MapWidth = _MAP_WIDTH,
            MapHeight = _MAP_HEIGHT,
            HorizontalWrap = false,
            VerticalWrap = false
        };
    }

    private static CampaignMap NewTile(int gameId, int mapOffset, int x, int y, CampaignBiome biome, CampaignGeography geography)
    {
        return new CampaignMap
        {
            CampaignMapId = mapOffset + x + (y * _MAP_WIDTH),
            GameId = gameId,
            XCoord = x,
            YCoord = y,
            BiomeTypeId = biome.BiomeTypeId,
            CampaignMapGeographyTypeId = geography.CampaignMapGeographyTypeId,
            Biome = biome,
            Geography = geography,
            Game = null!,
            FirstVisitor = null,
            LastVisitor = null,
            OwnedBy = null
        };
    }

    private static Battle NewBattle(int battleId, int campaignMapTileId, int? concludedAtRoundNumber)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new Battle
        {
            Id = battleId,
            GameId = _GAME_ID,
            CampaignMapTileId = campaignMapTileId,
            GlobalBattleName = $"Battle {battleId}",
            CommencedAtRoundNumber = 1,
            OriginalTileStatus = TileOwnershipStatus.Owned,
            OriginalTileOwnerId = _VIEWER,
            ConcludedAtRoundNumber = concludedAtRoundNumber,
            VictorId = null,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = []
        };
    }

    private async Task<IReadOnlyDictionary<int, TileOwnership>> ReadAsync(params int[] tileIds)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await _reader.GetTileOwnershipAsync(context, _GAME_ID, tileIds, CancellationToken.None);
    }

    [Test, Description("An owned tile comes back as Owned with its owner, read off the tile's own OwnedByPlayerId column.")]
    public async Task ShouldReportTheOwnerOfAnOwnedTile()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync(TileId(0, 0));

        Assert.Multiple(() =>
        {
            Assert.That(ownership[TileId(0, 0)].Status, Is.EqualTo(TileOwnershipStatus.Owned));
            Assert.That(ownership[TileId(0, 0)].OwnedByPlayerId, Is.EqualTo(_VIEWER));
        });
    }

    [Test, Description("An occupied tile reports both layers, so the occupier is the effective holder while the owner keeps ownership.")]
    public async Task ShouldReportBothLayersOfAnOccupiedTile()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync(TileId(1, 0));

        Assert.Multiple(() =>
        {
            Assert.That(ownership[TileId(1, 0)].Status, Is.EqualTo(TileOwnershipStatus.Occupied));
            Assert.That(ownership[TileId(1, 0)].OwnedByPlayerId, Is.EqualTo(_VIEWER));
            Assert.That(ownership[TileId(1, 0)].EffectiveHolderPlayerId, Is.EqualTo(_RIVAL));
        });
    }

    [Test, Description("A tile both Players claim names both of them, which is the case a single column on the tile could not express.")]
    public async Task ShouldNameBothClaimantsOfATileClaimedByTwoPlayers()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync(TileId(2, 0));

        Assert.Multiple(() =>
        {
            Assert.That(ownership[TileId(2, 0)].Status, Is.EqualTo(TileOwnershipStatus.Claimed));
            // In Player id order, because the client draws one colour per claimant along the border's dashes in this order.
            Assert.That(ownership[TileId(2, 0)].ClaimedByPlayerIds, Is.EqualTo(new[] { _VIEWER, _RIVAL }));
        });
    }

    [Test, Description("A battle still being fought makes its tile Disputed, which is derived from the Battle row and never stored on the tile.")]
    public async Task ShouldReportADisputedTileFromTheBattleOnIt()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync(TileId(0, 1));

        Assert.Multiple(() =>
        {
            Assert.That(ownership[TileId(0, 1)].Status, Is.EqualTo(TileOwnershipStatus.Disputed));
            // The ground under the fighting is still reported, since the battle does not change who held it.
            Assert.That(ownership[TileId(0, 1)].OwnedByPlayerId, Is.EqualTo(_VIEWER));
        });
    }

    [Test, Description("A concluded battle leaves its tile alone: the fighting is over, so the tile goes back to being read off its own columns.")]
    public async Task ShouldNotDisputeATileWhoseBattleHasConcluded()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync(TileId(0, 0));

        Assert.That(ownership[TileId(0, 0)].Status, Is.EqualTo(TileOwnershipStatus.Owned));
    }

    [Test, Description("A former owner with no current one comes back as Abandoned, held by nobody.")]
    public async Task ShouldReportAnAbandonedTile()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync(TileId(3, 0));

        Assert.Multiple(() =>
        {
            Assert.That(ownership[TileId(3, 0)].Status, Is.EqualTo(TileOwnershipStatus.Abandoned));
            Assert.That(ownership[TileId(3, 0)].LastOwnedByPlayerId, Is.EqualTo(_RIVAL));
            Assert.That(ownership[TileId(3, 0)].EffectiveHolderPlayerId, Is.Null);
        });
    }

    [Test, Description("A rebelling tile still reports as Owned, with the rebellion as the modifier it is and its percentage withheld.")]
    public async Task ShouldFlagARebellingTileWithoutChangingItsStatus()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync(TileId(2, 1));

        Assert.Multiple(() =>
        {
            Assert.That(ownership[TileId(2, 1)].Status, Is.EqualTo(TileOwnershipStatus.Owned));
            Assert.That(ownership[TileId(2, 1)].IsRebelling, Is.True);
        });
    }

    [Test, Description("A claim flag left set over no rows resolves as wilderness, so the flag decides what to look up and never what the answer is.")]
    public async Task ShouldReportWildernessWhenTheClaimFlagIsSetButNoRowsExist()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync(TileId(1, 1));

        Assert.Multiple(() =>
        {
            Assert.That(ownership[TileId(1, 1)].Status, Is.EqualTo(TileOwnershipStatus.Unclaimed));
            Assert.That(ownership[TileId(1, 1)].ClaimedByPlayerIds, Is.Empty);
        });
    }

    [Test, Description("A tile nobody holds is still described, so 'nobody holds this' is distinguishable from an absent answer.")]
    public async Task ShouldDescribeATileNobodyHolds()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync(TileId(3, 2));

        Assert.Multiple(() =>
        {
            Assert.That(ownership.ContainsKey(TileId(3, 2)), Is.True);
            Assert.That(ownership[TileId(3, 2)].Status, Is.EqualTo(TileOwnershipStatus.Unclaimed));
        });
    }

    [Test, Description("Only the tiles asked about come back, which is what lets the map read ask about the ground an Observer can see and nothing else.")]
    public async Task ShouldDescribeOnlyTheTilesAskedAbout()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync(TileId(0, 0), TileId(1, 0));

        Assert.That(ownership.Keys, Is.EquivalentTo(new[] { TileId(0, 0), TileId(1, 0) }));
    }

    [Test, Description("A tile id belonging to another Game is not described, so one Game's ownership can never be read through another's map read.")]
    public async Task ShouldNotDescribeATileFromAnotherGame()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync(_OTHER_MAP_OFFSET);

        Assert.That(ownership, Is.Empty);
    }

    [Test, Description("An empty request is answered without touching the database, since there is nothing to describe.")]
    public async Task ShouldReturnNothingForAnEmptyRequest()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync();

        Assert.That(ownership, Is.Empty);
    }

    [Test, Description("A tile with no claims at all comes back with an empty claimant list rather than a null one, so no caller has to null-check the usual case.")]
    public async Task ShouldReportAnEmptyClaimantListForUnclaimedGround()
    {
        IReadOnlyDictionary<int, TileOwnership> ownership = await ReadAsync(TileId(0, 0), TileId(3, 2));

        Assert.Multiple(() =>
        {
            Assert.That(ownership[TileId(0, 0)].ClaimedByPlayerIds, Is.Empty);
            Assert.That(ownership[TileId(3, 2)].ClaimedByPlayerIds, Is.Empty);
        });
    }
}
