"""Generate TechnologySeedData.cs from the parsed Tech.xlsx diagram."""
import io
import json
import os
import re
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__)) + '/'
OUT = HERE + '../../AscendantEmpire.Application/Features/DatabaseSeeding/SeedData/TechnologySeedData.cs'

# Stray duplicate boxes: a second box carrying a name another box already has.
#   168 'Plow animals' - isolated, no arrows at all; 'Plow Animals' carries the three prerequisites.
#   389 / 390 - fed BY Terrain Mastery, where the kept box feeds INTO it. Merging would make a two-node cycle.
DROP_SHAPES = {'168', '389', '390'}

# Typos corrected in display names only, because the code is derived from the name and has to be stable.
NAME_FIXES = {
    'Fundamentals of Military Serviice': 'Fundamentals of Military Service',
}

# Annotation text that names a seeded ExperienceType. Matched against the annotation lines, longest key first.
EXPERIENCE_WORDS = [
    ('wood-cutting', 'Extraction.Woodcutting'),
    ('woodcutting', 'Extraction.Woodcutting'),
    ('quarrying', 'Extraction.Quarrying'),
    ('gathering', 'Extraction.Foraging'),
    ('foraging', 'Extraction.Foraging'),
    ('hunting', 'Extraction.Hunting'),
    ('fishing', 'Extraction.Fishing'),
    ('mining', 'Extraction.Mining'),
]

_DIRECTED_BASE = 100
_DIRECTED_STEP = 100

d = json.load(io.open(HERE + 'tech_parsed.json', encoding='utf-8'))
shapes = {s['id']: s for s in d['shapes'] if s['id'] not in DROP_SHAPES and s['lines']}


def display_of(sid):
    raw = shapes[sid]['lines'][0]
    return NAME_FIXES.get(raw, raw)


def code_of(sid):
    """ProperCase with non-alphanumerics stripped - the same normalisation the asset upsert uses."""
    return ''.join(w[0].upper() + w[1:]
                   for w in re.split(r'[^a-zA-Z0-9]+', display_of(sid)) if w)


# --- Edges -----------------------------------------------------------------------------
prereqs = defaultdict(dict)
for c in d['connectors']:
    if not (c['start'] and c['end']):
        continue
    if c['start'] in DROP_SHAPES or c['end'] in DROP_SHAPES:
        continue
    if c['start'] not in shapes or c['end'] not in shapes:
        continue
    # Arrowheads sit at the connector's far end, so start is the prerequisite and end the dependent.
    kind = 'Optional' if c['dash'] == 'dash' else 'Mandatory'
    # A pair drawn twice, once solid and once dashed, is taken as mandatory: the stricter reading.
    if prereqs[c['end']].get(c['start']) != 'Mandatory':
        prereqs[c['end']][c['start']] = kind

# --- Depth, which the cost curve is derived from ---------------------------------------
depth = {}


def depth_of(sid, seen=()):
    if sid in depth:
        return depth[sid]
    if sid in seen:
        return 0
    ps = prereqs[sid]
    depth[sid] = 0 if not ps else 1 + max(depth_of(p, seen + (sid,)) for p in ps)
    return depth[sid]


for sid in shapes:
    depth_of(sid)


def experience_for(sid):
    text = ' '.join(shapes[sid]['lines'][1:]).lower()
    found = []
    for word, type_code in EXPERIENCE_WORDS:
        if word in text and type_code not in found:
            found.append(type_code)
    return found


def description_for(sid):
    lines = shapes[sid]['lines'][1:]
    return ' '.join(line.strip() for line in lines).strip()


def cs(text):
    return '"%s"' % text.replace('\\', '\\\\').replace('"', '\\"')


# --- Emit ------------------------------------------------------------------------------
order = sorted(shapes, key=lambda sid: (depth[sid], display_of(sid).lower()))

