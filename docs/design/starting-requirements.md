# Starting Requirements

What a database needs before a new game can be created. Creating a game spawns each player's starting
units — a **settler** and a **scout** — from the unit template catalogue.

**Database → Seed in the Entity Setup Tool creates all of it.** The seed writes the reference data (tag
types, geographies, biomes, map resource categories) *and* the unit-design catalogue the starter units need:
the organisation tags, the human body and the components that carry those tags, and the `settler` and
`scout` templates themselves. A freshly seeded database can create a game with no hand-authoring at all.

This page is the checklist for what that catalogue contains and how to change it. See
[military-organisation.md](units/military-organisation.md#leadership-model) for *why* these units are unled.

---

## Order matters

Each step depends on the one before it, and the seed runs them in this order for that reason:

1. **Organisation tags** — the components reference them by code.
2. **Components** — the templates are assembled from them.
3. **Templates** — compiled on save, which resolves the organisation tag from the components.

The seed is idempotent: every step goes through the same upsert handler the authoring tools use, matching on
`Code`, so re-running it updates the rows to match the code values and leaves anything added by hand alone.

A catalogue can also be moved between databases with the Entity Setup Tool's catalogue export/import zip,
which carries the tags (including their organisation fields), the components and the templates.

---

## 1. Organisation tags

Seeded from `TagSeedData`, and editable under **Tags** in the Entity Setup Tool. The two that matter are
`Military Organisational Group` tags with a **Leadership Model of `Unled`**, which is what makes them
officer-free and what supplies their spawn size.

| Field | Settler | Scout |
| --- | --- | --- |
| Code | `settler` | `scout` |
| Display Name | Settler | Scout |
| Tag Type | Military Organisational Group | Military Organisational Group |
| Movement Domain | Land | Land |
| Leadership Model | **Unled** | **Unled** |
| Unled Base (quantum) | 25 | 1 |
| Unled Default | 100 | 1 |
| Unled Max | 200 | 1 |

**Default is the spawn size.** A new game spawns each settler at 100 and each scout at 1. Change the
Default in `TagSeedData` (or in the Tag manager) to change what players start with.

The three sizes are validated together: all must be above 0, `Base ≤ Default ≤ Max`, and `Default` must be a
whole multiple of `Base`. The Tag manager, the seed and the server all enforce the same rules.

### The commissioned tag

`infantry` is the fallback organisation ordinary units get:

| Field | Value |
| --- | --- |
| Code | `infantry` |
| Tag Type | Military Organisational Group |
| Movement Domain | Land |
| Leadership Model | Commissioned |

Its unled sizes are blank — the server rejects them on a commissioned tag. A commissioned tag's sizing comes
from the player's own structure tiers under the officer rank ceilings, which players edit on the Military
Advisor's Organisation page. (Unled tags appear there read-only; there are no tiers to edit.)

### The rest of the seeded tags

`cavalry`, `light-armour`, `heavy-armour`, `artillery` and `ranged` are Military Organisational Group tags on
the Land domain with **no Leadership Model set** — placeholders for organisations that have not been designed
yet. A null model is legal on an organisation tag; it simply means nothing resolves to it yet.

Four `Ability` tags come with them: `self-managed`, `fully-packed`, `create-settlement` and
`create-early-camp`.

---

## 2. Components

Seeded from `UnitComponentSeedData`, and editable under **Components**. Every numeric stat uses the
**Scale 1,000 fixed-point convention** — 1,000 means 1.0. The seed writes raw values; the editors convert at
the edit boundary, so enter the human-friendly value where the field offers one.

### The body

| Field | Value |
| --- | --- |
| Code | `human` |
| Display Name | Human |
| Component Type | Body |
| Movement Domain | Land |
| Campaign Movement Points | 1000 *(fixed-point: 1,000 = 1.0)* |
| Battlefield Movement Points | *derived, not authored* — computed at compile time; see [unit-movement.md](units/unit-movement.md#the-allowance--built). |
| Power | 7000 |
| Hitpoints | 0 |
| Weight | 1000 |
| Detection | 1000 |
| Sight | 1000 |
| Designer Image | `/unit-design/base/human-primitive_full-size.png` |
| Tags | `infantry` |

The body exposes seven slots. A fitted component is rejected unless it sits in one of the body's own slots
and satisfies that slot's rules, so the rule and the component's type must line up:

| Slot code | Display name | Accepts (rule) |
| --- | --- | --- |
| `slot-1` | Primary Weapon | Component Type **in** Armour, Weapon |
| `slot-2` | Secondary Weapon | Component Type **in** Armour |
| `slot-4` | Head | Component Type **equals** Armour |
| `slot-5` | Legs | Component Type **equals** Armour |
| `slot-6` | Equipment 1 | Component Type **equals** Equipment |
| `slot-7` | Chest | Component Type **equals** Armour |
| `training-1` | Training 1 | Component Type **equals** Training |

The gap at `slot-3` is historical. Slot codes are the portable identity — a template names the slot it fits
into by code, and the seed matches slots by code too, so renaming one drops the old slot and everything
fitted to it.

### The organisation-carrying components

| Field | Camping Materials | Scout Training | Basic Recon |
| --- | --- | --- | --- |
| Code | `camping-materials` | `scout-training` | `basic-recon` |
| Component Type | Equipment | Training | Training |
| Tags | **`settler`**, `create-settlement`, `create-early-camp` | **`scout`** | **`scout`** |
| Weight | 6000 | 0 | 0 |
| Detection | — | 2000 | 0 |
| Sight | — | — | 1000 |
| Designer Image | `/unit-design/equipment/camping-goods_medium.png` | — | — |

The tag on these components is the whole mechanism: **a template takes its organisation from its
components**, and a fitted component's organisation tag overrides the body's. That is how the same human
body becomes a settler party or a lone scout.

`scout-training` and `basic-recon` both carry `scout`, so either resolves a template to the same unled
organisation; they differ in whether they buy detection or sight. The `scout` template fits `basic-recon`.

Two *different* organisation tags on the fitted components of one template make its organisation ambiguous
and **fail compilation** — so don't put both `settler` and `scout` on one design.

### The weapons

`primitive-spear` (Weapon, weight 1000, 1000 Pierce) and `club` (Weapon, weight 0, 1000 Crush) are seeded for
the body's weapon slots. Neither starter unit fits one.

---

## 3. Templates

Seeded from `StarterTemplateSeedData`, and editable in the **Unit Designer** as global/common templates. Each
is the body plus one fitted component:

| | Settler | Scout |
| --- | --- | --- |
| Code | `settler` | `scout` |
| Display Name | Settler | Scout |
| Base | `human` | `human` |
| `slot-6` (Equipment 1) | `camping-materials` | *(empty)* |
| `training-1` (Training 1) | *(empty)* | `basic-recon` |
| Map Image | `settlers_40px.png` | `tracker_40px.png` |
| Resolves to | Settler tag → Unled, 100 | Scout tag → Unled, 1 |

Game setup looks for the codes `SETTLER` and `SCOUT`. **Matching is case-insensitive**, which is what lets the
lower-case seeded codes satisfy it — and it matters for hand-authoring too, because the Unit Designer derives
the code from the display name and lower-cases it.

Templates compile automatically when saved, and the seed's bulk upsert compiles each one as it writes it, so
a seeded template arrives with a live snapshot. If you later edit a component the templates were built from,
their snapshots go stale — run **recompile all templates** to refresh them, otherwise fielded units keep the
old stats.

### The map image gotcha

A Unit with an unresolvable map image is **not drawn on the campaign map at all** — no placeholder, no
marker. The map resolves a template's `MapImagePath` only against the frontend's bundled
`src/assets/units/*` files, matched on the filename.

The seed sets those filenames directly. In the Unit Designer, developer mode lets you type the path (and
refuses to save it empty); outside developer mode a saved global template takes its map image from the
**base body's designer image** instead — which for `human` is not a bundled file, so a template saved that
way spawns correctly and is invisible.

---

## 4. Map start positions

Each player is placed on an authored start position, so **the gallery map must have at least as many start
positions as the game has players**. The seed does not create these — add them in the map editor. Positions
are taken best-scored first.

---

## Verifying the setup

Create a new game. If the catalogue is complete, each player begins with a 100-strong settler and a scout on
their start tile. If not, game creation fails with the specific reason:

| Message | Fix |
| --- | --- |
| "Starter template(s) not found in the catalogue…" | Run Database → Seed, or author the missing global template (step 3). |
| "…has no compiled snapshot — recompile the catalogue templates." | Run recompile all templates. |
| "…is not unled — starting units must resolve to an unled organisation tag…" | The template resolved to a commissioned tag or none: check the fitted component carries `settler` / `scout`, and that the tag's Leadership Model is Unled (step 1). |
| "…which has no authored UnledDefaultSize." | Fill in the tag's unled sizes (step 1). |
| "The map has N start position(s) but the game has M players…" | Add start positions (step 4). |

Nothing is left half-built: a failure aborts game creation rather than starting a game without units, and a
failed seed step aborts the seed with the handler's own errors recorded against the background job.

---

## Adding another starting unit later

`GameSetupService.StarterTemplateCodes` lists the codes every player starts with. Adding a code there makes
that template a hard requirement for every new game, so author the template first — in `StarterTemplateSeedData`
if it should be seeded — then add the code.

Each starter must resolve to an **unled** organisation tag — the spawn size is the tag's `UnledDefaultSize`,
and a commissioned template has no equivalent single default.

Keeping this list in code rather than in data is a deliberate decision, with the reasoning and the trigger
for revisiting it recorded in
[player-unlocks.md](unit-design/player-unlocks.md#the-starter-set-stays-a-code-constant).
