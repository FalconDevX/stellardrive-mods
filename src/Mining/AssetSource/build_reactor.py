# Model reaktora low-poly (styl części StellarDrive, kod brył w lowpoly.py). Wzór: model Meshy "Flux Reactor Module":
# kostka w czarnej ramie z dużymi pomarańczowymi narożnikami, biały korpus z panelami i nitami, kopuła z pierścieniami
# i klamrami, szklana tuba rdzenia we wnęce z przodu (świeci na cyjan), panel ze znakiem radiacji, ekranem i przyciskami,
# rury z kolankami, karbowane węże z zaworem, kratki wentylacyjne i ukośne pasy ostrzegawcze przy podstawie.
# Wynik: Assets/reactor_body.bin, Assets/reactor_core.bin (tuba rdzenia - osobno, świeci przy pracy), Assets/reactor_albedo.png.
# Użycie: python -I build_reactor.py <folder Assets> [plik .obj do podglądu]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, write_palette, write_preview_obj

OUT = sys.argv[1]
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None
H = 0.92  # połowa rozstawu słupów (część 2 x 2 x 2 m, środek podstawy w 0,0,0, przód = -Z)


# --- pomocnicze bryły (obrót walca z osi Y; obroty o wyznaczniku +1 zachowują kierunek ścianek) ---
def rot_x(p): return (p[1], -p[0], p[2])      # oś Y -> oś X
def rot_z(p): return (p[0], -p[2], p[1])      # oś Y -> oś Z


def add_rotated(dst, src, rot, offset):
    base = len(dst.v)
    for p, n, uv in src.v:
        q = rot(p)
        dst.v.append(((q[0] + offset[0], q[1] + offset[1], q[2] + offset[2]), rot(n), uv))
    dst.i += [base + i for i in src.i]


def cyl(m, center, axis, radius, length, color, sides=12, cap=None, radius_top=None):
    if axis == 'y':
        m.cylinder(center, radius, length, color, sides, cap_color=cap, radius_top=radius_top)
        return
    tmp = Mesh()
    tmp.cylinder((0, 0, 0), radius, length, color, sides, cap_color=cap, radius_top=radius_top)
    add_rotated(m, tmp, rot_x if axis == 'x' else rot_z, center)


def ribbed_hose(m, x0, y0, z, x1, y1, axis_y=True):
    # karbowany wąż: naprzemienne pierścienie (pionowo)
    n = 7
    for k in range(n):
        y = y0 + (y1 - y0) * (k + 0.5) / n
        cyl(m, (x0, y, z), 'y', 0.075 if k % 2 == 0 else 0.062, (y1 - y0) / n, 'black' if k % 2 == 0 else 'rubber', 10)


def disc_front(m, center, radius, color, sides=16, depth=0.01):
    cyl(m, center, 'z', radius, depth, color, sides)


body = Mesh()

# --- podstawa: płyta, kratki wentylacyjne, stopy w narożnikach ---
body.box((0, 0.07, 0), (2.0, 0.14, 2.0), 'black')
body.box((0, 0.17, 0), (1.72, 0.06, 1.72), 'dark')
for side in (-1, 1):
    for k in range(9):                                                   # kratki z przodu/tyłu i po bokach
        t = -0.64 + k * 0.16
        body.box((t, 0.07, side * 1.001), (0.07, 0.06, 0.01), 'dark')
        body.box((side * 1.001, 0.07, t), (0.01, 0.06, 0.07), 'dark')
for sx in (-1, 1):
    for sz in (-1, 1):
        body.box((sx * 0.93, 0.09, sz * 0.93), (0.34, 0.18, 0.34), 'dark', {'top': 'black'})
        body.box((sx * 0.93, 0.19, sz * 0.93), (0.3, 0.04, 0.3), 'orange')

# --- rama: słupy z dużymi pomarańczowymi narożnikami (z ciemną wnęką), opaski, belki z nitami ---
for sx in (-1, 1):
    for sz in (-1, 1):
        x, z = sx * H, sz * H
        body.box((x, 0.98, z), (0.18, 1.56, 0.18), 'black')
        body.box((x, 1.84, z), (0.32, 0.3, 0.32), 'orange', {'top': 'dark'})
        body.box((x, 1.995, z), (0.18, 0.012, 0.18), 'black')                 # wnęka na wierzchu narożnika
        body.box((x + sx * 0.161, 1.84, z), (0.01, 0.16, 0.1), 'black')       # szczeliny po bokach narożnika
        body.box((x, 1.84, z + sz * 0.161), (0.1, 0.16, 0.01), 'black')
        body.box((x, 1.2, z), (0.22, 0.06, 0.22), 'orange')                     # opaska w połowie słupa
        body.box((x, 0.3, z), (0.24, 0.1, 0.24), 'orange')                      # kołnierz u dołu
