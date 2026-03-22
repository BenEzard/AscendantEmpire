namespace AscendantEmpire.Domain.Enums.Maps;

[Flags]
public enum DetectionDirection
{
    None = 0,
    FullRadius = 1 << 0, // 1 current behavior: full radius in all directions

    North = 1 << 1,     // 2
    East = 1 << 2,      // 4
    South = 1 << 3,     // 8
    West = 1 << 4,      // 16
}
