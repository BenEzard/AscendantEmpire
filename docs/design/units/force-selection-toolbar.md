# Force Selection Toolbar

When a campaign map tile holds more than one of the player's Forces, a bar of those Forces is docked
under the [context action toolbar](force-context-action-toolbar.md). Clicking one selects it, and
ctrl+clicking adds or removes one. That is the whole of the selection mechanism.

Delivered by **TM-5**, replacing a "Select Force" button that opened a modal. Extended to a
multi-select, and the dormant checkbox channel removed in favour of it, in the work described under
[Multi-select](#multi-select).

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
  selectedForceIds?: ReadonlySet<number>;      // a set: several entries can be pressed at once
  unitImageSrcByUnitTemplateId?: ReadonlyMap<number, string>;
  unitNameByUnitTemplateId?: ReadonlyMap<number, string>;
  onSelect: (force: ObservedForceTile, append: boolean) => void;
}
```

All state lives in
[`useForceSelection.tsx`](../../../AscendantEmpire.AscendantEmpireUI/src/features/game/hooks/useForceSelection.tsx),
which already owned the selection and the per-tile visible Force. It gained `multiForceTile` (the
renamed `pendingForceSelectionTile` — no longer "the tile whose dialog can be reopened", now the bar's
data source), and its `selectedForce` became a `selectedForces` list with a memoised
`selectedForceIds` set derived from it.

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

## Multi-select

The bar is a multi-select. A plain left click sets the selection to the Force clicked; ctrl+click (⌘ on
macOS, where ctrl+click is the system context-menu gesture) adds a Force to the selection, or removes it
again when it is already in it.

The full behaviour, as a trace over three Forces A, B and C. This table is the specification, and
`useForceSelection.test.tsx` walks it gesture by gesture in one test — the rules are about
*transitions*, so splitting it into a test per row would lose what makes them mean anything:

| Gesture | Selection after |
|---|---|
| Left click A | `[A]` |
| Left click B | `[B]` |
| Ctrl+click B | `[]` — B was selected, so it is removed, leaving nothing selected |
| Left click A | `[A]` |
| Ctrl+click B | `[A, B]` |
| Ctrl+click C | `[A, B, C]` |
| Ctrl+click B | `[A, C]` — B was selected, so it is removed |
| Left click B | `[B]` — a plain click always clears first |

Clicking a **tile** is unaffected: it always replaces the selection with that tile's default Force.
Multi-select is the bar's gesture, and the map's click is what starts a fresh selection. Every selected
Force therefore shares a tile, which is what lets the backend keep answering in two queries.

### The bar reports the gesture, not the intent

`onSelect` passes `append`, read from `event.ctrlKey || event.metaKey`. What that *means* — add, or
remove if already present — is decided in `chooseForce`. Reading the modifier in the component keeps
the DOM event inside the presenter; deciding what it means in the hook keeps the bar free of selection
rules, exactly as it is free of availability rules.

### The empty selection is a real state

Row 3 above leaves nothing selected, and row 4 then clicks in the bar again. That round trip only works
if the bar survives an empty selection, so the dock in `GameUI` mounts on
`selectedForces.length > 0 || multiForceTile != null` rather than on a Force being selected. Without the
second half, ctrl+clicking the last Force off would unmount the very bar the player was clicking in,
leaving them to go back to the map to recover it. With it, the bar stays and the action toolbar simply
empties — the honest rendering of a selection with nothing in it.

### Pinning follows selection, not clicks

`chooseForce` pins the clicked Force as the one its tile draws, but **only when the click selected it**.
A ctrl+click that removes a Force deliberately leaves the tile's drawn Force alone: pinning there would
leave the map showing the one Force the player had just taken out.

### What the checkbox channel was, and why it went

The bar used to carry `selectionRequired` / `checkedForceIds` / `onCheckedChange` — a checkbox above
each Force, specified under TM-5 as the multi-select channel **TM-4** (executing a split) would drive.
Nothing ever set it.

Ctrl+click makes it redundant: the selection *is* the list of Forces an action applies to, and keeping
both would leave the bar with two competing notions of "more than one Force". The checkbox markup, the
three props, the `forceSelectionRequired` / `checkedForceIds` state, `toggleForceChecked` and the
`.force-selection-toolbar__checkbox` rule are all gone. TM-4 now plugs in by reading `selectedForces`
where `runForceAction` currently raises a toast.

One commitment survives the removal, because it turned out to be the same one: **choosing Forces to act
on must not need a second gesture**. The tick achieved that by being a separate channel; ctrl+click
achieves it by making the selection itself hold more than one Force.

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
| `ForceSelectionToolbar` | Vitest | 17 — visibility guards, troop count, both ordering keys, selection, ctrl/⌘+click reporting, several entries pressed at once, the empty selection, fade, clickability when spent, name tooltip, no checkboxes |
| `useForceSelection` | Vitest | 10 — own-Forces-only filtering, default selection, choosing, the whole gesture trace, emptying the selection, pinning on removal, resetting from the map, id-set identity |
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
