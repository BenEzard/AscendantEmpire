using AscendantEmpire.Application.Data;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Reads one Player's extraction efficiency modifiers, summed per Asset.
/// </summary>
/// <remarks>
/// The only reader of <c>PlayerExtractionEfficiencyModifier</c>. It returns sums rather than efficiencies because the starting efficiency belongs to the extraction
/// path, which applies it through <see cref="PlayerExtractionEfficiencies.GetEfficiency"/>.
/// </remarks>
internal interface IPlayerExtractionEfficiencyReader
{
    Task<PlayerExtractionEfficiencies> GetEfficienciesAsync(AscendantEmpireContext context, int playerId, CancellationToken cancelToken = default);
}