for sz in (-1, 1):
    body.box((0, 1.8, sz * H), (2 * H, 0.14, 0.14), 'black')
    for k in (-2, -1, 1, 2):
        body.box((k * 0.3, 1.8, sz * (H + 0.071)), (0.04, 0.04, 0.01), 'grey')  # nity na belkach
for sx in (-1, 1):
    body.box((sx * H, 1.8, 0), (0.14, 0.14, 2 * H), 'black')
    for k in (-2, -1, 1, 2):
        body.box((sx * (H + 0.071), 1.8, k * 0.3), (0.01, 0.04, 0.04), 'grey')

# --- biały korpus z panelami: szwy, nity, emblemat ---
body.box((0, 0.98, 0), (1.32, 1.52, 1.32), 'white', {'top': 'light'})
for y in (0.45, 1.5):                                                       # poziome szwy paneli
    for side in (-1, 1):
        body.box((0, y, side * 0.661), (1.3, 0.02, 0.01), 'grey')
        body.box((side * 0.661, y, 0), (0.01, 0.02, 1.3), 'grey')
for side in (-1, 1):
    for a in (-0.55, 0.55):
        for y in (0.33, 1.62):
            body.box((side * 0.662, y, a), (0.01, 0.04, 0.04), 'grey')       # nity na bokach
body.box((-0.662, 1.05, 0.0), (0.01, 0.22, 0.22), 'orange')                 # emblemat na lewym boku
body.box((-0.668, 1.05, 0.0), (0.01, 0.1, 0.1), 'white')
body.box((0.662, 1.05, 0.3), (0.01, 0.14, 0.14), 'orange')                  # mały emblemat z prawej

# --- wnęka rdzenia z przodu: ciemna rama, pierścienie tuby, ukośne pasy ostrzegawcze ---
body.box((0, 0.98, -0.6), (0.6, 1.16, 0.14), 'dark')                        # tylna ściana wnęki
for sx in (-1, 1):
    body.box((sx * 0.33, 0.98, -0.66), (0.06, 1.2, 0.04), 'black')          # boki ramy wnęki
body.box((0, 1.6, -0.66), (0.72, 0.08, 0.06), 'black')
body.box((0, 0.36, -0.66), (0.72, 0.08, 0.06), 'black')
cyl(body, (0, 1.5, -0.58), 'y', 0.22, 0.1, 'dark', 16, cap='black')         # pierścień nad tubą
cyl(body, (0, 0.46, -0.58), 'y', 0.22, 0.1, 'dark', 16, cap='black')        # pierścień pod tubą
for k in range(4):                                                           # pręty osłony tuby
    a = math.pi * (0.15 + 0.7 * k / 3) + math.pi
    body.box((math.cos(a) * 0.2, 0.98, -0.58 + math.sin(a) * 0.2), (0.025, 0.94, 0.025), 'black')
for k in range(7):                                                           # pasy ostrzegawcze (skos)
    x = -0.3 + k * 0.1
    body.tri((x, 0.21, -0.861), (x + 0.05, 0.31, -0.861), (x + 0.1, 0.31, -0.861), 'yellow')
    body.tri((x, 0.21, -0.861), (x + 0.1, 0.31, -0.861), (x + 0.05, 0.21, -0.861), 'yellow')
body.box((0, 0.26, -0.79), (0.72, 0.12, 0.14), 'black')                     # listwa pod pasami

# --- kopuła: pierścień, biała czasza, ciemne pierścienie, 6 klamer z wnęką ---
cyl(body, (0, 1.77, 0), 'y', 0.56, 0.08, 'grey', 20)
cyl(body, (0, 1.86, 0), 'y', 0.5, 0.12, 'white', 20, radius_top=0.42)
cyl(body, (0, 1.935, 0), 'y', 0.42, 0.03, 'dark', 20)
cyl(body, (0, 1.96, 0), 'y', 0.36, 0.04, 'white', 20, radius_top=0.3)
cyl(body, (0, 1.985, 0), 'y', 0.26, 0.02, 'black', 16)
cyl(body, (0, 1.997, 0), 'y', 0.12, 0.012, 'dark', 12)
for k in range(6):
    a = k * math.pi / 3 + math.pi / 6
    cx, cz = math.cos(a) * 0.5, math.sin(a) * 0.5
    body.box((cx, 1.86, cz), (0.14, 0.16, 0.14), 'orange', {'top': 'dark'})
    body.box((cx * 1.07, 1.86, cz * 1.07), (0.06, 0.08, 0.06), 'black')

