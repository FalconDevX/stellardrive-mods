# Generuje model koparki low-poly (styl części StellarDrive: proste bryły, płaskie kolory, ostre krawędzie pod kontury).
# Wzorowany na modelu z Meshy (4 nogi, rama, zasobnik, korpus z ekranem, oliwkowa skrzynia, wieżyczka, wiertło ze spiralą).
# Wynik: Assets/excavator_body.bin, Assets/excavator_drill.bin (format MSH1, układ Unity: Y w górę, przód = -Z)
# i Assets/excavator_albedo.png (paleta kolorów - każda ścianka ma UV w środku pola swojego koloru).
# Użycie: python -I build_excavator.py <folder Assets> [plik .obj do podglądu]
import math, os, struct, sys
from PIL import Image

OUT = sys.argv[1]
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None

# Paleta 4x4 pól po 64 px.
COLORS = {
    'yellow': (238, 170, 56), 'yellow_dark': (196, 132, 30), 'dark': (64, 68, 76), 'grey': (138, 144, 152),
    'light': (210, 214, 220), 'orange': (214, 98, 44), 'steel': (168, 174, 182), 'screen': (40, 96, 120),
    'black': (36, 38, 42), 'rubber': (50, 52, 56), 'olive': (112, 122, 72),
}
NAMES = list(COLORS)
CELL, GRID = 64, 4


def uv_of(color):
    i = NAMES.index(color)
    cx, cy = i % GRID, i // GRID
    return ((cx + 0.5) / GRID, 1 - (cy + 0.5) / GRID)


