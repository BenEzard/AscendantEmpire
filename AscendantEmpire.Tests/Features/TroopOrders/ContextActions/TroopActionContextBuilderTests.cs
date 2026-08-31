using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.TroopOrders.ContextActions;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Tests.Features.TroopOrders.ContextActions;

/// <summary>
/// Tests for <see cref="TroopActionContextBuilder"/> — the one part of the context-action pipeline that reads the database.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately on the in-memory tier. <see cref="CompiledUnitTemplate"/> owns EF complex types (<c>Attack</c>/<c>Resistance</c>) which the
/// in-memory provider cannot materialise, but this builder only ever projects <b>scalar</b> columns off that entity and never touches those
/// two — so, per <c>testing.md</c>'s "a query that never pulls in the owning entity is unaffected", it stays on the lighter tier. If a future
/// change makes the builder read a damage profile, these tests will start failing in the query shaper and must move to
/// <see cref="SqlServerTestBase"/>.
/// </para>
/// </remarks>
[TestFixture]
internal class TroopActionContextBuilderTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 1;
    private const int _OTHER_PLAYER_ID = 2;
    private const int _TILE_ID = 112;
    private const int _OTHER_TILE_ID = 113;
    private const int _UNLED_TEMPLATE_ID = 1;
    private const int _UNTAGGED_TEMPLATE_ID = 7;
    private const int _ORGANISATION_TAG_ID = 9;
    private const long _CAMPAIGN_MOVEMENT_POINTS = 1_000;

    private TroopActionContextBuilder _builder = null!;

    [SetUp]
    public async Task SetUp()
    {
        _builder = new TroopActionContextBuilder(DbFactory);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // The unled settler organisation from the ticket.
        context.Tags.Add(new Tag
        {
            Id = _ORGANISATION_TAG_ID,
            Code = "settler",
            DisplayName = "Settler",
            TagType = TagType.MilitaryOrganisationalGroup,
            LeadershipModel = LeadershipModel.Unled,
            UnledBaseSize = 25,
            UnledDefaultSize = 100,
            UnledMaxSize = 200
        });

        AddTemplate(context, _UNLED_TEMPLATE_ID, "Settler", _ORGANISATION_TAG_ID);
        AddTemplate(context, _UNTAGGED_TEMPLATE_ID, "Scout", organisationTagId: null);

        await context.SaveChangesAsync();
    }

    /// <summary>Adds a template plus the compiled snapshot every runtime read goes through.</summary>
    private static void AddTemplate(AscendantEmpireContext context, int templateId, string displayName, int? organisationTagId)
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
            OrganisationTagId = organisationTagId
        });
    }

    /// <summary>Adds a troop, defaulting to the player's own full-strength, unmoved settler on the shared tile.</summary>
    private static async Task AddTroopAsync(
        AscendantEmpireContext context,
        int unitInstanceId,
        int playerId = _PLAYER_ID,
        int campaignMapTileId = _TILE_ID,
        int unitTemplateId = _UNLED_TEMPLATE_ID,
        int healthyCount = 100,
        int injuredCount = 0,
        long movementPointsRemaining = _CAMPAIGN_MOVEMENT_POINTS,
        string? name = null)
    {
        context.UnitInstances.Add(new UnitInstance
        {
            Id = unitInstanceId,
            GameId = _GAME_ID,
            PlayerId = playerId,
            CampaignMapTileId = campaignMapTileId,
            UnitTemplateId = unitTemplateId,
            Name = name,
            InitialStrength = healthyCount + injuredCount,
            HealthyCount = healthyCount,
            InjuredCount = injuredCount,
            MovementPointsRemaining = movementPointsRemaining
        });

        await context.SaveChangesAsync();
    }

    [Test, Description("Everything the strategies read is flattened off the compiled snapshot and its organisation tag here, so that no EF entity "
        + "reaches the pure rule layer. If the projection stopped carrying the tag's sizing, every split rule would silently misjudge.")]
    public async Task ShouldFlattenTheTroopAndItsOrganisationTagWhenTheTroopIsLoadedAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddTroopAsync(context, unitInstanceId: 5);
        }

        TroopActionContext? result = await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, 5, CancellationToken.None);

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.SelectedTroop.UnitInstanceId, Is.EqualTo(5));
            Assert.That(result.SelectedTroop.TotalStrength, Is.EqualTo(100));
            Assert.That(result.SelectedTroop.CampaignMovementPoints, Is.EqualTo(_CAMPAIGN_MOVEMENT_POINTS),
                "The per-turn allowance should come off the compiled snapshot.");
            Assert.That(result.SelectedTroop.HasFullMovement, Is.True);
            Assert.That(result.SelectedTroop.Organisation, Is.Not.Null);
            Assert.That(result.SelectedTroop.Organisation!.IsUnled, Is.True);
            Assert.That(result.SelectedTroop.Organisation.UnledBaseSize, Is.EqualTo(25));
        }
    }

    [Test, Description("Reasons shown to the player name the troop, so an unnamed one still needs something to be called. Falling back to the compiled "
        + "template's name keeps those messages readable instead of leaving a blank where the name should be.")]
    public async Task ShouldFallBackToTheTemplateNameWhenTheTroopIsUnnamedAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddTroopAsync(context, unitInstanceId: 5, name: null);
        }

        TroopActionContext? result = await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, 5, CancellationToken.None);

        Assert.That(result!.SelectedTroop.DisplayName, Is.EqualTo("Settler"));
    }

    [Test, Description("A troop the player has named should be referred to by that name, not the template's, so the fallback must not override a name "
        + "that is actually set.")]
    public async Task ShouldUseTheTroopsOwnNameWhenItHasOneAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddTroopAsync(context, unitInstanceId: 5, name: "First Wagon Train");
        }

        TroopActionContext? result = await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, 5, CancellationToken.None);

        Assert.That(result!.SelectedTroop.DisplayName, Is.EqualTo("First Wagon Train"));
    }

    [Test, Description("Not every template carries an organisation tag, and that is legitimate rather than an error. The projection must yield a null "
        + "organisation so the strategies can hide themselves, instead of failing the whole query.")]
    public async Task ShouldLeaveOrganisationNullWhenTheTemplateHasNoOrganisationTagAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddTroopAsync(context, unitInstanceId: 6, unitTemplateId: _UNTAGGED_TEMPLATE_ID, healthyCount: 1);
        }

        TroopActionContext? result = await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, 6, CancellationToken.None);

        Assert.That(result!.SelectedTroop.Organisation, Is.Null);
    }

    [Test, Description("Actions that combine or transfer between troops need to know what else the player has standing on the tile. The selection "
        + "itself is excluded so a strategy cannot mistake it for a partner, and troops elsewhere are not gathered.")]
    public async Task ShouldGatherTheOwnersOtherTroopsWhenTheyShareTheTileAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddTroopAsync(context, unitInstanceId: 5);
            await AddTroopAsync(context, unitInstanceId: 6, unitTemplateId: _UNTAGGED_TEMPLATE_ID, healthyCount: 1);
            await AddTroopAsync(context, unitInstanceId: 7, campaignMapTileId: _OTHER_TILE_ID);
        }

        TroopActionContext? result = await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, 5, CancellationToken.None);

        Assert.That(result!.OtherTroopsOnTile.Select(t => t.UnitInstanceId), Is.EquivalentTo(new[] { 6 }),
            "Only the player's other troops on the same tile should be gathered.");
    }

    [Test, Description("Gathering enemy troops would let the toolbar confirm what is standing on a tile the player cannot actually see, leaking "
        + "information the vision rules are meant to withhold. They stay excluded until those rules are applied here.")]
    public async Task ShouldExcludeTroopsWhenTheyBelongToAnotherPlayerAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddTroopAsync(context, unitInstanceId: 5);
            await AddTroopAsync(context, unitInstanceId: 8, playerId: _OTHER_PLAYER_ID);
        }

        TroopActionContext? result = await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, 5, CancellationToken.None);

        Assert.That(result!.OtherTroopsOnTile, Is.Empty);
    }

    [Test, Description("A troop with no soldiers left is not really on the tile any more, and offering actions that involve it would let the player "
        + "act through a formation that no longer exists.")]
    public async Task ShouldExcludeTroopsWhenTheyHaveNoSoldiersLeftAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddTroopAsync(context, unitInstanceId: 5);
            await AddTroopAsync(context, unitInstanceId: 9, healthyCount: 0, injuredCount: 0);
        }

        TroopActionContext? result = await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, 5, CancellationToken.None);

        Assert.That(result!.OtherTroopsOnTile, Is.Empty);
    }

    [Test, Description("Asking about someone else's troop must not reveal that it exists, so it is reported exactly as a missing one. Any difference "
        + "in the response would let a player probe for enemy troop ids.")]
    public async Task ShouldReturnNullWhenTheSelectedTroopBelongsToAnotherPlayerAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddTroopAsync(context, unitInstanceId: 8, playerId: _OTHER_PLAYER_ID);
        }

        TroopActionContext? result = await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, 8, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test, Description("A troop id that matches nothing yields no context, which the service turns into a 404 rather than an empty toolbar.")]
    public async Task ShouldReturnNullWhenTheSelectedTroopDoesNotExistAsync()
    {
        TroopActionContext? result = await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, 999, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test, Description("The split rule turns entirely on whether any movement has been spent, so a partially spent troop must arrive with its real "
        + "remaining points and report that its turn is no longer whole.")]
    public async Task ShouldCarrySpentMovementThroughWhenTheTroopHasMovedAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddTroopAsync(context, unitInstanceId: 5, movementPointsRemaining: _CAMPAIGN_MOVEMENT_POINTS / 2);
        }

        TroopActionContext? result = await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, 5, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.SelectedTroop.MovementPointsRemaining, Is.EqualTo(500));
            Assert.That(result.SelectedTroop.HasFullMovement, Is.False);
        }
    }
}
