using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <summary>
/// Reads what the end of a Round regenerates renewable MapResources from: the catalogue's habitat values, and a Game's instances.
/// </summary>
/// <remarks>
/// Reads through the caller's context.
/// </remarks>
internal interface IMapResourceRegenerationReader
{
    /// <summary>
    /// Every habitat of every renewable MapResourceType, with the values its instances grow and spread by there.
    /// </summary>
    /// <remarks>
    /// One row per habitat rather than per type, because the values differ by Biome and Geography. Which row covers a tile is decided by <c>BiomeImpliedResourceRules.FindHabitat</c>, not here.
    /// </remarks>
    Task<IReadOnlyCollection<RenewableHabitat>> GetRenewableHabitatConfigAsync(AscendantEmpireContext context, CancellationToken cancelToken = default);

    /// <summary>
    /// Every renewable MapResource instance in the Game, with the tile it stands on and its type's survival values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dormant instances are included, since regrowth works on them. A Distribution comes with its amounts from <c>MapResourceInstanceAmount</c>; any other kind comes with none.
    /// </para>
    /// </remarks>
    Task<IReadOnlyCollection<MapResourceRegenerationInstance>> GetMapResourceInstancesAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default);

    /// <summary>
    /// The tiles spreads are aimed at, keyed by <c>CampaignMapId</c>, each with its terrain and the MapResources already on it.
    /// </summary>
    /// <remarks>
    /// Only the targets are read rather than the whole map, since few instances spread in any one Round.
    /// </remarks>
    /// <param name="campaignMapIds">The target tiles, each once.</param>
    Task<IReadOnlyDictionary<int, SpreadTargetTile>> GetSpreadTargetsAsync(AscendantEmpireContext context, int gameId, IReadOnlyCollection<int> campaignMapIds, CancellationToken cancelToken = default);

    /// <summary>
    /// The Game's <c>GenerationSeed</c>, or <see langword="null"/> when the Game does not exist.
    /// </summary>
    /// <remarks>
    /// Nullable because 0 is a valid seed, so a missing Game cannot be told apart by a default value.
    /// </remarks>
    Task<long?> GetGameSeedAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default);
}
