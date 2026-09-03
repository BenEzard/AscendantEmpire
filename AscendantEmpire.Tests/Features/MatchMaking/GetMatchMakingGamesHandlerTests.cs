using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.MatchMaking.Handlers;
using AscendantEmpire.BackendContracts.MatchMaking;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.MatchMaking;

/// <summary>
/// Tests for the matchmaking lobby listing.
/// </summary>
/// <remarks>
/// <para>
/// The listing used to reach the map dimensions by joining out to <c>mapgallery.CampaignMapGalleryIndex</c>, the row the game's map was originally copied from.
/// That row is shared by every game created from the same gallery map and can be edited independently of them, so it is not a safe source for a given game's
/// geometry. Each game now snapshots its own dimensions at setup and the listing reads those.
/// </para>
/// <para>
/// Runs at the in-memory tier: the handler is a single LINQ query with the grouping done client-side after materialising, so nothing here needs a real database.
/// </para>
/// </remarks>
internal class GetMatchMakingGamesHandlerTests : InMemoryTestBase
{
    private const int _USER_ID = 11;
    private const int _GAME_ID = 4;

    /// <summary>What the game itself records. The gallery row deliberately disagrees, so the two sources cannot be confused.</summary>
    private const int _GAME_MAP_WIDTH = 40;
    private const int _GAME_MAP_HEIGHT = 25;

    /// <summary>What the shared gallery row says, standing in for a gallery map that has been resized since this game was created.</summary>
    private const int _GALLERY_MAP_WIDTH = 300;
    private const int _GALLERY_MAP_HEIGHT = 260;

    private const int _GALLERY_INDEX_ID = 2;

    /// <summary>The colour held by the claimed seat, so the lobby can draw a chip beside it.</summary>
    private const string _CLAIMED_FACTION_COLOUR = "#c800de";

    private GetMatchMakingGamesHandler _handler = null!;

    [SetUp]
    public async Task SeedLobbyAsync()
    {
        _handler = new GetMatchMakingGamesHandler(NullLogger<GetMatchMakingGamesHandler>.Instance, DbFactory);

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignMapGalleryIndex.Add(new CampaignMapGalleryIndex
        {
            Id = _GALLERY_INDEX_ID,
            MapName = "Shared Gallery Map",
            Description = "Used by more than one game",
            Width = _GALLERY_MAP_WIDTH,
            Height = _GALLERY_MAP_HEIGHT,
            CreationStatus = MapCreationStatus.Complete,
            CreationEngine = MapCreationEngine.BlankMap,
            Status = "Complete",
            ErrorMessage = null,
            PropertiesJson = "{}",
            CampaignMapOffsetId = 0,
            HorizontalWrap = false,
            VerticalWrap = false,
        });

        context.GameIndex.Add(new GameIndex
        {
            Id = _GAME_ID,
            CampaignMapIndexId = _GALLERY_INDEX_ID,
            GameName = "Joinable Game",
            NumberOfPlayers = 2,
            // Only games that have finished setup and are in progress are listed.
            CreationStatus = GameCreationStatus.SetupComplete,
            PlayStatus = GamePlayStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            GenerationSeed = 1,
            BattleMapSize = 30,
            MapWidth = _GAME_MAP_WIDTH,
            MapHeight = _GAME_MAP_HEIGHT,
            CampaignMapOffsetId = 5_000,
        });

        // One seat already taken by the caller, one still free. The names are deliberately ordered
        // the opposite way round to the ids, so a listing sorted by label rather than by id shows up.
        context.Players.Add(new Player
        {
            Id = 1,
            GameId = _GAME_ID,
            UserId = _USER_ID,
            RulerTitle = "Queen",
            RulerName = "Taken",
            FactionName = "Claimed Faction",
            FactionColour = _CLAIMED_FACTION_COLOUR,
            RulerGender = RulerGender.Female,
            Status = PlayerStatus.Active,
            CampaignOffsetId = 1,
        });

        context.Players.Add(new Player
        {
            Id = 2,
            GameId = _GAME_ID,
            UserId = null,
            RulerTitle = "King",
            RulerName = "Free",
            FactionName = "Open Faction",
            Status = PlayerStatus.Active,
            CampaignOffsetId = 2,
        });

        await context.SaveChangesAsync();
    }

