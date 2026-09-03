using AscendantEmpire.Application;
using AscendantEmpire.Application.Common.Behaviours;
using AscendantEmpire.Application.Common.Validation;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Common.Behaviours;

/// <summary>
/// A request used only to close the pipeline's open generics, so these tests probe the wiring rather than any one feature.
/// </summary>
internal record ProbeRequest(int GameId = 1) : IRequest<ServiceResult<int>>;

/// <summary>
/// A validator the assembly scan is expected to find and register.
/// </summary>
internal class ProbeRequestValidator : Validator<ProbeRequest>
{
    public ProbeRequestValidator()
    {
        RuleFor(x => x.GameId).GreaterThan(0).WithMessage("Game Id is required.");
    }
}

/// <summary>
/// An abstract validator, which the scan must skip — it cannot be constructed.
/// </summary>
internal abstract class AbstractProbeValidator : Validator<ProbeRequest>;

/// <summary>
/// An open generic validator, which the scan must skip — it has no type to validate until it is closed.
/// </summary>
internal class OpenGenericProbeValidator<T> : Validator<T>;

/// <summary>
/// Pins how the validation pipeline is wired up: which validators the scan finds, and which requests the behaviour is applied to.
/// </summary>
/// <remarks>
/// <para>These are the failures that would otherwise be silent. A validator the scan misses does not fail loudly — the request simply goes unchecked; and a
/// behaviour registered twice would double every message a user sees.</para>
/// </remarks>
[TestFixture]
internal class ValidationWiringTests
{
    [Test, Description("The scan finds an ordinary validator and registers it against the type it validates, so a new validator needs no wiring.")]
    public void ShouldRegisterValidatorsFoundByTheAssemblyScan()
    {
        ServiceCollection services = new();
        services.AddValidatorsFromAssembly(typeof(ProbeRequestValidator).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.That(provider.GetServices<IValidator<ProbeRequest>>(), Has.One.TypeOf<ProbeRequestValidator>());
    }

    [Test, Description("Abstract and open generic validators cannot be constructed as they stand, so the scan must pass over them rather than fail.")]
    public void ShouldSkipValidatorsThatCannotBeConstructed()
    {
        ServiceCollection services = new();
        services.AddValidatorsFromAssembly(typeof(ProbeRequestValidator).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        // Resolving at all is the assertion for the open generic and the abstract type: had either been registered, constructing the sequence would throw.
        Assert.That(provider.GetServices<IValidator<ProbeRequest>>().ToArray(), Has.Length.EqualTo(1), "Only the concrete validator should be registered.");
    }

    [Test, Description("A validator is a shared singleton, since it holds no state and recompiling its rule selectors per request would be waste.")]
    public void ShouldRegisterValidatorsAsSingletons()
    {
        ServiceCollection services = new();
        services.AddValidatorsFromAssembly(typeof(ProbeRequestValidator).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.That(provider.GetService<IValidator<ProbeRequest>>(), Is.SameAs(provider.GetService<IValidator<ProbeRequest>>()));
    }

    [Test, Description("Exactly one validation behaviour is in the pipeline — a second copy would report every broken rule to the user twice.")]
    public void ShouldRegisterTheValidationBehaviourExactlyOnce()
    {
        ServiceCollection services = new();

        // The same two calls Program.cs makes, in the same order, so this pins what the running API actually composes.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediatrStartup).Assembly));
        services.AddApplicationServices();

        using ServiceProvider provider = services.BuildServiceProvider();

        IPipelineBehavior<ProbeRequest, ServiceResult<int>>[] behaviours = [.. provider.GetServices<IPipelineBehavior<ProbeRequest, ServiceResult<int>>>()];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(behaviours, Has.Length.EqualTo(1));
            Assert.That(behaviours[0], Is.TypeOf<ValidationBehaviour<ProbeRequest, ServiceResult<int>>>());
        }
    }

    [Test, Description("A request answering something other than a service result has nowhere to put a failure, so the behaviour is not applied to it.")]
    public void ShouldNotApplyTheBehaviourToAResponseThatCannotCarryAFailure()
    {
        ServiceCollection services = new();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        using ServiceProvider provider = services.BuildServiceProvider();

        // int is not a ServiceResultBase, so the container should decline to close the behaviour's generics over it rather than throw.
        Assert.That(provider.GetServices<IPipelineBehavior<ProbeRequest, int>>(), Is.Empty);
    }

    [Test, Description("The behaviour resolved from the container really does stop an invalid request, so the wiring and the behaviour agree end to end.")]
    public async Task ShouldStopAnInvalidRequestThroughTheResolvedBehaviourAsync()
    {
        ServiceCollection services = new();
        services.AddValidatorsFromAssembly(typeof(ProbeRequestValidator).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        using ServiceProvider provider = services.BuildServiceProvider();

        IPipelineBehavior<ProbeRequest, ServiceResult<int>> behaviour = provider.GetRequiredService<IPipelineBehavior<ProbeRequest, ServiceResult<int>>>();

        bool handlerWasCalled = false;

        ServiceResult<int> result = await behaviour.Handle(
            new ProbeRequest(GameId: 0),
            _ =>
            {
                handlerWasCalled = true;

                return Task.FromResult(ServiceResult<int>.Succeed(7));
            },
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handlerWasCalled, Is.False);
            Assert.That(result.Errors, Is.EqualTo(new[] { "Game Id is required." }));
        }
    }
}
