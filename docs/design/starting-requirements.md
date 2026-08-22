# Starting Requirements

What a database needs before a new game can be created. Creating a game spawns each player's starting
units — a **settler** and a **scout** — from the unit template catalogue. That catalogue is **authored by
hand**; nothing in the application creates it, and game setup fails with a specific error when it is missing
or incomplete.

This page is the checklist for configuring it. See
[military-organisation.md](units/military-organisation.md#leadership-model) for *why* these units are unled.

> **The database seed does not cover this.** Database → Seed in the Entity Setup Tool seeds reference data
> only (tag types, geographies, biomes). Unit design content is deliberately excluded.

---

## Order matters

Each step depends on the one before it, so author them in this order:

1. **Organisation tags** — the components reference them by code.
2. **Components** — the templates are assembled from them.
3. **Templates** — compiled on save, which resolves the organisation tag from the components.

A catalogue authored once can be moved between databases with the Entity Setup Tool's catalogue
export/import zip, which carries the tags (including their organisation fields), the components and the
templates. That is usually faster than re-authoring.

---

## 1. Organisation tags

Author under **Tags** in the Entity Setup Tool. Both are `Military Organisational Group` tags with a
**Leadership Model of `Unled`**, which is what makes them officer-free and what supplies their spawn size.

| Field | Settler | Scout |
| --- | --- | --- |
| Code | `settler` | `scout` |
| Display Name | Settler | Scout |
| Tag Type | Military Organisational Group | Military Organisational Group |
| Movement Domain | Land | Land |
| Leadership Model | **Unled** | **Unled** |
| Unled Base (quantum) | 25 | 1 |
| Unled Default | 200 | 1 |
| Unled Max | 5,000 | 1 |

**Default is the spawn size.** A new game spawns each settler at 200 and each scout at 1. Change the
Default to change what players start with.

The three sizes are validated together: all must be above 0, `Base ≤ Default ≤ Max`, and `Default` must be a
whole multiple of `Base`. The Tag manager and the server enforce the same rules.

### Optionally, a commissioned tag for everything else

Not required for the settler and scout, but worth authoring at the same time so ordinary units have an
organisation to fall back on:

| Field | Value |
| --- | --- |
| Code | `infantry` |
| Tag Type | Military Organisational Group |
| Movement Domain | Land |
| Leadership Model | Commissioned |

Leave the unled sizes blank — the server rejects them on a commissioned tag. A commissioned tag's sizing
comes from the player's own structure tiers under the officer rank ceilings, which players edit on the
Military Advisor's Organisation page. (Unled tags appear there read-only; there are no tiers to edit.)

---

## 2. Components

Author under **Components**. Three are needed: one body to build on, and one component per starting unit
carrying that unit's organisation tag.

### The body

| Field | Value |
| --- | --- |
| Code | `human-body` |
| Component Type | Body |
| Movement Domain | Land |
| Campaign Movement Points | 1000 *(fixed-point: 1,000 = 1.0)* |
| Battlefield Movement Points | 2000 *(battle-map movement is double the campaign map)* |
| Power / Hitpoints | 1000 each |
| Detection | 1000 |
| Tags | `infantry`, if you authored it |

Every numeric stat uses the **Scale 1,000 fixed-point convention** — 1,000 means 1.0. The editors convert
at the edit boundary, so enter the human-friendly value where the field offers one.

**The body needs two slots**, because the templates fit their organisation-carrying component into one:

| Slot | Accepts (rule) |
| --- | --- |
| Pack | Component Type **equals** Equipment |
| Training | Component Type **equals** Training |

A fitted component is rejected unless it sits in one of the body's own slots and satisfies that slot's
rules, so the rule and the component's type must line up.

### The two organisation-carrying components

| Field | Camping Goods | Scout Training |
| --- | --- | --- |
| Code | `camping-goods` | `scout-training` |
| Component Type | Equipment | Training |
| Tags | **`settler`** | **`scout`** |
| Weight | 2000 | 0 |
| Detection | — | 2000 |
| Visibility | 1000 | — |

The tag on these components is the whole mechanism: **a template takes its organisation from its
components**, and a fitted component's organisation tag overrides the body's. That is how the same human
body becomes a settler party or a lone scout.

Two *different* organisation tags on the fitted components of one template make its organisation ambiguous
and **fail compilation** — so don't put both `settler` and `scout` on one design.

---

## 3. Templates

Author in the **Unit Designer**, saving as a global/common template. Each is the body plus one fitted
component:

| | Settler | Scout |
| --- | --- | --- |
| Code | `settler` | `scout` |
| Display Name | Settler | Scout |
| Base | `human-body` | `human-body` |
| Pack slot | `camping-goods` | *(empty)* |
| Training slot | *(empty)* | `scout-training` |
| Resolves to | Settler tag → Unled, 200 | Scout tag → Unled, 1 |

Game setup looks for the codes `SETTLER` and `SCOUT`. **Matching is case-insensitive**, which matters
because the Unit Designer derives the code from the display name and lower-cases it — saving a global
template named "Settler" produces the code `settler`, and that satisfies the requirement.

Templates compile automatically when saved. If you later edit a component the templates were built from,
their snapshots go stale — run **recompile all templates** to refresh them, otherwise fielded units keep the
old stats.

### The map image gotcha

A troop with an unresolvable map image is **not drawn on the campaign map at all** — no placeholder, no
marker. The Unit Designer takes a global template's map image from the **base body's designer image**, and
the map resolves it only against the frontend's bundled `src/assets/units/*` files.

So give `human-body` a designer image that is one of those bundled files (for example
`assets/units/primitive-man-with-club_40px.png`), or the settler and scout will spawn correctly and be
invisible.

---

## 4. Map start positions

Each player is placed on an authored start position, so **the gallery map must have at least as many start
positions as the game has players**. Add them in the map editor. Positions are taken best-scored first.

---

## Verifying the setup

Create a new game. If the catalogue is complete, each player begins with a 200-strong settler and a scout on
their start tile. If not, game creation fails with the specific reason:

| Message | Fix |
| --- | --- |
| "Starter template(s) not found in the catalogue…" | Author the missing global template (step 3). |
| "…has no compiled snapshot — recompile the catalogue templates." | Run recompile all templates. |
| "…is not unled — starting units must resolve to an unled organisation tag…" | The template resolved to a commissioned tag or none: check the fitted component carries `settler` / `scout`, and that the tag's Leadership Model is Unled (step 1). |
| "…which has no authored UnledDefaultSize." | Fill in the tag's unled sizes (step 1). |
| "The map has N start position(s) but the game has M players…" | Add start positions (step 4). |

Nothing is left half-built: a failure aborts game creation rather than starting a game without units.

---

## Adding another starting unit later

`GameSetupService.StarterTemplateCodes` lists the codes every player starts with. Adding a code there makes
that template a hard requirement for every new game, so author the template first, then add the code.

Each starter must resolve to an **unled** organisation tag — the spawn size is the tag's `UnledDefaultSize`,
and a commissioned template has no equivalent single default.