# --- panel sterowania na prawym przednim słupie: znak radiacji, ekran, przyciski ---
px, pz = 0.6, -0.69
body.box((px, 1.05, pz), (0.3, 0.9, 0.06), 'white', {'front': 'light'})
body.box((px, 1.05, pz - 0.031), (0.26, 0.86, 0.004), 'light')
disc_front(body, (px, 1.33, pz - 0.035), 0.09, 'yellow', 16)
for k in range(3):                                                           # trójlistek radiacji
    a = math.pi / 2 + k * 2 * math.pi / 3
    a0, a1 = a - 0.45, a + 0.45
    zf = pz - 0.042
    body.tri((px + math.cos(a1) * 0.075, 1.33 + math.sin(a1) * 0.075, zf), (px + math.cos(a0) * 0.075, 1.33 + math.sin(a0) * 0.075, zf),
             (px + math.cos(a) * 0.022, 1.33 + math.sin(a) * 0.022, zf), 'black')
disc_front(body, (px, 1.33, pz - 0.04), 0.018, 'black', 8)
body.box((px, 1.08, pz - 0.035), (0.18, 0.13, 0.01), 'dark')
body.box((px, 1.08, pz - 0.041), (0.15, 0.1, 0.004), 'screen')
for k, c in enumerate(('yellow', 'orange', 'orange', 'yellow')):
    body.box((px - 0.045 + (k % 2) * 0.09, 0.86 - (k // 2) * 0.09, pz - 0.037), (0.06, 0.06, 0.014), c)
body.box((px, 0.66, pz - 0.035), (0.2, 0.04, 0.01), 'dark')

# --- lewy bok: rury z kolankami, karbowane węże, zawór ---
lx = -0.8
cyl(body, (lx, 0.85, -0.38), 'y', 0.08, 1.0, 'light', 12)                   # pionowa rura
cyl(body, (lx, 1.35, -0.38), 'y', 0.1, 0.06, 'orange', 12)
cyl(body, (lx, 0.42, -0.38), 'y', 0.1, 0.06, 'orange', 12)
cyl(body, (lx + 0.07, 1.35 + 0.0, -0.38), 'x', 0.08, 0.16, 'orange', 12)    # kolanko do korpusu
ribbed_hose(body, lx, 0.35, 0.1, lx, 1.2)
cyl(body, (lx, 1.24, 0.1), 'y', 0.09, 0.06, 'orange', 10)
cyl(body, (lx, 0.34, 0.1), 'y', 0.09, 0.06, 'orange', 10)
cyl(body, (lx - 0.06, 0.8, 0.42), 'x', 0.12, 0.03, 'orange', 12)            # koło zaworu
cyl(body, (lx - 0.03, 0.8, 0.42), 'x', 0.05, 0.08, 'dark', 8)
cyl(body, (lx + 0.02, 0.8, 0.42), 'y', 0.06, 0.5, 'grey', 10)

# --- prawy bok: rury i pozioma kolektor ---
rx = 0.8
for z, c in ((-0.25, 'light'), (0.25, 'dark')):
    cyl(body, (rx, 0.8, z), 'y', 0.07, 0.9, c, 10)
    for y in (0.5, 1.1):
        cyl(body, (rx, y, z), 'y', 0.09, 0.05, 'orange', 10)
cyl(body, (rx, 1.28, 0), 'z', 0.06, 0.7, 'grey', 10)
cyl(body, (rx, 1.28, -0.35), 'z', 0.08, 0.05, 'orange', 10)

# --- tył: zbiornik chłodzenia i przyłącze wody ---
cyl(body, (0.35, 0.9, 0.78), 'y', 0.14, 0.9, 'white', 14, cap='grey')
cyl(body, (0.35, 1.0, 0.78), 'y', 0.15, 0.05, 'orange', 14)
cyl(body, (-0.35, 0.9, 0.76), 'y', 0.06, 0.9, 'dark', 10)

# --- skrzynki przyłączeniowe (gniazda prądu po bokach, woda z tyłu) - położenie jak w Content/Reactor.cs ---
for x in (-0.99, 0.99):
    body.box((x, 0.42, 0.55), (0.1, 0.18, 0.16), 'dark', {'top': 'grey'})
body.box((0, 0.42, 0.99), (0.16, 0.18, 0.1), 'dark', {'top': 'blue'})

# --- tuba rdzenia (świeci na cyjan, gdy reaktor pracuje) ---
core = Mesh()
cyl(core, (0, 0.98, -0.58), 'y', 0.18, 0.94, 'cyan', 16)
for k in range(3):
    cyl(core, (0, 0.72 + k * 0.26, -0.58), 'y', 0.185, 0.025, 'dark', 16)

body.write(os.path.join(OUT, 'reactor_body.bin'))
core.write(os.path.join(OUT, 'reactor_core.bin'))
write_palette(os.path.join(OUT, 'reactor_albedo.png'))
if PREVIEW:
    write_preview_obj(PREVIEW, os.path.join(OUT, 'reactor_albedo.png'), ((body, (0, 0, 0)), (core, (0, 0, 0))))
