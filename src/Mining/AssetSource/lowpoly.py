# Wspólny kod modeli low-poly w stylu części StellarDrive: proste bryły, płaskie kolory z palety, kreskowanie
# przy dolnych krawędziach ścian (jak w teksturach gry). Format wyjściowy MSH1 czyta Content/ModAssets.cs.
import math, os, struct
from PIL import Image

# Paleta 5x5 pól po 128 px.
COLORS = {
    'yellow': (238, 170, 56), 'yellow_dark': (196, 132, 30), 'dark': (64, 68, 76), 'grey': (138, 144, 152),
    'light': (210, 214, 220), 'orange': (214, 98, 44), 'steel': (168, 174, 182), 'screen': (40, 96, 120),
    'black': (36, 38, 42), 'rubber': (50, 52, 56), 'olive': (112, 122, 72),
    'white': (226, 230, 234), 'cyan': (70, 190, 220), 'blue': (60, 110, 190),
}
# Kreskowanie jak w teksturach gry (ukośne ciemne kreski przy dolnej krawędzi ścianki) - pola z dopiskiem '#h'.
HATCHED = ('yellow', 'olive', 'grey', 'white')
NAMES = list(COLORS) + [c + '#h' for c in HATCHED]
CELL, GRID = 128, 5


def uv_of(color):
    i = NAMES.index(color)
    cx, cy = i % GRID, i // GRID
    return ((cx + 0.5) / GRID, 1 - (cy + 0.5) / GRID)


def cell_uv(color, u, v):
    # (u, v) w [0,1] wewnątrz pola koloru, v = 0 na dole ścianki
    i = NAMES.index(color)
    cx, cy = i % GRID, i // GRID
    m = 0.03
    return ((cx + m + u * (1 - 2 * m)) / GRID, 1 - (cy + 1 - m - v * (1 - 2 * m)) / GRID)


def hatch_cell(img, color, x0, y0):
    # ukośne kreski, gęste przy dole pola i zanikające ku górze
    import random
    rnd = random.Random(hash(color) & 0xffff)
    base = COLORS[color]
    stroke = tuple(int(c * 0.5) for c in base)
    px = img.load()
    for y in range(CELL):
        v = 1 - y / (CELL - 1)              # 0 = dół pola
        for x in range(CELL):
            px[x0 + x, y0 + y] = base
    spacing = 9
    for k in range(-CELL, CELL * 2, spacing):
        reach = 0.22 + 0.25 * rnd.random()  # jak wysoko sięga kreska (część wysokości pola)
        for t in range(CELL):
            x, y = k + t, CELL - 1 - t       # kreska pod kątem 45 stopni od dołu
            if not (0 <= x < CELL and 0 <= y < CELL):
                continue
            v = 1 - y / (CELL - 1)
            if v > reach:
                break
            for w in range(3):
                if 0 <= x + w < CELL:
                    px[x0 + x + w, y0 + y] = stroke


class Mesh:
    def __init__(self):
        self.v, self.i = [], []

    def tri(self, a, b, c, color, normals=None, uvs=None):
        if normals is None:
            n = cross(sub(b, a), sub(c, a))
            n = norm(n)
            normals = (n, n, n)
        if uvs is None:
            uvs = (uv_of(color),) * 3
        base = len(self.v)
        for p, nn, uv in zip((a, b, c), normals, uvs):
            self.v.append((p, nn, uv))
        self.i += [base, base + 1, base + 2]

    def quad(self, a, b, c, d, color, normals=None):
        # Ściany boczne (a = lewy dół, b = lewa góra, c = prawa góra, d = prawy dół) w kolorach z kreskowaniem
        # dostają UV na całe pole z kreskami, jeśli są wystarczająco duże.
        uvs = None
        vertical = abs(b[1] - a[1]) > 0.12 and abs(a[1] - d[1]) < 1e-6
        width = math.sqrt(sum((d[i] - a[i]) ** 2 for i in range(3)))
        if normals is None and color in HATCHED and vertical and width > 0.12:
            color = color + '#h'
            uvs = [cell_uv(color, 0, 0), cell_uv(color, 0, 1), cell_uv(color, 1, 1), cell_uv(color, 1, 0)]
        self.tri(a, b, c, color, normals[:3] if normals else None, (uvs[0], uvs[1], uvs[2]) if uvs else None)
        self.tri(a, c, d, color, (normals[0], normals[2], normals[3]) if normals else None, (uvs[0], uvs[2], uvs[3]) if uvs else None)

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


def write_palette(path):
    img = Image.new('RGB', (CELL * GRID, CELL * GRID), COLORS['grey'])
    for i, name in enumerate(NAMES):
        x, y = (i % GRID) * CELL, (i // GRID) * CELL
        if name.endswith('#h'):
            hatch_cell(img, name[:-2], x, y)
        else:
            img.paste(COLORS[name], (x, y, x + CELL, y + CELL))
    img.save(path)


def write_preview_obj(path, texture, parts):
    # OBJ do podglądu w Blenderze (importer OBJ zamienia Y-w-górę na Z; odbicie X zamienia układ lewoskrętny na prawoskrętny)
    nl = chr(10)
    with open(path, 'w') as f:
        f.write('mtllib preview.mtl' + nl + 'usemtl m' + nl)
        off = 0
        for m, shift in parts:
            for p, n, uv in m.v:
                f.write('v %f %f %f' % (-(p[0] + shift[0]), p[1] + shift[1], p[2] + shift[2]) + nl)
                f.write('vt %f %f' % uv + nl)
                f.write('vn %f %f %f' % (-n[0], n[1], n[2]) + nl)
            for t in range(0, len(m.i), 3):
                a, b, c = (m.i[t] + 1 + off, m.i[t + 1] + 1 + off, m.i[t + 2] + 1 + off)
                f.write('f %d/%d/%d %d/%d/%d %d/%d/%d' % (a, a, a, b, b, b, c, c, c) + nl)
            off += len(m.v)
    with open(os.path.join(os.path.dirname(path), 'preview.mtl'), 'w') as f:
        f.write('newmtl m' + nl + 'map_Kd ' + texture.replace(chr(92), '/') + nl)
