using System.Numerics;

namespace AscendantEmpire.Application.Helpers.ScaledNumerics;

/// <summary>
/// Represents a fixed-point game amount stored as a scaled integer.
///
/// Example with Scale = 1,000:
/// 1 wood     = 1,000 raw units
/// 0.5 wood   = 500 raw units
/// 0.01 wood  = 10 raw units
/// 0.001 wood = 1 raw unit
///
/// Store RawValue in the database as BIGINT / long.
/// </summary>
public readonly record struct ScaledAmount : IComparable<ScaledAmount>
{
    public const long Scale = 1_000;

    public long RawValue { get; }

    private ScaledAmount(long rawValue)
    {
        RawValue = rawValue;
    }

    public static ScaledAmount Zero => new(0);

    public static ScaledAmount FromRaw(long rawValue)
    {
        return new ScaledAmount(rawValue);
    }

    public static ScaledAmount FromWhole(long wholeAmount)
    {
        checked
        {
            return new ScaledAmount(wholeAmount * Scale);
        }
    }

    public static ScaledAmount FromDecimal(decimal amount)
    {
        return new ScaledAmount((long)Math.Round(amount * Scale, MidpointRounding.AwayFromZero));
    }

    public long ToWholeRoundedDown()
    {
        return RawValue / Scale;
    }

    public long ToWholeRoundedUp()
    {
        if (RawValue == 0)
        {
            return 0;
        }

        return (RawValue + Scale - 1) / Scale;
    }

    public decimal ToDecimal()
    {
        return RawValue / (decimal)Scale;
    }

    public long RemainderRaw()
    {
        return RawValue % Scale;
    }

    public ScaledAmount FloorToWhole()
    {
        return FromWhole(ToWholeRoundedDown());
    }

    public ScaledAmount Add(ScaledAmount other)
    {
        checked
        {
            return new ScaledAmount(RawValue + other.RawValue);
        }
    }

    public ScaledAmount Subtract(ScaledAmount other)
    {
        checked
        {
            return new ScaledAmount(RawValue - other.RawValue);
        }
    }

    public ScaledAmount Multiply(long multiplier)
    {
        checked
        {
            return new ScaledAmount(RawValue * multiplier);
        }
    }

    public ScaledAmount Divide(long divisor)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException();
        }

        return new ScaledAmount(RawValue / divisor);
    }

    /// <summary>
    /// Applies a percentage bonus and returns the bonus amount only.
    /// 
    /// Example:
    /// 1 wood with 1% bonus = 0.01 wood.
    /// </summary>
    public ScaledAmount CalculateBonus(ScaledPercent bonus)
    {
        BigInteger result = (BigInteger)RawValue * bonus.RawValue / ScaledPercent.Scale;
        return FromRaw(ToLongChecked(result));
    }

    /// <summary>
    /// Applies a percentage bonus and returns the original amount plus the bonus.
    /// 
    /// Example:
    /// 1 wood with 1% bonus = 1.01 wood.
    /// </summary>
    public ScaledAmount AddBonus(ScaledPercent bonus)
    {
        return Add(CalculateBonus(bonus));
    }

    public int CompareTo(ScaledAmount other)
    {
        return RawValue.CompareTo(other.RawValue);
    }

    public override string ToString()
    {
        return ToDecimal().ToString("0.####");
    }

    public static ScaledAmount operator +(ScaledAmount left, ScaledAmount right)
    {
        return left.Add(right);
    }

    public static ScaledAmount operator -(ScaledAmount left, ScaledAmount right)
    {
        return left.Subtract(right);
    }

    public static ScaledAmount operator *(ScaledAmount left, long right)
    {
        return left.Multiply(right);
    }

    public static ScaledAmount operator /(ScaledAmount left, long right)
    {
        return left.Divide(right);
    }

    public static bool operator <(ScaledAmount left, ScaledAmount right)
    {
        return left.RawValue < right.RawValue;
    }

    public static bool operator >(ScaledAmount left, ScaledAmount right)
    {
        return left.RawValue > right.RawValue;
    }

    public static bool operator <=(ScaledAmount left, ScaledAmount right)
    {
        return left.RawValue <= right.RawValue;
    }

    public static bool operator >=(ScaledAmount left, ScaledAmount right)
    {
        return left.RawValue >= right.RawValue;
    }

    private static long ToLongChecked(BigInteger value)
    {
        if (value > long.MaxValue || value < long.MinValue)
        {
            throw new OverflowException("The scaled amount is outside the range of Int64.");
        }

        return (long)value;
    }
}