class Mesh:
    def __init__(self):
        self.v, self.i = [], []

    def tri(self, a, b, c, color, normals=None):
        if normals is None:
            n = cross(sub(b, a), sub(c, a))
            n = norm(n)
            normals = (n, n, n)
        uv = uv_of(color)
        base = len(self.v)
        for p, nn in zip((a, b, c), normals):
            self.v.append((p, nn, uv))
        self.i += [base, base + 1, base + 2]

    def quad(self, a, b, c, d, color, normals=None):
        self.tri(a, b, c, color, normals[:3] if normals else None)
        self.tri(a, c, d, color, (normals[0], normals[2], normals[3]) if normals else None)

    def box(self, center, size, color, colors=None):
        # colors: opcjonalnie inny kolor dla ścian {'top','bottom','front','back','left','right'}
        cx, cy, cz = center
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        p = lambda x, y, z: (cx + x * hx, cy + y * hy, cz + z * hz)
        c = lambda face: (colors or {}).get(face, color)
        # Unity: lewoskrętny układ, ścianki zgodnie z ruchem wskazówek zegara patrząc z zewnątrz
        self.quad(p(-1, 1, -1), p(-1, 1, 1), p(1, 1, 1), p(1, 1, -1), c('top'))
        self.quad(p(-1, -1, 1), p(-1, -1, -1), p(1, -1, -1), p(1, -1, 1), c('bottom'))
        self.quad(p(-1, -1, -1), p(-1, 1, -1), p(1, 1, -1), p(1, -1, -1), c('front'))
        self.quad(p(1, -1, 1), p(1, 1, 1), p(-1, 1, 1), p(-1, -1, 1), c('back'))
        self.quad(p(-1, -1, 1), p(-1, 1, 1), p(-1, 1, -1), p(-1, -1, -1), c('left'))
        self.quad(p(1, -1, -1), p(1, 1, -1), p(1, 1, 1), p(1, -1, 1), c('right'))

    def cylinder(self, center, radius, height, color, sides=12, cap_color=None, smooth=True, radius_top=None):
        cx, cy, cz = center
        rt = radius if radius_top is None else radius_top
        y0, y1 = cy - height / 2, cy + height / 2
        for k in range(sides):
            a0, a1 = 2 * math.pi * k / sides, 2 * math.pi * (k + 1) / sides
            b0 = (cx + math.cos(a0) * radius, y0, cz + math.sin(a0) * radius)
            b1 = (cx + math.cos(a1) * radius, y0, cz + math.sin(a1) * radius)
            t0 = (cx + math.cos(a0) * rt, y1, cz + math.sin(a0) * rt)
            t1 = (cx + math.cos(a1) * rt, y1, cz + math.sin(a1) * rt)
            if smooth:
                n0 = norm((math.cos(a0), (radius - rt) / max(height, 1e-6), math.sin(a0)))
                n1 = norm((math.cos(a1), (radius - rt) / max(height, 1e-6), math.sin(a1)))
                self.quad(b0, t0, t1, b1, color, (n0, n0, n1, n1))
            else:
                self.quad(b0, t0, t1, b1, color)
            cc = cap_color or color
            if rt > 0:
                self.tri((cx, y1, cz), t1, t0, cc)
            if radius > 0:
                self.tri((cx, y0, cz), b0, b1, cc)

    def write(self, path):
        verts, index, lookup = [], [], {}
        for p, n, uv in self.v:
            key = tuple(round(x, 5) for x in p + n + uv)
            if key not in lookup:
                lookup[key] = len(verts)
                verts.append(p + n + uv)
        index = [lookup[tuple(round(x, 5) for x in self.v[i][0] + self.v[i][1] + self.v[i][2])] for i in self.i]
        with open(path, 'wb') as f:
            f.write(b'MSH1')
            f.write(struct.pack('<ii', len(verts), len(index)))
            for v in verts:
                f.write(struct.pack('<8f', *v))
            f.write(struct.pack('<%di' % len(index), *index))
        print(os.path.basename(path), len(verts), 'wierzchołków', len(index) // 3, 'trójkątów')


def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def norm(a):
    l = math.sqrt(sum(x * x for x in a)) or 1
    return (a[0] / l, a[1] / l, a[2] / l)


body = Mesh()
# Wymiary w metrach, układ Unity: X w prawo, Y w górę, przód koparki = -Z. Wzór: podgląd modelu z Meshy.
LX, LZ = 0.72, 0.62  # środki nóg (jak GroundPlacement.Feet w Patches/GroundPlacementPatch.cs)

# --- Nogi: schodkowa stopa, żółta osłona z ciemnymi pasami, szara czapa ---
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

# --- Rama z kwadratowych belek ---
for sz in (-1, 1):
    body.box((0, 0.62, sz * LZ), (2 * LX, 0.12, 0.12), 'grey', {'bottom': 'dark'})
for sx in (-1, 1):
    body.box((sx * LX, 0.62, 0), (0.12, 0.12, 2 * LZ), 'grey', {'bottom': 'dark'})
body.box((0.3, 0.72, 0.1), (0.12, 0.1, 2 * LZ - 0.1), 'dark')
body.box((-0.1, 0.72, 0.1), (0.12, 0.1, 2 * LZ - 0.1), 'dark')

# --- Duży zasobnik z przodu po lewej (żółte rogi, oliwkowe panele, ciemna pokrywa) ---
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

# --- Korpus z ekranem w szarej ramie, pasy ostrzegawcze ---
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

# --- Oliwkowa skrzynia z tyłu po lewej, rury do korpusu ---
body.box((-0.3, 1.43, 0.22), (0.52, 0.36, 0.46), 'olive', {'top': 'grey'})
body.box((-0.3, 1.62, 0.22), (0.56, 0.04, 0.5), 'dark')
body.box((-0.3, 1.43, -0.015), (0.3, 0.12, 0.01), 'dark')
body.cylinder((-0.08, 1.52, 0.33), 0.035, 0.3, 'dark', 8)
body.cylinder((-0.08, 1.35, 0.33), 0.035, 0.3, 'dark', 8)

# --- Wieżyczka: szara rama portalowa, żółty środek, czarne sprężyny, nakrętka z pomarańczowym pierścieniem ---
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

# --- Obudowa wału wiertła ---
DX, DZ = -0.02, -0.12
body.cylinder((DX, 0.74, DZ), 0.11, 0.14, 'dark', 12)
body.cylinder((DX, 0.645, DZ), 0.085, 0.05, 'grey', 12)

# --- Skrzynki przyłączeniowe prądu na zewnętrznych bokach nóg (gniazda wystają poza obrys koparki) ---
# prąd: prawa przednia i lewa tylna noga (szary daszek), sygnał: lewa przednia noga (żółty daszek)
for sx, sz, cap in ((1, -1, 'grey'), (-1, 1, 'grey'), (-1, -1, 'yellow')):
    body.box((sx * (LX + 0.17), 0.42, sz * LZ), (0.08, 0.18, 0.16), 'dark', {'top': cap})
    body.box((sx * (LX + 0.15), 0.42, sz * LZ), (0.04, 0.22, 0.04), 'grey')

# --- Wiertło (osobna siatka, oś obrotu w (0,0,0), w modelu w DRILL_PIVOT) ---
DRILL_PIVOT = (DX, 0.62, DZ)
drill = Mesh()
# Długie wiertło: w spoczynku schowane w korpusie, przy pracy wysuwa się w dół (ExcavatorVisuals).
DRILL_LENGTH = 1.12
drill.cylinder((0, -0.04, 0), 0.07, 0.08, 'dark', 10)
drill.cylinder((0, -0.55, 0), 0.05, 0.98, 'steel', 8)
drill.cylinder((0, -1.05, 0), 0.0, 0.14, 'steel', 8, radius_top=0.055)
turns, steps = 4.5, 64
top, bottom = -0.12, -0.96
for k in range(steps):
    t0, t1 = k / steps, (k + 1) / steps
    a0, a1 = t0 * turns * 2 * math.pi, t1 * turns * 2 * math.pi
    y0, y1 = top + (bottom - top) * t0, top + (bottom - top) * t1
    r_in = 0.045
    r_out, r_out1 = 0.14 - 0.04 * t0, 0.14 - 0.04 * t1
    th = 0.03
    i0 = (math.cos(a0) * r_in, y0, math.sin(a0) * r_in); o0 = (math.cos(a0) * r_out, y0, math.sin(a0) * r_out)
    i1 = (math.cos(a1) * r_in, y1, math.sin(a1) * r_in); o1 = (math.cos(a1) * r_out1, y1, math.sin(a1) * r_out1)
    up = lambda q: (q[0], q[1] + th, q[2])
    drill.quad(up(i0), up(i1), up(o1), up(o0), 'steel')
    drill.quad(i0, o0, o1, i1, 'grey')
    drill.quad(o0, up(o0), up(o1), o1, 'grey')

body.write(os.path.join(OUT, 'excavator_body.bin'))
drill.write(os.path.join(OUT, 'excavator_drill.bin'))
print('DRILL_PIVOT', DRILL_PIVOT)

img = Image.new('RGB', (CELL * GRID, CELL * GRID), COLORS['grey'])
for i, name in enumerate(NAMES):
    x, y = (i % GRID) * CELL, (i // GRID) * CELL
    img.paste(COLORS[name], (x, y, x + CELL, y + CELL))
img.save(os.path.join(OUT, 'excavator_albedo.png'))

if PREVIEW:
    # OBJ do podglądu w Blenderze (importer OBJ zamienia Y-w-górę na Z; odbicie X zamienia układ lewoskrętny na prawoskrętny)
    with open(PREVIEW, 'w') as f:
        f.write('mtllib excavator.mtl\nusemtl m\n')
        off = 0
        for m, shift in ((body, (0, 0, 0)), (drill, DRILL_PIVOT)):
            for p, n, uv in m.v:
                f.write('v %f %f %f\n' % (-(p[0] + shift[0]), p[1] + shift[1], p[2] + shift[2]))
                f.write('vt %f %f\n' % uv)
                f.write('vn %f %f %f\n' % (-n[0], n[1], n[2]))
            for t in range(0, len(m.i), 3):
                a, b, c = (m.i[t] + 1 + off, m.i[t + 1] + 1 + off, m.i[t + 2] + 1 + off)
                f.write('f %d/%d/%d %d/%d/%d %d/%d/%d\n' % (a, a, a, b, b, b, c, c, c))
            off += len(m.v)
    with open(os.path.join(os.path.dirname(PREVIEW), 'excavator.mtl'), 'w') as f:
        f.write('newmtl m\nmap_Kd %s\n' % os.path.join(OUT, 'excavator_albedo.png').replace('\\', '/'))
