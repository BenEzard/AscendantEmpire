using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Tests.Features.Units;

/// <summary>
/// Unit tests for <see cref="UnledSplitRules"/> — the rules governing how an unled Force may be divided, and how its wounded travel with the pieces.
/// </summary>
/// <remarks>
/// <para>
/// Pure logic over an in-memory <see cref="Tag"/>, so no database tier is needed.
/// </para>
/// <para>
/// The numbers are the design doc's Settler: base quantum 25, so a 200-strong party is eight groups and a 190-strong one is seven, the last worth 40.
/// See <c>docs/design/units/military-organisation.md</c>, "Splitting and merging".
/// </para>
/// </remarks>
internal class UnledSplitRulesTests
{
    private const int _UNLED_BASE_SIZE = 25;
    private const int _UNLED_MAX_SIZE = 5_000;

    /// <summary>Builds an unled organisation tag with the design doc's Settler sizing.</summary>
    private static Tag BuildUnledTag(int? baseSize = _UNLED_BASE_SIZE)
    {
        return new Tag
        {
            Code = OrganisationTagCodes.SETTLER,
            DisplayName = "Settler",
            TagType = TagType.MilitaryOrganisationalGroup,
            LeadershipModel = LeadershipModel.Unled,
            UnledBaseSize = baseSize,
            UnledDefaultSize = 200,
            UnledMaxSize = _UNLED_MAX_SIZE,
        };
    }

    // --- Group sizes: the shape a split is allowed to take -----------------------------------------------------------

    [Test, Description("Dividing into equal quanta is the simplest legal split, and the one the design's worked example leads with.")]
    public void ShouldAcceptTheSplitWhenEveryGroupIsASingleQuantum()
    {
        List<string> errors = UnledSplitRules.ValidateGroupSizes(BuildUnledTag(), totalStrength: 200, groupSizes: [25, 25, 25, 25, 25, 25, 25, 25]);

        Assert.That(errors, Is.Empty);
    }

    [Test, Description("Groups may be any multiple of the quantum and need not match each other, which is the whole point of letting the player compose the split.")]
    public void ShouldAcceptTheSplitWhenGroupsAreUnevenMultiplesOfTheQuantum()
    {
        List<string> errors = UnledSplitRules.ValidateGroupSizes(BuildUnledTag(), totalStrength: 200, groupSizes: [25, 75, 50, 50]);

        Assert.That(errors, Is.Empty);
    }

    [Test, Description("A Force whose strength is not a clean multiple still splits; the leftover rides with one group rather than blocking the split.")]
    public void ShouldAcceptTheSplitWhenOneGroupCarriesTheRemainder()
    {
        // 190 is seven quanta with 15 left over, so a group of 40 is one quantum plus the remainder.
        List<string> errors = UnledSplitRules.ValidateGroupSizes(BuildUnledTag(), totalStrength: 190, groupSizes: [25, 125, 40]);

        Assert.That(errors, Is.Empty);
    }

    [Test, Description("Two whole quanta is the inclusive floor for a split, so the most common division of all must not be an off-by-one away from refusal.")]
    public void ShouldAcceptTheSplitWhenTheForceIsExactlyTwoQuanta()
    {
        List<string> errors = UnledSplitRules.ValidateGroupSizes(BuildUnledTag(), totalStrength: 50, groupSizes: [25, 25]);

        Assert.That(errors, Is.Empty);
    }

    [Test, Description("One group is the Force as it already stands, so it is not a split and must be refused rather than silently doing nothing.")]
    public void ShouldRefuseTheSplitWhenOnlyOneGroupIsAsked()
    {
        List<string> errors = UnledSplitRules.ValidateGroupSizes(BuildUnledTag(), totalStrength: 200, groupSizes: [200]);

        Assert.That(errors, Has.One.Contains("at least 2 groups"));
    }

    [Test, Description("A group below one quantum is the lone part-group the design forbids outright — the remainder always rides with a whole group.")]
    public void ShouldRefuseTheSplitWhenAGroupIsSmallerThanOneQuantum()
    {
        List<string> errors = UnledSplitRules.ValidateGroupSizes(BuildUnledTag(), totalStrength: 200, groupSizes: [10, 190]);

        Assert.That(errors, Has.One.Contains("at least 25"));
    }

    [Test, Description("A sub-quantum remainder group is exactly the 'lone 15' the design rules out, and it is the mistake a careless client is most likely to send.")]
    public void ShouldRefuseTheSplitWhenTheRemainderIsLeftAsAGroupOfItsOwn()
    {
        List<string> errors = UnledSplitRules.ValidateGroupSizes(BuildUnledTag(), totalStrength: 190, groupSizes: [25, 25, 25, 25, 25, 25, 25, 15]);

        Assert.That(errors, Has.One.Contains("at least 25"));
    }

    [Test, Description("Groups that do not add up to the Force would invent or lose soldiers, so the sum is checked rather than trusted.")]
    public void ShouldRefuseTheSplitWhenTheGroupsDoNotAccountForTheWholeForce()
    {
        List<string> errors = UnledSplitRules.ValidateGroupSizes(BuildUnledTag(), totalStrength: 200, groupSizes: [25, 100]);

        Assert.That(errors, Has.One.Contains("account for all 200"));
    }

