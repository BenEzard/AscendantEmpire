namespace AscendantEmpire.Application.Helpers.ScaledNumerics;

/// <summary>
/// Represents a percentage as a scaled integer.
///
/// Scale = 1,000 means:
/// 100% = 1,000
/// 1%   = 10
/// 0.5% = 5
/// 0.1% = 1
///
/// Store RawValue if you want percentages in the database.
/// </summary>
public readonly record struct ScaledPercent
{
    public const long Scale = 1_000;

    public long RawValue { get; }

    private ScaledPercent(long rawValue)
    {
        RawValue = rawValue;
    }

    public static ScaledPercent Zero => new(0);

    public static ScaledPercent FromRaw(long rawValue)
    {
        return new ScaledPercent(rawValue);
    }

    public static ScaledPercent FromPercent(decimal percent)
    {
        return new ScaledPercent((long)Math.Round(percent / 100m * Scale, MidpointRounding.AwayFromZero));
    }

    public static ScaledPercent FromWholePercent(long percent)
    {
        checked
        {
            // One whole percent is Scale / 100 raw units (e.g. 10 at Scale 1,000), so derive it from
            // Scale rather than hardcoding it -- otherwise this breaks whenever Scale changes.
            return new ScaledPercent(percent * (Scale / 100));
        }
    }

    public decimal ToPercent()
    {
        return RawValue * 100m / Scale;
    }

    public override string ToString()
    {
        return $"{ToPercent():0.####}%";
    }

    public static ScaledPercent operator +(ScaledPercent left, ScaledPercent right)
    {
        checked
        {
            return new ScaledPercent(left.RawValue + right.RawValue);
        }
    }

    public static ScaledPercent operator -(ScaledPercent left, ScaledPercent right)
    {
        checked
        {
            return new ScaledPercent(left.RawValue - right.RawValue);
        }
    }
}