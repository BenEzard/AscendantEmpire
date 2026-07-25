using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Military;
using AscendantEmpire.Application.Features.Military.Handlers;
using AscendantEmpire.BackendContracts.Military.GetOfficerRanks;
using AscendantEmpire.BackendContracts.Military.GetOrganisationStructure;
using AscendantEmpire.BackendContracts.Military.GetOrganisationTags;
using AscendantEmpire.BackendContracts.Military.SaveOfficerRanks;
using AscendantEmpire.BackendContracts.Military.SaveOrganisationStructure;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Military;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.Military;

/// <summary>
/// Integration tests for the per-player military organisation handlers: officer rank ladders and
/// organisation structures.
/// </summary>
internal class MilitaryHandlersTests : SqlServerTestBase
{
    private int _playerId;

    /// <summary>Seeds a game and a single player, capturing the player id in <see cref="_playerId"/>.</summary>
    private async Task SeedGameAndPlayerAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var game = new GameIndex
        {
            GameName = "Test Game",
            CampaignMapIndexId = 0,
            NumberOfPlayers = 1,
            CreationStatus = GameCreationStatus.SetupInProgress,
            PlayStatus = GamePlayStatus.SetupInProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 0,
            BattleMapSize = 50,
        };
        context.GameIndex.Add(game);
        await context.SaveChangesAsync();

        var player = new Player
        {
            GameId = game.Id,
            RulerTitle = "King",
            RulerName = "Tester",
            FactionName = "Testers",
            Status = PlayerStatus.Active,
        };
        context.Players.Add(player);
        await context.SaveChangesAsync();

