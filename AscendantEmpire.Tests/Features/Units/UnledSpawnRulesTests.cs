using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Tests.Features.Units;

/// <summary>
/// Unit tests for <see cref="UnledSpawnRules"/> — the sizing rules applied when a Unit is created from a
/// template whose organisation tag is unled (size must be a whole multiple of the base quantum, capped at
/// the tag's max). Pure logic over an in-memory <see cref="Tag"/>, so no database tier is needed.
/// </summary>
internal class UnledSpawnRulesTests
{
    /// <summary>
    /// Builds an unled organisation tag with the design doc's Settler sizing (base 25, default 200, max 5,000).
    /// </summary>
    private static Tag BuildUnledTag(int baseSize = 25, int maxSize = 5000)
    {
        return new Tag
        {
            Code = "settler",
            DisplayName = "Settler",
            TagType = TagType.MilitaryOrganisationalGroup,
            LeadershipModel = LeadershipModel.Unled,
            UnledBaseSize = baseSize,
            UnledDefaultSize = 200,
            UnledMaxSize = maxSize,
        };
    }

    [TestCase(25), TestCase(50), TestCase(200), TestCase(5000)]
    [Description("Whole multiples of the base quantum up to the max are legal.")]
    public void MultipleOfBaseWithinMaxPasses(int size)
    {
        List<string> errors = UnledSpawnRules.ValidateSize(BuildUnledTag(), size);

        Assert.That(errors, Is.Empty);
    }

    [TestCase(90), TestCase(1), TestCase(0)]
    [Description("A size that is not a whole positive multiple of the base quantum is rejected — including sizes below one quantum and zero.")]
    public void NonMultipleOfBaseFails(int size)
    {
        List<string> errors = UnledSpawnRules.ValidateSize(BuildUnledTag(), size);

        Assert.That(errors, Is.Not.Empty);
    }

    [Test, Description("A size above the tag's max is rejected even when it is a clean multiple of the base.")]
    public void MultipleAboveMaxFails()
    {
        List<string> errors = UnledSpawnRules.ValidateSize(BuildUnledTag(baseSize: 25, maxSize: 200), 225);

        Assert.That(errors, Is.Not.Empty);
    }

    [Test, Description("No organisation tag imposes no unled sizing rule.")]
    public void NullTagPasses()
    {
        List<string> errors = UnledSpawnRules.ValidateSize(organisationTag: null, size: 123);

        Assert.That(errors, Is.Empty);
    }

    [Test, Description("A commissioned organisation tag imposes no unled sizing rule.")]
    public void CommissionedTagPasses()
    {
        var commissionedTag = new Tag
        {
            Code = "infantry",
            DisplayName = "Infantry",
            TagType = TagType.MilitaryOrganisationalGroup,
            LeadershipModel = LeadershipModel.Commissioned,
        };

        List<string> errors = UnledSpawnRules.ValidateSize(commissionedTag, size: 123);

        Assert.That(errors, Is.Empty);
    }

    [Test, Description("An unled tag with no authored sizes is a configuration error, not a silent pass.")]
    public void UnledTagWithoutSizesFails()
    {
        var misconfiguredTag = new Tag
        {
            Code = "broken",
            DisplayName = "Broken",
            TagType = TagType.MilitaryOrganisationalGroup,
            LeadershipModel = LeadershipModel.Unled,
        };

        List<string> errors = UnledSpawnRules.ValidateSize(misconfiguredTag, size: 25);

        Assert.That(errors, Is.Not.Empty);
    }
}
