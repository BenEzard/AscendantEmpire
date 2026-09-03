using AscendantEmpire.Application.Common.Validation;

namespace AscendantEmpire.Tests.Common.Validation;

/// <summary>
/// Pins the chain semantics of <see cref="Validator{T}"/> — how <c>WithMessage</c>, <c>When</c>, <c>RuleForEach</c> and rule ordering behave.
/// </summary>
/// <remarks>
/// <para>These are the behaviours the validators ported off FluentValidation depend on, and the ones that would change a user-facing message or silently
/// stop a rule from firing if they drifted. Nothing here touches a database.</para>
/// </remarks>
[TestFixture]
internal class ValidatorTests
{
    private enum SampleMode
    {
        First = 1,
        Second = 2
    }

    /// <summary>
    /// A stand-in for a request record, built valid so each test can break exactly one thing.
    /// </summary>
    private sealed record SampleModel(
        int GameId = 1,
        string MapName = "Valid",
        string? Email = null,
        SampleMode Mode = SampleMode.First,
        IReadOnlyCollection<int>? PlayerIds = null);

    /// <summary>
    /// Mirrors the campaign map index validators: one message, written after the second operator, so the first keeps its generated one.
    /// </summary>
    private sealed class MapNameValidator : Validator<SampleModel>
    {
        public MapNameValidator()
        {
            RuleFor(x => x.MapName)
                .NotEmpty()
                .MaximumLength(5)
                .WithMessage("Map name is required and must be 5 characters or less.");
        }
    }

    /// <summary>
    /// Mirrors the register-user validator: a chain of two operators gated by a single trailing condition.
    /// </summary>
    private sealed class ConditionalEmailValidator : Validator<SampleModel>
    {
        public ConditionalEmailValidator()
        {
            RuleFor(x => x.Email)
                .MaximumLength(5).WithMessage("Email is too long.")
                .EmailAddress().WithMessage("Email must be a valid email address.")
                .When(x => string.IsNullOrWhiteSpace(x.Email) == false);
        }
    }

    private sealed class PlayerIdsValidator : Validator<SampleModel>
    {
        public PlayerIdsValidator()
        {
            RuleForEach(x => x.PlayerIds)
                .GreaterThan(0)
                .WithMessage("Player Ids must be greater than 0.");
        }
    }

    private sealed class WholeInstanceValidator : Validator<SampleModel>
    {
        public WholeInstanceValidator()
        {
            RuleFor(x => x)
                .Must(model => model.MapName.Length <= model.GameId)
                .WithMessage(model => $"Map Name must be no longer than {model.GameId} characters.");
        }
    }

    /// <summary>
    /// Three independent rules, so a test can break all three at once and inspect what comes back.
    /// </summary>
    private sealed class ThreeRuleValidator : Validator<SampleModel>
    {
        public ThreeRuleValidator()
        {
            RuleFor(x => x.GameId).GreaterThan(0).WithMessage("Game Id is required.");
            RuleFor(x => x.MapName).NotEmpty().WithMessage("Map Name is required.");
            RuleFor(x => x.Mode).IsInEnum().WithMessage("Mode is not valid.");
        }
    }

    private sealed class CustomRuleValidator : Validator<SampleModel>
    {
        public CustomRuleValidator()
        {
            RuleFor(x => x).Custom((model, context) =>
            {
                if (model.GameId <= 0)
                {
                    context.AddFailure("Game Id must be positive.");
                }

                if (string.IsNullOrEmpty(model.MapName))
                {
                    context.AddFailure("Map Name must be supplied.");
                }
            });
        }
    }

