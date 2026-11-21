using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// One Force about to eat its own load: which Force, what it eats, and how much the tile left it wanting.
/// </summary>
/// <param name="FoodSupplySetId">The food <c>SupplySet</c> compiled onto the Force's design, which is what decides whether anything it carries is food at all.</param>
/// <param name="Shortfall">The rations the tile could not cover, which is the most the load will be asked for.</param>
internal sealed record CarriedFoodDraw(int UnitInstanceId, int FoodSupplySetId, ScaledAmount Shortfall);
