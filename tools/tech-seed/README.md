# Technology seed generator

Generates
[`TechnologySeedData.cs`](../../AscendantEmpire.Application/Features/DatabaseSeeding/SeedData/TechnologySeedData.cs)
from the **Tech Map** sheet of `AscendantEmpire_Files/Resources/Tech.xlsx`.

Every box on that sheet is one technology. Every arrow is a prerequisite edge, pointing from the
prerequisite to the technology that needs it; a **dashed** arrow is an optional prerequisite.

```bash
python tools/tech-seed/extract_diagram.py
```

```bash
python tools/tech-seed/generate_seed_data.py
```

The first reads the drawing out of the workbook and writes `tech_parsed.json` beside itself. The second
turns that into the C# seed data class, overwriting it in place. Pass a path to `extract_diagram.py` to
read a workbook from somewhere other than the default `P:/Development/AscendantEmpire_Files/Resources/Tech.xlsx`.

Only the standard library is used, so there is nothing to install.

## What the diagram does not carry

The generated file's XML remarks spell this out in full, and are the thing to read before trusting its
numbers. In short: the diagram has no eras, no research costs and no resolvable grant targets, so eras
default to `Stone`, directed costs are derived from depth in the tree, grant annotations are preserved as
description text rather than becoming `TechnologyGrant` rows, and acquisition costs are left empty.

## The 17 lost arrows

Excel records a connector's endpoints only when **both ends are dropped onto a shape**. Seventeen arrows
in the current workbook were left floating, so the file holds a line position and nothing else — and
resolving them to the nearest shape produces visibly wrong pairs, so the generator discards them. They
cluster around Early Diplomacy / Oral Tradition / Early Trade / Band Leadership, and around Herb Lore /
Ochre / the Climate and Terrain Skills.

To recover one: drag each end of the arrow until the shape shows its connection points and the end snaps
to one, then re-run both scripts. `extract_diagram.py` prints `resolved N` — that number going up means
arrows were recovered.

## Decisions baked into the generator

These are constants at the top of `generate_seed_data.py`, and changing them is the intended way to
revisit any of it:

- `DROP_SHAPES` — three stray duplicate boxes. A second "Plow animals" with no arrows on it, and second
  "Terrain Skill - Coastal" and "Terrain Skill - Ocean" boxes which are fed **by** Terrain Mastery where
  the kept boxes feed **into** it. Merging those would have formed a two-node cycle.
- `NAME_FIXES` — one corrected display name. The technology code is derived from the name and has to stay
  stable, so a typo is worth fixing before anything references it.
- `EXPERIENCE_WORDS` — the annotation text that maps onto a seeded `ExperienceType`. This is what gives 27
  of the 207 technologies an experience bucket; the rest are directed-only because the diagram offers
  nothing to connect them to.
- `_DIRECTED_BASE` / `_DIRECTED_STEP` — the placeholder cost curve, `100 + depth * 100`.

Codes are derived from the display name as ProperCase with non-alphanumerics stripped, and are **flat with
no era prefix**, deliberately: a code is a stable cross-reference key, and prefixing it with the era would
mean re-keying the whole catalogue once eras are actually assigned.
