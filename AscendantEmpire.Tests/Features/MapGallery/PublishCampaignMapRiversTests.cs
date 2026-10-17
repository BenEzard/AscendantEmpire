using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.MapGallery;

/// <summary>
/// Rivers reaching games already in progress when a gallery map is pushed out.
/// </summary>
/// <remarks>
/// <para>
/// Containerised tier: the publish handler bulk-copies into a temp table and updates through raw SQL inside an explicit
/// transaction, none of which the in-memory provider can reproduce — see <c>docs/testing.md</c>.
/// </para>
/// <para>
/// Without this, a river drawn in the editor would show on the gallery map and be invisible to every game already running on it,
/// which is the one way rivers could silently disagree between the map and the Forces walking over it.
/// </para>
/// </remarks>
internal sealed class PublishCampaignMapRiversTests : SqlServerTestBase
{
    [Test, Description("Pushing a map out copies its river columns onto the game's own map, alongside biome and geography.")]
    public async Task PublishCarriesRiversIntoAnInProgressGameAsync()
    {
        int galleryIndexId = await RiverMapSeed.SeedAsync(DbFactory);
        int gameId = await SeedInProgressGameAsync(galleryIndexId, riversOnTheGameMap: false);

        // Drawn on the gallery map after the game started, which is exactly the case the push-out is for.
        await SetGalleryRiversAsync(galleryIndexId, x: 0, y: 1, south: RiverWidth.None, east: RiverWidth.Large);

        await PublishAsync(galleryIndexId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        CampaignMap gameTile = await verify.CampaignMaps.AsNoTracking()
            .SingleAsync(t => t.GameId == gameId && t.XCoord == 0 && t.YCoord == 1);

        Assert.That(gameTile.EastRiver, Is.EqualTo(RiverWidth.Large));
    }

    [Test, Description("A river erased on the gallery map is erased in the game too, so a push-out removes as well as adds.")]
    public async Task PublishRemovesARiverTheAuthorErasedAsync()
    {
        int galleryIndexId = await RiverMapSeed.SeedAsync(DbFactory);
        int gameId = await SeedInProgressGameAsync(galleryIndexId, riversOnTheGameMap: true);

        // The gallery map has no rivers on it at all, so the game's copy should end up with none either.
        await PublishAsync(galleryIndexId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<CampaignMap> gameTiles = await verify.CampaignMaps.AsNoTracking()
            .Where(t => t.GameId == gameId)
            .ToListAsync();

        Assert.That(gameTiles.All(t => t.SouthRiver == RiverWidth.None && t.EastRiver == RiverWidth.None), Is.True);
    }

    // --- Helpers ----------------------------------------------------------------------------------

    private async Task PublishAsync(int galleryIndexId)
    {
        PublishCampaignMapUpdatesHandler handler = new(new NullLogger<PublishCampaignMapUpdatesHandler>(), DbFactory);

        ServiceResultBase result = await handler.Handle(new PublishCampaignMapUpdatesRequest(galleryIndexId), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    private async Task SetGalleryRiversAsync(int galleryIndexId, int x, int y, RiverWidth south, RiverWidth east)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Domain.Entities.Maps.CampaignMaps.CampaignMapGallery tile = await context.CampaignMapGallery
            .SingleAsync(t => t.CampaignMapGalleryIndexId == galleryIndexId && t.XCoord == x && t.YCoord == y);

        tile.SouthRiver = south;
        tile.EastRiver = east;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a game in progress on the gallery map, with its own copy of every tile.
    /// </summary>
    /// <remarks>
    /// Built by hand rather than through <c>GameSetupService</c>, because what is under test is one UPDATE against
    /// <c>game.CampaignMap</c> and a full setup would bring players, Forces and the starter catalogue with it.
    /// </remarks>
    /// <param name="riversOnTheGameMap">Whether the game's tiles start out carrying rivers the gallery map does not.</param>
    private async Task<int> SeedInProgressGameAsync(int galleryIndexId, bool riversOnTheGameMap)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // The game's own block of tile ids. Game setup allocates one per game and numbers tiles from it in row-major order, and the
        // column is ValueGeneratedNever, so the ids have to be chosen here exactly as setup would choose them.
        const int campaignMapOffsetId = 10_000;

        GameIndex game = new()
        {
            GameName = "River Publish Game",
            CampaignMapIndexId = galleryIndexId,
            NumberOfPlayers = 1,
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            CampaignMapOffsetId = campaignMapOffsetId,
            MapWidth = RiverMapSeed.MAP_WIDTH,
            MapHeight = RiverMapSeed.MAP_HEIGHT,
            HorizontalWrap = false,
            VerticalWrap = false,
            GenerationSeed = 0,
            BattleMapSize = 50
        };

        context.GameIndex.Add(game);
        await context.SaveChangesAsync();

        for (int y = 0; y < RiverMapSeed.MAP_HEIGHT; y++)
        {
            for (int x = 0; x < RiverMapSeed.MAP_WIDTH; x++)
            {
                context.CampaignMaps.Add(new CampaignMap
                {
                    CampaignMapId = campaignMapOffsetId + x + (y * RiverMapSeed.MAP_WIDTH),
                    GameId = game.Id,
                    XCoord = x,
                    YCoord = y,
                    BiomeTypeId = y == 0 ? BiomeType.Ocean : BiomeType.Grassland,
                    CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
                    SouthRiver = riversOnTheGameMap ? RiverWidth.Medium : RiverWidth.None,
                    EastRiver = RiverWidth.None,
                    // FK scalars above drive the relationships; the required navigations are not needed for insert.
                    Biome = null!,
                    Geography = null!,
                    Game = null!,
                    FirstVisitor = null,
                    LastVisitor = null,
                    OwnedBy = null
                });
            }
        }

        await context.SaveChangesAsync();

        return game.Id;
    }
}
