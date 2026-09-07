using System.Reflection;

using AscendantEmpire.Application;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Military.Handlers;
using AscendantEmpire.BackendContracts.Military.GetOfficerRanks;

using Microsoft.Extensions.DependencyInjection;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Common.Messaging;

/// <summary>
/// A request the scan is expected to find a handler for in this assembly.
/// </summary>
internal sealed record ScannedRequest : IRequest<ServiceResult<int>>;

/// <summary>
/// The handler the scan must find and register.
/// </summary>
internal sealed class ScannedHandler : IRequestHandler<ScannedRequest, ServiceResult<int>>
{
    public Task<ServiceResult<int>> Handle(ScannedRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(ServiceResult<int>.Succeed(1));
    }
}

/// <summary>
/// An abstract handler for the same request, which the scan must pass over — it cannot be constructed, and registering it would read as a duplicate.
/// </summary>
internal abstract class AbstractScannedHandler : IRequestHandler<ScannedRequest, ServiceResult<int>>
{
    public abstract Task<ServiceResult<int>> Handle(ScannedRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// An open generic handler, which the scan must pass over — it has no request to answer until it is closed.
/// </summary>
internal class OpenGenericScannedHandler<TRequest> : IRequestHandler<TRequest, ServiceResultBase>
    where TRequest : IRequest<ServiceResultBase>
{
    public Task<ServiceResultBase> Handle(TRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(ServiceResultBase.Succeed());
    }
}

/// <summary>
/// A stand-in second handler for a request the Application layer already answers, used to provoke the duplicate check.
/// </summary>
/// <remarks>
/// It claims a request from the Application assembly rather than one declared here, so that it clashes only in the test that registers it by hand and never
/// in the scans of this assembly the other tests do.
/// </remarks>
internal sealed class RivalOfficerRanksHandler : IRequestHandler<GetOfficerRanksRequest, ServiceResult<GetOfficerRanksResult>>
{
    public Task<ServiceResult<GetOfficerRanksResult>> Handle(GetOfficerRanksRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(ServiceResult<GetOfficerRanksResult>.Succeed(new GetOfficerRanksResult { Ranks = [] }));
    }
}

/// <summary>
/// Pins how request handlers are discovered and registered, which is the part a new handler relies on without ever writing a line of wiring.
/// </summary>
/// <remarks>
/// <para>These are the failures that would otherwise be silent or arbitrary. A handler the scan misses does not fail where it was written — the request
/// simply has nowhere to go at runtime; and two handlers claiming one request would leave the answer down to whichever the container registered last.</para>
/// <para>The scan walks this test assembly in several of these, so no two handlers declared anywhere in it may claim the same request. That is the same rule
/// the scan enforces on the Application layer, and the reason the fakes above each have a request of their own.</para>
/// </remarks>
[TestFixture]
internal class MediatorRegistrationTests
{
    /// <summary>
    /// The assembly holding the real handlers, which is what the API scans at startup.
    /// </summary>
    private static Assembly ApplicationAssembly => typeof(ApplicationStartup).Assembly;

    [Test, Description("The scan finds a concrete handler and registers it against the request it answers, so a new handler needs no wiring of its own.")]
    public void ShouldRegisterHandlersFoundByTheAssemblyScan()
    {
        ServiceCollection services = new();
        services.AddMediator(typeof(ScannedHandler).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.That(provider.GetService<IRequestHandler<ScannedRequest, ServiceResult<int>>>(), Is.TypeOf<ScannedHandler>());
    }

    [Test, Description("Abstract and open generic handlers cannot answer a request as they stand, so the scan passes over them rather than registering them.")]
    public void ShouldSkipHandlersThatCannotBeConstructed()
    {
        ServiceCollection services = new();

        // Reaching the assertion at all is half of it: had the abstract handler been registered it would have tripped the duplicate check, and had the open
        // generic been registered the container could not have constructed it.
        services.AddMediator(typeof(ScannedHandler).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.That(provider.GetServices<IRequestHandler<ScannedRequest, ServiceResult<int>>>().ToArray(), Has.Length.EqualTo(1));
    }

    [Test, Description("The scan brings the mediator with it, so registering handlers and being able to dispatch to them are never out of step.")]
    public void ShouldRegisterTheMediator()
    {
        ServiceCollection services = new();
        services.AddMediator(ApplicationAssembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.That(provider.GetService<IMediator>(), Is.TypeOf<Mediator>());
    }

    [Test, Description("Scanning the same assembly twice leaves one registration per request, so a duplicated startup call is harmless rather than fatal.")]
    public void ShouldBeIdempotentOverTheSameAssembly()
    {
        ServiceCollection services = new();
        services.AddMediator(typeof(ScannedHandler).Assembly);
        services.AddMediator(typeof(ScannedHandler).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.That(provider.GetServices<IRequestHandler<ScannedRequest, ServiceResult<int>>>().ToArray(), Has.Length.EqualTo(1));
    }

    [Test, Description("A second handler for a request fails at startup naming both, rather than leaving which one answers down to registration order.")]
    public void ShouldRefuseTwoHandlersForTheSameRequest()
    {
        ServiceCollection services = new();
        services.AddTransient<IRequestHandler<GetOfficerRanksRequest, ServiceResult<GetOfficerRanksResult>>, RivalOfficerRanksHandler>();

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() => services.AddMediator(ApplicationAssembly));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception!.Message, Does.Contain(nameof(RivalOfficerRanksHandler)));
            Assert.That(exception.Message, Does.Contain(nameof(GetOfficerRanksHandler)));
        }
    }

    [Test, Description("Every request the Application layer declares has a handler registered for it, so no endpoint can send one that nothing answers.")]
    public void ShouldRegisterAHandlerForEveryRequestInTheApplicationAssembly()
    {
        ServiceCollection services = new();
        services.AddMediator(ApplicationAssembly);

        // Read off the descriptors rather than resolving each one: a handler's own dependencies — a DbContext factory, a logger — are registered elsewhere,
        // and this is about which requests were wired up, not whether the rest of the container is complete.
        HashSet<Type> registered = [.. services.Select(descriptor => descriptor.ServiceType)];

        List<string> unhandled = [];

        foreach (Type requestType in ApplicationAssembly.GetTypes().Where(IsConcreteRequest))
        {
            foreach (Type responseType in ResponseTypesOf(requestType))
            {
                if (registered.Contains(typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType)) == false)
                {
                    unhandled.Add(requestType.FullName!);
                }
            }
        }

        Assert.That(unhandled, Is.Empty);
    }

    /// <summary>
    /// Whether a type is a request that could actually be sent — abstract bases and open generics are neither dispatched nor handled.
    /// </summary>
    private static bool IsConcreteRequest(Type type)
    {
        return type.IsAbstract == false
            && type.IsGenericTypeDefinition == false
            && ResponseTypesOf(type).Length > 0;
    }

    /// <summary>
    /// The response types a request declares, which is one for every request in the codebase but is not enforced by the interface.
    /// </summary>
    private static Type[] ResponseTypesOf(Type type)
    {
        return [.. type.GetInterfaces()
            .Where(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IRequest<>))
            .Select(candidate => candidate.GetGenericArguments()[0])];
    }
}
