import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from inkpoly import Mesh

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets')

SOCKET_Y = 0.13
VENT_H = 0.34
INTAKE_H = 0.86


def socket_stub(m, y, z_face):
    m.box((0, y, z_face + 0.09), (0.22, 0.2, 0.18), 'grey')
    m.cylinder((0, y, z_face + 0.03), 0.075, 0.06, 'blue', 16, axis='z')
    m.cylinder((0, y, z_face + 0.008), 0.095, 0.016, 'grey', 20, axis='z')
    m.cylinder((0, y, z_face - 0.001), 0.06, 0.004, 'black', 16, axis='z')
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        m.cylinder((math.cos(a) * 0.082, y + math.sin(a) * 0.082, z_face), 0.008, 0.01, 'steel', 6, axis='z')
    m.box((0, y + 0.135, z_face + 0.005), (0.16, 0.025, 0.01), 'blue')


vent = Mesh()
vent.box((0, 0.03, 0), (0.98, 0.06, 0.98), 'dark')
for k in range(7):
    t = -0.42 + k * 0.14
    for side in (-1, 1):
        vent.box((t, 0.03, side * 0.493), (0.07, 0.045, 0.01), 'yellow')
        vent.box((side * 0.493, 0.03, t), (0.01, 0.045, 0.07), 'yellow')
vent.box((0, 0.105, 0), (0.9, 0.09, 0.9), 'grey')
for sx in (-1, 1):
    vent.box((sx * 0.44, 0.225, 0), (0.02, 0.15, 0.9), 'grey')
    vent.box((0, 0.225, sx * 0.44), (0.9, 0.15, 0.02), 'grey')
vent.box((0, 0.152, 0), (0.86, 0.004, 0.86), 'dark')
for sx in (-1, 1):
    vent.box((sx * 0.45, VENT_H - 0.03, 0), (0.06, 0.06, 0.96), 'steel')
    vent.box((0, VENT_H - 0.03, sx * 0.45), (0.96, 0.06, 0.06), 'steel')
for sx in (-1, 1):
    for sz in (-1, 1):
        vent.cylinder((sx * 0.45, VENT_H + 0.003, sz * 0.45), 0.022, 0.012, 'dark', 8)
vent.box((0, VENT_H - 0.16, 0), (0.84, 0.01, 0.84), 'black')
vent.cylinder((0, VENT_H - 0.105, 0), 0.34, 0.15, 'dark', 32, caps=False)
vent.cylinder((0, VENT_H - 0.035, 0), 0.36, 0.012, 'steel', 32, caps=False)
vent.cylinder((0, VENT_H - 0.15, 0), 0.05, 0.02, 'dark', 12)
for k in range(3):
    a = 2 * math.pi * k / 3 + math.pi / 6
    vent.box((math.cos(a) * 0.19, VENT_H - 0.15, math.sin(a) * 0.19), (0.3, 0.018, 0.025), 'grey', rot=(0, -math.degrees(a), 0))
vent.cylinder((0, VENT_H - 0.135, 0), 0.07, 0.03, 'grey', 16, cap_color='dark')
for r in (0.12, 0.22, 0.32):
    vent.cylinder((0, VENT_H - 0.012, 0), r, 0.012, 'light', 32, caps=False)
    vent.cylinder((0, VENT_H - 0.012, 0), r - 0.008, 0.012, 'light', 32, caps=False)
for k in range(8):
    a = 2 * math.pi * k / 8
    vent.box((math.cos(a) * 0.19, VENT_H - 0.012, math.sin(a) * 0.19), (0.34, 0.012, 0.012), 'light', rot=(0, -math.degrees(a), 0))
vent.cylinder((0, VENT_H - 0.01, 0), 0.04, 0.016, 'orange', 12)
vent.box((0.3, 0.18, -0.452), (0.18, 0.1, 0.01), 'blue')
vent.box((0.3, 0.18, -0.458), (0.12, 0.05, 0.004), 'white')
socket_stub(vent, SOCKET_Y, -0.49)

