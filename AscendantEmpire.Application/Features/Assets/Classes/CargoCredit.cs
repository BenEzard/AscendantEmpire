namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// One quantity of one Asset arriving in a Force's cargo.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not an <see cref="AssetCredit"/>. That carries a Settlement, because a credit to a Settlement is placed across the network it sits in and
/// bounded by what each bank there can hold; cargo has no network, no capacity and no spread, so a credit to one names the bank and nothing else.
/// </para>
/// <para><see cref="RawQuantity"/> is scaled by <c>ScaledAmount.Scale</c>, like every other amount in the game.</para>
/// </remarks>
internal sealed record CargoCredit(int AssetId, long RawQuantity);
