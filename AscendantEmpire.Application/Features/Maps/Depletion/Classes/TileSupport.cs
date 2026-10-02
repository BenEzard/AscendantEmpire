using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Classes;

/// <summary>
/// How well a tile can feed a set of Forces, from <c>TileSupportForecaster</c>: the level and the number of whole Turns it can feed them.
/// </summary>
/// <param name="Level">The support level, for the colour.</param>
/// <param name="Turns">
/// The whole Turns the tile feeds the Forces, regrowth included: 0 up to <c>TileSupportForecaster.MAX_FORECAST_TURNS</c>. <see langword="null"/> when the tile still feeds
/// them after that many - it lasts beyond the forecast, which may mean indefinitely.
/// </param>
internal sealed record TileSupport(TileSupportLevel Level, int? Turns);
