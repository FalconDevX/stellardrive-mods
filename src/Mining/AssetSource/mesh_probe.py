# Promień w siatkę MSH1 (Assets/*.bin): gdzie jest powierzchnia modelu od strony gniazda. Używane do ustawienia gniazd
# i ich mocowań (build_mounts.py) tak, żeby gniazdo leżało tuż poza obrysem kolizji, a mocowanie sięgało ścianki.
# Użycie: python -I mesh_probe.py <plik .bin> [os(x|z) znak y od do krok]  - bez dodatkowych: siatka próbek na 4 bokach
import struct, sys
import numpy as np


def load(path):
    d = open(path, 'rb').read()
    nv, ni = struct.unpack_from('<ii', d, 4)
    v = np.frombuffer(d, dtype='<f4', count=nv * 8, offset=12).reshape(nv, 8)[:, :3].astype(np.float64)
    idx = np.frombuffer(d, dtype='<i4', count=ni, offset=12 + nv * 32).reshape(-1, 3)
    return v[idx[:, 0]], v[idx[:, 1]], v[idx[:, 2]]


def ray(tris, origin, direction):
    # najbliższe trafienie (Möller-Trumbore) dla wszystkich trójkątów naraz
    a, b, c = tris
    e1, e2 = b - a, c - a
    d = np.asarray(direction, float)
    p = np.cross(d, e2)
    det = np.einsum('ij,ij->i', e1, p)
    ok = np.abs(det) > 1e-9
    inv = np.where(ok, 1.0 / np.where(ok, det, 1), 0)
    s = np.asarray(origin, float) - a
    u = np.einsum('ij,ij->i', s, p) * inv
    q = np.cross(s, e1)
    v = (q @ d) * inv
    t = np.einsum('ij,ij->i', e2, q) * inv
    hit = ok & (u >= 0) & (v >= 0) & (u + v <= 1) & (t > 1e-6)
    return t[hit].min() if hit.any() else None


def surface(tris, axis, sign, y, lateral):
    # punkt na powierzchni modelu: promień z zewnątrz (3 m) w stronę środka wzdłuż osi `axis` po stronie `sign`
    o = [0.0, y, 0.0]
    k = 0 if axis == 'x' else 2
    o[k] = sign * 3.0
    o[2 - k] = lateral
    d = [0.0, 0.0, 0.0]
    d[k] = -sign
    t = ray(tris, o, d)
    return None if t is None else sign * (3.0 - t)


if __name__ == '__main__':
    tris = load(sys.argv[1])
    allv = np.concatenate(tris)
    print('obrys', np.round(allv.min(axis=0), 3).tolist(), np.round(allv.max(axis=0), 3).tolist())
    for axis, sign in (('x', -1), ('x', 1), ('z', -1), ('z', 1)):
        print(f'--- bok {"-" if sign < 0 else "+"}{axis}: powierzchnia (y \\ poprzecznie)')
        lats = np.arange(-0.9, 0.91, 0.15) if allv.max() > 1.2 else np.arange(-0.3, 0.31, 0.1)
        print('      ' + ' '.join(f'{l:6.2f}' for l in lats))
        for y in np.arange(0.1, min(1.4, allv[:, 1].max()), 0.1):
            row = [surface(tris, axis, sign, y, l) for l in lats]
            print(f'{y:5.2f} ' + ' '.join('  ----' if r is None else f'{r:6.2f}' for r in row))
