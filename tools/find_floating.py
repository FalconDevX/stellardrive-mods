import argparse
import json
import os
import struct

import numpy as np
from scipy.spatial import cKDTree

GAME_EXPORT = os.path.join(os.environ.get("STELLARDRIVE_DIR", r"C:\Program Files (x86)\Steam\steamapps\common\StellarDrive Demo"), "UserData", "Mining", "export")


def read_mesh(path):
    with open(path, "rb") as f:
        data = f.read()
    verts, count = struct.unpack_from("<ii", data, 4)
    v = np.frombuffer(data, dtype="<f4", count=verts * 8, offset=12).reshape(verts, 8)
    idx = np.frombuffer(data, dtype="<i4", count=count, offset=12 + verts * 32).reshape(-1, 3)
    return v[:, :3].astype(np.float64), idx


def components(pos, tris):
    keys = np.round(pos / 1e-4).astype(np.int64)
    _, weld = np.unique(keys, axis=0, return_inverse=True)
    weld = weld.ravel()
    parent = np.arange(weld.max() + 1)

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    for a, b, c in weld[tris]:
        ra, rb, rc = find(a), find(b), find(c)
        parent[rb] = ra
        parent[find(rc)] = ra
    roots = np.array([find(weld[t[0]]) for t in tris])
    return [tris[roots == r] for r in np.unique(roots)]


def samples(pos, tris, step):
    out = []
    for a, b, c in pos[tris]:
        area = np.linalg.norm(np.cross(b - a, c - a)) / 2
        n = min(20000, max(3, int(area / (step * step))))
        r = np.random.default_rng(0).random((n, 2))
        r[r.sum(1) > 1] = 1 - r[r.sum(1) > 1]
        out.append(a + np.outer(r[:, 0], b - a) + np.outer(r[:, 1], c - a))
        out.append(np.array([a, b, c, (a + b) / 2, (b + c) / 2, (a + c) / 2]))
    return np.vstack(out)


def analyse(meshes, gap, step):
    pieces = []
    for name, (pos, tris) in meshes.items():
        if len(tris) == 0:
            continue
        for comp in components(pos, tris):
            pieces.append((name, pos, comp))
    clouds = [samples(pos, comp, step) for _, pos, comp in pieces]
    owner = np.concatenate([np.full(len(c), i) for i, c in enumerate(clouds)])
    tree = cKDTree(np.vstack(clouds))
    floating = []
    for i, (name, pos, comp) in enumerate(pieces):
        best = np.inf
        d, j = tree.query(clouds[i], k=8)
        other = owner[j] != i
        if other.any():
            best = d[other].min()
        elif len(pieces) > 1:
            best = cKDTree(np.vstack([c for k, c in enumerate(clouds) if k != i])).query(clouds[i], k=1)[0].min()
        if best > gap:
            best = refine(pieces, i, best)
        if best > gap:
            pts = pos[comp.ravel()]
            floating.append((best, name, pts.mean(0), pts.max(0) - pts.min(0), len(comp)))
    return len(pieces), sorted(floating, key=lambda f: -f[0])


def refine(pieces, i, best):
    _, pos, comp = pieces[i]
    pts = pos[comp.ravel()]
    lo, hi = pts.min(0) - best - 0.01, pts.max(0) + best + 0.01
    near = []
    for k, (_, p, c) in enumerate(pieces):
        if k == i:
            continue
        tri = p[c]
        keep = np.all(tri.max(1) >= lo, axis=1) & np.all(tri.min(1) <= hi, axis=1)
        if keep.any():
            near.append(tri[keep])
    if not near:
        return best
    others = np.vstack(near)
    step = max(0.0015, min(0.004, best / 3))
    own = samples(pos, comp, step)
    flat = others.reshape(-1, 3)
    return min(best, cKDTree(samples(flat, np.arange(len(flat)).reshape(-1, 3), step)).query(own, k=1)[0].min())


def main():
    parser = argparse.ArgumentParser(description="List mesh pieces that do not touch the rest of a part.")
    parser.add_argument("part", help="part id from the game export, or .bin files separated by commas")
    parser.add_argument("--export", default=GAME_EXPORT)
    parser.add_argument("--gap", type=float, default=0.004, help="pieces farther than this from everything else are reported")
    parser.add_argument("--step", type=float, default=0.01, help="surface sampling distance")
    args = parser.parse_args()
    if args.part.isdigit():
        with open(os.path.join(args.export, "manifest.json"), encoding="utf-8") as f:
            part = next(p for p in json.load(f)["parts"] if p["id"] == int(args.part))
        files = {f"{m['name']} ({m['file']})": os.path.join(args.export, m["file"]) for m in part["meshes"]}
        print(part["label"])
    else:
        files = {os.path.basename(p): p for p in args.part.split(",")}
    meshes = {name: read_mesh(path) for name, path in files.items()}
    count, floating = analyse(meshes, args.gap, args.step)
    print(f"{count} pieces, {len(floating)} floating (gap > {args.gap})")
    for gap, name, center, size, tris in floating:
        print(f"  gap {gap:.4f}  {name}  center {np.round(center, 3)}  size {np.round(size, 3)}  {tris} tris")


if __name__ == "__main__":
    main()
