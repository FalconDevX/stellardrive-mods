# Model reaktora low-poly (styl części StellarDrive, kod brył w lowpoly.py). Wzór: model Meshy "Flux Reactor Module":
# kostka w czarnej ramie z pomarańczowymi narożnikami, biały rdzeń z kopułą, cyjanowe okno rdzenia, rury i zawory
# po bokach, panel ze znakiem radiacji i lampkami, pasy ostrzegawcze przy podstawie.
# Wynik: Assets/reactor_body.bin, Assets/reactor_core.bin (okno rdzenia - osobno, świeci przy pracy), Assets/reactor_albedo.png.
# Użycie: python -I build_reactor.py <folder Assets> [plik .obj do podglądu]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, write_palette, write_preview_obj

OUT = sys.argv[1]
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None
H = 0.92  # połowa szerokości (2 x 2 x 2 m, środek podstawy w 0,0,0, przód = -Z)

body = Mesh()
# podstawa
body.box((0, 0.06, 0), (1.9, 0.12, 1.9), 'dark')
body.box((0, 0.15, 0), (1.7, 0.06, 1.7), 'grey')
for k in range(8):                                                      # pasy ostrzegawcze z przodu
    body.box((-0.7 + k * 0.2, 0.06, -0.951), (0.1, 0.08, 0.01), 'yellow' if k % 2 == 0 else 'black')
# rama: 4 słupy z pomarańczowymi czapami, belki u góry
for sx in (-1, 1):
    for sz in (-1, 1):
        x, z = sx * H, sz * H
        body.box((x, 0.95, z), (0.16, 1.6, 0.16), 'dark')
        body.box((x, 1.84, z), (0.26, 0.22, 0.26), 'orange', {'top': 'dark'})
        body.box((x, 0.22, z), (0.24, 0.14, 0.24), 'orange')
for sz in (-1, 1):
    body.box((0, 1.78, sz * H), (2 * H, 0.12, 0.12), 'dark')
for sx in (-1, 1):
    body.box((sx * H, 1.78, 0), (0.12, 0.12, 2 * H), 'dark')
# biały korpus rdzenia
body.box((0, 0.95, 0), (1.3, 1.5, 1.3), 'white', {'top': 'grey'})
body.box((0, 0.95, -0.651), (0.5, 1.0, 0.01), 'grey')                  # wnęka na okno rdzenia
# kopuła z pierścieniem i pomarańczowymi zaczepami
body.cylinder((0, 1.75, 0), 0.5, 0.1, 'grey', 16)
body.cylinder((0, 1.85, 0), 0.42, 0.12, 'white', 16, cap_color='dark', radius_top=0.34)
body.cylinder((0, 1.93, 0), 0.22, 0.05, 'dark', 12, cap_color='black')
for k in range(6):
    a = k * math.pi / 3
    body.box((math.cos(a) * 0.48, 1.8, math.sin(a) * 0.48), (0.1, 0.12, 0.1), 'orange')
# panel sterowania z prawej strony przodu (znak radiacji jako żółty kwadrat z ciemnym środkiem, lampki)
body.box((0.48, 1.25, -0.66), (0.26, 0.26, 0.02), 'yellow')
body.box((0.48, 1.25, -0.672), (0.1, 0.1, 0.01), 'black')
body.box((0.48, 0.8, -0.66), (0.26, 0.5, 0.02), 'light')
body.box((0.48, 0.92, -0.672), (0.18, 0.12, 0.01), 'screen')
for k, c in enumerate(('yellow', 'orange', 'orange')):
    body.box((0.42 + (k % 2) * 0.12, 0.7 - (k // 2) * 0.1, -0.672), (0.06, 0.06, 0.01), c)
# rury i zawory po bokach
for sx in (-1, 1):
    x = sx * 0.72
    body.cylinder((x, 0.75, -0.3), 0.07, 0.9, 'light', 10)
    body.cylinder((x, 0.75, 0.3), 0.07, 0.9, 'dark', 10)
    for y in (0.5, 1.0):
        body.cylinder((x, y, -0.3), 0.09, 0.06, 'orange', 10)
    body.box((x, 1.25, 0), (0.1, 0.1, 0.7), 'grey')
# skrzynki przyłączeniowe (gniazda prądu po bokach, woda z tyłu)
for x in (-0.99, 0.99):
    body.box((x, 0.42, 0.55), (0.1, 0.18, 0.16), 'dark', {'top': 'grey'})
body.box((0, 0.42, 0.99), (0.16, 0.18, 0.1), 'dark', {'top': 'blue'})

# okno rdzenia (świeci na cyjan, gdy reaktor pracuje)
core = Mesh()
core.box((0, 0.95, -0.662), (0.38, 0.86, 0.01), 'cyan')
for k in range(4):
    core.box((0, 0.62 + k * 0.22, -0.666), (0.4, 0.03, 0.01), 'dark')

body.write(os.path.join(OUT, 'reactor_body.bin'))
core.write(os.path.join(OUT, 'reactor_core.bin'))
write_palette(os.path.join(OUT, 'reactor_albedo.png'))
if PREVIEW:
    write_preview_obj(PREVIEW, os.path.join(OUT, 'reactor_albedo.png'), ((body, (0, 0, 0)), (core, (0, 0, 0))))
