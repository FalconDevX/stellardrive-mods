import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, write_palette, write_preview_obj

OUT = sys.argv[1]
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None

el = Mesh()
el.box((0, 0.04, 0), (0.92, 0.08, 0.92), 'dark')
el.box((0, 0.12, 0), (0.84, 0.08, 0.84), 'grey')
for x, band in ((-0.22, 'cyan'), (0.22, 'blue')):
    el.cylinder((x, 0.55, 0.16), 0.18, 0.78, 'white', 14, cap_color='grey')
    el.cylinder((x, 0.6, 0.16), 0.185, 0.07, band, 14)
    el.cylinder((x, 0.97, 0.16), 0.08, 0.06, 'dark', 10)
el.box((0, 0.42, -0.24), (0.76, 0.52, 0.34), 'yellow', {'top': 'dark'})
el.box((0, 0.48, -0.412), (0.46, 0.26, 0.01), 'grey')
el.box((0, 0.48, -0.418), (0.36, 0.18, 0.01), 'screen')
for k, c in enumerate(('cyan', 'blue', 'white')):
    el.box((-0.24 + k * 0.24, 0.26, -0.415), (0.1, 0.05, 0.02), c)
el.box((0, 0.98, 0.16), (0.52, 0.05, 0.05), 'dark')
el.cylinder((0, 0.78, -0.05), 0.03, 0.45, 'dark', 8)

el.write(os.path.join(OUT, 'electrolyzer.bin'))
write_palette(os.path.join(OUT, 'hydrogen_albedo.png'))
if PREVIEW:
    tex = os.path.join(OUT, 'hydrogen_albedo.png')
    write_preview_obj(os.path.join(PREVIEW, 'electrolyzer.obj'), tex, ((el, (0, 0, 0)),))
