namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// What one run of the camp deliveries actually moved.
/// </summary>
/// <remarks>
/// For the end-of-Turn log rather than for a player: a delivery is silent, and a camp that delivered nothing is indistinguishable to the player from one that was never
/// designated. Two figures rather than one because "no camp was eligible" and "every eligible camp was empty" are different situations with the same effect.
/// </remarks>
/// <param name="CampsEmptied">How many camps emptied something into their parent. A camp holding nothing is not counted.</param>
/// <param name="BalancesMoved">How many balance rows were moved, across all of those camps.</param>
public sealed record AssetDeliveryOutcome(int CampsEmptied, int BalancesMoved);
