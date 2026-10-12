import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, write_preview_obj

OUT = sys.argv[1]
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None

jp = Mesh()
jp.box((0, 0, 0.03), (0.34, 0.44, 0.06), 'dark')
jp.box((0, 0.12, 0.07), (0.2, 0.12, 0.04), 'orange')
for x in (-0.09, 0.09):
    jp.cylinder((x, 0.0, 0.13), 0.075, 0.38, 'white', 10, cap_color='grey')
    jp.cylinder((x, 0.05, 0.13), 0.078, 0.04, 'orange', 10)
    jp.cylinder((x, -0.23, 0.13), 0.04, 0.08, 'dark', 8, radius_top=0.06)
jp.box((0, 0.21, 0.13), (0.26, 0.04, 0.05), 'dark')

jp.write(os.path.join(OUT, 'jetpack.bin'))
if PREVIEW:
    write_preview_obj(PREVIEW, os.path.join(OUT, 'reactor_parts.png'), ((jp, (0, 0, 0)),))
