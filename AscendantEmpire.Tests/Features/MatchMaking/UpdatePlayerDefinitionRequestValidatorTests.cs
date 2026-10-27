using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.MatchMaking.Handlers;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.Tests.Features.MatchMaking;

/// <summary>
/// Tests for the rules guarding a player definition before it reaches the handler.
/// </summary>
/// <remarks>
/// <para>
/// Three rules here are easy to "tidy" into something wrong. The ruler title carries no <c>NotEmpty</c> on purpose, because a blank title is a valid
/// choice that the handler turns into the default placeholder; the ruler portrait carries none for the same reason, and its shape rule is guarded by a
/// <c>When</c> so that a blank one is not measured against it; and the gender check leans on <see cref="RulerGender"/> starting at 1, so a request that
/// simply omits it fails rather than silently arriving as Male.
/// </para>
/// <para>
/// Runs with no database at all: the validator is constructed directly, which is the lowest tier <c>testing.md</c> describes.
/// </para>
/// </remarks>
[TestFixture]
internal class UpdatePlayerDefinitionRequestValidatorTests
{
    private UpdatePlayerDefinitionRequestValidator _validator = null!;

    [SetUp]
    public void CreateValidator()
    {
        _validator = new UpdatePlayerDefinitionRequestValidator();
    }

