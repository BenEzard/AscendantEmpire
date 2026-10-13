using System.Text.RegularExpressions;

using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Behaviours;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Common.Behaviours;

/// <summary>
/// Pins what <see cref="ExceptionHandlingBehaviour{TRequest, TResponse}"/> does with an exception a handler let go.
/// </summary>
/// <remarks>
/// <para>The behaviour is constructed directly rather than resolved from a container, so these tests are about its own decisions. Nothing here needs a
/// database: a delegate standing in for the handler is the whole of the system under test.</para>
/// <para>What these protect is the reason the per-handler catch-alls were removed at all. If the behaviour stopped answering 500, every unexpected exception
/// would silently become a 400 again; if the correlation id stopped reaching the rendered log message, a reference quoted by a user would find nothing in the
/// log file.</para>
/// </remarks>
[TestFixture]
internal class ExceptionHandlingBehaviourTests
{
    /// <summary>
    /// The sentinel an unhandled exception is raised with, chosen so a leak into a response is unmistakable.
    /// </summary>
    private const string _SENSITIVE_MESSAGE = "Login failed for user 'sa' against server 'db-prod-01'.";

    private sealed record SampleRequest(int GameId = 1);

    private static ExceptionHandlingBehaviour<SampleRequest, TResponse> BuildBehaviour<TResponse>(
        CapturingLogger<ExceptionHandlingBehaviour<SampleRequest, TResponse>> logger,
        string environmentName = StubHostEnvironment.PRODUCTION)
        where TResponse : ServiceResultBase, new()
        => new(logger, new StubHostEnvironment(environmentName));

    /// <summary>
    /// The correlation id out of the one sentence the caller is given, or null when the sentence does not carry one.
    /// </summary>
    private static string? ReadCorrelationId(string error)
    {
        Match match = Regex.Match(error, @"Quote reference ([0-9a-f]+) when reporting it\.");

        return match.Success ? match.Groups[1].Value : null;
    }