    [Test, Description("WithMessage binds to the operator immediately before it, so the untouched NotEmpty still reports the generated message.")]
    public void ShouldUseGeneratedMessageForTheOperatorWithMessageDidNotBindTo()
    {
        SampleModel model = new SampleModel() with { MapName = string.Empty };

        ValidationResult result = new MapNameValidator().Validate(model);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False, "An empty map name breaks the NotEmpty rule.");
            Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.EqualTo("'Map Name' must not be empty."));
        }
    }

    [Test, Description("The explicit message belongs to MaximumLength alone, which is the operator it was written after.")]
    public void ShouldUseExplicitMessageForTheOperatorWithMessageBoundTo()
    {
        SampleModel model = new SampleModel() with { MapName = "FarTooLong" };

        ValidationResult result = new MapNameValidator().Validate(model);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False, "A ten-character map name breaks the MaximumLength(5) rule.");
            Assert.That(result.Errors.Select(e => e.ErrorMessage), Is.EqualTo(new[] { "Map name is required and must be 5 characters or less." }));
        }
    }

    [Test, Description("A trailing When gates every operator in the chain, not just the one it was written beside.")]
    public void ShouldSkipEveryOperatorInTheChainWhenTheConditionIsFalse()
    {
        SampleModel model = new SampleModel() with { Email = null };

        ValidationResult result = new ConditionalEmailValidator().Validate(model);

        Assert.That(result.IsValid, Is.True, "With no email supplied the condition is false, so neither the length nor the format rule should run.");
    }

    [Test, Description("With the condition met, every operator in the chain runs and each reports separately.")]
    public void ShouldRunEveryOperatorInTheChainWhenTheConditionIsTrue()
    {
        // Seven characters and no '@', so it breaks both the length rule and the format rule.
        SampleModel model = new SampleModel() with { Email = "abcdefg" };

        ValidationResult result = new ConditionalEmailValidator().Validate(model);

        Assert.That(result.Errors.Select(e => e.ErrorMessage), Is.EqualTo(new[] { "Email is too long.", "Email must be a valid email address." }));
    }

    [Test, Description("A null collection is nothing to check rather than a failure — presence is a separate rule's job.")]
    public void ShouldSkipRuleForEachWhenTheCollectionIsNull()
    {
        SampleModel model = new SampleModel() with { PlayerIds = null };

        ValidationResult result = new PlayerIdsValidator().Validate(model);

        Assert.That(result.IsValid, Is.True, "RuleForEach over a null collection should not fire at all.");
    }

    [Test, Description("RuleForEach checks each element in turn, so a collection with two bad entries reports two failures.")]
    public void ShouldReportOneFailurePerOffendingElement()
    {
        SampleModel model = new SampleModel() with { PlayerIds = [1, 0, -3] };

        ValidationResult result = new PlayerIdsValidator().Validate(model);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Errors, Has.Count.EqualTo(2), "Only the zero and the negative id break the rule.");
            Assert.That(result.Errors.Select(e => e.ErrorMessage), Is.All.EqualTo("Player Ids must be greater than 0."));
        }
    }

    [Test, Description("A rule over the whole instance can compare properties against each other, and its message can read from the instance.")]
    public void ShouldSupportRulesOverTheWholeInstance()
    {
        SampleModel model = new SampleModel() with { GameId = 3, MapName = "TooLong" };

        ValidationResult result = new WholeInstanceValidator().Validate(model);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False, "A seven-character name exceeds the three characters the instance allows.");
            Assert.That(result.Errors.Select(e => e.ErrorMessage), Is.EqualTo(new[] { "Map Name must be no longer than 3 characters." }));
        }
    }

    [Test, Description("Rules do not short-circuit, so one call reports everything wrong rather than only the first thing.")]
    public void ShouldReportEveryBrokenRuleInDeclarationOrder()
    {
        SampleModel model = new SampleModel() with { GameId = 0, MapName = string.Empty, Mode = (SampleMode)99 };

        ValidationResult result = new ThreeRuleValidator().Validate(model);

        Assert.That(
            result.Errors.Select(e => e.ErrorMessage),
            Is.EqualTo(new[] { "Game Id is required.", "Map Name is required.", "Mode is not valid." }));
    }

    [Test, Description("A Custom rule writes its own failures, so one rule can report several distinct problems at once.")]
    public void ShouldAllowOneCustomRuleToReportSeveralFailures()
    {
        SampleModel model = new SampleModel() with { GameId = 0, MapName = string.Empty };

        ValidationResult result = new CustomRuleValidator().Validate(model);

        Assert.That(result.Errors.Select(e => e.ErrorMessage), Is.EqualTo(new[] { "Game Id must be positive.", "Map Name must be supplied." }));
    }

    [Test, Description("A Custom rule that finds nothing wrong records nothing, so it cannot fail an otherwise valid instance.")]
    public void ShouldReportNothingWhenACustomRuleFindsNoProblem()
    {
        ValidationResult result = new CustomRuleValidator().Validate(new SampleModel());

        Assert.That(result.IsValid, Is.True, "A valid instance breaks neither of the custom checks.");
    }

    [Test, Description("The positive control: without it, a rule made accidentally unsatisfiable would still pass every test above.")]
    public void ShouldSucceedWhenNothingIsBroken()
    {
        SampleModel model = new SampleModel() with { PlayerIds = [1, 2] };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(new MapNameValidator().Validate(model).IsValid, Is.True, "A five-character name satisfies both operators.");
            Assert.That(new PlayerIdsValidator().Validate(model).IsValid, Is.True, "Every player id is positive.");
            Assert.That(new ThreeRuleValidator().Validate(model).IsValid, Is.True, "Every field of the valid model satisfies its rule.");
        }
    }

    [Test, Description("A valid instance reports an empty error list, so callers reading Errors never have to null-check it.")]
    public void ShouldReportAnEmptyErrorListWhenValid()
    {
        ValidationResult result = new ThreeRuleValidator().Validate(new SampleModel());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Errors, Is.Not.Null);
            Assert.That(result.Errors, Is.Empty);
        }
    }
}
