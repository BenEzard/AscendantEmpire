using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Pure unit tests for <see cref="SightingFidelityRules"/> and the solo coalition rule — no database, because both are fixed rules over their arguments.
/// </summary>
/// <remarks>
/// The Force test pins a <b>stub</b>. It is meant to break when the real fidelity curve lands, as the signal that the disclosure rule has changed.
/// </remarks>
[TestFixture]
internal class SightingFidelityRulesTests
{
    [Test, Description("Ground and buildings do not hide from an Observer that can see them, so a tile is always seen in full detail.")]
    public void ShouldSeeATileInFullDetail()
    {
        Assert.That(SightingFidelityRules.ForTile(), Is.EqualTo(SightingFidelity.Detail));
    }

    [Test, Description("Until the Force curve is designed, a Force in sight is seen in full detail, matching what the live map already shows of it.")]
    public void ShouldSeeAForceInFullDetailWhileTheCurveIsStubbed()
    {
        var inputs = new ForceSightingInputs
        {
            PlayerTurnId = 1,
            TurnSequence = 1,
            ObserverId = 1,
            ObserverRecordType = RecordType.Structure,
            ObserverDetection = SightingFidelityRules.STRUCTURE_DETECTION,
            Distance = 5,
            SubjectUnitInstanceId = 2,
            SubjectUnitTemplateId = 3,
            SubjectVisibility = 0,
            SubjectBiomeType = BiomeType.Jungle,
            SubjectGeographyType = CampaignMapGeographyType.Hilly
        };

        Assert.That(SightingFidelityRules.ForForce(inputs), Is.EqualTo(SightingFidelity.Detail));
    }

    [Test, Description("With no relations model every Player sees alone, so each is a coalition of one identified by their own id.")]
    public void ShouldMakeEachPlayerACoalitionOfOne()
    {
        IReadOnlyCollection<VisionCoalition> coalitions = SoloCoalitions.From([3, 1, 3]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(coalitions.Select(coalition => coalition.CoalitionId), Is.EqualTo(new[] { 1, 3 }), "One per distinct Player, in id order.");
            Assert.That(coalitions.All(coalition => coalition.MemberPlayerIds.SequenceEqual([coalition.CoalitionId])), Is.True);
        }
    }
}
