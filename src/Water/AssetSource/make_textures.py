# Tekstury wody (bezszwowe, 512 px): kaustyki (siateczka jasnych linii - krawędzie komórek Voronoi), piana
# (nieregularne plamy i pasy) i normal mapa drobnych fal. Użycie: python -I make_textures.py <folder wyjściowy>
import sys, os
import numpy as np
from PIL import Image

OUT = sys.argv[1]
N = 512
rng = np.random.default_rng(7)


def tile_noise(n, freq, seed):
    # bezszwowy szum wartości (interpolacja na siatce z zawijaniem)
    r = np.random.default_rng(seed).random((freq, freq))
    ys, xs = np.mgrid[0:n, 0:n] * freq / n
    x0, y0 = np.floor(xs).astype(int), np.floor(ys).astype(int)
    fx, fy = xs - x0, ys - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a = r[y0 % freq, x0 % freq]; b = r[y0 % freq, (x0 + 1) % freq]
    c = r[(y0 + 1) % freq, x0 % freq]; d = r[(y0 + 1) % freq, (x0 + 1) % freq]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def jittered(cells, jitter, seed):
    # punkty na siatce z losowym przesunięciem - bez prawie pokrywających się punktów (dawały klinowate plamy)
    r = np.random.default_rng(seed)
    return [((i + 0.5 + (r.random() - 0.5) * jitter) / cells, (j + 0.5 + (r.random() - 0.5) * jitter) / cells)
            for i in range(cells) for j in range(cells)]


def voronoi_f1f2(points, n, warp_amount=0.0, seed=0):
    # odległości do najbliższego i drugiego punktu, z zawijaniem (bezszwowo); płynne zawirowanie współrzędnych
    ys, xs = np.mgrid[0:n, 0:n] / n
    if warp_amount:
        xs = xs + (tile_noise(n, 5, seed) - 0.5) * warp_amount
        ys = ys + (tile_noise(n, 5, seed + 1) - 0.5) * warp_amount
    f1 = np.full((n, n), 9.0); f2 = np.full((n, n), 9.0)
    for px, py in points:
        for ox in (-1, 0, 1):
            for oy in (-1, 0, 1):
                d = np.sqrt((xs - px - ox) ** 2 + (ys - py - oy) ** 2)
                f2 = np.where(d < f1, f1, np.minimum(f2, d))
                f1 = np.minimum(f1, d)
    return f1, f2


def warp(img, amount, seed):
    # zawirowanie linii (jak na wzorcu - faliste, nie proste krawędzie)
    nx = tile_noise(N, 6, seed) - 0.5; ny = tile_noise(N, 6, seed + 1) - 0.5
    ys, xs = np.mgrid[0:N, 0:N]
    sx = ((xs + nx * amount * N) % N).astype(int); sy = ((ys + ny * amount * N) % N).astype(int)
    return img[sy, sx]


# kaustyki: krawędzie Voronoi, miękkie, zawirowane
f1, f2 = voronoi_f1f2(jittered(5, 0.8, 1), N, 0.12, 3)
edge = np.clip(1 - (f2 - f1) / 0.03, 0, 1) ** 1.6
Image.fromarray((edge * 255).astype(np.uint8)).convert('L').save(os.path.join(OUT, 'water_caustics.png'))

# piana: plamy z progowanego szumu + drobne dziury (jak piana przy brzegu na wzorcu)
fn = 0.6 * tile_noise(N, 8, 11) + 0.3 * tile_noise(N, 16, 12) + 0.1 * tile_noise(N, 32, 13)
g1, g2 = voronoi_f1f2(jittered(8, 0.85, 2), N, 0.08, 5)
holes = np.clip((g2 - g1) / 0.035, 0, 1)            # środki komórek = dziury w pianie
foam = np.clip((fn - 0.4) * 5, 0, 1) * np.clip(1.25 - holes, 0, 1)
Image.fromarray((foam * 255).astype(np.uint8)).convert('L').save(os.path.join(OUT, 'water_foam.png'))

# normal mapa: wysokość z szumów, normalna z gradientu (z zawijaniem)
h = 0.5 * tile_noise(N, 8, 21) + 0.3 * tile_noise(N, 16, 22) + 0.2 * tile_noise(N, 32, 23)
h = h + 0.15 * (1 - f1 / f1.max())
dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 6
dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 6
nz = 1 / np.sqrt(dx * dx + dy * dy + 1)
nrm = np.stack([(-dx * nz + 1) / 2, (-dy * nz + 1) / 2, (nz + 1) / 2], -1)
Image.fromarray((nrm * 255).astype(np.uint8)).save(os.path.join(OUT, 'water_normal.png'))
print('gotowe')
