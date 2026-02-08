namespace AscendantEmpire.Domain.Enums.Structures;

[Flags]
public enum TileSlotType
{
    None = 0,
    Primary = 1 << 0,
    Secondary = 1 << 1,
    Transportation = 1 << 2,
    Environmental = 1 << 3,
}

/*
None → 0
Primary → 1
Secondary → 2
Transportation → 4
Environmental → 8
Primary + Secondary → 1 + 2 = 3
Primary + Transportation → 1 + 4 = 5
All → 1 + 2 + 4 + 8 = 15
 */