parts = []
for sid in order:
    display = display_of(sid)
    directed = _DIRECTED_BASE + depth[sid] * _DIRECTED_STEP
    experience = experience_for(sid)
    description = description_for(sid)

    body = []
    body.append('                Code = %s,' % cs(code_of(sid)))
    body.append('                DisplayName = %s,' % cs(display))
    if description:
        body.append('                Description = %s,' % cs(description))
    body.append('                Era = TechnologyEra.Stone,')
    body.append('                DirectedRequired = %d,' % directed)
    if experience:
        body.append('                ExperienceRequired = %d,' % (directed // 2))
        body.append('                ExperienceTypeCodes = [%s],' % ', '.join(cs(e) for e in experience))

    ps = prereqs[sid]
    if ps:
        rows = []
        for pid in sorted(ps, key=lambda p: display_of(p).lower()):
            if ps[pid] == 'Optional':
                rows.append('                    new UpsertTechnologyPrerequisiteDto\n'
                            '                    {\n'
                            '                        PrerequisiteTechnologyCode = %s,\n'
                            '                        PrerequisiteKind = TechnologyPrerequisiteKind.Optional\n'
                            '                    }' % cs(code_of(pid)))
            else:
                rows.append('                    new UpsertTechnologyPrerequisiteDto '
                            '{ PrerequisiteTechnologyCode = %s }' % cs(code_of(pid)))
        body.append('                Prerequisites =\n                [\n%s\n                ],' % ',\n'.join(rows))

    # Drop the trailing comma of the last member.
    body[-1] = body[-1].rstrip(',')

    parts.append('            new UpsertTechnologyDto\n            {\n%s\n            }' % '\n'.join(body))

optional_count = sum(1 for sid in prereqs for k in prereqs[sid].values() if k == 'Optional')
edge_count = sum(len(v) for v in prereqs.values())
roots = [sid for sid in shapes if not prereqs[sid]]
experience_count = sum(1 for sid in shapes if experience_for(sid))

header = '''using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// The initial <c>technology.Technology</c> catalogue applied by the database seed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Generated from the "Tech Map" sheet of <c>AscendantEmpire_Files/Resources/Tech.xlsx</c>.</b> Each box in that diagram is one technology and each arrow
/// is a prerequisite edge, with a dashed arrow meaning an optional one. {TECH_COUNT} technologies and {EDGE_COUNT} edges came across, {OPTIONAL_COUNT} of them
/// optional. The graph is acyclic and {DEPTH_MAX} levels deep.
/// </para>
/// <para>
/// Matched by code, so re-running the seed renames or re-orders a row rather than duplicating it, and never removes one added by hand. Codes are derived from
/// the display name - ProperCase with non-alphanumerics stripped - and are <b>flat, with no era prefix</b>. That is deliberate: a code is a stable
/// cross-reference key, and prefixing it with a field this file is explicitly unsure about (see below) would mean re-keying the whole catalogue later.
/// </para>
/// <para>
/// SortOrder is left unset throughout and derived from list position, and the list is ordered by depth in the tree and then by name, so roots come first and
/// a prerequisite is always written before the technology that needs it. Icon paths are left unset until artwork exists.
/// </para>
///
/// <para><b>What the diagram did not carry, and what was assumed instead:</b></para>
/// <list type="bullet">
///   <item><description>
///     <b>Eras.</b> The diagram has no era dimension - no bands, and its fill colours mark status rather than age. Everything is therefore
///     <c>TechnologyEra.Stone</c>, which is certainly wrong for the later entries: Copper, Metallurgy, Writing, Banking and Brickworks are not Stone-age
///     technologies. Era only matters to the <c>RandomisedEqual</c> requirement generator, which is not implemented, so nothing reads it yet - but it wants
///     assigning before that lands.
///   </description></item>
///   <item><description>
///     <b>Research costs.</b> The diagram has no numbers. <c>DirectedRequired</c> is derived from depth in the tree as
///     <c>{DIRECTED_BASE} + depth * {DIRECTED_STEP}</c>, so a root costs {DIRECTED_BASE} and the deepest costs {DIRECTED_MAX}. That is a placeholder curve,
///     not a balance decision.
///   </description></item>
///   <item><description>
///     <b>Experience sources.</b> {EXPERIENCE_COUNT} technologies carry an annotation naming something that matches a seeded <c>ExperienceType</c> - "Bonus:
///     Hunting", "Bonus: Fishing" and so on. Those get an experience bucket of half their directed requirement, fed by the matching type. The rest are
///     directed-only, because the diagram gives nothing to connect them to.
///   </description></item>
///   <item><description>
///     <b>Grants.</b> Around a hundred boxes carry <c>Gives:</c>, <c>Asset:</c>, <c>Unit:</c>, <c>Building:</c>, <c>Specialist:</c> or <c>Action:</c>
///     annotations. Almost none of them name anything that exists in the Asset, unit-component or template catalogues, so they could not become
///     <c>TechnologyGrant</c> rows - the upsert would reject the batch for an unresolvable target. <b>Every annotation line is preserved verbatim in
///     <c>Description</c></b> instead, so the information is here when those targets exist. Wiring them up is outstanding work, and doing so means growing
///     <c>TechnologySeedStep.DependsOn</c> to cover whichever catalogues they point at.
///   </description></item>
///   <item><description>
///     <b>Acquisition costs.</b> None, and none were invented. No Asset stockpile is modelled yet - <c>AssetBank</c> and <c>AssetBankBalance</c> are
///     unmapped - so a technology with cost rows could not be unlocked at all.
///   </description></item>
/// </list>
///
/// <para><b>Known gaps in the source diagram:</b></para>
/// <list type="bullet">
///   <item><description>
///     <b>17 arrows were lost.</b> Excel only records a connector's endpoints when both ends are dropped onto a shape; those 17 were left floating, so the
///     file holds a line position and nothing else. Resolving them to the nearest shape produced visibly wrong pairs, so they were discarded rather than
///     guessed. They cluster around Early Diplomacy / Oral Tradition / Early Trade / Band Leadership, and around Herb Lore / Ochre / the Climate and Terrain
///     Skills. Re-attaching them in the spreadsheet and regenerating is the fix.
///   </description></item>
///   <item><description>
///     <b>{ROOT_COUNT} technologies have no prerequisite at all</b>, including Banking, Writing, Metallurgy, Military, Horse Riding, Brickworks, Ceramics and
///     Political Organisation. Some of that is the 17 lost arrows; the rest are arrows that were never drawn. Taken literally this is the starting set a new
///     game would grant, which is implausibly large - so <c>GameSetupService.StarterTechnologyCodes</c> should name its starters explicitly rather than
///     inferring them from this.
///   </description></item>
///   <item><description>
///     <b>Three stray duplicate boxes were dropped.</b> A second "Plow animals" box with no arrows on it, and second "Terrain Skill - Coastal" and "Terrain
///     Skill - Ocean" boxes which were fed BY Terrain Mastery where the kept boxes feed INTO it - merging those would have formed a two-node cycle.
///   </description></item>
///   <item><description>
///     <b>One display name was corrected:</b> "Fundamentals of Military Serviice" to "Fundamentals of Military Service". The code derives from the name and
///     has to be stable, so the typo was worth fixing before anything referenced it. Every other name is verbatim from the diagram, including the spelling
///     inside annotation text.
///   </description></item>
/// </list>
/// </remarks>
internal static class TechnologySeedData
{
    /// <summary>
    /// Builds the whole technology catalogue.
    /// </summary>
    public static IReadOnlyCollection<UpsertTechnologyDto> Build()
    {
        return
        [
'''

header = (header
          .replace('{TECH_COUNT}', str(len(shapes)))
          .replace('{EDGE_COUNT}', str(edge_count))
          .replace('{OPTIONAL_COUNT}', str(optional_count))
          .replace('{DEPTH_MAX}', str(max(depth.values()) + 1))
          .replace('{DIRECTED_BASE}', str(_DIRECTED_BASE))
          .replace('{DIRECTED_STEP}', str(_DIRECTED_STEP))
          .replace('{DIRECTED_MAX}', str(_DIRECTED_BASE + max(depth.values()) * _DIRECTED_STEP))
          .replace('{EXPERIENCE_COUNT}', str(experience_count))
          .replace('{ROOT_COUNT}', str(len(roots))))

body = ',\n'.join(parts)
footer = '\n        ];\n    }\n}\n'

io.open(OUT, 'w', encoding='utf-8-sig', newline='\r\n').write(header + body + footer)

print('technologies %d  edges %d  optional %d  roots %d  experience %d  maxdepth %d'
      % (len(shapes), edge_count, optional_count, len(roots), experience_count, max(depth.values())))
print('longest display name %d, longest code %d'
      % (max(len(display_of(s)) for s in shapes), max(len(code_of(s)) for s in shapes)))
print('longest description %d' % max((len(description_for(s)) for s in shapes), default=0))
print('written', OUT)
