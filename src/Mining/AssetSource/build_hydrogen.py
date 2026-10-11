# Modele low-poly do napędu wodorowego (styl części StellarDrive, kod brył w lowpoly.py):
#  - silnik wodorowy (wzór: model Meshy "Frostbite Cannon"): blok z tyłu, sekcja z rurami chłodzenia, rozszerzona dysza.
#    Dysza na lokalnej górze (+Y), bo gra pcha statek w kierunku -Y części (ShipThrust).
#  - elektrolizer: dwa zbiorniki (wodór, tlen) i blok z ekranem na podstawie.
# Wynik: Assets/hydrogen_thruster.bin, Assets/electrolyzer.bin, Assets/hydrogen_albedo.png.
# Użycie: python -I build_hydrogen.py <folder Assets> [folder na podgląd .obj]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, write_palette, write_preview_obj

OUT = sys.argv[1]
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None

# ---------------- Silnik wodorowy (1 x 2 x 1 m, środek podstawy w 0,0,0) ----------------
th = Mesh()
# blok z tyłu (biały, ciemny panel z przodu i kratki)
th.box((0, 0.3, 0), (0.66, 0.6, 0.66), 'white', {'top': 'grey', 'bottom': 'dark'})
th.box((0, 0.27, -0.335), (0.36, 0.4, 0.02), 'dark')
th.box((0, 0.27, -0.347), (0.14, 0.24, 0.01), 'black')
for k in range(3):
    th.box((0.335, 0.4 - k * 0.07, 0.12), (0.02, 0.03, 0.2), 'dark')
    th.box((-0.335, 0.4 - k * 0.07, 0.12), (0.02, 0.03, 0.2), 'dark')
th.box((0, 0.03, 0), (0.7, 0.06, 0.7), 'dark')
for sx in (-1, 1):
    for sz in (-1, 1):
        th.box((sx * 0.3, 0.6, sz * 0.3), (0.08, 0.04, 0.08), 'orange')   # pomarańczowe narożniki
# sekcja środkowa z pierścieniami i rurami chłodzenia
th.cylinder((0, 0.92, 0), 0.23, 0.64, 'white', 16, cap_color='dark')
th.cylinder((0, 0.66, 0), 0.25, 0.06, 'dark', 16)
th.cylinder((0, 1.0, 0), 0.24, 0.05, 'orange', 16)
th.cylinder((0, 1.2, 0), 0.25, 0.06, 'dark', 16)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    x, z = math.cos(a) * 0.29, math.sin(a) * 0.29
    th.cylinder((x, 0.93, z), 0.035, 0.58, 'dark', 8)                     # rura chłodzenia
    th.box((x, 0.82, z), (0.08, 0.08, 0.08), 'orange')                     # zawór
th.box((0, 0.93, -0.25), (0.16, 0.22, 0.06), 'grey')                       # skrzynka z boku
th.box((0, 0.98, -0.282), (0.08, 0.05, 0.01), 'cyan')
# kołnierz i dysza
th.cylinder((0, 1.3, 0), 0.2, 0.14, 'dark', 16)
th.cylinder((0, 1.6, 0), 0.19, 0.46, 'light', 16, radius_top=0.37)
th.cylinder((0, 1.84, 0), 0.38, 0.04, 'dark', 16, cap_color='black')
NOZZLE_TOP = (0, 1.86, 0)

# ---------------- Elektrolizer (1 x 1 x 1 m) ----------------
el = Mesh()
el.box((0, 0.04, 0), (0.92, 0.08, 0.92), 'dark')
el.box((0, 0.12, 0), (0.84, 0.08, 0.84), 'grey')
# zbiornik wodoru (biały z cyjanowym pasem) i tlenu (biały z niebieskim) z tyłu
for x, band in ((-0.22, 'cyan'), (0.22, 'blue')):
    el.cylinder((x, 0.55, 0.16), 0.18, 0.78, 'white', 14, cap_color='grey')
    el.cylinder((x, 0.6, 0.16), 0.185, 0.07, band, 14)
    el.cylinder((x, 0.97, 0.16), 0.08, 0.06, 'dark', 10)
# blok sterujący z ekranem z przodu
el.box((0, 0.42, -0.24), (0.76, 0.52, 0.34), 'yellow', {'top': 'dark'})
el.box((0, 0.48, -0.412), (0.46, 0.26, 0.01), 'grey')
el.box((0, 0.48, -0.418), (0.36, 0.18, 0.01), 'screen')
for k, c in enumerate(('cyan', 'blue', 'white')):
    el.box((-0.24 + k * 0.24, 0.26, -0.415), (0.1, 0.05, 0.02), c)       # lampki: wodór, tlen, woda
# rury u góry
el.box((0, 0.98, 0.16), (0.52, 0.05, 0.05), 'dark')
el.cylinder((0, 0.78, -0.05), 0.03, 0.45, 'dark', 8)

th.write(os.path.join(OUT, 'hydrogen_thruster.bin'))
el.write(os.path.join(OUT, 'electrolyzer.bin'))
write_palette(os.path.join(OUT, 'hydrogen_albedo.png'))
print('NOZZLE_TOP', NOZZLE_TOP)
if PREVIEW:
    tex = os.path.join(OUT, 'hydrogen_albedo.png')
    write_preview_obj(os.path.join(PREVIEW, 'thruster.obj'), tex, ((th, (0, 0, 0)),))
    write_preview_obj(os.path.join(PREVIEW, 'electrolyzer.obj'), tex, ((el, (0, 0, 0)),))
