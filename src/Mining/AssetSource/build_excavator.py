import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, write_palette, write_preview_obj

OUT = sys.argv[1]
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None

body = Mesh()
LX, LZ = 0.72, 0.62

for sx in (-1, 1):
    for sz in (-1, 1):
        x, z = sx * LX, sz * LZ
        body.box((x, 0.03, z), (0.42, 0.06, 0.42), 'dark')
        body.box((x, 0.10, z), (0.34, 0.08, 0.34), 'grey')
        body.box((x, 0.19, z), (0.24, 0.10, 0.24), 'dark')
        body.box((x, 0.49, z), (0.26, 0.50, 0.26), 'yellow', {'top': 'dark'})
        for face in (-1, 1):
            body.box((x, 0.49, z + face * 0.135), (0.07, 0.46, 0.015), 'dark')
        body.box((x + sx * 0.135, 0.49, z), (0.015, 0.46, 0.07), 'dark')
        body.box((x, 0.78, z), (0.30, 0.08, 0.30), 'grey')
        body.box((x, 0.84, z), (0.18, 0.05, 0.18), 'yellow')

for sz in (-1, 1):
    body.box((0, 0.62, sz * LZ), (2 * LX, 0.12, 0.12), 'grey', {'bottom': 'dark'})
for sx in (-1, 1):
    body.box((sx * LX, 0.62, 0), (0.12, 0.12, 2 * LZ), 'grey', {'bottom': 'dark'})
body.box((0.47, 0.72, 0.1), (0.12, 0.1, 2 * LZ - 0.1), 'dark')
body.box((-0.1, 0.72, 0.1), (0.12, 0.1, 2 * LZ - 0.1), 'dark')

HX, HY, HZ = -0.3, 1.04, -0.36
body.box((HX, HY, HZ), (1.04, 0.42, 0.62), 'yellow', {'top': 'dark', 'bottom': 'dark'})
body.box((HX, HY + 0.01, HZ - 0.312), (0.66, 0.24, 0.01), 'olive')
body.box((HX, HY + 0.01, HZ + 0.312), (0.66, 0.24, 0.01), 'olive')
body.box((HX - 0.522, HY + 0.01, HZ), (0.01, 0.24, 0.34), 'olive')
body.box((HX - 0.53, HY + 0.02, HZ - 0.12), (0.02, 0.15, 0.12), 'dark')
body.box((HX - 0.54, HY + 0.04, HZ - 0.12), (0.01, 0.04, 0.05), 'light')
body.box((HX, HY + 0.235, HZ), (1.08, 0.05, 0.66), 'grey')
body.box((HX + 0.04, HY + 0.285, HZ), (0.64, 0.05, 0.4), 'dark')
body.box((HX, HY - 0.225, HZ), (1.08, 0.04, 0.66), 'grey')
for k in range(4):
    body.box((HX + (-0.46 if k % 2 == 0 else 0.46), HY + (0.15 if k < 2 else -0.15), HZ - 0.316), (0.04, 0.04, 0.01), 'dark')

CX, CY, CZ = 0.25, 1.15, 0.1
body.box((CX, CY, CZ), (0.74, 0.64, 0.66), 'yellow', {'bottom': 'dark'})
FZ = CZ - 0.335
body.box((CX, CY + 0.18, FZ), (0.46, 0.07, 0.04), 'grey')
body.box((CX, CY - 0.18, FZ), (0.46, 0.07, 0.04), 'grey')
body.box((CX - 0.195, CY, FZ), (0.07, 0.43, 0.04), 'grey')
body.box((CX + 0.195, CY, FZ), (0.07, 0.43, 0.04), 'grey')
body.box((CX, CY, FZ + 0.01), (0.32, 0.29, 0.02), 'screen')
for k in range(6):
    body.box((CX - 0.27 + k * 0.11, CY - 0.27, FZ + 0.005), (0.05, 0.06, 0.02), 'black')
body.box((CX, CY + 0.335, CZ), (0.78, 0.04, 0.7), 'grey')

body.box((-0.3, 1.43, 0.22), (0.52, 0.36, 0.46), 'olive', {'top': 'grey'})
body.box((-0.3, 1.62, 0.22), (0.56, 0.04, 0.5), 'dark')
body.box((-0.3, 1.43, -0.015), (0.3, 0.12, 0.01), 'dark')
body.cylinder((-0.08, 1.52, 0.33), 0.035, 0.3, 'dark', 8)
body.cylinder((-0.08, 1.35, 0.33), 0.035, 0.3, 'dark', 8)
body.cylinder((-0.44, 1.8, 0.34), 0.05, 0.32, 'dark', 10)
body.cylinder((-0.44, 1.97, 0.34), 0.065, 0.04, 'black', 10)
EXHAUST_TOP = (-0.44, 1.99, 0.34)

