using System.Linq.Expressions;

using AscendantEmpire.Application.Common.Validation;

namespace AscendantEmpire.Tests.Common.Validation;

/// <summary>
/// Pins each operator in <see cref="RuleBuilderExtensions"/> — what it accepts, what it rejects, and the message it falls back to.
/// </summary>
/// <remarks>
/// <para>The boundary cases matter more than the obvious ones: whether an absent value is a failure or a skip differs from operator to operator, and the
/// ported validators were written against those exact answers. Nothing here touches a database.</para>
/// </remarks>
[TestFixture]
internal class RuleBuilderExtensionsTests
{
    private enum SampleMode
    {
        First = 1,
        Second = 2
    }

    /// <summary>
    /// One model carrying a property of each shape the operators are declared for.
    /// </summary>
    private sealed record SampleModel(
        int Count = 5,
        int? OptionalCount = 5,
        decimal Cost = 5m,
        string Name = "abc",
        string? OptionalName = null,
        SampleMode Mode = SampleMode.First,
        IReadOnlyCollection<int>? Items = null);

    /// <summary>
    /// A validator whose rules the test supplies, so an operator can be exercised without a bespoke subclass per case.
    /// </summary>
    /// <remarks>
    /// <para>Constructed empty and given its rules afterwards, rather than taking them as a constructor argument, so it stays constructible without
    /// arguments — the assembly scan in <c>AddValidatorsFromAssembly</c> refuses a validator that is not, and it sweeps this assembly too.</para>
    /// </remarks>
    private sealed class TestValidator : Validator<SampleModel>
    {
        /// <summary>Exposes the protected <c>RuleFor</c> so a test can declare a rule inline.</summary>
        public IRuleBuilder<SampleModel, TProperty> On<TProperty>(Expression<Func<SampleModel, TProperty>> selector)
        {
            return RuleFor(selector);
        }
    }

    /// <summary>
    /// Runs one inline rule over one model and hands back the messages it produced.
    /// </summary>
    private static string[] MessagesFrom(Action<TestValidator> declareRules, SampleModel model)
    {
        TestValidator validator = new();

        declareRules(validator);

        return [.. validator.Validate(model).Errors.Select(failure => failure.ErrorMessage)];
    }

    [TestCase(null, false, Description = "A null string is null, so NotNull rejects it.")]
    [TestCase("", true, Description = "An empty string is still an instance, so NotNull accepts it — NotEmpty is the operator that rejects it.")]
    [TestCase("abc", true, Description = "A populated string passes.")]
    public void ShouldRejectOnlyNullValuesForNotNull(string? optionalName, bool expectedValid)
    {
        SampleModel model = new SampleModel() with { OptionalName = optionalName };

        string[] messages = MessagesFrom(v => v.On(x => x.OptionalName).NotNull(), model);

        Assert.That(messages, expectedValid ? Is.Empty : Is.Not.Empty);
    }

    [Test, Description("A non-nullable value type can never be null, so NotNull on one always passes rather than tripping on the zero default.")]
    public void ShouldAlwaysPassNotNullForANonNullableValueType()
    {
        SampleModel model = new SampleModel() with { Count = 0 };

        Assert.That(MessagesFrom(v => v.On(x => x.Count).NotNull(), model), Is.Empty);
    }

    [Test, Description("An absent nullable value type is null, so NotNull rejects it — this is the shape the map index Id rule relies on.")]
    public void ShouldRejectAnAbsentNullableValueTypeForNotNull()
    {
        SampleModel model = new SampleModel() with { OptionalCount = null };

        Assert.That(MessagesFrom(v => v.On(x => x.OptionalCount).NotNull(), model), Is.Not.Empty);
    }

    [TestCase(null, false, Description = "Null is empty.")]
    [TestCase("", false, Description = "An empty string is empty.")]
    [TestCase("   ", false, Description = "Whitespace only counts as empty, matching what FluentValidation did.")]
    [TestCase("a", true, Description = "A single character is enough to be populated.")]
    public void ShouldTreatBlankTextAsEmpty(string? optionalName, bool expectedValid)
    {
        SampleModel model = new SampleModel() with { OptionalName = optionalName };

        string[] messages = MessagesFrom(v => v.On(x => x.OptionalName).NotEmpty(), model);

        Assert.That(messages, expectedValid ? Is.Empty : Is.Not.Empty);
    }

