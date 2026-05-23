using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Maps.MapGallery.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;

using NSubstitute;

namespace AscendantEmpire.Tests;

public class GameSqlServerTestBase : SqlServerTestBase
{
    internal int _mapWidth = 2;
    internal int _mapHeight = 2;
    internal int _numberOfPlayers = 2;

    internal ICampaignMapGalleryService _mapGalleryService = null!;
    internal GameSetupService _service = null!;
    internal int _galleryIndexId;
    internal CampaignMapGalleryProperties _completedMapProperties = null!;
    internal CapturingLogger<GameSetupService> _capturingLogger = null!;

    public async Task SeedGameAsync(int mapWidth,
        int mapHeight,
        int numberOfPlayers)
    {
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _numberOfPlayers = numberOfPlayers;

        _galleryIndexId = await SeedMinimumGalleryDataAsync(mapWidth, mapHeight);

        _completedMapProperties = new CampaignMapGalleryProperties
        {
            Id = _galleryIndexId,
            MapName = "Test Map",
            Width = mapWidth,
            Height = mapHeight,
            CreationStatus = MapCreationStatus.Complete,
            CampaignMapOffsetId = 0,
            HorizontalWrap = false,
            VerticalWrap = false
        };

        _mapGalleryService = Substitute.For<ICampaignMapGalleryService>();
        _mapGalleryService
            .GetCampaignMapGalleryProperties(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResultBase.ServiceResult<CampaignMapGalleryProperties>.Succeed(_completedMapProperties));

        _capturingLogger = new CapturingLogger<GameSetupService>();
        _service = new GameSetupService(
            _capturingLogger,
            DbFactory,
            _mapGalleryService);
    }

    /// <summary>
    /// Seeds the minimum rows needed by the raw SQL INSERT in <c>GenerateCampaignMapAsync</c>: a <see cref="CampaignBiome"/>, a <see cref="CampaignGeography"/>, a <see cref="CampaignMapGalleryIndex"/>, 
    /// and <c>Width × Height</c> <see cref="CampaignMapGallery"/> tile rows.
    /// </summary>
    /// <returns>The Id of the seeded <see cref="CampaignMapGalleryIndex"/>.</returns>
    public async Task<int> SeedMinimumGalleryDataAsync(int mapWidth, int mapHeight)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        if (!ctx.CampaignBiomes.Any(b => b.BiomeTypeId == BiomeType.Unknown))
        {
            ctx.CampaignBiomes.Add(new CampaignBiome
            {
                BiomeTypeId = BiomeType.Unknown,
                Name = "Unknown",
                MapEditorHtmlColor = "#000000",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.0m,
                IsPaintableOnCampaignMap = false,
                CanBeElevated = false
            });
        }

        if (!ctx.CampaignBiomes.Any(b => b.BiomeTypeId == BiomeType.Grassland))
        {
            ctx.CampaignBiomes.Add(new CampaignBiome
            {
                BiomeTypeId = BiomeType.Grassland,
                Name = "Grassland",
                MapEditorHtmlColor = "#90ee90",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.1m,
                IsPaintableOnCampaignMap = true,
                CanBeElevated = true
            });
        }

        if (!ctx.CampaignGeographies.Any(g => g.CampaignMapGeographyTypeId == CampaignMapGeographyType.Unknown))
        {
            ctx.CampaignGeographies.Add(new CampaignGeography
            {
                CampaignMapGeographyTypeId = CampaignMapGeographyType.Unknown,
                Name = "Unknown",
                RelativeElevation = 0,
                MovementCost = 1.0m,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = false
            });
        }

        if (!ctx.CampaignGeographies.Any(g => g.CampaignMapGeographyTypeId == CampaignMapGeographyType.Flat))
        {
            ctx.CampaignGeographies.Add(new CampaignGeography
            {
                CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
                Name = "Flat",
                RelativeElevation = 0,
                MovementCost = 1.0m,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = true
            });
        }

        await ctx.SaveChangesAsync();

        var galleryIndex = new CampaignMapGalleryIndex
        {
            MapName = "Integration Test Map",
            Description = "Seeded for CreateCampaignTurnQueue tests",
            Width = mapWidth,
            Height = mapHeight,
            CreationStatus = MapCreationStatus.Complete,
            CreationEngine = MapCreationEngine.BlankMap,
            Status = "Complete",
            ErrorMessage = null,
            PropertiesJson = "{}",
            CampaignMapOffsetId = 0,
            HorizontalWrap = false,
            VerticalWrap = false
        };
        ctx.CampaignMapGalleryIndex.Add(galleryIndex);
        await ctx.SaveChangesAsync();

        // Gallery tiles — referenced by the SQL INSERT in GenerateCampaignMapAsync.
        CampaignBiome biome = ctx.CampaignBiomes.First(b => b.BiomeTypeId == BiomeType.Grassland);
        CampaignGeography geo = ctx.CampaignGeographies.First(g => g.CampaignMapGeographyTypeId == CampaignMapGeographyType.Flat);

        int tileId = 1;
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                ctx.CampaignMapGallery.Add(new CampaignMapGallery
                {
                    CampaignMapGalleryId = tileId++,
                    CampaignMapGalleryIndexId = galleryIndex.Id,
                    XCoord = x,
                    YCoord = y,
                    BiomeTypeId = BiomeType.Grassland,
                    CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
                    Biome = biome,
                    Geography = geo,
                    Index = galleryIndex
                });
            }
        }

        await ctx.SaveChangesAsync();
        return galleryIndex.Id;
    }

    /// <summary>
    /// Seeds a <see cref="GameIndex"/> with <see cref="_numberOfPlayers"/> players directly, bypassing <see cref="GameSetupService"/> — used to arrange the duplicate-queue test.
    /// </summary>
    /// <returns>The Id of the seeded <see cref="GameIndex"/>.</returns>
    public async Task<int> SeedGameWithPlayersAsync(int numberOfPlayers)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        var game = new GameIndex
        {
            GameName = "Pre-seeded Game",
            CampaignMapIndexId = _galleryIndexId,
            NumberOfPlayers = numberOfPlayers,
            CreationStatus = GameCreationStatus.SetupInProgress,
            PlayStatus = GamePlayStatus.SetupInProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            HorizontalWrap = false,
            VerticalWrap = false
        };
        ctx.GameIndex.Add(game);
        await ctx.SaveChangesAsync();

        for (int i = 1; i <= numberOfPlayers; i++)
        {
            ctx.Players.Add(new Domain.Entities.Games.Player
            {
                GameId = game.Id,
                UserId = null,
                RulerTitle = "King",
                RulerName = $"Player {i}",
                FactionName = $"Faction {i}",
                Status = Domain.Enums.Games.PlayerStatus.Active
            });
        }
        await ctx.SaveChangesAsync();

        return game.Id;
    }
}