    [Test, Description("An exception a handler let go is answered as a 500, not the 400 a bare result defaults to.")]
    public async Task ShouldAnswerAnUnhandledExceptionWithAnInternalServerErrorAsync()
    {
        CapturingLogger<ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>>> logger = new();
        ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>> behaviour = BuildBehaviour<ServiceResult<int>>(logger);

        ServiceResult<int> result = await behaviour.Handle(
            new SampleRequest(),
            _ => throw new InvalidOperationException(_SENSITIVE_MESSAGE),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError), "InternalServerError is what the API answers as HTTP 500.");
            Assert.That(result.Errors, Has.Count.EqualTo(1), "Outside Development the caller gets the reference and nothing else.");
        }
    }

    [Test, Description("The reference the caller is told to quote is the same id written to the log, or the exception could never be found from it.")]
    public async Task ShouldWriteTheCorrelationIdItGaveTheCallerToTheLogAsync()
    {
        CapturingLogger<ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>>> logger = new();
        ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>> behaviour = BuildBehaviour<ServiceResult<int>>(logger);

        ServiceResult<int> result = await behaviour.Handle(
            new SampleRequest(),
            _ => throw new InvalidOperationException(_SENSITIVE_MESSAGE),
            CancellationToken.None);

        string? correlationId = ReadCorrelationId(result.Errors.First());
        CapturedLogEntry entry = logger.Entries.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(correlationId, Is.Not.Null, "The caller has to be given something to quote.");
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception, Is.TypeOf<InvalidOperationException>(), "The exception itself belongs in the log, with its stack trace.");
            Assert.That(entry.Message, Does.Contain(correlationId!), "The id has to reach the rendered message; the Serilog template renders nothing else.");
            Assert.That(entry.Message, Does.Contain(nameof(SampleRequest)), "Naming the request is what says where the failure came from.");
        }
    }

    [Test, Description("Outside Development the exception's own message is withheld, because an unanticipated one may name a server, table or credential.")]
    public async Task ShouldNotLeakTheExceptionMessageOutsideDevelopmentAsync()
    {
        CapturingLogger<ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>>> logger = new();
        ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>> behaviour = BuildBehaviour<ServiceResult<int>>(logger, StubHostEnvironment.PRODUCTION);

        ServiceResult<int> result = await behaviour.Handle(
            new SampleRequest(),
            _ => throw new InvalidOperationException(_SENSITIVE_MESSAGE),
            CancellationToken.None);

        Assert.That(string.Join(" ", result.Errors), Does.Not.Contain(_SENSITIVE_MESSAGE));
    }

    [Test, Description("In Development the exception's type and message are added, since losing them would be a step backwards for whoever is debugging.")]
    public async Task ShouldIncludeTheExceptionMessageInDevelopmentAsync()
    {
        CapturingLogger<ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>>> logger = new();
        ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>> behaviour = BuildBehaviour<ServiceResult<int>>(logger, StubHostEnvironment.DEVELOPMENT);

        ServiceResult<int> result = await behaviour.Handle(
            new SampleRequest(),
            _ => throw new InvalidOperationException(_SENSITIVE_MESSAGE),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Errors, Has.Count.EqualTo(2));
            Assert.That(result.Errors.Last(), Is.EqualTo($"{nameof(InvalidOperationException)}: {_SENSITIVE_MESSAGE}"));
        }
    }

    [Test, Description("A cancelled request is not a fault, so it propagates rather than being logged as an error and answered as a 500.")]
    public void ShouldLetACancelledRequestPropagate()
    {
        CapturingLogger<ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>>> logger = new();
        ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>> behaviour = BuildBehaviour<ServiceResult<int>>(logger);

        using CancellationTokenSource source = new();
        source.Cancel();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                async () => await behaviour.Handle(new SampleRequest(), token => Task.FromCanceled<ServiceResult<int>>(token), source.Token),
                Throws.InstanceOf<OperationCanceledException>());

            Assert.That(logger.Entries, Is.Empty, "Nothing went wrong, so nothing should reach the log.");
        }
    }

    [Test, Description("A cancellation raised while our own token is still live did not come from this request, so it is treated as the fault it is.")]
    public async Task ShouldTreatACancellationOnALiveTokenAsAFailureAsync()
    {
        CapturingLogger<ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>>> logger = new();
        ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>> behaviour = BuildBehaviour<ServiceResult<int>>(logger);

        // An unrelated token that was already cancelled, standing in for an inner operation timing out on one of its own.
        using CancellationTokenSource unrelated = new();
        unrelated.Cancel();

        ServiceResult<int> result = await behaviour.Handle(
            new SampleRequest(),
            _ => throw new OperationCanceledException(unrelated.Token),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError));
            Assert.That(logger.Entries, Has.Count.EqualTo(1), "A cancellation nobody asked for is worth a log entry.");
        }
    }

    [Test, Description("A handler that returns normally is passed straight back, so the behaviour costs a succeeding request nothing.")]
    public async Task ShouldPassASuccessfulResponseStraightBackAsync()
    {
        CapturingLogger<ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>>> logger = new();
        ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>> behaviour = BuildBehaviour<ServiceResult<int>>(logger);

        ServiceResult<int> result = await behaviour.Handle(
            new SampleRequest(),
            _ => Task.FromResult(ServiceResult<int>.Succeed(7)),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue, Is.EqualTo(7));
            Assert.That(logger.Entries, Is.Empty);
        }
    }

    [Test, Description("A failure a handler returned deliberately is left alone — only a thrown exception is the behaviour's business.")]
    public async Task ShouldLeaveADeliberateFailureUntouchedAsync()
    {
        CapturingLogger<ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>>> logger = new();
        ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>> behaviour = BuildBehaviour<ServiceResult<int>>(logger);

        ServiceResult<int> result = await behaviour.Handle(
            new SampleRequest(),
            _ => Task.FromResult(ServiceResult<int>.NotFound("No such game.")),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound), "A 404 the handler chose must not be rewritten as a 500.");
            Assert.That(result.Errors, Is.EqualTo(new[] { "No such game." }));
        }
    }

    [Test, Description("A handler answering the bare ServiceResultBase gets a failure of that same shape.")]
    public async Task ShouldBuildAFailureForABareServiceResultAsync()
    {
        CapturingLogger<ExceptionHandlingBehaviour<SampleRequest, ServiceResultBase>> logger = new();
        ExceptionHandlingBehaviour<SampleRequest, ServiceResultBase> behaviour = BuildBehaviour<ServiceResultBase>(logger);

        ServiceResultBase result = await behaviour.Handle(
            new SampleRequest(),
            _ => throw new InvalidOperationException(_SENSITIVE_MESSAGE),
            CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError));
    }

    [Test, Description("A two-argument result gets a real failure of its own type, the shape an object initializer is used precisely to get right.")]
    public async Task ShouldBuildAFailureForATwoArgumentServiceResultAsync()
    {
        CapturingLogger<ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int, string>>> logger = new();
        ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int, string>> behaviour = BuildBehaviour<ServiceResult<int, string>>(logger);

        ServiceResult<int, string> result = await behaviour.Handle(
            new SampleRequest(),
            _ => throw new InvalidOperationException(_SENSITIVE_MESSAGE),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.TypeOf<ServiceResult<int, string>>());
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.InternalServerError));
        }
    }

    [Test, Description("Each failure gets its own reference, so two users quoting one id cannot land on the same log entry.")]
    public async Task ShouldGiveEachFailureItsOwnCorrelationIdAsync()
    {
        CapturingLogger<ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>>> logger = new();
        ExceptionHandlingBehaviour<SampleRequest, ServiceResult<int>> behaviour = BuildBehaviour<ServiceResult<int>>(logger);

        ServiceResult<int> first = await behaviour.Handle(
            new SampleRequest(), _ => throw new InvalidOperationException("one"), CancellationToken.None);
        ServiceResult<int> second = await behaviour.Handle(
            new SampleRequest(), _ => throw new InvalidOperationException("two"), CancellationToken.None);

        Assert.That(ReadCorrelationId(first.Errors.First()), Is.Not.EqualTo(ReadCorrelationId(second.Errors.First())));
    }
}