    [Test, Description("An empty collection is empty, which is how the battle turn queue rejects a queue with nobody in it.")]
    public void ShouldTreatAnEmptyCollectionAsEmpty()
    {
        SampleModel empty = new SampleModel() with { Items = [] };
        SampleModel populated = new SampleModel() with { Items = [1] };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(MessagesFrom(v => v.On(x => x.Items).NotEmpty(), empty), Is.Not.Empty);
            Assert.That(MessagesFrom(v => v.On(x => x.Items).NotEmpty(), populated), Is.Empty);
        }
    }

    [Test, Description("NotEmpty falls back to the type's default for anything else, so a zero int counts as empty.")]
    public void ShouldTreatTheDefaultValueAsEmpty()
    {
        SampleModel model = new SampleModel() with { Count = 0 };

        Assert.That(MessagesFrom(v => v.On(x => x.Count).NotEmpty(), model), Is.Not.Empty);
    }

    [TestCase(0, false, Description = "Zero is not greater than zero — the case behind every 'Game Id is required.' rule.")]
    [TestCase(-1, false, Description = "A negative id is not greater than zero.")]
    [TestCase(1, true, Description = "One is greater than zero.")]
    public void ShouldCompareWithGreaterThan(int count, bool expectedValid)
    {
        SampleModel model = new SampleModel() with { Count = count };

        Assert.That(MessagesFrom(v => v.On(x => x.Count).GreaterThan(0), model), expectedValid ? Is.Empty : Is.Not.Empty);
    }

    [Test, Description("An absent nullable value has nothing to compare, so GreaterThan rejects it rather than quietly passing.")]
    public void ShouldRejectAnAbsentNullableValueForGreaterThan()
    {
        SampleModel absent = new SampleModel() with { OptionalCount = null };
        SampleModel present = new SampleModel() with { OptionalCount = 1 };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(MessagesFrom(v => v.On(x => x.OptionalCount).GreaterThan(0), absent), Is.Not.Empty);
            Assert.That(MessagesFrom(v => v.On(x => x.OptionalCount).GreaterThan(0), present), Is.Empty);
        }
    }

    [TestCase(-3, false, Description = "Below a negative bound still fails — the geography elevation rule uses a bound of -2.")]
    [TestCase(-2, true, Description = "The bound itself passes, which is what distinguishes this from GreaterThan.")]
    [TestCase(7, true, Description = "Comfortably above the bound.")]
    public void ShouldCompareWithGreaterThanOrEqualTo(int count, bool expectedValid)
    {
        SampleModel model = new SampleModel() with { Count = count };

        Assert.That(MessagesFrom(v => v.On(x => x.Count).GreaterThanOrEqualTo(-2), model), expectedValid ? Is.Empty : Is.Not.Empty);
    }

    [TestCase(9, false, Description = "Below the lower bound fails.")]
    [TestCase(10, true, Description = "The lower bound itself is included.")]
    [TestCase(600, true, Description = "The upper bound itself is included.")]
    [TestCase(601, false, Description = "Above the upper bound fails.")]
    public void ShouldIncludeBothEndsOfInclusiveBetween(int count, bool expectedValid)
    {
        SampleModel model = new SampleModel() with { Count = count };

        Assert.That(MessagesFrom(v => v.On(x => x.Count).InclusiveBetween(10, 600), model), expectedValid ? Is.Empty : Is.Not.Empty);
    }

    [Test, Description("InclusiveBetween works on decimals too, which is what the biome movement cost rule needs.")]
    public void ShouldApplyInclusiveBetweenToDecimals()
    {
        SampleModel tooLow = new SampleModel() with { Cost = 0.001m };
        SampleModel atLowerBound = new SampleModel() with { Cost = 0.01m };
        SampleModel tooHigh = new SampleModel() with { Cost = 100m };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(MessagesFrom(v => v.On(x => x.Cost).InclusiveBetween(0.01m, 99.99m), tooLow), Is.Not.Empty);
            Assert.That(MessagesFrom(v => v.On(x => x.Cost).InclusiveBetween(0.01m, 99.99m), atLowerBound), Is.Empty);
            Assert.That(MessagesFrom(v => v.On(x => x.Cost).InclusiveBetween(0.01m, 99.99m), tooHigh), Is.Not.Empty);
        }
    }

    [TestCase(null, true, Description = "A null string has no length to exceed, so it passes — presence is NotEmpty's job.")]
    [TestCase("abc", true, Description = "Shorter than the limit passes.")]
    [TestCase("abcde", true, Description = "Exactly the limit passes.")]
    [TestCase("abcdef", false, Description = "One character over the limit fails.")]
    public void ShouldApplyMaximumLength(string? optionalName, bool expectedValid)
    {
        SampleModel model = new SampleModel() with { OptionalName = optionalName };

        Assert.That(MessagesFrom(v => v.On(x => x.OptionalName).MaximumLength(5), model), expectedValid ? Is.Empty : Is.Not.Empty);
    }

    [TestCase(null, true, Description = "A null string is skipped rather than failed, matching MaximumLength.")]
    [TestCase("1234567", false, Description = "Seven characters is under the eight-character password minimum.")]
    [TestCase("12345678", true, Description = "Exactly the minimum passes.")]
    public void ShouldApplyMinimumLength(string? optionalName, bool expectedValid)
    {
        SampleModel model = new SampleModel() with { OptionalName = optionalName };

        Assert.That(MessagesFrom(v => v.On(x => x.OptionalName).MinimumLength(8), model), expectedValid ? Is.Empty : Is.Not.Empty);
    }

    [TestCase(null, true, Description = "A null string is skipped rather than failed.")]
    [TestCase("#abc", true, Description = "Three-digit hex passes the biome colour pattern.")]
    [TestCase("#AABBCC", true, Description = "Six-digit hex passes.")]
    [TestCase("abc", false, Description = "No leading hash fails.")]
    [TestCase("#abcd", false, Description = "Four digits is neither of the accepted lengths.")]
    public void ShouldApplyMatches(string? optionalName, bool expectedValid)
    {
        SampleModel model = new SampleModel() with { OptionalName = optionalName };

        string[] messages = MessagesFrom(v => v.On(x => x.OptionalName).Matches("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$"), model);

        Assert.That(messages, expectedValid ? Is.Empty : Is.Not.Empty);
    }

    [TestCase(null, true, Description = "A null address is skipped, which is why the register-user rule pairs it with a When.")]
    [TestCase("someone@example.com", true, Description = "An ordinary address passes.")]
    [TestCase("a@b", true, Description = "The check is deliberately loose — only sending mail proves an address works.")]
    [TestCase("no-at-sign", false, Description = "No '@' at all fails.")]
    [TestCase("@example.com", false, Description = "Nothing before the '@' fails.")]
    [TestCase("someone@", false, Description = "Nothing after the '@' fails.")]
    [TestCase("a@b@c", false, Description = "More than one '@' fails.")]
    public void ShouldApplyEmailAddress(string? optionalName, bool expectedValid)
    {
        SampleModel model = new SampleModel() with { OptionalName = optionalName };

        Assert.That(MessagesFrom(v => v.On(x => x.OptionalName).EmailAddress(), model), expectedValid ? Is.Empty : Is.Not.Empty);
    }

    [Test, Description("IsInEnum rejects a number that names no member, which is the case an API boundary actually lets through.")]
    public void ShouldRejectAnUndeclaredEnumValue()
    {
        SampleModel declared = new SampleModel() with { Mode = SampleMode.Second };
        SampleModel undeclared = new SampleModel() with { Mode = (SampleMode)99 };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(MessagesFrom(v => v.On(x => x.Mode).IsInEnum(), declared), Is.Empty);
            Assert.That(MessagesFrom(v => v.On(x => x.Mode).IsInEnum(), undeclared), Is.Not.Empty);
        }
    }

    [Test, Description("Must runs the predicate it is given and fails when it answers false.")]
    public void ShouldApplyMust()
    {
        SampleModel model = new SampleModel() with { Items = [1, 1, 2] };

        string[] messages = MessagesFrom(
            v => v.On(x => x.Items).Must(items => items is not null && items.Distinct().Count() == items.Count).WithMessage("Duplicate Player Ids are not allowed."),
            model);

        Assert.That(messages, Is.EqualTo(new[] { "Duplicate Player Ids are not allowed." }));
    }

    [Test, Description("The generated NotEmpty message names the property in words, which is the one default the ported validators still rely on.")]
    public void ShouldGenerateTheNotEmptyMessageFromThePropertyName()
    {
        SampleModel model = new SampleModel() with { OptionalName = null };

        Assert.That(MessagesFrom(v => v.On(x => x.OptionalName).NotEmpty(), model), Is.EqualTo(new[] { "'Optional Name' must not be empty." }));
    }
}
