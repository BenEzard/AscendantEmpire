namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// What one run of the camp deliveries actually moved, and what the parents had no room for.
/// </summary>
/// <remarks>
/// For the end-of-Turn log rather than for a player: a delivery is silent, and a camp that delivered nothing is indistinguishable to the player from one that was never
/// designated. Three figures rather than one because "no camp was eligible", "every eligible camp was empty" and "the parents were full" are different situations with
/// the same effect on a balance.
/// </remarks>
/// <param name="CampsEmptied">How many camps the parent's network took everything from. A camp holding nothing is not counted, nor is one left holding a clamped remainder.</param>
/// <param name="BalancesMoved">How many balance rows something was taken out of, across all of those camps.</param>
/// <param name="BalancesHeldBack">How many balance rows the network could not take in full, because the storage class they belong to was full in every bank it reached.</param>
public sealed record AssetDeliveryOutcome(int CampsEmptied, int BalancesMoved, int BalancesHeldBack);
