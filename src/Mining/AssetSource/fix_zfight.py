import os, struct, sys
from collections import defaultdict

STEP = 0.0015
ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets')


def read(path):
    d = open(path, 'rb').read()
    if d[:4] != b'MSH1':
        return None
    nv, ni = struct.unpack_from('<ii', d, 4)
    verts = [list(struct.unpack_from('<8f', d, 12 + 32 * k)) for k in range(nv)]
    idx = list(struct.unpack_from('<%di' % ni, d, 12 + 32 * nv))
    return verts, idx


def write(path, verts, idx):
    with open(path, 'wb') as f:
        f.write(b'MSH1')
        f.write(struct.pack('<ii', len(verts), len(idx)))
        for v in verts:
            f.write(struct.pack('<8f', *v))
        f.write(struct.pack('<%di' % len(idx), *idx))


def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def basis(n):
    a = (1, 0, 0) if abs(n[0]) < 0.9 else (0, 1, 0)
    u = cross(n, a)
    l = dot(u, u) ** 0.5
    u = (u[0] / l, u[1] / l, u[2] / l)
    return u, cross(n, u)


def tri_overlap(p, q):
    def edges(t):
        return [(t[i], t[(i + 1) % 3]) for i in range(3)]
    for t in (p, q):
        for a, b in edges(t):
            ax, ay = b[1] - a[1], a[0] - b[0]
            pa = [x * ax + y * ay for x, y in p]
            qa = [x * ax + y * ay for x, y in q]
            if max(pa) <= min(qa) + 1e-5 or max(qa) <= min(pa) + 1e-5:
                return False
    return True


def process(path, apply):
    data = read(path)
    if data is None:
        return 0
    verts, idx = data
    tris = []
    for t in range(len(idx) // 3):
        a, b, c = (verts[idx[3 * t + k]][:3] for k in range(3))
        n = cross(sub(b, a), sub(c, a))
        l = dot(n, n) ** 0.5
        if l < 1e-9:
            continue
        n = (n[0] / l, n[1] / l, n[2] / l)
        tris.append((t, n, dot(n, a)))
    buckets = defaultdict(list)
    for t, n, d in tris:
        buckets[(round(n[0], 3), round(n[1], 3), round(n[2], 3), round(d, 4))].append(t)
    moves = {}
    conflicts = 0
    for key, members in buckets.items():
        if len(members) < 2:
            continue
        n = key[:3]
        u, w = basis(n)
        parent = {t: t for t in members}

        def find(x):
            while parent[x] != x:
                parent[x] = parent[parent[x]]
                x = parent[x]
            return x
        owner = {}
        for t in members:
            for k in range(3):
                vi = idx[3 * t + k]
                if vi in owner:
                    parent[find(t)] = find(owner[vi])
                else:
                    owner[vi] = t
        islands = defaultdict(list)
        for t in members:
            islands[find(t)].append(t)
        if len(islands) < 2:
            continue
        flat = {}
        for root, ts in islands.items():
            flat[root] = [[(dot(verts[idx[3 * t + k]][:3], u), dot(verts[idx[3 * t + k]][:3], w)) for k in range(3)] for t in ts]
        order = sorted(islands, key=lambda r: min(islands[r]))
        level = {}
        for i, r in enumerate(order):
            lv = 0
            for s in order[:i]:
                if any(tri_overlap(p, q) for p in flat[r] for q in flat[s]):
                    lv = max(lv, level[s] + 1)
            level[r] = lv
            if lv > 0:
                conflicts += 1
                for t in islands[r]:
                    moves[t] = (n, lv * STEP)
    if apply and moves:
        for t, (n, off) in moves.items():
            for k in range(3):
                v = list(verts[idx[3 * t + k]])
                v[0] += n[0] * off
                v[1] += n[1] * off
                v[2] += n[2] * off
                verts.append(v)
                idx[3 * t + k] = len(verts) - 1
        write(path, verts, idx)
    return conflicts


if __name__ == '__main__':
    apply = '--apply' in sys.argv
    names = [a for a in sys.argv[1:] if not a.startswith('--')] or sorted(f for f in os.listdir(ASSETS) if f.endswith('.bin'))
    for name in names:
        c = process(os.path.join(ASSETS, name), apply)
        print(f'{name}: {c} overlapping faces' + (' moved' if apply and c else ''))
