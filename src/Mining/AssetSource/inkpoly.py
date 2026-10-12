# Modele w stylu rysunku z gry (jak duże drzwi): tekstura Assets/door_albedo.png (build_door_texture.py) - pola
# kolorów z czarnym konturem, wytartą krawędzią i kreskowaniem. Ścianki prostopadłościanów mapujemy "9-slice"
# (kontur stałej grubości w metrach - jak Content/BigDoorModel.cs), walce: kontur na górnej i dolnej krawędzi,
# denka z obwódką. Format wyjściowy MSH1 (Content/ModAssets.LoadMesh). Układ Unity: Y w górę, przód = -Z.
import math, os, struct

NAMES = ['yellow', 'yellow_dark', 'dark', 'grey', 'light', 'orange', 'steel', 'screen', 'black', 'rubber', 'olive',
         'white', 'cyan', 'blue', 'green', 'red']
GRID = 4
MARGIN = 26 / 256
BORDER = 0.045
INSET = 1.5 / 256


def _cell(color):
    i = NAMES.index(color)
    return i % GRID, i // GRID


def _uv(color, fu, fv):
    cx, cy = _cell(color)
    fu = INSET + fu * (1 - 2 * INSET)
    fv = INSET + fv * (1 - 2 * INSET)
    return ((cx + fu) / GRID, 1 - (cy + 1 - fv) / GRID)


def _slice(t, length):
    # położenie t na krawędzi długości `length` -> współrzędna w polu (0..1)
    if length < BORDER * 2.5:
        return t / length
    if t <= BORDER:
        return MARGIN * t / BORDER
    if t >= length - BORDER:
        return 1 - MARGIN * (length - t) / BORDER
    return MARGIN + (1 - 2 * MARGIN) * (t - BORDER) / (length - 2 * BORDER)


def _cuts(length):
    c = [0.0]
    if length >= BORDER * 2.5:
        c += [BORDER, length - BORDER]
    return c + [length]


def _rot(euler):
    # obrót jak w Unity (Quaternion.Euler: najpierw Z, potem X, potem Y), kąty w stopniach
    x, y, z = (math.radians(a) for a in euler)
    cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
    rz = ((cz, -sz, 0), (sz, cz, 0), (0, 0, 1))
    rx = ((1, 0, 0), (0, cx, -sx), (0, sx, cx))
    ry = ((cy, 0, sy), (0, 1, 0), (-sy, 0, cy))

    def mul(a, b):
        return tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)) for i in range(3))
    return mul(ry, mul(rx, rz))


def _apply(m, v):
    return tuple(sum(m[i][k] * v[k] for k in range(3)) for i in range(3))


def _cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def _sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def _dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def _norm(a):
    l = math.sqrt(_dot(a, a)) or 1
    return (a[0] / l, a[1] / l, a[2] / l)