    [Test, Description("The lobby reports the dimensions the game recorded for itself, not the shared gallery row's, which may since have changed.")]
    public async Task ListingReportsTheGamesOwnMapDimensionsAsync()
    {
        ServiceResultBase.ServiceResult<IReadOnlyCollection<MatchMakingGame>> result =
            await _handler.Handle(new GetMatchMakingGamesRequest(_USER_ID), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        MatchMakingGame game = result.SuccessValue!.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.MapWidth, Is.EqualTo(_GAME_MAP_WIDTH));
            Assert.That(game.MapHeight, Is.EqualTo(_GAME_MAP_HEIGHT));

            Assert.That(game.MapWidth, Is.Not.EqualTo(_GALLERY_MAP_WIDTH), "The gallery row's width must not be what is reported.");
            Assert.That(game.MapHeight, Is.Not.EqualTo(_GALLERY_MAP_HEIGHT));
        }
    }

    [Test, Description("Dropping the gallery join does not change which slots are listed or how they are flagged.")]
    public async Task ListingStillReportsEverySlotAndItsClaimStateAsync()
    {
        ServiceResultBase.ServiceResult<IReadOnlyCollection<MatchMakingGame>> result =
            await _handler.Handle(new GetMatchMakingGamesRequest(_USER_ID), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        MatchMakingGame game = result.SuccessValue!.Single();

        MatchMakingSlot claimed = game.Slots.Single(s => s.PlayerId == 1);
        MatchMakingSlot free = game.Slots.Single(s => s.PlayerId == 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.GameId, Is.EqualTo(_GAME_ID));
            Assert.That(game.Slots, Has.Count.EqualTo(2), "Both active slots are listed, claimed and free alike.");

            Assert.That(claimed.IsClaimed, Is.True);
            Assert.That(claimed.IsClaimedByCurrentUser, Is.True, "The caller holds this seat.");

            Assert.That(free.IsClaimed, Is.False);
            Assert.That(free.IsClaimedByCurrentUser, Is.False);
        }
    }

    [Test, Description("Each slot carries its faction colour, which is what the lobby draws its colour chips from.")]
    public async Task ListingCarriesEachSlotsFactionColourAsync()
    {
        ServiceResultBase.ServiceResult<IReadOnlyCollection<MatchMakingGame>> result =
            await _handler.Handle(new GetMatchMakingGamesRequest(_USER_ID), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        MatchMakingGame game = result.SuccessValue!.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.Slots.Single(s => s.PlayerId == 1).FactionColour, Is.EqualTo(_CLAIMED_FACTION_COLOUR));
            Assert.That(game.Slots.Single(s => s.PlayerId == 2).FactionColour, Is.Null, "A slot nobody has defined has no colour to draw.");
        }
    }

    [Test, Description("Slots are ordered by id, so renaming a ruler cannot make the seats jump about between refreshes while a lobby fills.")]
    public async Task SlotsAreOrderedByIdRatherThanByLabelAsync()
    {
        ServiceResultBase.ServiceResult<IReadOnlyCollection<MatchMakingGame>> result =
            await _handler.Handle(new GetMatchMakingGamesRequest(_USER_ID), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        MatchMakingGame game = result.SuccessValue!.Single();

        // Seat 1 labels as "Queen Taken (...)" and seat 2 as "King Free (...)", so ordering by label
        // would put them the other way round.
        Assert.That(game.Slots.Select(s => s.PlayerId), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test, Description("A game whose gallery row has gone missing is still listed, where the old left join would have reported a zero-sized map.")]
    public async Task ListingSurvivesAMissingGalleryRowAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            context.CampaignMapGalleryIndex.RemoveRange(context.CampaignMapGalleryIndex);
            await context.SaveChangesAsync();
        }

        ServiceResultBase.ServiceResult<IReadOnlyCollection<MatchMakingGame>> result =
            await _handler.Handle(new GetMatchMakingGamesRequest(_USER_ID), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        MatchMakingGame game = result.SuccessValue!.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.MapWidth, Is.EqualTo(_GAME_MAP_WIDTH), "The dimensions come from the game, so a missing gallery row cannot blank them.");
            Assert.That(game.MapHeight, Is.EqualTo(_GAME_MAP_HEIGHT));
        }
    }
}
