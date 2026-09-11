using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Tests for <see cref="SoloCoalitionResolver"/>: which Players are counted when every Player sees alone.
/// </summary>
/// <remarks>
/// On the in-memory tier: it is one LINQ query over <c>Players</c>. The rule that turns Player ids into coalitions is covered without a database in
/// <see cref="SightingFidelityRulesTests"/>.
/// </remarks>
[TestFixture]
internal class SoloCoalitionResolverTests : InMemoryTestBase
{
    private const int _GAME_ID = 1;
    private const int _OTHER_GAME_ID = 2;

    [Test, Description("Only the Game's active Players are coalitions, since an eliminated Player has nobody left to report what their Observers saw.")]
    public async Task ShouldMakeACoalitionOfEachActivePlayerInTheGameAsync()
    {
        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            seedContext.Players.AddRange(
                NewPlayer(id: 1, _GAME_ID, PlayerStatus.Active),
                NewPlayer(id: 2, _GAME_ID, PlayerStatus.Active),
                NewPlayer(id: 3, _GAME_ID, PlayerStatus.Eliminated),
                NewPlayer(id: 4, _OTHER_GAME_ID, PlayerStatus.Active));

            await seedContext.SaveChangesAsync();
        }

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        IReadOnlyCollection<VisionCoalition> coalitions = await new SoloCoalitionResolver().ResolveAsync(context, _GAME_ID);

        Assert.That(coalitions.Select(coalition => coalition.CoalitionId), Is.EqualTo(new[] { 1, 2 }));
    }

    private static Player NewPlayer(int id, int gameId, PlayerStatus status)
    {
        return new Player
        {
            Id = id,
            GameId = gameId,
            Status = status,
            RulerTitle = "King",
            RulerName = $"Ruler {id}",
            FactionName = $"Faction {id}"
        };
    }
}
