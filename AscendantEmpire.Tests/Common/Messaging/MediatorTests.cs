using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;

using Microsoft.Extensions.DependencyInjection;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Common.Messaging;

/// <summary>
/// Pins what <see cref="IMediator"/> does with a request: which handler it reaches, and what runs around it on the way.
/// </summary>
/// <remarks>
/// <para>Handlers are registered by hand here rather than found by a scan, so these tests are about dispatch alone. The scan that finds them in a real
/// assembly is covered in <see cref="MediatorRegistrationTests"/>.</para>
/// <para>These replace what the MediatR package used to guarantee. Nothing here asserts an implementation detail of the dispatcher — every one of them is
/// a promise the handlers and behaviours across the Application layer were already written against.</para>
/// </remarks>
[TestFixture]
internal class MediatorTests
{
    /// <summary>
    /// A request whose handler simply echoes what it was given, so a test can tell the request really reached it.
    /// </summary>
    private sealed record EchoRequest(int Value) : IRequest<ServiceResult<int>>;

    private sealed class EchoHandler : IRequestHandler<EchoRequest, ServiceResult<int>>
    {
        public Task<ServiceResult<int>> Handle(EchoRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(ServiceResult<int>.Succeed(request.Value));
        }
    }

    /// <summary>
    /// A request nothing handles, for the case where a handler was never registered.
    /// </summary>
    private sealed record UnhandledRequest : IRequest<ServiceResult<int>>;

    /// <summary>
    /// A request that answers the base result type, as the seeding steps do when they dispatch through an <c>IRequest&lt;ServiceResultBase&gt;</c>.
    /// </summary>
    private sealed record BaseTypedRequest : IRequest<ServiceResultBase>;

    private sealed class BaseTypedHandler : IRequestHandler<BaseTypedRequest, ServiceResultBase>
    {
        public Task<ServiceResultBase> Handle(BaseTypedRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult<ServiceResultBase>(new ServiceResultBase { StatusCode = ServiceResultStatus.Success });
        }
    }

    /// <summary>
    /// Records the order the pipeline ran in, shared by every behaviour and handler in one test.
    /// </summary>
    private sealed class CallLog
    {
        public List<string> Entries { get; } = [];
    }

    /// <summary>
    /// A behaviour that notes when it was entered and left, so the nesting of the pipeline can be read off the log.
    /// </summary>
    private sealed class RecordingBehaviour<TRequest, TResponse>(CallLog log, string name) : IPipelineBehaviour<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            log.Entries.Add($"{name}:before");

            TResponse response = await next(cancellationToken);

            log.Entries.Add($"{name}:after");