FAN_Y = VENT_H - 0.09
fan = Mesh()
fan.cylinder((0, 0, 0), 0.055, 0.06, 'dark', 16, cap_color='grey')
fan.cylinder((0, 0.035, 0), 0.04, 0.012, 'orange', 12, radius_top=0.02)
BLADES, SEGMENTS = 7, 6
for k in range(BLADES):
    a = 360.0 * k / BLADES
    for j in range(SEGMENTS):
        r0 = 0.055 + j * (0.325 - 0.055) / SEGMENTS
        r1 = r0 + (0.325 - 0.055) / SEGMENTS
        rm = (r0 + r1) / 2
        chord = 0.075 + 0.05 * (j / (SEGMENTS - 1))
        pitch = 38 - 18 * (j / (SEGMENTS - 1))
        sweep = a + 9 * (j / (SEGMENTS - 1))
        rad = math.radians(sweep)
        fan.box((math.cos(rad) * rm, 0, math.sin(rad) * rm), (r1 - r0 + 0.004, 0.008, chord), 'steel' if j < SEGMENTS - 1 else 'light', rot=(pitch, -sweep, 0))
fan.cylinder((0, 0, 0), 0.33, 0.02, 'grey', 32, caps=False)

intake = Mesh()
intake.box((0, 0.03, 0), (0.98, 0.06, 0.98), 'dark')
for k in range(7):
    t = -0.42 + k * 0.14
    for side in (-1, 1):
        intake.box((t, 0.03, side * 0.493), (0.07, 0.045, 0.01), 'yellow')
        intake.box((side * 0.493, 0.03, t), (0.01, 0.045, 0.07), 'yellow')
intake.box((0, 0.13, 0), (0.6, 0.14, 0.6), 'grey')
for sx in (-1, 1):
    for sz in (-1, 1):
        intake.box((sx * 0.4, 0.09, sz * 0.4), (0.1, 0.06, 0.1), 'orange')
        intake.cylinder((sx * 0.4, 0.125, sz * 0.4), 0.02, 0.02, 'steel', 8)
intake.cylinder((0, 0.42, 0), 0.2, 0.44, 'light', 24)
for y in (0.26, 0.4, 0.54):
    intake.cylinder((0, y, 0), 0.215, 0.03, 'steel', 24, caps=False)
for k in range(4):
    a = math.radians(45 + k * 90)
    intake.box((math.cos(a) * 0.205, 0.42, math.sin(a) * 0.205), (0.05, 0.44, 0.04), 'orange', rot=(0, -math.degrees(a) + 90, 0))
intake.cylinder((0, 0.66, 0), 0.32, 0.06, 'dark', 28)
intake.cylinder((0, 0.72, 0), 0.3, 0.06, 'black', 28, caps=False)
for k in range(20):
    a = 2 * math.pi * k / 20
    intake.box((math.cos(a) * 0.3, 0.72, math.sin(a) * 0.3), (0.02, 0.07, 0.03), 'steel', rot=(0, -math.degrees(a), 0))
intake.cylinder((0, 0.79, 0), 0.38, 0.06, 'grey', 28, cap_color='steel', radius_top=0.34)
intake.cylinder((0, 0.84, 0), 0.2, 0.04, 'steel', 20, cap_color='dark', radius_top=0.12)
intake.cylinder((0, INTAKE_H, 0), 0.04, 0.02, 'red', 10)
intake.box((0, 0.42, -0.205), (0.14, 0.12, 0.01), 'blue')
intake.box((0, 0.42, -0.211), (0.09, 0.05, 0.004), 'white')
intake.box((0, SOCKET_Y, -0.38), (0.22, 0.2, 0.16), 'grey')
socket_stub(intake, SOCKET_Y, -0.49)

if __name__ == '__main__':
    vent.write(os.path.join(OUT, 'vent_big.bin'))
    intake.write(os.path.join(OUT, 'intake_big.bin'))
    fan.write(os.path.join(OUT, 'vent_fan.bin'))
