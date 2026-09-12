using AscendantEmpire.FrontendContracts.Intelligence;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <inheritdoc cref="IKnownStructureCollection" />
public class DTKnownStructureCollection : IKnownStructureCollection
{
    public required IReadOnlyCollection<IKnownStructure> KnownStructures { get; set; }
}
