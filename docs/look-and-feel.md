# UI Look and Feel

Conventions for the React frontend's visual language. This is about *how things look and why*, not
about component structure.

The first section it covers is **selection** — the single most repeated visual state in the app, and
currently the most fragmented.

---

## The mechanism: `@theme`, not `:root`

The frontend is on **Tailwind v4** (`@tailwindcss/vite`, `@import "tailwindcss"` at the top of
`src/index.css`). There is **no `tailwind.config.js`** — v4 configures from CSS.

That matters because there are two ways to declare a colour, and only one of them is useful here:

| | Gives a CSS variable | Gives Tailwind utilities |
|---|---|---|
| `:root { --x: … }` | Yes | **No** |
| `@theme { --color-x: … }` | Yes | **Yes** — `bg-x`, `border-x`, `text-x`, `ring-x`, `accent-x`, plus `/50` opacity modifiers |

The app styles in both worlds: hand-written rules in `src/index.css` (`.command-toolbox`,
`.observed-force`, `.force-selection-toolbar`) and Tailwind utilities in `.tsx`
(`bg-slate-100`, `border-yellow-400`). A token has to serve both, so it must be declared in `@theme`
and must sit in the **`--color-*` namespace** for the utilities to be generated.

`src/styles/bs-vars.css` already declares variables under a plain `:root`. That is why
`--nav-selected-bg` can only ever be used as `var(--nav-selected-bg)` and has no `bg-nav-selected`
utility. New tokens should not follow that pattern.

> **Ordering is not a concern.** Custom properties resolve where they are *used*, not where they are
> declared, so a `:root` block in `bs-vars.css` may reference a token declared in `index.css`'s
> `@theme` regardless of which file loads first.

---

## Selection tokens

Three tokens, declared in `src/index.css` immediately after the Tailwind import:

```css
@theme {
  /* Selection. The identity colour of "this is the thing you have chosen", used app-wide.
     Three steps rather than one because a selected surface needs an edge and a fill, and those want
     different weights of the same hue. Density (the /NN opacity) is chosen at the call site — a 50px
     tile and a full-width nav row need different amounts of the same colour to read equally. */
  --color-selection: #eab308;      /* Edges, rings, accents, checkboxes. */
  --color-selection-tint: #fde047; /* The fill wash across a selected surface. */
  --color-selection-wash: #fefce8; /* The near-white base a selected surface sits on. */
}
```

These are the exact values the force selection bar already uses, so adopting them changes nothing
about how that bar looks — it only gives the colour a name that everything else can share.

### Using them

In hand-written CSS:

```css
.thing--selected {
  border-color: color-mix(in srgb, var(--color-selection) 55%, transparent);
  background: color-mix(in srgb, var(--color-selection-tint) 18%, transparent);
}
```

In Tailwind:

```tsx
className={isSelected ? "border-selection/55 bg-selection-tint/18" : "border-slate-200"}
```

### Density guidance

Reach for these before inventing a number:

| Context | Edge | Fill |
|---|---|---|
| Small tile or icon button (~50px) | `/55` | `/18` |
| Hover, before commitment | `/40` | `/12` |
| Full-width row, nav item, table row | `/60` | `/30`–`/35` |

A wide surface needs **more** fill than a small one to read as equally selected — the same 18% wash
that reads clearly on a 50px tile all but vanishes across a 200px nav row.

---

## Applying it to the Military Advisor

The Military Advisor dialog is built on `PagedDialog`, whose page nav marks the active page with
`nav-item-selected`. `OfficerClassPage`'s movement-domain tabs use the same class. Both resolve to one
variable in `src/styles/bs-vars.css`:

```css
--nav-selected-bg: #fed7aa; /* orange-200 */
```

So **the whole dialog re-skins from one line**:

```css
/* Selection is one colour app-wide — see docs/look-and-feel.md. Denser than a map tile's fill because
   a full-width nav row needs more of the colour to read as equally selected. */
--nav-selected-bg: color-mix(in srgb, var(--color-selection-tint) 35%, transparent);
```

That covers the Military Advisor's page nav (Officer Class / Military Organisation) and the domain
tabs inside Officer Class, and it carries to every other `PagedDialog` for free.

Two things to check by eye afterwards:

- **The fill becomes translucent where it was solid.** `#fed7aa` is opaque; a `color-mix` with
  `transparent` is not. On the dialog's white surface that is fine, but the selected item now sits in
  the same stack as its `hover:bg-slate-50` siblings — confirm the selected row still wins visually.
- **Nav items are wide.** 35% is the starting point, not a result. Tune it until a selected tab reads
  as clearly as a selected force does in the bar.

---

## Migrating the rest

Every place that means "selected" today, and what it uses:

| Call site | Currently | Notes |
|---|---|---|
| `.nav-item-selected` (`bs-vars.css`) | `#fed7aa` orange-200 | Drives `PagedDialog` nav and `OfficerClassPage` domain tabs. One line, two features. |
| `.force-selection-toolbar__button[aria-pressed="true"]` | yellow-500 `/55` + yellow-300 `/18` | Already the target values — swap the literals for tokens, no visual change. |
| `.force-selection-toolbar__button:hover` | yellow-500 `/40` + yellow-300 `/12` | As above. |
| `.command-toolbox__action:hover` | yellow-500 `/65` + yellow-300 `/22` | Slightly heavier than the bar below it; worth aligning while tokenising. |
| `.observed-force__selection-corner` (×4) | `rgba(250, 204, 21, 0.95)` yellow-**400** | A *different* yellow from the toolbar's yellow-500. Unifying means picking one. |
| `.observed-force--selected` glow | `rgba(250, 204, 21, 0.95)` yellow-400 | Same. |
| `SlotPlaceHolder.tsx` (Engine) | `ring-2 ring-yellow-400/50` + a hardcoded `rgba(250,204,21,0.15)` shadow | Utility-side; becomes `ring-selection/50`. |
| `TagPicker` / list rows | `bg-slate-100` | Grey, not yellow. Decide whether these are "selected" or merely "active". |
| Colour-swatch pickers | `border-[var(--bs-primary)]` | Blue. Same decision. |

### Not selection — leave alone

The app uses yellow for two unrelated things, and a find-and-replace would merge them:

- **`#facc15` on `.observed-force__health-fill--injured`** — that is *injured troops*, a status colour
  that happens to be yellow. It must be free to change independently of selection.
- **`--bs-warning: #ffc107`** — likewise.
- **`.observed-force__marker` pips (`#facc15`)** — a unit-count marker, decoration rather than state.

If selection and "injured" share a token, the day someone retunes selection to orange, every wounded
unit turns orange too.

---

## Tooltips

`Tooltip` (`components/ui/Tooltip.tsx`) is the standard one: portaled, flips above or below, clamped
to the viewport, and `whitespace-pre` so multi-line content works. Use it everywhere **except the
campaign map's tiles**, which carry a native `title` attribute instead.

That exception is deliberate. `Tooltip` holds `visible` state per instance and runs a layout effect
and a `getBoundingClientRect` on hover, and it wraps its children in an `inline-flex` span — which on
a tile would land between the CSS grid and its items and break the grid outright. A 30 x 20 map is
600 tiles, and `TileGrid` is built the other way on purpose: primitive-returning accessors, memoised
tiles, and `useHoveredTileStore` keeping the pointer *outside* React state so that hovering never
re-renders the grid.

So styling the tile tooltip is not a matter of swapping the component in. It means one instance for
the whole grid, subscribed to the hover store already tracking the pointer and positioned from the
hovered tile's coordinates — the shape `TileSupportMarker` already uses as a single child of the
grid.

---

## Traps

**The hardcoded hexes are Tailwind v3 values.** `#eab308` was `yellow-500` in v3; in v4 `yellow-500`
is `#f0b100`. Writing `border-yellow-500` next to an existing `rgba(234, 179, 8, …)` therefore gives
two *different* yellows that look like they should match. This is the strongest argument for the
token: **use `selection`, never `yellow-*`.**

**Selection tokens are for edges and fills, never text.** `#eab308` on white is roughly 1.9:1, far
below the 4.5:1 needed for body text.

**Colour must never be the only signal.** The app already gets this right — `aria-pressed` on the
force bar's entries, `aria-current="page"` on `PagedDialog`'s nav — and new selection UI should keep
carrying the state in the markup as well as the paint.

**Translucent tokens assume a light surface.** `:root` in `index.css` sets a dark
`background-color: #242424` and `color: rgba(255,255,255,0.87)`, which several components explicitly
fight (see the comments in `LoginForm.tsx` and `MatchMaker.tsx`). A `color-mix(…, transparent)` fill
takes its apparent colour from whatever is behind it, so check any new use against the surface it
actually lands on rather than assuming white.

---

## Rollout

1. ~~Add the `@theme` block.~~ **Done.**
2. ~~Point `--nav-selected-bg` at it.~~ **Done** — the Military Advisor's page nav and Officer Class's
   domain tabs now carry the selection yellow at `35%`. That density is a starting point; retune it in
   `bs-vars.css` if a selected tab doesn't read as clearly as a selected force does.
3. ~~Swap the force selection bar's literals for tokens.~~ **Done**, and verified a true no-op: the
   built CSS emits `linear-gradient(#fefce8fa, #fde0471f)`, byte-identical to the `rgba()` literals it
   replaced.
4. **Not started.** Align `.command-toolbox__action:hover` (yellow-500 `/65`) and the `ObservedForce`
   selection corners and glow (yellow-**400**) onto the token. A deliberate visual change, not a
   refactor — the corners are currently a different yellow from everything else.
5. **Not started.** Decide, case by case, whether the grey (`bg-slate-100`) and blue
   (`border-[var(--bs-primary)]`) "selected" states are the same concept as this one, or genuinely
   mean something else.

### Verified during rollout

- **Theme variables are not tree-shaken.** All three reach `:root` in the built CSS even with no
  utility referencing them, so `bs-vars.css` can safely `var()` them. Worth re-checking if the tokens
  ever move to `@theme inline`, which resolves at build time instead.
- **Utilities generate as documented**, opacity modifiers included, and Lightning CSS emits a flat hex
  fallback (`#eab3088c`) beside the `color-mix()` for browsers that lack it.
- **`in oklab` vs `in srgb` does not matter here.** Tailwind's generated utilities mix in oklab while
  the hand-written rules in `index.css` use srgb; mixing against `transparent` only scales alpha, so
  both produce the same colour.
