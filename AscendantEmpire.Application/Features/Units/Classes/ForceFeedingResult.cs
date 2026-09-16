using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Units.Classes;

/// <summary>
/// How well one Force fed at the end of its owner's Turn.
/// </summary>
/// <param name="UnitInstanceId">The Force.</param>
/// <param name="CampaignMapTileId">The campaign map tile it fed on.</param>
/// <param name="Need">The food it needed.</param>
/// <param name="Fed">The food it got: <paramref name="Need"/> less <paramref name="Shortfall"/>.</param>
/// <param name="Shortfall">The food it went without; 0 when it fed in full.</param>
internal sealed record ForceFeedingResult(int UnitInstanceId, int CampaignMapTileId, ScaledAmount Need, ScaledAmount Fed, ScaledAmount Shortfall)
{
    /// <summary>
    /// Whether the Force got less than it needed.
    /// </summary>
    public bool IsUnderfed => Shortfall > ScaledAmount.Zero;
}