    [Test, Description("Only one group may be ragged; two ragged groups would split the remainder itself, which no legal composition of quanta can produce.")]
    public void ShouldRefuseTheSplitWhenMoreThanOneGroupIsNotAWholeMultiple()
    {
        // 30 and 170 each leave something over, so neither is a clean run of quanta and the remainder has been divided.
        List<string> errors = UnledSplitRules.ValidateGroupSizes(BuildUnledTag(), totalStrength: 200, groupSizes: [30, 170]);

        Assert.That(errors, Has.One.Contains("one group may carry the remainder"));
    }

    [Test, Description("Every broken rule is reported at once, so fixing one problem does not simply reveal the next on the following request.")]
    public void ShouldReportEveryBrokenRuleWhenSeveralFail()
    {
        List<string> errors = UnledSplitRules.ValidateGroupSizes(BuildUnledTag(), totalStrength: 200, groupSizes: [10]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors, Has.One.Contains("at least 2 groups"));
            Assert.That(errors, Has.One.Contains("at least 25"));
            Assert.That(errors, Has.One.Contains("account for all 200"));
        }
    }

    [Test, Description("No organisation tag means no unled quantum to divide along, so the rule has nothing to say rather than inventing a guardrail.")]
    public void ShouldImposeNoRuleWhenThereIsNoOrganisationTag()
    {
        List<string> errors = UnledSplitRules.ValidateGroupSizes(organisationTag: null, totalStrength: 200, groupSizes: [90, 110]);

        Assert.That(errors, Is.Empty);
    }

    [Test, Description("Commissioned formations detach along tier lines instead, a different mechanic, so the unled quantum rule does not apply to them.")]
    public void ShouldImposeNoRuleWhenTheOrganisationIsCommissioned()
    {
        var commissionedTag = new Tag
        {
            Code = "infantry",
            DisplayName = "Infantry",
            TagType = TagType.MilitaryOrganisationalGroup,
            LeadershipModel = LeadershipModel.Commissioned,
        };

        List<string> errors = UnledSplitRules.ValidateGroupSizes(commissionedTag, totalStrength: 200, groupSizes: [90, 110]);

        Assert.That(errors, Is.Empty);
    }

    [Test, Description("An unled tag missing its base size is bad catalogue data, surfaced plainly rather than letting every size through unchecked.")]
    public void ShouldRefuseTheSplitWhenAnUnledTagHasNoAuthoredBaseSize()
    {
        List<string> errors = UnledSplitRules.ValidateGroupSizes(BuildUnledTag(baseSize: null), totalStrength: 200, groupSizes: [100, 100]);

        Assert.That(errors, Has.One.Contains("no authored base size"));
    }

    [Test, Description("A split only ever shrinks a Force, so the tag's max cannot be breached by one and must not be re-litigated here.")]
    public void ShouldImposeNoMaximumSizeRuleOnASplit()
    {
        // The whole Force is already above the max — bad data, or a tag re-authored downwards — yet dividing it is still an improvement, never a breach.
        Tag tag = BuildUnledTag();
        tag.UnledMaxSize = 50;

        List<string> errors = UnledSplitRules.ValidateGroupSizes(tag, totalStrength: 200, groupSizes: [100, 100]);

        Assert.That(errors, Is.Empty);
    }

    // --- Apportioning the wounded ------------------------------------------------------------------------------------

    [Test, Description("The wounded travel with their group in proportion to its size, because nothing in the design concentrates them anywhere.")]
    public void ShouldApportionTheInjuredInProportionToGroupSize()
    {
        List<int> injured = UnledSplitRules.ApportionInjured([25, 75, 100], totalInjuredCount: 20);

        Assert.That(injured, Is.EqualTo(new[] { 3, 7, 10 }));
    }

    [Test, Description("Rounding must not invent or lose a casualty, so the apportioned parts add back to exactly what the Force was carrying.")]
    public void ShouldApportionEveryInjuredSoldierWhenTheSharesDoNotDivideCleanly()
    {
        List<int> injured = UnledSplitRules.ApportionInjured([25, 75, 50, 50], totalInjuredCount: 7);

        Assert.That(injured.Sum(), Is.EqualTo(7));
    }

    [Test, Description("Ties go to the earlier group so the result is deterministic, and the server's outcome matches the preview the player confirmed.")]
    public void ShouldGiveTheRoundingLeftoverToTheEarlierGroupWhenFractionsTie()
    {
        List<int> injured = UnledSplitRules.ApportionInjured([1, 1, 1], totalInjuredCount: 10);

        Assert.That(injured, Is.EqualTo(new[] { 4, 3, 3 }));
    }

    [Test, Description("An unwounded Force divides into unwounded groups; apportioning nothing must not hand out a phantom casualty.")]
    public void ShouldApportionNothingWhenTheForceCarriesNoInjured()
    {
        List<int> injured = UnledSplitRules.ApportionInjured([25, 75, 100], totalInjuredCount: 0);

        Assert.That(injured, Is.EqualTo(new[] { 0, 0, 0 }));
    }
}
