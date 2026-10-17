import os, random
import numpy as np
from PIL import Image

CELL, GRID = 256, 4
MARGIN = 26
COLORS = {
    'yellow': (238, 170, 56), 'yellow_dark': (196, 132, 30), 'dark': (64, 68, 76), 'grey': (138, 144, 152),
    'light': (210, 214, 220), 'orange': (214, 98, 44), 'steel': (168, 174, 182), 'screen': (40, 96, 120),
    'black': (36, 38, 42), 'rubber': (50, 52, 56), 'olive': (112, 122, 72),
    'white': (226, 230, 234), 'cyan': (70, 190, 220), 'blue': (60, 110, 190),
    'green': (70, 200, 90), 'red': (220, 64, 52),
}
NO_HATCH = ('screen', 'cyan', 'blue', 'black', 'rubber', 'green', 'red')
INK = np.array((22, 22, 26), float)
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets')

def noise1d(rnd, n, scale, amp):
    k = n // scale + 2
    pts = np.array([rnd.uniform(-1, 1) for _ in range(k)])
    x = np.arange(n) / scale
    i = x.astype(int)
    f = x - i
    f = f * f * (3 - 2 * f)
    return (pts[i] * (1 - f) + pts[i + 1] * f) * amp

def rough(rnd, n):
    return noise1d(rnd, n, 24, 2.5) + noise1d(rnd, n, 5, 1.2)

def cell(name):
    rnd = random.Random(name)
    base = np.array(COLORS[name], float)
    c = CELL
    y, x = np.mgrid[0:c, 0:c].astype(float)
    v = 1 - y / (c - 1)
    img = np.empty((c, c, 3))
    shade = 0.95 + 0.08 * v ** 0.8
    img[:] = base * shade[..., None]

    def edge_dist(jag):
        left = x - jag[0][y.astype(int)]
        right = (c - 1 - x) - jag[1][y.astype(int)]
        top = y - jag[2][x.astype(int)]
        bottom = (c - 1 - y) - jag[3][x.astype(int)]
        d = np.minimum(np.minimum(left, right), np.minimum(top, bottom))
        r = MARGIN * 0.8
        cx = np.clip(x, r, c - 1 - r)
        cy = np.clip(y, r, c - 1 - r)
        hyp = np.hypot(x - cx, y - cy)
        corner = np.where(hyp > 0, r - hyp, c)
        return np.minimum(d, corner)

    light_jag = [rough(rnd, c) * 2.2 + 2 for _ in range(4)]
    d_light = edge_dist(light_jag)
    d_ink = edge_dist([rough(rnd, c) * 0.35 for _ in range(4)])

    worn = (d_light < 15) & (d_ink >= 9)
    lighten = 0.5 if name in ('black', 'rubber', 'dark') else 0.6
    img[worn] = img[worn] * (1 - lighten) + np.array((245, 245, 245)) * lighten

    if name not in NO_HATCH:
        inner = c - 2 * MARGIN
        hx0, hx1 = MARGIN + 8, MARGIN + inner * rnd.uniform(0.45, 0.6)
        hy0, hy1 = c - MARGIN - inner * rnd.uniform(0.3, 0.42), c - MARGIN - 8
        jx = noise1d(rnd, c, 14, 7) + noise1d(rnd, c, 4, 2)
        jy = noise1d(rnd, c, 14, 7) + noise1d(rnd, c, 4, 2)
        region = (x > hx0 + jy[y.astype(int)] * 0.5) & (x < hx1 + jy[y.astype(int)]) & \
                 (y > hy0 + jx[x.astype(int)]) & (y < hy1 + jx[x.astype(int)] * 0.3)
        stripes = ((x + y) % 9) < 3.5
        h = region & stripes
        img[h] = img[h] * 0.5
        soft = region & ~stripes
        img[soft] = img[soft] * 0.88

    if name in ('screen', 'cyan', 'blue'):
        glare = (np.abs((x - y) - c * 0.15) < c * 0.06) | (np.abs((x - y) - c * 0.35) < c * 0.02)
        g = glare & (d_ink > MARGIN)
        img[g] = img[g] * 0.6 + 255 * 0.4

    for _ in range(26):
        px, py = rnd.uniform(MARGIN, c - MARGIN), rnd.uniform(MARGIN, c - MARGIN)
        rad = rnd.uniform(0.8, 2.6)
        mask = (x - px) ** 2 + (y - py) ** 2 < rad * rad
        img[mask] = img[mask] * (0.7 if rnd.random() < 0.5 else 1.0) + (0 if rnd.random() < 0.5 else 40)

    ink = d_ink < 9
    img[ink] = INK
    return np.clip(img, 0, 255).astype(np.uint8)

def main():
    atlas = np.zeros((CELL * GRID, CELL * GRID, 3), np.uint8)
    atlas[:] = INK.astype(np.uint8)
    for i, name in enumerate(COLORS):
        cx, cy = i % GRID, i // GRID
        atlas[cy * CELL:(cy + 1) * CELL, cx * CELL:(cx + 1) * CELL] = cell(name)
    Image.fromarray(atlas).save(os.path.join(OUT, 'door_albedo.png'))
    print('door_albedo.png', atlas.shape)

if __name__ == '__main__':
    main()
