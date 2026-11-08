using AscendantEmpire.Application.Data;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <summary>
/// Records what a Game's Players know of its MapResources without having to look for it.
/// </summary>
/// <remarks>
/// Discovery proper — a Player's likelihood, the tech switches that turn a type on, and a unit searching a tile — is the rest of the feature and lives
/// elsewhere. This covers only what is known before anyone has moved. See "Discovery" in <c>docs/design/campaign-map/map-resources.md</c>.
/// </remarks>
internal interface IPlayerMapResourceDiscoveryService
{
    /// <summary>
    /// Gives every Player the Game's instances of types that are discoverable by default.
    /// </summary>
    /// <remarks>
    /// Reads and writes through the caller's context, so it sees the Players Game setup has just added rather than needing them committed first.
    /// </remarks>
    /// <param name="playerIds">The Game's Players. None is a success with nothing to do.</param>
    /// <returns>How many rows were written.</returns>
    Task<ServiceResult<int>> SeedDefaultDiscoveriesAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<int> playerIds,
        CancellationToken cancellationToken);
}
