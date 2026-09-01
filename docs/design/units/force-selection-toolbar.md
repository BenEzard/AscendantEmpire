# Force Selection Toolbar

When a campaign map tile holds more than one of the player's Forces, a bar of those Forces is docked
under the [context action toolbar](force-context-action-toolbar.md). Clicking one selects it. That is
the whole of the selection mechanism.

Delivered by **TM-5**, replacing a "Select Force" button that opened a modal.

## Why it is not a context action

The context action toolbar is a presenter over a backend pipeline: the backend decides what a Force
*can* do and why, and the frontend decides how that looks. This bar has none of that. It has no
strategy, no availability rules, and asks the backend nothing — it renders the Forces the tile already
gave the map, and reports a click. Folding it into that document would blur its thesis, so it lives
here instead.

The two are still docked together, because to the player they are one control surface.

## What replaced what

Selecting between stacked Forces used to be a modal round-trip: a "Select Force" button appeared on the
context action toolbar, opened `ForceSelectionDialog`, and the player chose and dismissed. A modal for
what is really a quick, repeated toggle — and one that hid the map while the player was trying to
reason about the tile.

Removed with it:

* `ForceSelectionDialog.tsx`.
* The `"t"` shortcut that reopened the dialog. It existed only to reopen something; there is nothing to
  reopen now.
* `FRONTEND_ONLY_ACTION_ORDER` in `forceActionRegistry.tsx`. "Select Force" was its only entry, and the
  only frontend-only action there has ever been. The 1–999 band the C# `ForceActionType` enum reserves
  stays reserved for the next one.
* The re-arming step in `chooseForce`, which set the pending tile back to itself so the button had
  something to reopen with. With the bar always present it was a no-op.

## Shape

[`ForceSelectionToolbar.tsx`](../../../AscendantEmpire.AscendantEmpireUI/src/features/game/components/ForceSelectionToolbar.tsx)
is a presenter, like `ContextActionToolbar`: it holds no selection state and makes no decisions. Its
only local machinery is the ref and effect that keep the selected entry scrolled into view.

```ts
interface ForceSelectionToolbarProps {
  visible: boolean;
  tile: ForceSelectionToolbarTile | null;      // { x, y, forces }
  selectedForceId?: number;
  unitImageSrcByUnitTemplateId?: ReadonlyMap<number, string>;
  selectionRequired?: boolean;                 // adds a checkbox per Force
  checkedForceIds?: ReadonlySet<number>;
  onSelect: (force: ObservedForceTile) => void;
  onCheckedChange?: (forceId: number, checked: boolean) => void;
}
```

All state lives in
[`useForceSelection.tsx`](../../../AscendantEmpire.AscendantEmpireUI/src/features/game/hooks/useForceSelection.tsx),
which already owned `selectedForce` and the per-tile visible Force. It gained `multiForceTile` (the
renamed `pendingForceSelectionTile` — no longer "the tile whose dialog can be reopened", now the bar's
data source), plus `forceSelectionRequired` and `checkedForceIds`.

Each entry reuses
[`ObservedForce`](../../../AscendantEmpire.AscendantEmpireUI/src/features/map/components/ObservedForce.tsx),
so the icon and the three-segment health bar are identical to the map's. That component is
`position: absolute; inset: 8%`, so the bar wraps it in a sized, relative span. The
healthy/injured/dead percentage maths was deliberately **not** extracted: the bar wants the whole
marker, not the numbers, and lifting them out would still leave the markup to duplicate.

**None of its rules are overridden**, and that is the point. `ObservedForce` insets and scales itself
from whatever box it is given, so the entry sizes its marker to `TILE_SIZE_PX` — imported from
`TileGrid`, not copied — and the unit renders at exactly the size it renders on the map. Matching by
construction rather than by arithmetic, so it stays matched if the tile size ever changes.

The cost is that the box's bottom ~5px are transparent (the 8% inset, plus the letterboxing
`object-fit: contain` leaves under a square sprite), which pushed the troop count away from the icon it
captions. The count is pulled back with a negative top margin rather than by shrinking the box, because
the box has to stay a tile for the size to match.

Its `isSelected` prop is deliberately not passed. On the map that draws yellow corner brackets and a
glow, which earn their place picking one unit out of a crowded grid; on a tile sitting in a row of
tiles they are noise, and the button's own border and tint already say which entry is selected. That
tint is also kept much lighter than the action toolbar's. That gap narrowed when the action buttons lost
their captions and became small icon tiles themselves, but it is still deliberate: those hold a flat
icon that a strong yellow can sit behind, while these hold unit art the tint would fight for attention.

Hovering an entry shows the **template's** name ("Settler") via the shared `Tooltip`. That name is not
per-unit: `UnitInstance.Name` exists and is documented as the player's own name for a Unit, but nothing
writes it and no UI sets one, so it is null everywhere. Layering it in later is a matter of preferring
it over the template name in one expression.

Below the image, the Force's **current strength** — `healthyTroops + injuredTroops`, the soldiers still
standing, not the strength it was created at. The health bar already shows what has been lost.

## Decisions

### Only the player's own Forces

The bar never lists an enemy or ally sharing the tile. That filtering happens in `selectForceFromTile`,
which builds the bar's tile from `ownedForces` alone — the bar shows what it is given. A rival Force is
still drawn on the tile itself; it is simply not something this player can select.

This is also why masking `HasMovementRemaining` for unowned Forces (below) costs the bar nothing.

### Ordering: movement first, then id

Forces that can still act sort ahead of those that cannot, so a scrolling bar shows the useful ones
without the player hunting. The `id` tiebreak is what makes the order *stable*: `tile.forces` arrives
in whatever order the query returned it, and without a second key the buttons would shuffle under the
cursor on every refetch — the same reasoning behind `ContextActionToolbar` sorting its own actions.

