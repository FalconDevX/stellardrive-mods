import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets')

THRUSTER = [
    ((-0.42, 0.30, 0.05), 'x', -0.35, 'cyan'),
    ((0.42, 0.30, 0.05), 'x', 0.35, 'cyan'),
    ((0.0, 0.30, 0.44), 'z', 0.41, 'yellow'),
]
REACTOR = [
    ((-1.00, 0.50, -0.60), 'x', -0.95, 'yellow'),
    ((1.00, 0.50, 0.55), 'x', 0.96, 'yellow'),
    ((0.75, 0.50, 0.97), 'z', 0.81, 'blue'),
    ((-0.75, 0.22, 0.97), 'z', 0.91, 'white'),
]

def mount(m, pos, axis, wall, color):
    k = 0 if axis == 'x' else 2
    sign = 1 if pos[k] > 0 else -1
    inner, outer = wall - sign * 0.03, pos[k] - sign * 0.012
    length = abs(outer - inner)

    def at(t, size):
        c = list(pos)
        c[k] = t
        s = [size[0], size[1], size[0]]
        s[k] = size[2]
        return tuple(c), tuple(s)
    c, s = at((inner + outer) / 2, (0.11, 0.11, length))
    m.box(c, s, 'dark')
    c, s = at(wall + sign * 0.006, (0.2, 0.2, 0.024))
    m.box(c, s, 'grey')
    c, s = at(outer - sign * 0.012, (0.15, 0.15, 0.03))
    m.box(c, s, color)

for name, items in (('thruster_mounts.bin', THRUSTER), ('reactor_mounts.bin', REACTOR)):
    m = Mesh()
    for item in items:
        mount(m, *item)
    m.write(os.path.join(OUT, name))
