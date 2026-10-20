using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Structures.Handlers;
using AscendantEmpire.BackendContracts.Structures.GetStructureDisplayInfo;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Structures.Handlers;

/// <summary>
/// Tests for <see cref="GetStructureDisplayInfoHandler"/>: that the client is given a name and image for every kind of Structure the campaign map can show.
/// </summary>
/// <remarks>
/// In-memory, because the read is a plain LINQ projection over the Structure catalogue.
/// </remarks>
[TestFixture]
internal class GetStructureDisplayInfoHandlerTests : InMemoryTestBase
{
    [Test, Description("Every campaign-map Structure kind comes back with its name and image, and a battle-map-only kind does not, since no campaign intelligence can ever name one.")]
    public async Task ShouldReturnEveryCampaignMapStructureKindWithItsNameAndImageAsync()
    {
        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            context.Set<Structure>().AddRange(NewStructureType(2, "Village", canBeBuiltOnCampaignMap: true), NewStructureType(1, "Watchtower", canBeBuiltOnCampaignMap: true), NewStructureType(3, "Palisade", canBeBuiltOnCampaignMap: false));
            await context.SaveChangesAsync();
        }

        var handler = new GetStructureDisplayInfoHandler(DbFactory);

        ServiceResult<IReadOnlyCollection<StructureDisplayInfo>> result = await handler.Handle(new GetStructureDisplayInfoRequest(), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.Select(info => (info.StructureId, info.Name)), Is.EqualTo(new[] { (1, "Watchtower"), (2, "Village") }), "In id order.");
            Assert.That(result.SuccessValue!.First().MapImagePath, Is.EqualTo("Watchtower.png"));
        }
    }

    private static Structure NewStructureType(int id, string name, bool canBeBuiltOnCampaignMap)
    {
        return new Structure
        {
            Id = id,
            Code = name.ToLowerInvariant(),
            Name = name,
            RequiresSlotType = TileSlotType.Primary,
            AllowedPositions = PositionOnTile.WholeTile,
            CanBeBuiltOnCampaignMap = canBeBuiltOnCampaignMap,
            CanBeBuiltOnBattleMap = canBeBuiltOnCampaignMap == false,
            DualMapBuildTrigger = false,
            BuildPointsRequired = 0,
            SightRange = 2,
            SightDirection = DetectionDirection.FullRadius,
            Asserts = StructureAssertion.Owned,
            MapImagePath = $"{name}.png",
            FullImagePath = $"{name}-full.png"
        };
    }
}
