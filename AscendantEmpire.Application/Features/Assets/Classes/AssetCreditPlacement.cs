namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// What one <see cref="AssetCredit"/> actually placed across its network, and what no bank in it had room for.
/// </summary>
/// <remarks>
/// <para>
/// <b>One of these per credit, in the order the credits were handed over</b>, so a caller that built a credit per row can walk the two together without
/// matching them up again.
/// </para>
/// <para>
/// <b><paramref name="RawUnplaced"/> is the whole content of a clamped credit.</b> A credit is never refused for want of storage - it places what fits and
/// reports the rest - so this figure is the only thing that says production was lost, and whether to waste it or to refuse before extracting next time is the
/// caller's to decide. The storage class comes back with it because that is what bound, and a caller reporting the loss has to name it.
/// </para>
/// <para>Both figures are raw scaled on the <c>ScaledAmount.Scale</c> convention of 1,000.</para>
/// </remarks>
/// <param name="Credit">The credit this answers for.</param>
/// <param name="StorageClassId">The class the Asset is kept under, which is the capacity the spread was bounded by.</param>
/// <param name="RawPlaced">How much landed in a bank, across every bank the spread reached.</param>
/// <param name="RawUnplaced">How much was placed nowhere, because every bank the spread reached was full for that class.</param>
internal sealed record AssetCreditPlacement(AssetCredit Credit, int StorageClassId, long RawPlaced, long RawUnplaced);
