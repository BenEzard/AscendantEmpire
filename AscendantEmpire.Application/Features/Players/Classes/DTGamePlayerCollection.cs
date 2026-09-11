using AscendantEmpire.FrontendContracts.Player;

namespace AscendantEmpire.Application.Features.Players.Classes;

/// <inheritdoc cref="IGamePlayerCollection" />
public class DTGamePlayerCollection : IGamePlayerCollection
{
    public required IReadOnlyCollection<IGamePlayer> Players { get; set; }
}