class Mesh:
    def __init__(self):
        self.v, self.i = [], []

    def _tri(self, pts, normals, uvs, outward):
        # kolejność zgodna z ruchem wskazówek patrząc z zewnątrz (przód w Unity)
        a, b, c = pts
        if _dot(_cross(_sub(b, a), _sub(c, a)), outward) < 0:
            pts, normals, uvs = (a, c, b), (normals[0], normals[2], normals[1]), (uvs[0], uvs[2], uvs[1])
        base = len(self.v)
        for p, n, uv in zip(pts, normals, uvs):
            self.v.append((p, n, uv))
        self.i += [base, base + 1, base + 2]

    def _quad(self, pts, normals, uvs, outward):
        self._tri((pts[0], pts[1], pts[2]), (normals[0], normals[1], normals[2]), (uvs[0], uvs[1], uvs[2]), outward)
        self._tri((pts[0], pts[2], pts[3]), (normals[0], normals[2], normals[3]), (uvs[0], uvs[2], uvs[3]), outward)

    def box(self, center, size, color, rot=(0, 0, 0), colors=None):
        # colors: inny kolor ścianek {'top','bottom','front','back','left','right'}
        m = _rot(rot)
        half = [s / 2 for s in size]
        faces = {'right': (0, 1), 'left': (0, -1), 'top': (1, 1), 'bottom': (1, -1), 'back': (2, 1), 'front': (2, -1)}
        for name, (k, sign) in faces.items():
            col = (colors or {}).get(name, color)
            a, b = (2, 1) if k == 0 else (0, 2) if k == 1 else (0, 1)   # na bokach v = Y (kreskowanie na dole)
            la, lb = size[a], size[b]
            n_local = [0, 0, 0]
            n_local[k] = sign
            n = _norm(_apply(m, n_local))
            ca, cb = _cuts(la), _cuts(lb)
            for i in range(len(ca) - 1):
                for j in range(len(cb) - 1):
                    pts, uvs = [], []
                    for ta, tb in ((ca[i], cb[j]), (ca[i], cb[j + 1]), (ca[i + 1], cb[j + 1]), (ca[i + 1], cb[j])):
                        p = [0, 0, 0]
                        p[k] = sign * half[k]
                        p[a] = ta - half[a]
                        p[b] = tb - half[b]
                        w = _apply(m, p)
                        pts.append((center[0] + w[0], center[1] + w[1], center[2] + w[2]))
                        uvs.append(_uv(col, _slice(ta, la), _slice(tb, lb)))
                    self._quad(pts, [n] * 4, uvs, n)

    def cylinder(self, center, radius, height, color, sides=16, axis='y', cap_color=None, radius_top=None, caps=True):
        # walec wzdłuż osi `axis` ('x', 'y', 'z'); kontur na krawędziach podstaw, denka z obwódką
        rt = radius if radius_top is None else radius_top
        perm = {'y': (0, 1, 2), 'x': (1, 0, 2), 'z': (0, 2, 1)}[axis]   # oś walca (lokalne Y) -> oś świata

        def turn(p):
            return (p[perm[0]], p[perm[1]], p[perm[2]])

        def w(p):
            q = turn(p)
            return (center[0] + q[0], center[1] + q[1], center[2] + q[2])
        h0, h1 = -height / 2, height / 2
        cuts = _cuts(height)
        slope = (radius - rt) / max(height, 1e-6)
        for s in range(sides):
            a0, a1 = 2 * math.pi * s / sides, 2 * math.pi * (s + 1) / sides
            u0, u1 = MARGIN + (1 - 2 * MARGIN) * s / sides, MARGIN + (1 - 2 * MARGIN) * (s + 1) / sides
            n0 = _norm(turn(_norm((math.cos(a0), slope, math.sin(a0)))))
            n1 = _norm(turn(_norm((math.cos(a1), slope, math.sin(a1)))))
            outward = _norm(turn((math.cos((a0 + a1) / 2), 0, math.sin((a0 + a1) / 2))))
            for j in range(len(cuts) - 1):
                pts, uvs = [], []
                for a, u, t in ((a0, u0, cuts[j]), (a0, u0, cuts[j + 1]), (a1, u1, cuts[j + 1]), (a1, u1, cuts[j])):
                    r = radius + (rt - radius) * t / height
                    pts.append(w((math.cos(a) * r, h0 + t, math.sin(a) * r)))
                    uvs.append(_uv(color, u, _slice(t, height)))
                self._quad(pts, [n0, n0, n1, n1], uvs, outward)
            if caps:
                cc = cap_color or color
                rim, mid = _uv(cc, 0.02, 0.5), _uv(cc, 0.5, 0.5)
                for y, r, sign in ((h1, rt, 1), (h0, radius, -1)):
                    if r <= 0:
                        continue
                    nn = turn((0, sign, 0))
                    self._tri((w((0, y, 0)), w((math.cos(a0) * r, y, math.sin(a0) * r)), w((math.cos(a1) * r, y, math.sin(a1) * r))),
                              (nn, nn, nn), (mid, rim, rim), nn)

    def write(self, path):
        verts, lookup, index = [], {}, []
        for p, n, uv in self.v:
            key = tuple(round(x, 5) for x in p + n + uv)
            if key not in lookup:
                lookup[key] = len(verts)
                verts.append(p + n + uv)
            index.append(lookup[key])
        with open(path, 'wb') as f:
            f.write(b'MSH1')
            f.write(struct.pack('<ii', len(verts), len(index)))
            for v in verts:
                f.write(struct.pack('<8f', *v))
            f.write(struct.pack('<%di' % len(index), *index))
        print(os.path.basename(path), len(verts), 'wierzchołków', len(index) // 3, 'trójkątów')

    def write_obj(self, path, mtl_texture, offset=(0, 0, 0), name='m', start=1):
        # podgląd w Blenderze (X odbity: układ lewoskrętny -> prawoskrętny)
        lines = ['o ' + name]
        for p, n, uv in self.v:
            lines.append('v %f %f %f' % (-(p[0] + offset[0]), p[1] + offset[1], p[2] + offset[2]))
        for p, n, uv in self.v:
            lines.append('vt %f %f' % uv)
        for p, n, uv in self.v:
            lines.append('vn %f %f %f' % (-n[0], n[1], n[2]))
        for t in range(0, len(self.i), 3):
            a, b, c = (start + self.i[t + k] for k in range(3))
            lines.append('f %d/%d/%d %d/%d/%d %d/%d/%d' % (a, a, a, b, b, b, c, c, c))
        return lines, start + len(self.v)
