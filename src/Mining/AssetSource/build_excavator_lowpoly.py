# Generuje model koparki low-poly (styl części StellarDrive: proste bryły, płaskie kolory, ostre krawędzie pod kontury).
# Wzorowany na modelu z Meshy (4 nogi, rama, korpus z ekranem, boczny silnik, wieżyczka, wiertło ze spiralą).
# Wynik: Assets/excavator_body.bin, Assets/excavator_drill.bin (format MSH1, układ Unity: Y w górę, przód = -Z)
# i Assets/excavator_albedo.png (paleta kolorów - każda ścianka ma UV w środku pola swojego koloru).
# Użycie: python -I build_excavator.py <folder Assets> [plik .obj do podglądu]
import math, os, struct, sys
from PIL import Image

OUT = sys.argv[1]
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None

# Paleta 4x4 pól po 64 px.
COLORS = {
    'yellow': (236, 172, 42), 'yellow_dark': (196, 132, 30), 'dark': (64, 68, 76), 'grey': (138, 144, 152),
    'light': (210, 214, 220), 'orange': (214, 98, 44), 'steel': (168, 174, 182), 'screen': (40, 96, 120),
    'black': (36, 38, 42), 'rubber': (50, 52, 56),
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
LX, LZ = 0.74, 0.62  # nogi

# --- Nogi ze stopami ---
for sx in (-1, 1):
    for sz in (-1, 1):
        x, z = sx * LX, sz * LZ
        body.box((x, 0.04, z), (0.38, 0.08, 0.38), 'rubber')                       # stopa
        body.box((x, 0.12, z), (0.28, 0.08, 0.28), 'dark')
        body.box((x, 0.47, z), (0.2, 0.62, 0.2), 'yellow', {'top': 'dark'})        # kolumna
        body.box((x, 0.42, z), (0.22, 0.07, 0.22), 'dark')                         # opaska
        body.box((x, 0.82, z), (0.26, 0.1, 0.26), 'dark')                          # przegub

# --- Rama ---
for sx in (-1, 1):
    body.box((sx * LX, 0.82, 0), (0.14, 0.12, 2 * LZ), 'dark')
for sz in (-1, 1):
    body.box((0, 0.82, sz * LZ), (2 * LX, 0.12, 0.14), 'dark')

# --- Korpus ---
body.box((0.08, 1.12, 0.02), (1.0, 0.5, 0.9), 'yellow', {'bottom': 'dark'})
body.box((0.08, 0.89, 0.02), (1.06, 0.06, 0.96), 'dark')                           # płyta pod korpusem
body.box((0.08, 1.38, 0.02), (1.04, 0.05, 0.94), 'yellow_dark')                    # listwa górna
# panel z ekranem z przodu (-Z)
body.box((0.14, 1.15, -0.44), (0.48, 0.34, 0.03), 'grey')
body.box((0.14, 1.17, -0.457), (0.32, 0.2, 0.01), 'screen')
body.box((-0.22, 1.2, -0.445), (0.1, 0.1, 0.02), 'orange')                         # lampka/przycisk
# pasy ostrzegawcze z przodu
for k in range(7):
    body.box((-0.34 + k * 0.14, 0.96, -0.445), (0.07, 0.07, 0.02), 'black')

# --- Boczna skrzynia silnika (lewa strona, jak w oryginale) ---
body.box((-0.62, 1.04, -0.06), (0.5, 0.36, 0.7), 'grey', {'top': 'light'})
body.box((-0.62, 1.24, -0.06), (0.54, 0.05, 0.74), 'dark')
for k in range(3):                                                                  # kratka wentylacji
    body.box((-0.875, 1.04, -0.26 + k * 0.2), (0.02, 0.2, 0.06), 'dark')
body.box((-0.62, 0.92, -0.06), (0.54, 0.06, 0.74), 'yellow')

# --- Wieżyczka z silnikiem wiertła ---
TX, TZ = 0.08, 0.1
for sx in (-1, 1):
    for sz in (-1, 1):
        body.box((TX + sx * 0.24, 1.66, TZ + sz * 0.18), (0.08, 0.52, 0.08), 'dark')
body.box((TX, 1.94, TZ), (0.62, 0.07, 0.5), 'dark')
body.box((TX, 1.43, TZ), (0.62, 0.06, 0.5), 'dark')
body.cylinder((TX, 1.66, TZ), 0.17, 0.38, 'grey', 14, cap_color='dark')
body.cylinder((TX, 1.66, TZ), 0.18, 0.1, 'orange', 14)
body.cylinder((TX, 2.02, TZ), 0.1, 0.1, 'dark', 10, cap_color='black')

# --- Obudowa wału wiertła pod korpusem ---
DX, DZ = 0.08, 0.02
body.cylinder((DX, 0.72, DZ), 0.13, 0.28, 'dark', 12)
body.cylinder((DX, 0.56, DZ), 0.1, 0.06, 'yellow', 12)

# --- Wiertło (osobna siatka, oś obrotu w (0,0,0), w modelu w DRILL_PIVOT) ---
DRILL_PIVOT = (DX, 0.53, DZ)
drill = Mesh()
drill.cylinder((0, -0.04, 0), 0.07, 0.08, 'dark', 10)
drill.cylinder((0, -0.36, 0), 0.045, 0.6, 'steel', 8)
drill.cylinder((0, -0.73, 0), 0.0, 0.14, 'steel', 8, radius_top=0.05)               # czubek
# spirala: łopatki skręcone wokół wału
turns, steps = 3.2, 44
top, bottom = -0.08, -0.64
for k in range(steps):
    t0, t1 = k / steps, (k + 1) / steps
    a0, a1 = t0 * turns * 2 * math.pi, t1 * turns * 2 * math.pi
    y0, y1 = top + (bottom - top) * t0, top + (bottom - top) * t1
    r_in, r_out = 0.04, 0.12 - 0.04 * t0
    r_out1 = 0.12 - 0.04 * t1
    th = 0.018
    i0 = (math.cos(a0) * r_in, y0, math.sin(a0) * r_in); o0 = (math.cos(a0) * r_out, y0, math.sin(a0) * r_out)
    i1 = (math.cos(a1) * r_in, y1, math.sin(a1) * r_in); o1 = (math.cos(a1) * r_out1, y1, math.sin(a1) * r_out1)
    up = lambda p: (p[0], p[1] + th, p[2])
    drill.quad(up(i0), up(i1), up(o1), up(o0), 'steel')                              # góra łopatki
    drill.quad(i0, o0, o1, i1, 'grey')                                               # spód
    drill.quad(o0, up(o0), up(o1), o1, 'grey')                                       # krawędź

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
