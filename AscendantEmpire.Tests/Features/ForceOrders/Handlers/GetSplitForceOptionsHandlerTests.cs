using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.Handlers;
using AscendantEmpire.BackendContracts.ForceOrders.SplitForce;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.ForceOrders.Handlers;

/// <summary>
/// Tests for <see cref="GetSplitForceOptionsHandler"/> — the numbers the split dialog opens with.
/// </summary>
/// <remarks>
/// <para>
/// On the in-memory tier. The handler reads through <see cref="ForceActionContextBuilder"/>, which projects only scalar columns off
/// <see cref="CompiledUnitTemplate"/> and never its <c>Attack</c>/<c>Resistance</c> complex types, and the handler itself opens no transaction — so per
/// <c>docs/testing.md</c> this is the lightest tier that suffices.
/// </para>
/// </remarks>
[TestFixture]
internal class GetSplitForceOptionsHandlerTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 1;
    private const int _OTHER_PLAYER_ID = 2;
    private const int _TILE_ID = 112;
    private const int _UNLED_TEMPLATE_ID = 1;
    private const int _COMMISSIONED_TEMPLATE_ID = 2;
    private const int _SIZELESS_TEMPLATE_ID = 3;
    private const int _UNLED_TAG_ID = 9;
    private const int _COMMISSIONED_TAG_ID = 10;
    private const int _SIZELESS_TAG_ID = 11;
    private const int _UNLED_BASE_SIZE = 25;
    private const long _CAMPAIGN_MOVEMENT_POINTS = 1_000;

    private GetSplitForceOptionsHandler _handler = null!;

    [SetUp]
    public async Task SetUp()
    {
        _handler = new GetSplitForceOptionsHandler(
            NullLogger<GetSplitForceOptionsHandler>.Instance,
            new ForceActionContextBuilder(DbFactory));

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // The design doc's Settler: unled, base quantum 25.
        AddTag(context, _UNLED_TAG_ID, OrganisationTagCodes.SETTLER, "Settler", LeadershipModel.Unled, _UNLED_BASE_SIZE);

        // A commissioned organisation, which splits along tier lines instead and so has no business in this dialog.
        AddTag(context, _COMMISSIONED_TAG_ID, "infantry", "Infantry", LeadershipModel.Commissioned, unledBaseSize: null);

        // Bad catalogue data: unled, but authored with no quantum to divide along.
        AddTag(context, _SIZELESS_TAG_ID, "broken", "Broken", LeadershipModel.Unled, unledBaseSize: null);

        AddTemplate(context, _UNLED_TEMPLATE_ID, "Settler", _UNLED_TAG_ID);
        AddTemplate(context, _COMMISSIONED_TEMPLATE_ID, "Infantry", _COMMISSIONED_TAG_ID);
        AddTemplate(context, _SIZELESS_TEMPLATE_ID, "Broken", _SIZELESS_TAG_ID);

        AddTile(context);

        await context.SaveChangesAsync();
    }

    private static void AddTag(
        AscendantEmpireContext context,
        int tagId,
        string code,
        string displayName,
        LeadershipModel leadershipModel,
        int? unledBaseSize)
    {
        context.Tags.Add(new Tag
        {
            Id = tagId,
            Code = code,
            DisplayName = displayName,
            TagType = TagType.MilitaryOrganisationalGroup,
            LeadershipModel = leadershipModel,
            UnledBaseSize = unledBaseSize,
            UnledDefaultSize = unledBaseSize == null ? null : 200,
            UnledMaxSize = unledBaseSize == null ? null : 5_000
        });
    }

    /// <summary>Adds a template plus the compiled snapshot every runtime read goes through.</summary>
    private static void AddTemplate(AscendantEmpireContext context, int templateId, string displayName, int organisationTagId)
    {
        context.UnitTemplates.Add(new UnitTemplate
        {
            Id = templateId,
            DisplayName = displayName,
            MapImagePath = $"{displayName.ToLowerInvariant()}.png",
            BodyComponentId = 1
        });

        context.CompiledUnitTemplates.Add(new CompiledUnitTemplate
        {
            UnitTemplateId = templateId,
            DisplayName = displayName,
            MapImagePath = $"{displayName.ToLowerInvariant()}.png",
            BodyComponentId = 1,
            CampaignMovementPoints = _CAMPAIGN_MOVEMENT_POINTS,
            OrganisationTagId = organisationTagId,
            Capabilities = []
        });
    }

    /// <summary>
    /// Adds the single tile every Force in these tests stands on, plus the biome and geography it draws on.
    /// </summary>
    /// <remarks>
    /// A real tile is needed because the context builder projects the tile's water flag and geography off the Force's <c>CampaignMap</c> navigation.
    /// </remarks>
    private static void AddTile(AscendantEmpireContext context)
    {
        var flat = new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Name = "Flat",
            MovementCost = 1m,
            IsWater = false,
            IsTraversable = true,
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
            XCoord = 1,
            YCoord = 1,
            BiomeTypeId = grassland.BiomeTypeId,
            CampaignMapGeographyTypeId = flat.CampaignMapGeographyTypeId,
            Biome = grassland,
            Geography = flat,
            Game = null!,
            FirstVisitor = null,
            LastVisitor = null,
            OwnedBy = null
        });
    }

    /// <summary>Adds a Force, defaulting to the player's own 100-strong unled settler.</summary>
    private static async Task AddForceAsync(
        AscendantEmpireContext context,
        int unitInstanceId,
        int playerId = _PLAYER_ID,
        int unitTemplateId = _UNLED_TEMPLATE_ID,
        int healthyCount = 90,
        int injuredCount = 10,
        string? name = null)
    {
        context.UnitInstances.Add(new UnitInstance
        {
            Id = unitInstanceId,
            GameId = _GAME_ID,
            PlayerId = playerId,
            CampaignMapTileId = _TILE_ID,
            UnitTemplateId = unitTemplateId,
            Name = name,
            InitialStrength = healthyCount + injuredCount,
            HealthyCount = healthyCount,
            InjuredCount = injuredCount,
            CampaignMovementRemaining = _CAMPAIGN_MOVEMENT_POINTS
        });

        await context.SaveChangesAsync();
    }

    private async Task<ServiceResult<SplitForceOptions>> HandleAsync(int unitInstanceId, int playerId = _PLAYER_ID)
    {
        return await _handler.Handle(new GetSplitForceOptionsRequest(_GAME_ID, playerId, unitInstanceId), CancellationToken.None);
    }

    [Test, Description("The authored quantum is the one number the dialog cannot work without, and reading it from the tag is what makes the dialog correct for every unled tag rather than just the Settler.")]
    public async Task ShouldReportTheAuthoredBaseSizeWhenTheForceIsUnledAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5);
        }

        ServiceResult<SplitForceOptions> result = await HandleAsync(5);

        Assert.That(result.HasSucceeded, Is.True);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.BaseSize, Is.EqualTo(_UNLED_BASE_SIZE));
            Assert.That(result.SuccessValue.UnitInstanceId, Is.EqualTo(5));
            Assert.That(result.SuccessValue.DisplayName, Is.EqualTo("Settler"));
            Assert.That(result.SuccessValue.LeadershipModel, Is.EqualTo(LeadershipModel.Unled),
                "The dialog titles itself with the authored leadership model rather than assuming the one this endpoint happens to serve today.");
        }
    }

    [Test, Description("The wounded travel with their group, so they count towards the strength a split divides — reporting only the healthy would let the dialog offer a split that leaves soldiers unaccounted for.")]
    public async Task ShouldCountTheInjuredTowardsTheStrengthBeingSplitAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5, healthyCount: 90, injuredCount: 10);
        }

        ServiceResult<SplitForceOptions> result = await HandleAsync(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.TotalStrength, Is.EqualTo(100));
            Assert.That(result.SuccessValue.HealthyCount, Is.EqualTo(90));
            Assert.That(result.SuccessValue.InjuredCount, Is.EqualTo(10));
        }
    }

    [Test, Description("A Force that is not in this game cannot be reported on, and saying so as Not Found rather than an empty success stops the dialog opening on nothing.")]
    public async Task ShouldReturnNotFoundWhenTheForceIsNotInThisGameAsync()
    {
        ServiceResult<SplitForceOptions> result = await HandleAsync(unitInstanceId: 404);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("Another player's Force is indistinguishable from one that does not exist, so the caller learns nothing about what someone else is fielding.")]
    public async Task ShouldReturnNotFoundWhenTheForceBelongsToAnotherPlayerAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 6, playerId: _OTHER_PLAYER_ID);
        }

        ServiceResult<SplitForceOptions> result = await HandleAsync(6);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("A commissioned formation detaches along tier lines, which asks for a tier and a count rather than a list of sizes, so this contract cannot describe it and must refuse rather than invent a quantum.")]
    public async Task ShouldRefuseWhenTheOrganisationIsCommissionedAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 7, unitTemplateId: _COMMISSIONED_TEMPLATE_ID);
        }

        ServiceResult<SplitForceOptions> result = await HandleAsync(7);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.One.Contains("not an unled force"));
        }
    }

    [Test, Description("An unled tag with no authored quantum is bad catalogue data, reported in the same words the toolbar's own strategy uses so the two cannot appear to disagree.")]
    public async Task ShouldRefuseWhenAnUnledTagHasNoAuthoredBaseSizeAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 8, unitTemplateId: _SIZELESS_TEMPLATE_ID);
        }

        ServiceResult<SplitForceOptions> result = await HandleAsync(8);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Has.One.Contains("no authored base size"));
        }
    }
}
