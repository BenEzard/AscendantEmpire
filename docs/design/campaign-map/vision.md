# Vision on the campaign map

Part of the [Campaign Map](README.md) design.

What a Force or Building can see is determined by both **Sight** and **Detection**.

> This section is the *design* of vision. For how it is stored, sent to the client and drawn —
> explored ground versus ground currently watched — see [Fog of War](../../fog-of-war.md).

Rivers are not part of any of this. They do not block sight, and they are **explored terrain**: a
player sees a river on ground they have explored whether or not anything is watching it, and an edge
shows as soon as either of its two banks has been explored. See
[tiles.md](tiles.md#what-a-player-is-shown).

## Sight

Sight is the ability to see the *geography of the land, buildings, and certain assets* from a
distance. It is determined by:

- the Sight value of the observer, worked out by the rules below;
- the presence of specialists with a `SIGHT_PERK`;
- the geography type of the occupied and surrounding tiles;
- the biome type of the occupied and surrounding tiles;
- vision-blocking infrastructure (e.g. a border wall).

A unit automatically sees the tile it occupies. For every additional point of unit sight it may see
one more tile, provided nothing impedes the line of sight.

### How an observer's Sight is worked out

- A **Unit's** Sight is the **sum** of the Sight values of its components — the base and everything
  fitted to it. Sight is authored per component and is optional: a component with no Sight value
  contributes nothing, so a Unit built entirely from such components sees only the tile it stands on.
- A **Command's** Sight is the **maximum** Sight of the Units within it. A Command does not add its
  members' Sight together.
- A Unit or Command standing on a tile with a **Structure** sees `MAX(own Sight, Structure Sight)`.
- Two Units, or a Unit and a Command, standing on the same tile **do not** share Sight or Detection.
  They observe independently; neither lends the other its range.

> **Implementation note.** Every Unit and every Structure is emitted as its own map observer, and the
> revealed map is the union of what they each see. A union of ranges radiating from the same tile is
> arithmetically the maximum of those ranges, so the Command and Structure rules above already hold
> without being special-cased anywhere in the sight pipeline. Only the per-Unit sum is computed
> explicitly, at template compile time into `CompiledUnitTemplate.TotalSight`.
>
> The rules in this section are computed in exactly one place, the pure `FieldOfView` class, which both the live map read and the intelligence witness service use. See [intelligence-and-sightings.md](../intelligence-and-sightings.md#the-witness-service).

| Geography | Relative elevation | | Biome | Blocks sight |
| --- | --- | --- | --- | --- |
| Unexplored | 0 | | Unexplored | False |
| Ice | 0 | | Arid | False |
| Coastal | 0 | | Coastal | False |
| Ocean | 0 | | Desert | False |
| Plain | 0 | | Forest | True |
| Hilly | 1 | | Grassland | False |
| Mountainous | 3 | | Ice | False |
| Void | 10 | | Jungle | True |
| | | | Lake | False |
| | | | Mangrove | True |
| | | | Ocean | False |
| | | | Tundra | False |
| | | | Void | True |
| | | | Woodland | True |

### Standing in dense terrain

A sight-blocking biome takes an observer's sight away outright: it reports the tile it occupies and
nothing else. That beats the elevation bonus — a hilly jungle is still a jungle, and the extra height
buys nothing through the canopy.

There is one addition, and it is not a weakening of the blindness. A blinded observer also reveals
each **adjacent tile its own movement domain could not enter**:

| Observer | Reveals | Stays dark |
|---|---|---|
| A land force | Water, mountains, void | Every ordinary land neighbour |
| A ship | Land, mountains, void | Open water |
| A flier | Mountains, void | Everything passable |
| A **structure** | Judged as a land force | — |

Deep in a jungle every neighbour is ordinary land, so a land force reveals **nothing at all** and the
biome is exactly as concealing as it looks. On a jungle coast it reveals the sea and not the shoreline
beside it.

> **Why the addition exists.** With no sight, the only way left to uncover a neighbouring tile is to
> walk onto it. That works for land — step out of the jungle and look back — but not for anything the
> observer is barred from. A jungle-covered coastline could otherwise never reveal the sea beyond it,
> by any legal action in the game, and the same held for a mountain or void tile beside a jungle. The
> reveal is therefore not really *sight*; it is the map conceding what the player could never have
> gone to find out.

The rule asks the movement rules themselves, so "revealed" and "refused a step" are the same
question with the same answer. The map can never show water it would then let a force sail into, nor
refuse a step onto ground it had never revealed.

It asks the **tile** rules, not the step rules, and rivers are deliberately outside them. A river
belongs to the step across it rather than to either tile, and a tile beyond a Large river can still be
entered from another side — so a river changes nothing about what is revealed here.

A **structure** has no movement domain of its own and is judged as a land force. Without that, a
settlement founded on a coastal jungle would sit beside sea nothing could ever uncover — the force
that founded it was standing on the same blocking tile, so it revealed nothing either.

This is all a rule about the *occupied* tile. A sight-blocking tile further out still stops vision
dead at itself, exactly as before, and is what the sight path below is walking to find.

### Technical implementation of sight

For a unit at (40, 40) with a Sight value of 4:

1. If the occupied tile has a sight-blocking biome, the unit can see no further — sight is zero and the
   walk below never starts. It still reveals the neighbours it could not have reached; see
   [Standing in dense terrain](#standing-in-dense-terrain).
2. Calculate the coordinates of tiles surrounding the occupied tile in a rectangle.
3. For each, compute a **relative (focal-offset) grid** — how many x/y values return to the occupied
   tile — and note whether the tile is diagonal, on the same horizontal/vertical, and its radius.

**Sight path** — to find where obstacles block further vision, each tile has a **sight parent** from
which its value flows:

- the occupied tile has no parent;
- a radius-1 tile's parent is the occupied tile;
- tiles on a diagonal/horizontal/vertical from the occupied tile use the preceding tile;
- the remaining tiles are more complex — each quadrant is split into an upper and a lower triangle,
  and the parent is the diagonal tile for as long as possible (e.g. for radius 3, evaluating (2,7)
  looks back at (6,2) then (5,3)).

Notation: `US` = Unit Sight, `B` = Biome, `G` = Geography, `b` = buff, `p` = perk. A value of 200
gives a radius of 2.

| Occupied tile | R1 | R2 | R3 | Outcome |
| --- | --- | --- | --- | --- |
| US=100, B=Jungle (200b) | | | | `US > Bb` false → 0; current tile only |
| US=100, G=Hilly (200b;100p), B=Jungle (200b) | | | | `US > Bb` false → 0; current tile only |
| US=100, G=Plain (100b;0p), B=Arid (0b;0p) | | | | `US > Bb`=100; +Gp=100; see 1 tile |
| US=100, G=Hilly (200b;100p), B=Arid | B=Arid | B=Arid | | `US > Bb`=100; +Gp=200; see 2 tiles |
| US=100, G=Mountainous (300b;200p), B=Arid | B=Arid | B=Arid | B=Arid | `US > Bb`=100; +Gp=300; see 3 tiles |
| US=100, G=Plain, B=Arid | B=Jungle (200b) | | | see 1 tile, then blocked by Jungle |
| US=100, G=Hilly (200b;100p) | B=Jungle (200b) | | | see 1 tile, then blocked by Jungle |

## Detection

Detection is a unit's ability to see **enemy units** on the campaign map. It is determined by:

1. the Detection value of each Unit in the Force;
2. the size of the unit (more men = more eyes = greater detection);
3. modifiers from any Specialists in the Force;
4. the **Disturbance** value of the enemy unit;
5. terrain.

Points 1–3 determine whether a unit *may* detect an enemy on a tile; points 4–5 determine the
*terrain* a unit can see.

| Asset type | Detection when on tile | Detection for surrounding tile |
| --- | --- | --- |
| Wood (Forest/Jungle) | Max detection of 0 when standing on a wood-asset tile (no surrounding tiles shown) | Max detection of 0 when looking at a Forest tile |

### Hidden Forces and food

> TODO (CM-45): A hidden Force still feeds, and feeding adds to the tile's shared `Taken`. That
> lowers what every watching Player sees on the tile — the toolbar's sustenance values, the support
> level and the turns in the [support indicator](support-indicator.md) — which signals the Force is
> there. Going hungry must not give it away either. Waits on hiding and detection being modelled.
