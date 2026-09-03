using AscendantEmpire.Application.Common.Validation;

namespace AscendantEmpire.Tests.Common.Validation;

/// <summary>
/// Pins the PascalCase splitting that default validation messages are built from.
/// </summary>
/// <remarks>
/// <para>This reproduces FluentValidation's own algorithm. It matters because the campaign map index validators leave one <c>NotEmpty()</c> without an
/// explicit message, so the wording a user sees for that rule comes straight out of here.</para>
/// </remarks>
[TestFixture]
internal class PropertyNameFormatterTests
{
    [TestCase("MapName", "Map Name", Description = "The two-word case the campaign map index validators depend on.")]
    [TestCase("GameId", "Game Id", Description = "A trailing short word still opens its own word.")]
    [TestCase("PlayerIdsInTurnOrder", "Player Ids In Turn Order", Description = "Every internal boundary splits, however many words there are.")]
    [TestCase("Name", "Name", Description = "A single word is left alone.")]
    [TestCase("", "", Description = "An empty name has no boundary to split and must not throw.")]
    public void ShouldSplitPascalCaseIntoWords(string input, string expected)
    {
        Assert.That(PropertyNameFormatter.SplitPascalCase(input), Is.EqualTo(expected));
    }

    [TestCase("ID", "ID", Description = "A run of capitals is an acronym, not a run of one-letter words.")]
    [TestCase("HtmlID", "Html ID", Description = "An acronym following a word splits from it but stays whole itself.")]
    public void ShouldKeepAcronymsWhole(string input, string expected)
    {
        Assert.That(PropertyNameFormatter.SplitPascalCase(input), Is.EqualTo(expected));
    }
}
