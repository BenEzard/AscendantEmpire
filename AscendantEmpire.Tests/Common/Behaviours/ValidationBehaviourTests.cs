using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Behaviours;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Common.Behaviours;

/// <summary>
/// Pins what <see cref="ValidationBehaviour{TRequest, TResponse}"/> does with a request before its handler sees it.
/// </summary>
/// <remarks>
/// <para>The behaviour is constructed directly here rather than resolved from a container, so these tests are about its own decisions. The wiring that
/// decides which requests it is applied to at all is covered in <see cref="ValidationWiringTests"/>.</para>
/// <para>The predecessor of this class had no tests at all, because it lived in the Api project and the test project could not reach it.</para>
/// </remarks>
[TestFixture]
internal class ValidationBehaviourTests
{
    private sealed record SampleRequest(int GameId = 1, int PlayerId = 1);

    private sealed class GameIdValidator : Validator<SampleRequest>
    {
        public GameIdValidator()
        {
            RuleFor(x => x.GameId).GreaterThan(0).WithMessage("Game Id is required.");
        }
    }

    private sealed class PlayerIdValidator : Validator<SampleRequest>
    {
        public PlayerIdValidator()
        {
            RuleFor(x => x.PlayerId).GreaterThan(0).WithMessage("Player Id is required.");
        }
    }

    /// <summary>
    /// A validator whose rule fails with a blank message, standing in for one written without a message to fall back on.
    /// </summary>
    private sealed class BlankMessageValidator : Validator<SampleRequest>
    {
        public BlankMessageValidator()
        {
            RuleFor(x => x.GameId).GreaterThan(0).WithMessage("   ");
        }
    }

    /// <summary>
    /// Tracks whether the pipeline carried on to the handler, and what it answered when it did.
    /// </summary>
    private sealed class HandlerSpy<TResponse>(TResponse response)
    {
        public bool WasCalled { get; private set; }

        public Task<TResponse> HandleAsync(CancellationToken cancel)
        {
            WasCalled = true;

            return Task.FromResult(response);
        }
    }

    [Test, Description("A broken rule stops the request at the pipeline, so the handler never runs against input it was not written to survive.")]
    public async Task ShouldNotCallTheHandlerWhenValidationFailsAsync()
    {
        HandlerSpy<ServiceResult<int>> handler = new(ServiceResult<int>.Succeed(7));
        ValidationBehaviour<SampleRequest, ServiceResult<int>> behaviour = new([new GameIdValidator()]);

        ServiceResult<int> result = await behaviour.Handle(new SampleRequest(GameId: 0), handler.HandleAsync, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.WasCalled, Is.False, "The handler should not be reached once a rule has been broken.");
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError), "GenericError is what the API answers as HTTP 400.");
            Assert.That(result.Errors, Is.EqualTo(new[] { "Game Id is required." }));
        }
    }

    [Test, Description("A valid request reaches the handler untouched and the handler's own response is what comes back.")]
    public async Task ShouldCallTheHandlerWhenValidationPassesAsync()
    {
        HandlerSpy<ServiceResult<int>> handler = new(ServiceResult<int>.Succeed(7));
        ValidationBehaviour<SampleRequest, ServiceResult<int>> behaviour = new([new GameIdValidator()]);

        ServiceResult<int> result = await behaviour.Handle(new SampleRequest(), handler.HandleAsync, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.WasCalled, Is.True);
            Assert.That(result.SuccessValue, Is.EqualTo(7), "The handler's response should be passed straight back.");
        }
    }

    [Test, Description("A request with no validator registered is not treated as invalid — it simply carries on to its handler.")]
    public async Task ShouldCallTheHandlerWhenNoValidatorIsRegisteredAsync()
    {
        HandlerSpy<ServiceResult<int>> handler = new(ServiceResult<int>.Succeed(7));
        ValidationBehaviour<SampleRequest, ServiceResult<int>> behaviour = new([]);

        ServiceResult<int> result = await behaviour.Handle(new SampleRequest(GameId: 0), handler.HandleAsync, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.WasCalled, Is.True, "With nothing declared to check, there is nothing to fail.");
            Assert.That(result.HasSucceeded, Is.True);
        }
    }

    [Test, Description("Every registered validator runs, so one response accounts for everything wrong rather than only what the first validator found.")]
    public async Task ShouldReportFailuresFromEveryValidatorAsync()
    {
        HandlerSpy<ServiceResult<int>> handler = new(ServiceResult<int>.Succeed(7));
        ValidationBehaviour<SampleRequest, ServiceResult<int>> behaviour = new([new GameIdValidator(), new PlayerIdValidator()]);

        ServiceResult<int> result = await behaviour.Handle(new SampleRequest(GameId: 0, PlayerId: 0), handler.HandleAsync, CancellationToken.None);

        Assert.That(result.Errors, Is.EqualTo(new[] { "Game Id is required.", "Player Id is required." }));
    }

    [Test, Description("A blank message would reach the user as an empty bullet, so it is dropped and the request carries on.")]
    public async Task ShouldIgnoreBlankFailureMessagesAsync()
    {
        HandlerSpy<ServiceResult<int>> handler = new(ServiceResult<int>.Succeed(7));
        ValidationBehaviour<SampleRequest, ServiceResult<int>> behaviour = new([new BlankMessageValidator()]);

        ServiceResult<int> result = await behaviour.Handle(new SampleRequest(GameId: 0), handler.HandleAsync, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.WasCalled, Is.True, "With nothing to say, there is nothing to answer the caller with.");
            Assert.That(result.HasSucceeded, Is.True);
        }
    }

    [Test, Description("A handler answering the bare ServiceResultBase gets a failure of that same shape.")]
    public async Task ShouldBuildAFailureForABareServiceResultAsync()
    {
        HandlerSpy<ServiceResultBase> handler = new(ServiceResultBase.Succeed());
        ValidationBehaviour<SampleRequest, ServiceResultBase> behaviour = new([new GameIdValidator()]);

        ServiceResultBase result = await behaviour.Handle(new SampleRequest(GameId: 0), handler.HandleAsync, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(result.Errors, Is.EqualTo(new[] { "Game Id is required." }));
        }
    }

    [Test, Description("A two-argument result gets a real failure of its own type — the shape the reflection this replaced would have thrown on.")]
    public async Task ShouldBuildAFailureForATwoArgumentServiceResultAsync()
    {
        // Built by initializer rather than Succeed, whose two-argument overload widens the success type to int? and so answers a different closed type.
        ServiceResult<int, string> success = new() { StatusCode = ServiceResultStatus.Success, SuccessValue = 7 };

        HandlerSpy<ServiceResult<int, string>> handler = new(success);
        ValidationBehaviour<SampleRequest, ServiceResult<int, string>> behaviour = new([new GameIdValidator()]);

        ServiceResult<int, string> result = await behaviour.Handle(new SampleRequest(GameId: 0), handler.HandleAsync, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.WasCalled, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(result.Errors, Is.EqualTo(new[] { "Game Id is required." }));
        }
    }
}