            return response;
        }
    }

    /// <summary>
    /// A behaviour that answers on its own rather than calling the rest of the pipeline, as the validation behaviour does for a broken rule.
    /// </summary>
    private sealed class ShortCircuitBehaviour<TRequest, TResponse>(TResponse response) : IPipelineBehaviour<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }

    [Test, Description("A request reaches the handler registered for it and its answer comes back, which is the whole of what a call site relies on.")]
    public async Task ShouldDispatchARequestToItsHandlerAsync()
    {
        using ServiceProvider provider = BuildProvider(services => services.AddTransient<IRequestHandler<EchoRequest, ServiceResult<int>>, EchoHandler>());

        ServiceResult<int> result = await provider.GetRequiredService<IMediator>().Send(new EchoRequest(42));

        Assert.That(result.SuccessValue, Is.EqualTo(42));
    }

    [Test, Description("A request dispatched through a base-typed reference still finds its handler, which is how the seeding steps send their upserts.")]
    public async Task ShouldDispatchARequestHeldThroughItsBaseResponseTypeAsync()
    {
        using ServiceProvider provider = BuildProvider(
            services => services.AddTransient<IRequestHandler<BaseTypedRequest, ServiceResultBase>, BaseTypedHandler>());

        // Deliberately typed as the interface, not the record: this is the shape DatabaseSeedExecutor.RunStepAsync takes its request in.
        IRequest<ServiceResultBase> request = new BaseTypedRequest();

        ServiceResultBase result = await provider.GetRequiredService<IMediator>().Send(request);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success));
    }

    [Test, Description("A request with no handler fails loudly and names itself, rather than returning null for a caller to trip over later.")]
    public void ShouldThrowWhenNoHandlerIsRegisteredForTheRequest()
    {
        using ServiceProvider provider = BuildProvider(_ => { });

        IMediator mediator = provider.GetRequiredService<IMediator>();

        InvalidOperationException? exception = Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(new UnhandledRequest()));

        Assert.That(exception!.Message, Does.Contain(nameof(UnhandledRequest)));
    }

    [Test, Description("A null request is refused at the call rather than surfacing as a null reference from somewhere inside the pipeline.")]
    public void ShouldThrowWhenTheRequestIsNull()
    {
        using ServiceProvider provider = BuildProvider(_ => { });

        IMediator mediator = provider.GetRequiredService<IMediator>();

        Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Send<ServiceResult<int>>(null!));
    }

    [Test, Description("Behaviours nest in registration order with the first registered outermost, so a validation step added first really does run first.")]
    public async Task ShouldRunBehavioursInRegistrationOrderAroundTheHandlerAsync()
    {
        CallLog log = new();

        using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddSingleton(log);
            services.AddTransient<IRequestHandler<EchoRequest, ServiceResult<int>>, EchoHandler>();
            services.AddTransient<IPipelineBehaviour<EchoRequest, ServiceResult<int>>>(
                _ => new RecordingBehaviour<EchoRequest, ServiceResult<int>>(log, "outer"));
            services.AddTransient<IPipelineBehaviour<EchoRequest, ServiceResult<int>>>(
                _ => new RecordingBehaviour<EchoRequest, ServiceResult<int>>(log, "inner"));
        });

        await provider.GetRequiredService<IMediator>().Send(new EchoRequest(1));

        Assert.That(log.Entries, Is.EqualTo(new[] { "outer:before", "inner:before", "inner:after", "outer:after" }));
    }

    [Test, Description("A behaviour that answers without calling next stops the pipeline there, which is how a broken rule keeps the handler from running.")]
    public async Task ShouldNotReachTheHandlerWhenABehaviourShortCircuitsAsync()
    {
        CallLog log = new();

        using ServiceProvider provider = BuildProvider(services =>
        {
            services.AddSingleton(log);
            services.AddTransient<IRequestHandler<EchoRequest, ServiceResult<int>>, EchoHandler>();
            services.AddTransient<IPipelineBehaviour<EchoRequest, ServiceResult<int>>>(
                _ => new ShortCircuitBehaviour<EchoRequest, ServiceResult<int>>(ServiceResult<int>.Succeed(-1)));

            // Registered after the short-circuiting one, so it sits inside it and must never be entered.
            services.AddTransient<IPipelineBehaviour<EchoRequest, ServiceResult<int>>>(
                _ => new RecordingBehaviour<EchoRequest, ServiceResult<int>>(log, "inner"));
        });

        ServiceResult<int> result = await provider.GetRequiredService<IMediator>().Send(new EchoRequest(42));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue, Is.EqualTo(-1));
            Assert.That(log.Entries, Is.Empty);
        }
    }

    [Test, Description("The cancellation token given to Send is the one the handler receives, so a cancelled request is not left running.")]
    public async Task ShouldPassTheCallersCancellationTokenThroughToTheHandlerAsync()
    {
        CancellationToken observed = default;

        using ServiceProvider provider = BuildProvider(services => services.AddTransient<IRequestHandler<TokenRequest, ServiceResult<int>>>(
            _ => new TokenCapturingHandler(token => observed = token)));

        using CancellationTokenSource cancellation = new();

        await provider.GetRequiredService<IMediator>().Send(new TokenRequest(), cancellation.Token);

        Assert.That(observed, Is.EqualTo(cancellation.Token));
    }

    /// <summary>
    /// The request the token test dispatches.
    /// </summary>
    /// <remarks>
    /// Its own request type rather than sharing <see cref="EchoRequest"/>, because no two handlers in this assembly may claim the same request — the scan
    /// in <see cref="MediatorRegistrationTests"/> walks this assembly and would read a second one as a duplicate.
    /// </remarks>
    private sealed record TokenRequest : IRequest<ServiceResult<int>>;

    /// <summary>
    /// A handler that reports the token it was handed, for the test that the caller's token survives the pipeline.
    /// </summary>
    private sealed class TokenCapturingHandler(Action<CancellationToken> capture) : IRequestHandler<TokenRequest, ServiceResult<int>>
    {
        public Task<ServiceResult<int>> Handle(TokenRequest request, CancellationToken cancellationToken)
        {
            capture(cancellationToken);

            return Task.FromResult(ServiceResult<int>.Succeed(1));
        }
    }

    /// <summary>
    /// Builds a container holding the mediator and whatever the test registers alongside it.
    /// </summary>
    /// <remarks>
    /// The mediator is registered directly rather than through <see cref="MediatorRegistration.AddMediator"/>, so the handlers in play are only the ones
    /// <paramref name="register"/> adds and one test's wiring cannot leak into another's.
    /// </remarks>
    private static ServiceProvider BuildProvider(Action<IServiceCollection> register)
    {
        ServiceCollection services = new();

        services.AddTransient<IMediator, Mediator>();

        register(services);

        return services.BuildServiceProvider();
    }
}
