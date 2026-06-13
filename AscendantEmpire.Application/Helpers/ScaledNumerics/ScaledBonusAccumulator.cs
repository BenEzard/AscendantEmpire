using System.Numerics;

namespace AscendantEmpire.Application.Helpers.ScaledNumerics;

/// <summary>
/// Keeps the remainder from repeated percentage calculations.
/// 
/// Most of the time, ScaledAmount alone is enough.
/// This is useful when repeated calculations create fractions smaller than one raw scaled unit.
/// </summary>
public sealed class ScaledBonusAccumulator
{
    private BigInteger _carry;

    public long CarryForStorage
    {
        get
        {
            if (_carry > long.MaxValue || _carry < long.MinValue)
            {
                throw new OverflowException("Carry is outside the range of Int64.");
            }

            return (long)_carry;
        }
    }

    public ScaledBonusAccumulator()
    {
    }

    public ScaledBonusAccumulator(long carryFromStorage)
    {
        _carry = carryFromStorage;
    }

    /// <summary>
    /// Calculates the bonus amount and keeps any remainder internally.
    /// </summary>
    public ScaledAmount CalculateBonus(ScaledAmount amount, ScaledPercent bonus)
    {
        BigInteger numerator = (BigInteger)amount.RawValue * bonus.RawValue + _carry;

        BigInteger bonusRaw = numerator / ScaledPercent.Scale;
        _carry = numerator % ScaledPercent.Scale;

        if (bonusRaw > long.MaxValue || bonusRaw < long.MinValue)
        {
            throw new OverflowException("Bonus amount is outside the range of Int64.");
        }

        return ScaledAmount.FromRaw((long)bonusRaw);
    }

    /// <summary>
    /// Applies the bonus and returns amount + bonus.
    /// </summary>
    public ScaledAmount AddBonus(ScaledAmount amount, ScaledPercent bonus)
    {
        return amount + CalculateBonus(amount, bonus);
    }

    public void Reset()
    {
        _carry = BigInteger.Zero;
    }
}