using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Tests.Features.ForceOrders.ContextActions;

/// <summary>
/// Tests for <see cref="ForceActionContextBuilder"/> — the one part of the context-action pipeline that reads the database.
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
internal class ForceActionContextBuilderTests : InMemoryTestBase
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

    private ForceActionContextBuilder _builder = null!;

    [SetUp]
    public async Task SetUp()
    {
        _builder = new ForceActionContextBuilder(DbFactory);

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

    /// <summary>Adds a Force, defaulting to the player's own full-strength, unmoved settler on the shared tile.</summary>
    private static async Task AddForceAsync(
        AscendantEmpireContext context,
        int unitInstanceId,
        int playerId = _PLAYER_ID,
        int campaignMapTileId = _TILE_ID,
        int unitTemplateId = _UNLED_TEMPLATE_ID,
        int healthyCount = 100,
        int injuredCount = 0,
        long campaignMovementRemaining = _CAMPAIGN_MOVEMENT_POINTS,
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
            CampaignMovementRemaining = campaignMovementRemaining
        });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Builds for a selection of one and unwraps it, since most of what this builder does is per-Force and reads better without the collection.
    /// </summary>
    private async Task<ForceActionContext?> BuildSingleAsync(int forceId)
    {
        IReadOnlyCollection<ForceActionContext>? contexts = await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, [forceId], CancellationToken.None);

        return contexts?.SingleOrDefault();
    }

    [Test, Description("The force and its organisation tag are flattened off the snapshot, so no EF entity reaches the pure rule layer.")]
    public async Task ShouldFlattenTheForceAndItsOrganisationTagWhenTheForceIsLoadedAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5);
        }

        ForceActionContext? result = await BuildSingleAsync(5);

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.SelectedForce.UnitInstanceId, Is.EqualTo(5));
            Assert.That(result.SelectedForce.TotalStrength, Is.EqualTo(100));
            Assert.That(result.SelectedForce.CampaignMovementPoints, Is.EqualTo(_CAMPAIGN_MOVEMENT_POINTS),
                "The per-turn allowance should come off the compiled snapshot.");
            Assert.That(result.SelectedForce.HasFullMovement, Is.True);
            Assert.That(result.SelectedForce.Organisation, Is.Not.Null);
            Assert.That(result.SelectedForce.Organisation!.IsUnled, Is.True);
            Assert.That(result.SelectedForce.Organisation.UnledBaseSize, Is.EqualTo(25));
        }
    }

    [Test, Description("An unnamed force falls back to its template's name, so player-facing reasons never show a blank.")]
    public async Task ShouldFallBackToTheTemplateNameWhenTheForceIsUnnamedAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5, name: null);
        }

        ForceActionContext? result = await BuildSingleAsync(5);

        Assert.That(result!.SelectedForce.DisplayName, Is.EqualTo("Settler"));
    }

    [Test, Description("The template-name fallback must not override a name the player has actually set.")]
    public async Task ShouldUseTheForcesOwnNameWhenItHasOneAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5, name: "First Wagon Train");
        }

        ForceActionContext? result = await BuildSingleAsync(5);

        Assert.That(result!.SelectedForce.DisplayName, Is.EqualTo("First Wagon Train"));
    }

    [Test, Description("A template with no organisation tag yields a null organisation, so strategies hide rather than the query failing.")]
    public async Task ShouldLeaveOrganisationNullWhenTheTemplateHasNoOrganisationTagAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 6, unitTemplateId: _UNTAGGED_TEMPLATE_ID, healthyCount: 1);
        }

        ForceActionContext? result = await BuildSingleAsync(6);

        Assert.That(result!.SelectedForce.Organisation, Is.Null);
    }

    [Test, Description("Friendly forces on the tile are gathered, excluding the selection itself so it cannot be mistaken for a partner.")]
    public async Task ShouldGatherTheOwnersOtherForcesWhenTheyShareTheTileAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5);
            await AddForceAsync(context, unitInstanceId: 6, unitTemplateId: _UNTAGGED_TEMPLATE_ID, healthyCount: 1);
            await AddForceAsync(context, unitInstanceId: 7, campaignMapTileId: _OTHER_TILE_ID);
        }

        ForceActionContext? result = await BuildSingleAsync(5);

        Assert.That(result!.OtherForcesOnTile.Select(t => t.UnitInstanceId), Is.EquivalentTo(new[] { 6 }),
            "Only the player's other forces on the same tile should be gathered.");
    }

    [Test, Description("Enemy forces stay excluded, since gathering them would leak what the vision rules are meant to withhold.")]
    public async Task ShouldExcludeForcesWhenTheyBelongToAnotherPlayerAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5);
            await AddForceAsync(context, unitInstanceId: 8, playerId: _OTHER_PLAYER_ID);
        }

        ForceActionContext? result = await BuildSingleAsync(5);

        Assert.That(result!.OtherForcesOnTile, Is.Empty);
    }

    [Test, Description("A force with no soldiers left is excluded, so the player cannot act through a formation that no longer exists.")]
    public async Task ShouldExcludeForcesWhenTheyHaveNoSoldiersLeftAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5);
            await AddForceAsync(context, unitInstanceId: 9, healthyCount: 0, injuredCount: 0);
        }

        ForceActionContext? result = await BuildSingleAsync(5);

        Assert.That(result!.OtherForcesOnTile, Is.Empty);
    }

    [Test, Description("Another player's force reports exactly as a missing one, or the response would let ids be probed for.")]
    public async Task ShouldReturnNullWhenTheSelectedForceBelongsToAnotherPlayerAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 8, playerId: _OTHER_PLAYER_ID);
        }

        ForceActionContext? result = await BuildSingleAsync(8);

        Assert.That(result, Is.Null);
    }

    [Test, Description("A force id that matches nothing yields no context, which the service turns into a 404 rather than an empty toolbar.")]
    public async Task ShouldReturnNullWhenTheSelectedForceDoesNotExistAsync()
    {
        ForceActionContext? result = await BuildSingleAsync(999);

        Assert.That(result, Is.Null);
    }

    [Test, Description("The split rule turns on spent movement, so a partly spent force reports its real remaining points.")]
    public async Task ShouldCarrySpentMovementThroughWhenTheForceHasMovedAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5, campaignMovementRemaining: _CAMPAIGN_MOVEMENT_POINTS / 2);
        }

        ForceActionContext? result = await BuildSingleAsync(5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.SelectedForce.CampaignMovementRemaining, Is.EqualTo(500));
            Assert.That(result.SelectedForce.HasFullMovement, Is.False);
        }
    }

    [Test, Description("A selection yields one context per force, ordered by id so the disabled reasons cannot reshuffle between requests.")]
    public async Task ShouldBuildOneContextPerForceInIdOrderWhenSeveralAreSelectedAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5);
            await AddForceAsync(context, unitInstanceId: 6);
            await AddForceAsync(context, unitInstanceId: 7);
        }

        // Deliberately passed out of order: the ordering must come from the builder, not from however the client listed the ids.
        IReadOnlyCollection<ForceActionContext>? result =
            await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, [7, 5, 6], CancellationToken.None);

        Assert.That(result!.Select(c => c.SelectedForce.UnitInstanceId), Is.EqualTo(new[] { 5, 6, 7 }));
    }

    [Test, Description("Each selected force sees its own tile-mates, including the other selected forces standing beside it.")]
    public async Task ShouldGiveEachSelectedForceItsOwnTileMatesWhenSeveralAreSelectedAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5);
            await AddForceAsync(context, unitInstanceId: 6);
            await AddForceAsync(context, unitInstanceId: 7, campaignMapTileId: _OTHER_TILE_ID);
        }

        List<ForceActionContext> result = [.. (await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, [5, 6], CancellationToken.None))!];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result[0].OtherForcesOnTile.Select(f => f.UnitInstanceId), Is.EquivalentTo(new[] { 6 }),
                "Being selected alongside a force does not remove it from the tile.");
            Assert.That(result[1].OtherForcesOnTile.Select(f => f.UnitInstanceId), Is.EquivalentTo(new[] { 5 }));
        }
    }

    [Test, Description("A selection spanning tiles still resolves, so nothing here has to change if cross-tile selection is ever allowed.")]
    public async Task ShouldResolveEachForcesTileSeparatelyWhenTheSelectionSpansTilesAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5);
            await AddForceAsync(context, unitInstanceId: 6);
            await AddForceAsync(context, unitInstanceId: 7, campaignMapTileId: _OTHER_TILE_ID);
        }

        List<ForceActionContext> result = [.. (await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, [5, 7], CancellationToken.None))!];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result[0].OtherForcesOnTile.Select(f => f.UnitInstanceId), Is.EquivalentTo(new[] { 6 }));
            Assert.That(result[1].OtherForcesOnTile, Is.Empty, "The force alone on its tile should see no tile-mates.");
        }
    }

    [Test, Description("One bad id fails the whole selection, so the toolbar never judges fewer forces than the player has selected.")]
    public async Task ShouldReturnNullWhenOnlyOneForceOfTheSelectionCannotBeLoadedAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5);
            await AddForceAsync(context, unitInstanceId: 8, playerId: _OTHER_PLAYER_ID);
        }

        IReadOnlyCollection<ForceActionContext>? missing =
            await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, [5, 999], CancellationToken.None);

        IReadOnlyCollection<ForceActionContext>? foreign =
            await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, [5, 8], CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(missing, Is.Null, "A force id that matches nothing should fail the selection.");
            Assert.That(foreign, Is.Null, "Another player's force should fail the selection just as a missing one does.");
        }
    }

    [Test, Description("The same force listed twice is one force, so a repeated id does not read as an incomplete selection.")]
    public async Task ShouldTreatARepeatedForceIdAsOneForceWhenTheSelectionContainsDuplicatesAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            await AddForceAsync(context, unitInstanceId: 5);
        }

        IReadOnlyCollection<ForceActionContext>? result =
            await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, [5, 5], CancellationToken.None);

        Assert.That(result!.Select(c => c.SelectedForce.UnitInstanceId), Is.EqualTo(new[] { 5 }));
    }

    [Test, Description("An empty selection yields no contexts, which the service reports as not-found rather than an empty toolbar.")]
    public async Task ShouldReturnNullWhenNothingIsSelectedAsync()
    {
        IReadOnlyCollection<ForceActionContext>? result = await _builder.BuildAsync(_GAME_ID, _PLAYER_ID, [], CancellationToken.None);

        Assert.That(result, Is.Null);
    }
}
