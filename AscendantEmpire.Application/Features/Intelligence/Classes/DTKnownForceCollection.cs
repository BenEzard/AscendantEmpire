using AscendantEmpire.FrontendContracts.Intelligence;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <inheritdoc cref="IKnownForceCollection" />
public class DTKnownForceCollection : IKnownForceCollection
{
    public required IReadOnlyCollection<IKnownForce> KnownForces { get; set; }
}