    [Test, Description("A fully populated request passes, so the rest of the fixture is measuring the rule under test rather than a broken baseline.")]
    public void ValidRequestPasses()
    {
        ValidationResult result = _validator.Validate(BuildRequest());

        Assert.That(result.IsValid, Is.True, () => string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [TestCase(null, Description = "An omitted ruler title is accepted; the handler stores the default placeholder for it.")]
    [TestCase("", Description = "An empty ruler title is accepted for the same reason.")]
    [TestCase("   ", Description = "A whitespace-only ruler title is accepted for the same reason.")]
    public void BlankRulerTitleIsAccepted(string? rulerTitle)
    {
        ValidationResult result = _validator.Validate(BuildRequest(rulerTitle: rulerTitle));

        Assert.That(result.IsValid, Is.True, () => string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Test, Description("A ruler title past the column length is rejected here rather than truncated or thrown by the database.")]
    public void OverLongRulerTitleIsRejected()
    {
        ValidationResult result = _validator.Validate(BuildRequest(rulerTitle: new string('a', Player.RULER_TITLE_MAX_LENGTH + 1)));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("", Description = "A ruler name is required, unlike the title.")]
    [TestCase("   ", Description = "A whitespace-only ruler name counts as missing.")]
    public void BlankRulerNameIsRejected(string rulerName)
    {
        ValidationResult result = _validator.Validate(BuildRequest(rulerName: rulerName));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("", Description = "A faction name is required.")]
    [TestCase("   ", Description = "A whitespace-only faction name counts as missing.")]
    public void BlankFactionNameIsRejected(string factionName)
    {
        ValidationResult result = _validator.Validate(BuildRequest(factionName: factionName));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase(0, Description = "An omitted gender arrives as 0, which is not a defined member, so it is rejected rather than defaulting to Male.")]
    [TestCase(3, Description = "A value outside the enum is rejected.")]
    public void UndefinedGenderIsRejected(int rulerGender)
    {
        ValidationResult result = _validator.Validate(BuildRequest(rulerGender: (RulerGender)rulerGender));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("#c800de", true, Description = "A lower-case six-digit hex colour is accepted.")]
    [TestCase("#C800DE", true, Description = "Case does not matter; the handler lower-cases the value before storing it.")]
    [TestCase("", false, Description = "A faction colour is required.")]
    [TestCase("red", false, Description = "A CSS colour name is rejected: the frontend sends hex, and the backend has no colour parser to compare names with.")]
    [TestCase("#fff", false, Description = "Three-digit shorthand is rejected, so two spellings of one colour cannot both be stored.")]
    [TestCase("rgb(1,2,3)", false, Description = "A functional notation is rejected for the same reason.")]
    [TestCase("c800de", false, Description = "The leading hash is required.")]
    public void FactionColourMustBeSixDigitHex(string factionColour, bool expectedValid)
    {
        ValidationResult result = _validator.Validate(BuildRequest(factionColour: factionColour));

        Assert.That(result.IsValid, Is.EqualTo(expectedValid), () => string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [TestCase("", Description = "A people name is required, like the faction's own name.")]
    [TestCase("   ", Description = "A whitespace-only people name counts as missing.")]
    public void BlankPeopleNameIsRejected(string peopleName)
    {
        ValidationResult result = _validator.Validate(BuildRequest(peopleName: peopleName));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("", Description = "A demonym is required.")]
    [TestCase("   ", Description = "A whitespace-only demonym counts as missing.")]
    public void BlankDemonymIsRejected(string demonym)
    {
        ValidationResult result = _validator.Validate(BuildRequest(demonym: demonym));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("", Description = "A culture name is required.")]
    [TestCase("   ", Description = "A whitespace-only culture name counts as missing.")]
    public void BlankCultureNameIsRejected(string cultureName)
    {
        ValidationResult result = _validator.Validate(BuildRequest(cultureName: cultureName));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("", Description = "A language is required.")]
    [TestCase("   ", Description = "A whitespace-only language counts as missing.")]
    public void BlankLanguageIsRejected(string language)
    {
        ValidationResult result = _validator.Validate(BuildRequest(language: language));

        Assert.That(result.IsValid, Is.False);
    }

    [Test, Description("Each people-and-culture name is length-checked against its own column, so none of the four can reach the database over-long.")]
    public void OverLongPeopleAndCultureNamesAreRejected()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_validator.Validate(BuildRequest(peopleName: new string('a', Player.PEOPLE_NAME_MAX_LENGTH + 1))).IsValid, Is.False);
            Assert.That(_validator.Validate(BuildRequest(demonym: new string('a', Player.DEMONYM_MAX_LENGTH + 1))).IsValid, Is.False);
            Assert.That(_validator.Validate(BuildRequest(cultureName: new string('a', Player.CULTURE_NAME_MAX_LENGTH + 1))).IsValid, Is.False);
            Assert.That(_validator.Validate(BuildRequest(language: new string('a', Player.LANGUAGE_MAX_LENGTH + 1))).IsValid, Is.False);
        }
    }

    [TestCase("", Description = "An omitted ruler portrait is accepted; the handler stores the default for it.")]
    [TestCase("   ", Description = "A whitespace-only ruler portrait is accepted for the same reason.")]
    public void BlankRulerPortraitIsAccepted(string rulerPortraitPath)
    {
        ValidationResult result = _validator.Validate(BuildRequest(rulerPortraitPath: rulerPortraitPath));

        Assert.That(result.IsValid, Is.True, () => string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Test, Description("A ruler portrait path past the column length is rejected here rather than thrown by the database.")]
    public void OverLongRulerPortraitIsRejected()
    {
        ValidationResult result = _validator.Validate(BuildRequest(rulerPortraitPath: $"/portraits/{new string('a', Player.RULER_PORTRAIT_PATH_MAX_LENGTH)}.png"));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("https://evil.example/x.png", Description = "An off-site URL is rejected: the path is rendered into an img src that every other player sees.")]
    [TestCase("//evil.example/x.png", Description = "A protocol-relative URL is rejected for the same reason.")]
    [TestCase("/elsewhere/01_western_king.png", Description = "A path outside the portraits folder is rejected, so only the shipped set can be chosen.")]
    [TestCase("/portraits/../secrets.png", Description = "A traversal segment is rejected rather than normalised.")]
    [TestCase("/portraits/01_western_king.svg", Description = "A non-png extension is rejected, since every shipped portrait is a png.")]
    public void RulerPortraitOutsideTheOfferedShapeIsRejected(string rulerPortraitPath)
    {
        ValidationResult result = _validator.Validate(BuildRequest(rulerPortraitPath: rulerPortraitPath));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("/portraits/01_western_king.png", Description = "The first shipped portrait passes, pinning the shape rule as permissive enough for the real values.")]
    [TestCase("/portraits/20_blue_crowned_queen.png", Description = "The last shipped portrait passes too, so the rule is not accidentally anchored to one file.")]
    public void RulerPortraitTheUiOffersIsAccepted(string rulerPortraitPath)
    {
        ValidationResult result = _validator.Validate(BuildRequest(rulerPortraitPath: rulerPortraitPath));

        Assert.That(result.IsValid, Is.True, () => string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    /// <summary>
    /// A valid request, with only the value a test cares about overridden.
    /// </summary>
    private static UpdatePlayerDefinitionRequest BuildRequest(
        int userId = 11,
        int gameId = 4,
        string? rulerTitle = "Empress",
        string rulerName = "Aelara",
        RulerGender rulerGender = RulerGender.Female,
        string factionName = "The Ember Pact",
        string factionColour = "#c800de",
        string peopleName = "The Vashti",
        string demonym = "Vashti",
        string cultureName = "Vashti Highland",
        string language = "Old Vashti",
        string rulerPortraitPath = "/portraits/07_northern_queen.png")
    {
        return new UpdatePlayerDefinitionRequest(
            userId,
            gameId,
            rulerTitle,
            rulerName,
            rulerGender,
            factionName,
            factionColour,
            peopleName,
            demonym,
            cultureName,
            language,
            rulerPortraitPath);
    }
}