The order does shift as Forces spend their movement during a turn. That is the point of the first key.

### The fade is cosmetic — a spent Force is still clickable

A Force with no movement left is greyed and half-faded. It is **not** disabled: no `disabled`
attribute, no `aria-disabled`, and the modifier class deliberately omits the `pointer-events: none`
that the neighbouring `.command-toolbox__action:disabled` rule carries.

The player still has to be able to select a spent Force — not least to interrupt orders it is already
carrying out. The fade says "can't move", not "can't touch". Both the CSS and a test say so, because
the disabled rule sitting next to it reads as the pattern to copy.

### A single scrolling row

The bar is `flex-wrap: nowrap` with `overflow-x: auto`, capped at `min(90vw, 900px)`. Wrapping would
grow the dock upward over the map and shove the context action toolbar around as the Force count
changed; scrolling keeps the dock a fixed height whatever the tile holds.

Because selection is usually set by clicking the *tile* rather than the bar, the selected entry can
start out of view, so it scrolls itself in with `scrollIntoView({ block: "nearest", inline: "nearest" })`
— `"nearest"` so it moves only when the entry actually is out of view, and never scrolls the page.

### Layout: one dock, not two fixed bars

`.command-toolbox` used to position itself (`position: fixed; bottom: 16px`). It no longer does. Both
bars now stack in a `.command-dock` flex column that owns the placement, so the selection bar sits
under the toolbar without either knowing the other's height — and the toolbar's height changes with its
action count, so a hard-coded offset on the lower bar would have drifted. The dock is
`pointer-events: none` with `pointer-events: auto` on its children, so the gap between the bars doesn't
swallow clicks meant for the map.

### `selectionRequired` — built now, driven later

`selectionRequired` puts a checkbox above each Force, turning the bar into a multi-select. Nothing sets
it yet: it is the channel **TM-4** (executing a split) and the future join work will drive, specified
now so the bar was built once rather than retrofitted.

Two things it commits to:

* **The tick is a separate channel from the selection.** Checking a Force must not change which Force
  is selected. The checkbox is therefore a *sibling* of the button, not a child — nesting an `<input>`
  in a `<button>` is invalid markup anyway, and would have fired `onSelect` on every tick.
* **The checked set lives in the hook, not the bar.** It costs one exported setter that nothing calls
  until TM-4, which is a smaller and more honest amount of speculative surface than shipping a prop
  that can never be true. TM-4 plugs in by turning `forceSelectionRequired` on where `runForceAction`
  currently raises a toast, and reading `checkedForceIds` when its dialog confirms.

The checked set and the mode are both cleared inside `selectForceFromTile` rather than in an effect on
`multiForceTile` — an effect would fire a second render pass and briefly show the previous tile's ticks
against the new tile's Forces.

## Remaining movement over the wire

The fade needs to know whether a Force can still move, which the client had no way to see. Added
through the existing projection chain — no migration; `UnitInstance.MovementPointsRemaining` already
exists, rescaled to `bigint` under TM-2.

`PlayerForce` → `ObservedForce` → `IForceStub` / `DTForceStub` → `ObservedForceDto` → `ObservedForceTile`.

Two decisions about the contract:

**A derived boolean, not the raw value.** `MovementPointsRemaining` is raw fixed-point on the Scale
1,000 convention. Shipping it would make every consumer responsible for knowing that, and would leave
the frontend guessing which threshold applies — the codebase already has two (`> 0` for "any movement
left", `>= CampaignMovementPoints` for "a whole turn's worth"). The boundary converts, exactly as the
same handler already does turning `TotalDetection` into whole tiles of sight range.

**Masked for Forces the player does not own.** `ObservedForce` covers every Force on a seen tile, not
just the player's own, so the field is `bool?` and comes back `null` for a rival's. How much movement
an enemy has left is intel the vision rules never granted — seeing that a Force is standing there is
one thing, knowing whether it can still act is another. The context action pipeline refuses enemy
Forces for the same reason. The client normalizer defaults `null` to `true`, so nothing is ever faded
on no evidence.

## Known gap: everything is greyed out

`MovementPointsRemaining` is not written by any production code — not on spawn, not at turn rollover.
That is **TM-3**, not started, and it means every Force reads `0`, so **every entry in the bar renders
faded** today.

This was chosen deliberately over projecting a temporary `true`. The fade follows the data; faking it
would have hidden the gap behind a UI that looked finished. It is the same symptom the Split Force
action already shows, and TM-3 fixes both at once.

## Testing

| Area | Tier | Tests |
|---|---|---|
| `ForceSelectionToolbar` | Vitest | 14 — visibility guards, troop count, both ordering keys, selection, fade, clickability when spent, name tooltip, checkbox channel |
| `useForceSelection` | Vitest | 6 — own-Forces-only filtering, default selection, choosing, checked-set clearing |
| `HasMovementRemaining` projection | SqlServer | 1 — reported for the viewer's own Force, `null` for a rival's standing on the same tile |

Two things worth knowing when adding to these:

* Toolbar tests **must** pass a `unitImageSrcByUnitTemplateId` map. `ObservedForce` returns `null`
  outright when it cannot resolve an image, so without one every marker assertion passes against an
  empty DOM.
* Assert the fade on the **class**, not the computed style. jsdom loads no stylesheet, so `filter`
  never computes.

The backend test parks the rival Force on the viewer's own tile before asserting the mask. Left where
it spawned, the assertion would pass vacuously on any map where the rival happened to start out of
sight. It also sets `MovementPointsRemaining` by hand for both states, since nothing writes the column:
without that, the projection would look correct while simply always answering `false`.