TX, TZ = CX, CZ
for sx in (-1, 1):
    for sz in (-1, 1):
        body.box((TX + sx * 0.27, 1.72, TZ + sz * 0.2), (0.1, 0.56, 0.1), 'grey')
for sz in (-1, 1):
    body.box((TX, 2.02, TZ + sz * 0.2), (0.64, 0.1, 0.1), 'grey')
for sx in (-1, 1):
    body.box((TX + sx * 0.27, 2.02, TZ), (0.1, 0.1, 0.5), 'grey')
body.box((TX, 1.72, TZ), (0.3, 0.5, 0.28), 'yellow')
for sx in (-1, 1):
    for k in range(5):
        body.cylinder((TX + sx * 0.19, 1.52 + k * 0.09, TZ), 0.05, 0.05, 'black', 8)
body.cylinder((TX, 2.12, TZ), 0.15, 0.1, 'dark', 12)
body.cylinder((TX, 2.2, TZ), 0.155, 0.07, 'orange', 12)
body.cylinder((TX, 2.27, TZ), 0.14, 0.07, 'dark', 12, cap_color='black')

DX, DZ = CX, CZ
body.cylinder((DX, 0.79, DZ), 0.12, 0.08, 'dark', 12)
body.cylinder((DX, 0.735, DZ), 0.095, 0.03, 'grey', 12)

for sx, sz, cap in ((1, -1, 'grey'), (-1, 1, 'grey'), (-1, -1, 'yellow')):
    body.box((sx * (LX + 0.17), 0.42, sz * LZ), (0.08, 0.18, 0.16), 'dark', {'top': cap})
    body.box((sx * (LX + 0.15), 0.42, sz * LZ), (0.04, 0.22, 0.04), 'grey')

DRILL_PIVOT = (DX, 0.72, DZ)
drill = Mesh()
DRILL_LENGTH = 1.9
drill.cylinder((0, -0.04, 0), 0.075, 0.08, 'dark', 10)
drill.cylinder((0, -0.45, 0), 0.055, 0.74, 'grey', 8)
drill.cylinder((0, -0.83, 0), 0.075, 0.04, 'dark', 10)
drill.cylinder((0, -1.3, 0), 0.05, 0.92, 'steel', 8)
drill.cylinder((0, -1.83, 0), 0.0, 0.14, 'steel', 8, radius_top=0.055)
turns, steps = 5.0, 70
top, bottom = -0.86, -1.72
for k in range(steps):
    t0, t1 = k / steps, (k + 1) / steps
    a0, a1 = t0 * turns * 2 * math.pi, t1 * turns * 2 * math.pi
    y0, y1 = top + (bottom - top) * t0, top + (bottom - top) * t1
    r_in = 0.045
    r_out, r_out1 = 0.15 - 0.04 * t0, 0.15 - 0.04 * t1
    th = 0.03
    i0 = (math.cos(a0) * r_in, y0, math.sin(a0) * r_in); o0 = (math.cos(a0) * r_out, y0, math.sin(a0) * r_out)
    i1 = (math.cos(a1) * r_in, y1, math.sin(a1) * r_in); o1 = (math.cos(a1) * r_out1, y1, math.sin(a1) * r_out1)
    up = lambda q: (q[0], q[1] + th, q[2])
    drill.quad(up(i0), up(i1), up(o1), up(o0), 'steel')
    drill.quad(i0, o0, o1, i1, 'grey')
    drill.quad(o0, up(o0), up(o1), o1, 'grey')

body.write(os.path.join(OUT, 'excavator_body.bin'))
drill.write(os.path.join(OUT, 'excavator_drill.bin'))
print('DRILL_PIVOT', DRILL_PIVOT, 'EXHAUST_TOP', EXHAUST_TOP)

write_palette(os.path.join(OUT, 'excavator_albedo.png'))
if PREVIEW:
    write_preview_obj(PREVIEW, os.path.join(OUT, 'excavator_albedo.png'), ((body, (0, 0, 0)), (drill, DRILL_PIVOT)))
