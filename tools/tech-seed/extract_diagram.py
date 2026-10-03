"""Extract the Tech Map diagram, preserving each shape's lines and fill colour."""
import io
import json
import re
import os
import sys
import xml.etree.ElementTree as ET
import zipfile

# The workbook lives outside the repository; override with the first argument.
XLSX = sys.argv[1] if len(sys.argv) > 1 else 'P:/Development/AscendantEmpire_Files/Resources/Tech.xlsx'
HERE = os.path.dirname(os.path.abspath(__file__)) + '/'


NS = {
    'xdr': 'http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing',
    'a': 'http://schemas.openxmlformats.org/drawingml/2006/main',
}

with zipfile.ZipFile(XLSX) as z:
    root = ET.fromstring(z.read('xl/drawings/drawing1.xml'))


def lines_of(sp):
    """One entry per paragraph, so the shape's first line stays separable from its annotations."""
    out = []
    for p in sp.findall('.//a:p', NS):
        # Soft line breaks inside one paragraph are as meaningful as paragraph breaks here, so both split a line.
        buf = ''
        for node in p.iter():
            tag = node.tag.split('}')[-1]
            if tag == 't':
                buf += node.text or ''
            elif tag == 'br':
                if buf.strip():
                    out.append(re.sub(r'\s+', ' ', buf).strip())
                buf = ''
        if buf.strip():
            out.append(re.sub(r'\s+', ' ', buf).strip())
    return out


def fill_of(sp):
    """The shape's solid fill, as a theme name or an explicit hex, when it has one."""
    spPr = sp.find('xdr:spPr', NS)
    if spPr is None:
        return None
    solid = spPr.find('a:solidFill', NS)
    if solid is None:
        return None
    srgb = solid.find('a:srgbClr', NS)
    if srgb is not None:
        return srgb.get('val')
    scheme = solid.find('a:schemeClr', NS)
    if scheme is not None:
        shade = scheme.find('a:lumMod', NS)
        tint = scheme.find('a:lumOff', NS)
        return '%s/%s/%s' % (
            scheme.get('val'),
            shade.get('val') if shade is not None else '-',
            tint.get('val') if tint is not None else '-')
    return None


def frame_of(el):
    xfrm = el.find('.//a:xfrm', NS)
    if xfrm is None:
        return None
    off = xfrm.find('a:off', NS)
    ext = xfrm.find('a:ext', NS)
    if off is None or ext is None:
        return None
    return {'x': int(off.get('x')), 'y': int(off.get('y')),
            'cx': int(ext.get('cx')), 'cy': int(ext.get('cy'))}


shapes = {}
connectors = []

for anchor in root:
    frm = anchor.find('xdr:from', NS)
    col = int(frm.find('xdr:col', NS).text) if frm is not None else None
    row = int(frm.find('xdr:row', NS).text) if frm is not None else None

    for sp in anchor.iter('{%s}sp' % NS['xdr']):
        nv = sp.find('.//xdr:nvSpPr/xdr:cNvPr', NS)
        if nv is None:
            continue
        shapes[nv.get('id')] = {
            'id': nv.get('id'),
            'lines': lines_of(sp),
            'fill': fill_of(sp),
            'frame': frame_of(sp),
            'col': col,
            'row': row,
        }

    for cxn in anchor.iter('{%s}cxnSp' % NS['xdr']):
        nv = cxn.find('.//xdr:nvCxnSpPr/xdr:cNvPr', NS)
        st = cxn.find('.//a:stCxn', NS)
        en = cxn.find('.//a:endCxn', NS)
        ln = cxn.find('.//a:ln', NS)
        d = ln.find('a:prstDash', NS) if ln is not None else None
        connectors.append({
            'id': nv.get('id') if nv is not None else None,
            'start': st.get('id') if st is not None else None,
            'end': en.get('id') if en is not None else None,
            'dash': d.get('val') if d is not None else None,
            'frame': frame_of(cxn),
        })

io.open(HERE + 'tech_parsed.json', 'w', encoding='utf-8').write(
    json.dumps({'shapes': list(shapes.values()), 'connectors': connectors}, indent=1))

print('shapes %d  connectors %d  resolved %d  dashed %d'
      % (len(shapes), len(connectors),
         sum(1 for c in connectors if c['start'] and c['end']),
         sum(1 for c in connectors if c['dash'] == 'dash')))
print()
print('fills:')
fills = {}
for s in shapes.values():
    fills.setdefault(s['fill'], []).append(s['lines'][0] if s['lines'] else '?')
for f, names in sorted(fills.items(), key=lambda kv: -len(kv[1])):
    print('  %-28s %3d   e.g. %s' % (f, len(names), ', '.join(names[:4])))
print()
print('x-ranges (EMU) of shapes, to see whether columns encode an era:')
xs = sorted(s['frame']['x'] for s in shapes.values() if s['frame'])
print('  min %d  max %d  count %d' % (xs[0], xs[-1], len(xs)))
print()
print('annotation prefixes used on second-and-later lines:')
pref = {}
for s in shapes.values():
    for line in s['lines'][1:]:
        m = re.match(r'([A-Za-z][A-Za-z &/]{0,20}):', line)
        key = m.group(1) if m else '(no prefix)'
        pref.setdefault(key, 0)
        pref[key] += 1
for k, v in sorted(pref.items(), key=lambda kv: -kv[1]):
    print('  %-16s %d' % (k, v))