        _playerId = player.Id;
    }

    /// <summary>Adds a tag of the given type and returns its id.</summary>
    private async Task<int> SeedTagAsync(string code, string displayName, TagType tagType)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var tag = new Tag { Code = code, DisplayName = displayName, TagType = tagType };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();
        return tag.Id;
    }

    /// <summary>Adds a MilitaryOrganisationalGroup tag with a movement domain and returns its id.</summary>
    private async Task<int> SeedOrgTagWithDomainAsync(string code, string displayName, DomainType domain)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var tag = new Tag
        {
            Code = code,
            DisplayName = displayName,
            TagType = TagType.MilitaryOrganisationalGroup,
            MovementDomain = domain,
        };
        context.Tags.Add(tag);
        await context.SaveChangesAsync();
        return tag.Id;
    }

    /// <summary>Persists a reduced rank ladder for the player in a domain: <paramref name="inUseCount"/> levels, all in use.</summary>
    private async Task SeedPlayerRanksAsync(DomainType domain, int inUseCount)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        for (int level = 1; level <= inUseCount; level++)
        {
            context.PlayerOfficerRanks.Add(new PlayerOfficerRank
            {
                PlayerId = _playerId,
                MovementDomain = domain,
                Level = level,
                Title = $"Rank {level}",
                MaxSoldiers = 100 * level,
                MaxAttachedSpecialists = level,
                InUse = true,
            });
        }
        await context.SaveChangesAsync();
    }

    [Test, Description("With no persisted ranks, the code defaults are returned for every seeded domain.")]
    public async Task GetOfficerRanks_ReturnsDefaults_WhenNoneSaved()
    {
        await SeedGameAndPlayerAsync();

        var handler = new GetOfficerRanksHandler(new NullLogger<GetOfficerRanksHandler>(), DbFactory);
        ServiceResult<GetOfficerRanksResult> result = await handler.Handle(new GetOfficerRanksRequest(_playerId), CancellationToken.None);

        int expected = MilitaryOrganisationDefaults.SeededDomains.Count * Domain.Entities.Military.PlayerOfficerRank.MAX_LEVEL;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success));
            Assert.That(result.SuccessValue!.Ranks, Has.Count.EqualTo(expected));
            // The top rank (level 6) has no soldier ceiling.
            Assert.That(result.SuccessValue!.Ranks.Where(r => r.Level == 6), Has.All.Matches<OfficerRankView>(r => r.MaxSoldiers == null));
        }
    }

    [Test, Description("Saved ranks round-trip: a bulk save is read back with the same values.")]
    public async Task SaveThenGetOfficerRanks_Roundtrips()
    {
        await SeedGameAndPlayerAsync();

        List<SaveOfficerRankDto> ranks =
        [
            new() { MovementDomain = DomainType.Land, Level = 1, Title = "Sergeant", MaxSoldiers = 30, MaxAttachedSpecialists = 1, InUse = true },
            new() { MovementDomain = DomainType.Land, Level = 2, Title = "Commander", MaxSoldiers = null, MaxAttachedSpecialists = 4, InUse = true },
        ];

        await using (AscendantEmpireContext saveContext = await DbFactory.CreateDbContextAsync())
        {
            var saveHandler = new SaveOfficerRanksHandler(new NullLogger<SaveOfficerRanksHandler>(), saveContext);
            ServiceResultBase saveResult = await saveHandler.Handle(new SaveOfficerRanksRequest(_playerId, ranks), CancellationToken.None);
            Assert.That(saveResult.StatusCode, Is.EqualTo(ServiceResultStatus.SuccessNoContent));
        }

        var getHandler = new GetOfficerRanksHandler(new NullLogger<GetOfficerRanksHandler>(), DbFactory);
        ServiceResult<GetOfficerRanksResult> getResult = await getHandler.Handle(new GetOfficerRanksRequest(_playerId), CancellationToken.None);

        OfficerRankView? landLevel1 = getResult.SuccessValue!.Ranks.SingleOrDefault(r => r.MovementDomain == DomainType.Land && r.Level == 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(landLevel1, Is.Not.Null);
            Assert.That(landLevel1!.Title, Is.EqualTo("Sergeant"));
            Assert.That(landLevel1.MaxSoldiers, Is.EqualTo(30));
            // Land now has only the two saved levels; the other seeded domains fall back to defaults.
            Assert.That(getResult.SuccessValue!.Ranks.Count(r => r.MovementDomain == DomainType.Land), Is.EqualTo(2));
        }
    }

    [Test, Description("A duplicate (domain, level) in a save is rejected with a validation failure.")]
    public async Task SaveOfficerRanks_RejectsDuplicateLevel()
    {
        await SeedGameAndPlayerAsync();

        List<SaveOfficerRankDto> ranks =
        [
            new() { MovementDomain = DomainType.Land, Level = 1, Title = "A", MaxSoldiers = 10, MaxAttachedSpecialists = 1, InUse = true },
            new() { MovementDomain = DomainType.Land, Level = 1, Title = "B", MaxSoldiers = 10, MaxAttachedSpecialists = 1, InUse = true },
        ];

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new SaveOfficerRanksHandler(new NullLogger<SaveOfficerRanksHandler>(), context);
        ServiceResultBase result = await handler.Handle(new SaveOfficerRanksRequest(_playerId, ranks), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
    }

    [Test, Description("Only MilitaryOrganisationalGroup tags are returned for the combo box feed.")]
    public async Task GetOrganisationTags_ReturnsOnlyOrganisationGroupTags()
    {
        await SeedGameAndPlayerAsync();
        await SeedTagAsync("infantry", "Infantry", TagType.MilitaryOrganisationalGroup);
        await SeedTagAsync("stealth", "Stealth", TagType.Combat);

        var handler = new GetOrganisationTagsHandler(new NullLogger<GetOrganisationTagsHandler>(), DbFactory);
        ServiceResult<GetOrganisationTagsResult> result = await handler.Handle(new GetOrganisationTagsRequest(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success));
            Assert.That(result.SuccessValue!.Tags, Has.Count.EqualTo(1));
            Assert.That(result.SuccessValue!.Tags.Single().Code, Is.EqualTo("infantry"));
        }
    }

    [Test, Description("A tag with no saved structure returns the code default, flagged IsDefault.")]
    public async Task GetOrganisationStructure_ReturnsDefault_WhenNoneSaved()
    {
        await SeedGameAndPlayerAsync();
        int tagId = await SeedTagAsync("infantry", "Infantry", TagType.MilitaryOrganisationalGroup);

        var handler = new GetOrganisationStructureHandler(new NullLogger<GetOrganisationStructureHandler>(), DbFactory);
        ServiceResult<OrganisationStructureView> result = await handler.Handle(new GetOrganisationStructureRequest(_playerId, tagId), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success));
            Assert.That(result.SuccessValue!.IsDefault, Is.True);
            Assert.That(result.SuccessValue!.Tiers, Has.Count.EqualTo(MilitaryOrganisationDefaults.DefaultStructureTiers.Count));
        }
    }

    [Test, Description("Requesting a structure for a non-organisation tag is a not-found.")]
    public async Task GetOrganisationStructure_NotFound_ForNonOrgTag()
    {
        await SeedGameAndPlayerAsync();
        int tagId = await SeedTagAsync("stealth", "Stealth", TagType.Combat);

        var handler = new GetOrganisationStructureHandler(new NullLogger<GetOrganisationStructureHandler>(), DbFactory);
        ServiceResult<OrganisationStructureView> result = await handler.Handle(new GetOrganisationStructureRequest(_playerId, tagId), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("Saving a structure then re-saving replaces the tier set; the read reflects the latest save.")]
    public async Task SaveThenGetOrganisationStructure_RoundtripsAndReplaces()
    {
        await SeedGameAndPlayerAsync();
        int tagId = await SeedTagAsync("infantry", "Infantry", TagType.MilitaryOrganisationalGroup);

        async Task SaveAsync(IReadOnlyCollection<SaveOrganisationTierDto> tiers)
        {
            await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
            var handler = new SaveOrganisationStructureHandler(new NullLogger<SaveOrganisationStructureHandler>(), context);
            ServiceResultBase result = await handler.Handle(new SaveOrganisationStructureRequest(_playerId, tagId, tiers), CancellationToken.None);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.SuccessNoContent));
        }

        // First save: three tiers.
        await SaveAsync(
        [
            new() { TierIndex = 1, Name = "Squad", Multiplier = 8 },
            new() { TierIndex = 2, Name = "Platoon", Multiplier = 4 },
            new() { TierIndex = 3, Name = "Company", Multiplier = 3 },
        ]);

        // Second save: two tiers — should replace, not merge.
        await SaveAsync(
        [
            new() { TierIndex = 1, Name = "Ship", Multiplier = 1 },
            new() { TierIndex = 2, Name = "Squadron", Multiplier = 3 },
        ]);

        var getHandler = new GetOrganisationStructureHandler(new NullLogger<GetOrganisationStructureHandler>(), DbFactory);
        ServiceResult<OrganisationStructureView> result = await getHandler.Handle(new GetOrganisationStructureRequest(_playerId, tagId), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.IsDefault, Is.False);
            Assert.That(result.SuccessValue!.Tiers, Has.Count.EqualTo(2));
            Assert.That(result.SuccessValue!.Tiers.OrderBy(t => t.TierIndex).First().Name, Is.EqualTo("Ship"));
        }
    }

    [Test, Description("A structure with more than the maximum number of tiers is rejected.")]
    public async Task SaveOrganisationStructure_RejectsTooManyTiers()
    {
        await SeedGameAndPlayerAsync();
        int tagId = await SeedTagAsync("infantry", "Infantry", TagType.MilitaryOrganisationalGroup);

        // Six tiers — one over the maximum of five.
        List<SaveOrganisationTierDto> tiers =
        [
            new() { TierIndex = 1, Name = "T1", Multiplier = 10 },
            new() { TierIndex = 2, Name = "T2", Multiplier = 2 },
            new() { TierIndex = 3, Name = "T3", Multiplier = 2 },
            new() { TierIndex = 4, Name = "T4", Multiplier = 2 },
            new() { TierIndex = 5, Name = "T5", Multiplier = 2 },
            new() { TierIndex = 6, Name = "T6", Multiplier = 2 },
        ];

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new SaveOrganisationStructureHandler(new NullLogger<SaveOrganisationStructureHandler>(), context);
        ServiceResultBase result = await handler.Handle(new SaveOrganisationStructureRequest(_playerId, tagId, tiers), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
    }

    [Test, Description("A domain-tagged hierarchy's tier cap is max(1, in-use ranks - 1), and the default tiers are trimmed to it.")]
    public async Task GetOrganisationStructure_DerivesTierCapFromDomainRanks()
    {
        await SeedGameAndPlayerAsync();
        int tagId = await SeedOrgTagWithDomainAsync("fighter_wing", "Fighter Wing", DomainType.Air);
        await SeedPlayerRanksAsync(DomainType.Air, 3); // 3 in-use ranks → cap 2.

        var handler = new GetOrganisationStructureHandler(new NullLogger<GetOrganisationStructureHandler>(), DbFactory);
        ServiceResult<OrganisationStructureView> result = await handler.Handle(new GetOrganisationStructureRequest(_playerId, tagId), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.MovementDomain, Is.EqualTo(DomainType.Air));
            Assert.That(result.SuccessValue!.MaxTiers, Is.EqualTo(2));
            Assert.That(result.SuccessValue!.RankTitles, Has.Count.EqualTo(3));
            // The code default (5 tiers) is trimmed to the domain cap.
            Assert.That(result.SuccessValue!.Tiers, Has.Count.EqualTo(2));
        }
    }

    [Test, Description("Saving more tiers than the domain's rank ladder allows is rejected.")]
    public async Task SaveOrganisationStructure_RejectsTiersOverDomainCap()
    {
        await SeedGameAndPlayerAsync();
        int tagId = await SeedOrgTagWithDomainAsync("fighter_wing", "Fighter Wing", DomainType.Air);
        await SeedPlayerRanksAsync(DomainType.Air, 3); // cap 2.

        List<SaveOrganisationTierDto> tiers =
        [
            new() { TierIndex = 1, Name = "Flight", Multiplier = 4 },
            new() { TierIndex = 2, Name = "Squadron", Multiplier = 3 },
            new() { TierIndex = 3, Name = "Wing", Multiplier = 2 },
        ];

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new SaveOrganisationStructureHandler(new NullLogger<SaveOrganisationStructureHandler>(), context);
        ServiceResultBase result = await handler.Handle(new SaveOrganisationStructureRequest(_playerId, tagId, tiers), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
    }
}
