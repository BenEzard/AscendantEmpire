namespace AscendantEmpire.Domain.Enums.Maps;

[Flags]
public enum DetectionDirection
{
    None = 0,
    FullRadius = 1 << 0, // current behavior: full radius in all directions

    North = 1 << 1,
    East = 1 << 2,
    South = 1 << 3,
    West = 1 << 4,
}
