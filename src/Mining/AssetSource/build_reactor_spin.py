# Ruchome części reaktora (do modelu z build_reactor_glb.py), styl low-poly z lowpoly.py:
#  - reactor_core.bin: rdzeń w szklanej tubie - świecący walec w pionowe pasy z trzema prętami; obraca się wokół osi Y
#    (środek w 0, 0, 0; w grze ustawiony w osi tuby). Zasłania szkło tuby z modelu (promień szkła ok. 0.14 m).
#  - reactor_rotor.bin: wirnik na kopule - pierścień z łopatkami i lampkami (środek w 0, 0, 0, obraca się wokół Y).
# Wynik: Assets/reactor_core.bin, Assets/reactor_rotor.bin, Assets/reactor_parts.png (paleta).
# Użycie: python -I build_reactor_spin.py <folder Assets> [folder na podgląd .obj]
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, write_palette, write_preview_obj

OUT = sys.argv[1]
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None

CORE_R, CORE_H = 0.152, 0.46     # oś tuby: (0, 0.64, -+0.45) w układzie części - Content/Reactor.cs
core = Mesh()
n = 16
for k in range(n):
    a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
    color = 'cyan' if k % 2 == 0 else 'blue'
    y0, y1 = -CORE_H / 2, CORE_H / 2
    p = lambda a, y: (math.cos(a) * CORE_R, y, math.sin(a) * CORE_R)
    n0 = (math.cos(a0), 0, math.sin(a0))
    n1 = (math.cos(a1), 0, math.sin(a1))
    core.quad(p(a0, y0), p(a0, y1), p(a1, y1), p(a1, y0), color, (n0, n0, n1, n1))
for k in range(3):                                    # pręty obracające się razem z rdzeniem
    a = 2 * math.pi * k / 3
    core.box((math.cos(a) * (CORE_R + 0.004), 0, math.sin(a) * (CORE_R + 0.004)), (0.022, CORE_H, 0.022), 'dark')
for y in (-0.12, 0.0, 0.12):                          # pierścienie świecące jaśniej
    core.cylinder((0, y, 0), CORE_R + 0.002, 0.018, 'white', n, cap_color='white')

rotor = Mesh()
rotor.cylinder((0, 0.01, 0), 0.3, 0.02, 'dark', 24, cap_color='black')
rotor.cylinder((0, 0.025, 0), 0.08, 0.03, 'grey', 16, cap_color='dark')
for k in range(6):                                    # łopatki
    a = 2 * math.pi * k / 6
    rotor.box((math.cos(a) * 0.19, 0.03, math.sin(a) * 0.19), (0.05, 0.02, 0.05), 'orange')
    rotor.box((math.cos(a + 0.5) * 0.25, 0.024, math.sin(a + 0.5) * 0.25), (0.03, 0.012, 0.03), 'cyan')
for k in range(3):                                    # ramiona
    a = 2 * math.pi * k / 3
    for t in range(4):
        r = 0.09 + t * 0.05
        rotor.box((math.cos(a) * r, 0.026, math.sin(a) * r), (0.05, 0.016, 0.03), 'grey')

core.write(os.path.join(OUT, 'reactor_core.bin'))
rotor.write(os.path.join(OUT, 'reactor_rotor.bin'))
write_palette(os.path.join(OUT, 'reactor_parts.png'))
if PREVIEW:
    write_preview_obj(os.path.join(PREVIEW, 'reactor_spin.obj'), os.path.join(OUT, 'reactor_parts.png'), ((core, (0, 0, 0)), (rotor, (0, 0.5, 0))))
