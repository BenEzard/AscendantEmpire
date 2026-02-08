namespace AscendantEmpire.Domain.Enums.Structures;

public enum PositionOnTile
{
    /// <summary>
    /// Not defined
    /// </summary>
    None = 0,

    /// <summary>
    /// The Structure occupies the entire tile. (Not mutually exclusive)
    /// </summary>
    WholeTile = 1 << 0,

    /// <summary>
    /// The Structure has been placed in a specific location on the Battle Map.
    /// </summary>
    CustomLocation = 1 << 1,

    NorthernBoundary = 1 << 2,
    SouthernBoundary = 1 << 3,
    EasternBoundary = 1 << 4,
    WesternBoundary = 1 << 5,

    BoundaryMask = NorthernBoundary | SouthernBoundary | EasternBoundary | WesternBoundary,
    ModeMask = WholeTile | CustomLocation,
}

/*
None = 0
WholeTile = 1 << 0 = 1
CustomLocation = 1 << 1 = 2
NorthernBoundary = 1 << 2 = 4
SouthernBoundary = 1 << 3 = 8
EasternBoundary = 1 << 4 = 16
WesternBoundary = 1 << 5 = 32

Masks:
BoundaryMask = 4 | 8 | 16 | 32 = 60
ModeMask = 1 | 2 = 3

Examples (what you’d insert):
Whole tile only → 1
Custom location only → 2
North boundary only → 4
Whole tile + North + West → 1 + 4 + 32 = 37
Custom location + all boundaries → 2 + 60 = 62
 */