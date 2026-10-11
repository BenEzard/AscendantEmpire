using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Maps.MapGallery.Services;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Turns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.Application.Features.Technologies.Services;

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

        // Game setup now spawns each player's starting units, so every full SetupGameAsync run needs the
        // starter catalogue (org tags, components, templates + compiled snapshots) and one start position per
        // player on the gallery map.
        await SeedStarterCatalogueAsync();
        await SeedStarterAssetsAsync();
        await SeedTechnologyCatalogueAsync();
        await SeedStartPositionsAsync(_galleryIndexId, numberOfPlayers, mapWidth, mapHeight);

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

        // The real writer over the real turn service. Setup places its starting Forces with no Turn, so the turn service is never actually asked for a sequence here; it is real rather than
        // substituted so that fixtures building on this base, which do open Turns, get the genuine allocator.
        var forceLifecycleWriter = new ForceLifecycleWriter(
            NullLogger<ForceLifecycleWriter>.Instance,
            new PlayerTurnService(NullLogger<PlayerTurnService>.Instance, DbFactory));

        // The real generators and reconcile, for the same reason the force writer is real: fixtures building on this base assert on the technology rows setup writes.
        ITechnologyRequirementGenerator[] requirementGenerators =
        [
            new IdenticalTechnologyRequirementGenerator(),
            new UnimplementedTechnologyRequirementGenerator(TechnologyCustomisation.RandomisedEqual),
            new UnimplementedTechnologyRequirementGenerator(TechnologyCustomisation.FullyRandomised)
        ];

        _service = new GameSetupService(
            _capturingLogger,
            DbFactory,
            _mapGalleryService,
            forceLifecycleWriter,
            requirementGenerators,
            new PlayerTechnologyAvailabilityService(NullLogger<PlayerTechnologyAvailabilityService>.Instance),
            TestGameSettings.Create());
    }

    /// <summary>
    /// Seeds the asset catalogue, which game setup unlocks a starter subset of for every Player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs the real seed data through the real upsert, rather than hand-writing the handful of rows the starter codes name. That way the catalogue a test runs
    /// against is the catalogue production gets, and <c>GameSetupService.StarterAssetCodes</c> cannot drift from it without failing here.
    /// </para>
    /// <para>
    /// Idempotent, because the upsert matches on code - a fixture that seeds twice updates rather than duplicating.
    /// </para>
    /// </remarks>
    public async Task SeedStarterAssetsAsync()
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        var handler = new UpsertAssetsHandler(NullLogger<UpsertAssetsHandler>.Instance, ctx);

        ServiceResultBase result = await handler.Handle(new UpsertAssetsRequest(AssetSeedData.Build()), CancellationToken.None);

        if (result.HasSucceeded == false)
        {
            throw new InvalidOperationException($"Seeding the asset catalogue for the test failed: {string.Join("; ", result.Errors)}");
        }
    }

    /// <summary>
    /// Seeds the experience type and technology catalogues, which game setup generates every Player's requirements from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs the real seed data through the real upserts, for the same reason the asset catalogue does: the catalogue a test runs against is the catalogue
    /// production gets, so <c>GameSetupService.StarterTechnologyCodes</c> cannot drift from it without failing here.
    /// </para>
    /// <para>
    /// Experience types first - the technology seed data names them by code and the upsert rejects a code it cannot resolve.
    /// </para>
    /// <para>
    /// Idempotent, because both upserts match on code.
    /// </para>
    /// </remarks>
    public async Task SeedTechnologyCatalogueAsync()
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        var experienceTypeHandler = new UpsertExperienceTypesHandler(NullLogger<UpsertExperienceTypesHandler>.Instance, ctx);

        ServiceResultBase experienceTypeResult = await experienceTypeHandler.Handle(
            new UpsertExperienceTypesRequest(ExperienceTypeSeedData.Build()),
            CancellationToken.None);

        if (experienceTypeResult.HasSucceeded == false)
        {
            throw new InvalidOperationException(
                $"Seeding the experience type catalogue for the test failed: {string.Join("; ", experienceTypeResult.Errors)}");
        }

        var technologyHandler = new UpsertTechnologiesHandler(NullLogger<UpsertTechnologiesHandler>.Instance, ctx);

        ServiceResultBase technologyResult = await technologyHandler.Handle(
            new UpsertTechnologiesRequest(TechnologySeedData.Build()),
            CancellationToken.None);

        if (technologyResult.HasSucceeded == false)
        {
            throw new InvalidOperationException(
                $"Seeding the technology catalogue for the test failed: {string.Join("; ", technologyResult.Errors)}");
        }
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
                RegenerationPercent = 0.1m,
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
                RegenerationPercent = 0.1m,
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
    /// Seeds one <see cref="CampaignMapStartPosition"/> per player on the gallery map, walking the grid so
    /// every position lands on a distinct in-bounds tile.
    /// </summary>
    public async Task SeedStartPositionsAsync(int galleryIndexId, int numberOfPlayers, int mapWidth, int mapHeight)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        for (int positionIndex = 0; positionIndex < numberOfPlayers; positionIndex++)
        {
            ctx.CampaignMapStartPositions.Add(new CampaignMapStartPosition
            {
                CampaignMapGalleryIndexId = galleryIndexId,
                XCoord = positionIndex % mapWidth,
                YCoord = positionIndex / mapWidth % mapHeight,
                DisplayName = $"Start {positionIndex + 1}",
                LocationScore = numberOfPlayers - positionIndex,
            });
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the starter catalogue game setup depends on: the unled Settler/Scout organisation tags, a human
    /// body plus the camping-goods / scout-training components carrying those tags, and the SETTLER / SCOUT
    /// global templates with hand-built compiled snapshots (skipping the compile pipeline keeps the base
    /// light — snapshot correctness is covered by the compilation tests).
    /// </summary>
    /// <remarks>
    /// This is test arrangement, not application behaviour: the app never creates this content — a real
    /// catalogue is hand-authored, and game setup fails without it. These rows stand in for that catalogue so
    /// <c>SetupGameAsync</c> has something to read.
    /// </remarks>
    public async Task SeedStarterCatalogueAsync()
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        // The design doc's worked sizes: settlers spawn at 200 by default, scouts are individuals.
        var settlerTag = new Tag
        {
            Code = "settler",
            DisplayName = "Settler",
            TagType = TagType.MilitaryOrganisationalGroup,
            MovementDomain = DomainType.Land,
            LeadershipModel = LeadershipModel.Unled,
            UnledBaseSize = 25,
            UnledDefaultSize = 200,
            UnledMaxSize = 5000,
            SystemCreated = true,
        };

        var scoutTag = new Tag
        {
            Code = "scout",
            DisplayName = "Scout",
            TagType = TagType.MilitaryOrganisationalGroup,
            MovementDomain = DomainType.Land,
            LeadershipModel = LeadershipModel.Unled,
            UnledBaseSize = 1,
            UnledDefaultSize = 1,
            UnledMaxSize = 1,
            SystemCreated = true,
        };

        var humanBody = new UnitComponent
        {
            Code = "human-body",
            DisplayName = "Human",
            ComponentType = ComponentType.Body,
            Weight = 0,
            // Sight mirrors Detection here so the hand-built snapshots below stay consistent with what a real
            // compile of these components would produce.
            Sight = 1000,
            Detection = 1000,
            Body = new UnitComponentBody
            {
                MovementDomain = DomainType.Land,
                CampaignMovementPoints = 1000,
                BattlefieldMovementPoints = 2000,
                Power = 1000,
                Hitpoints = 1000,
            },
        };

        var campingGoods = new UnitComponent
        {
            Code = "camping-goods",
            DisplayName = "Camping Goods",
            ComponentType = ComponentType.Equipment,
            Weight = 2000,
            Tags = [new UnitComponentTag { Tag = settlerTag }],
        };

        var scoutTraining = new UnitComponent
        {
            Code = "scout-training",
            DisplayName = "Scout Training",
            ComponentType = ComponentType.Training,
            Weight = 0,
            Sight = 2000,
            Detection = 2000,
            Tags = [new UnitComponentTag { Tag = scoutTag }],
        };

        ctx.UnitComponents.AddRange(humanBody, campingGoods, scoutTraining);
        await ctx.SaveChangesAsync();

        // Global templates (no game/player) with the codes GameSetupService.StarterTemplateCodes expects.
        // The fitted components are attached directly (no slots involved) — assembly rules are out of scope here.
        var settlerTemplate = new UnitTemplate
        {
            Code = "SETTLER",
            DisplayName = "Settler",
            MapImagePath = "assets/units/test-settler.png",
            BodyComponentId = humanBody.Id,
        };

        var scoutTemplate = new UnitTemplate
        {
            Code = "SCOUT",
            DisplayName = "Scout",
            MapImagePath = "assets/units/test-scout.png",
            BodyComponentId = humanBody.Id,
        };

        ctx.UnitTemplates.AddRange(settlerTemplate, scoutTemplate);
        await ctx.SaveChangesAsync();

        // Hand-built snapshots carrying the resolved organisation tag, which is all game setup reads.
        ctx.CompiledUnitTemplates.AddRange(
            new CompiledUnitTemplate
            {
                UnitTemplateId = settlerTemplate.Id,
                DisplayName = "Settler",
                MapImagePath = "assets/units/test-settler.png",
                BodyComponentId = humanBody.Id,
                MovementDomain = DomainType.Land,
                OrganisationTagId = settlerTag.Id,
                CampaignMovementPoints = 1000,
                BattlefieldMovementPoints = 2000,
                Power = 1000,
                Hitpoints = 1000,
                TotalWeight = 2000,
                // The campaign map turns TotalSight into whole tiles of sight range, so 1,000 gives the Settler a
                // radius of 1. TotalDetection no longer affects visibility and is kept only as its own stat.
                TotalSight = 1000,
                TotalDetection = 1000,
            },
            new CompiledUnitTemplate
            {
                UnitTemplateId = scoutTemplate.Id,
                DisplayName = "Scout",
                MapImagePath = "assets/units/test-scout.png",
                BodyComponentId = humanBody.Id,
                MovementDomain = DomainType.Land,
                OrganisationTagId = scoutTag.Id,
                CampaignMovementPoints = 1000,
                BattlefieldMovementPoints = 2000,
                Power = 1000,
                Hitpoints = 1000,
                TotalWeight = 0,
                // 3,000 gives the Scout a radius of 3 — the wider view the visibility tests rely on.
                TotalSight = 3000,
                TotalDetection = 3000,
            });

        await ctx.SaveChangesAsync();
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
            VerticalWrap = false,
            GenerationSeed = 0,
            BattleMapSize = 50,
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
                PeopleName = "People",
                Demonym = "Person",
                CultureName = "Culture",
                Language = "Language",
                RulerPortraitPath = "/portraits/ruler_01.png",
                Status = Domain.Enums.Games.PlayerStatus.Active
            });
        }
        await ctx.SaveChangesAsync();

        return game.Id;
    }

    /// <summary>
    /// Moves a Game that has finished setup into play, standing in for the slot claim that would normally start it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GameSetupService.SetupGameAsync"/> leaves a Game on <see cref="GamePlayStatus.AwaitingPlayers"/>: setup builds the Game,
    /// but play only begins when the last Player slot is claimed. Anything that happens during a Game - spawning a unit, reading a Player's
    /// view of the map, ending a Turn - looks the Game up through a filter that requires it to be in progress, so a fixture that sets a Game
    /// up and then plays with it has to start it too.
    /// </para>
    /// <para>
    /// Use this where the Game starting is scaffolding rather than the thing under test. Where it <i>is</i> the thing under test, claim the
    /// slots for real instead - see <c>StartOfGameTests</c>.
    /// </para>
    /// </remarks>
    public async Task StartGameAsync(int gameId)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        await ctx.GameIndex
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(g => g.PlayStatus, GamePlayStatus.InProgress));
    }

    /// <summary>
    /// Opens the first Player's Turn in a Game's campaign lane, on the Round its Queue is currently on, and returns the new Turn's id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="StartGameAsync"/> only changes the play status, so a Game started through it has a Turn Queue but no Player Turn. Play never reaches that state - a Game goes in progress in
    /// the same transaction that opens its first Turn - and anything that stamps its writes with a Turn, such as the Player's campaign map read, refuses a Game without one.
    /// </para>
    /// <para>
    /// Kept apart from <see cref="StartGameAsync"/> rather than folded into it, because some fixtures rely on there being no open Turn: with none, a spawn places its Unit without writing any
    /// movement history.
    /// </para>
    /// </remarks>
    public async Task<int> OpenFirstPlayerTurnAsync(int gameId)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        // The Game's own campaign lane rather than a battle's, and the Round game setup left it on, so the Turn agrees with its Queue.
        var campaignQueue = await ctx.TurnQueues
            .Where(q => q.GameId == gameId && q.QueueType == TurnQueueType.Campaign && q.BattleId == null)
            .Select(q => new { q.Id, q.CurrentRoundNumber })
            .SingleAsync();

        int firstPlayerId = await ctx.Players.Where(p => p.GameId == gameId).OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();

        // The real service, so the row is exactly the one play would open.
        var playerTurnService = new PlayerTurnService(NullLogger<PlayerTurnService>.Instance, DbFactory);

        ServiceResultBase.ServiceResult<int> result = await playerTurnService.OpenPlayerTurnAsync(ctx, campaignQueue.Id, firstPlayerId, campaignQueue.CurrentRoundNumber);

        // Failing here names the real problem, rather than leaving a later read to fail with a refusal about a missing Turn.
        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue;
    }
}
