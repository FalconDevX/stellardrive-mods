# Mocowania gniazd (rury, prąd, sygnał) do modeli z GLB: gniazdo musi leżeć tuż poza obrysem kolizji części
# (inaczej kolizja zasłania je i nie da się podpiąć kabla/rury), a model bywa w tym miejscu wcięty - mocowanie
# (kołnierz przy ściance, króciec, obejma przy gnieździe) łączy gniazdo z konstrukcją, żeby nie wisiało w powietrzu.
# Położenia gniazd i ścianek zmierzone w mesh_probe.py; te same liczby są w Content/HydrogenThruster.cs i Reactor.cs.
# Wynik: Assets/thruster_mounts.bin, Assets/reactor_mounts.bin (paleta lowpoly: hydrogen_albedo.png / reactor_parts.png).
# Użycie: python -I build_mounts.py <folder Assets>
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets')

# (położenie gniazda, oś 'x' / 'z', gdzie zaczyna się ścianka modelu na tej osi, kolor obejmy)
THRUSTER = [
    ((-0.42, 0.30, 0.05), 'x', -0.35, 'cyan'),     # wodór / tlen
    ((0.42, 0.30, 0.05), 'x', 0.35, 'cyan'),
    ((0.0, 0.30, 0.44), 'z', 0.41, 'yellow'),      # prąd
]
REACTOR = [
    ((-1.00, 0.50, -0.60), 'x', -0.95, 'yellow'),  # prąd (lewy bok, przy przednim słupie)
    ((1.00, 0.50, 0.55), 'x', 0.96, 'yellow'),     # prąd (prawy bok)
    ((0.75, 0.50, 0.97), 'z', 0.81, 'blue'),       # woda (tył)
    ((-0.75, 0.22, 0.97), 'z', 0.91, 'white'),     # sygnał (tył, przy podstawie)
]


def mount(m, pos, axis, wall, color):
    k = 0 if axis == 'x' else 2
    sign = 1 if pos[k] > 0 else -1
    inner, outer = wall - sign * 0.03, pos[k] - sign * 0.012     # króciec wchodzi lekko w ściankę
    length = abs(outer - inner)

    def at(t, size):
        c = list(pos)
        c[k] = t
        s = [size[0], size[1], size[0]]
        s[k] = size[2]
        return tuple(c), tuple(s)
    c, s = at((inner + outer) / 2, (0.11, 0.11, length))
    m.box(c, s, 'dark')                                            # króciec
    c, s = at(wall + sign * 0.006, (0.2, 0.2, 0.024))
    m.box(c, s, 'grey')                                            # kołnierz na ściance
    c, s = at(outer - sign * 0.012, (0.15, 0.15, 0.03))
    m.box(c, s, color)                                             # obejma przy gnieździe (kolor rodzaju)


for name, items in (('thruster_mounts.bin', THRUSTER), ('reactor_mounts.bin', REACTOR)):
    m = Mesh()
    for item in items:
        mount(m, *item)
    m.write(os.path.join(OUT, name))
