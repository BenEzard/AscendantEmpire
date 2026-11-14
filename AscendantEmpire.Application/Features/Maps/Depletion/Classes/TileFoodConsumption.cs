using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Classes;

/// <summary>
/// What one tile's Forces ate, ready to be spent: the tile's depletion row and the amount taken from each of its sources.
/// </summary>
/// <param name="Depletion">The tile's depletion row, tracked by the caller's context, which the Tile sources are charged against.</param>
/// <param name="Taken">What was taken from each source, in Asset units, as <see cref="FoodExtraction.Taken"/> gives it.</param>
internal sealed record TileFoodConsumption(CampaignMapDepletion Depletion, IReadOnlyDictionary<FoodSourceKey, ScaledAmount> Taken);
