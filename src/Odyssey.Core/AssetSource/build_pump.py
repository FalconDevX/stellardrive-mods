import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from inkpoly import Mesh

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets')
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None

SOCKET_Y = 0.3
SOCKETS = [(-0.33, 'yellow'), (-0.11, 'white'), (0.11, 'blue'), (0.33, 'blue')]
PANEL_Z = 0.455
body = Mesh()
body.box((0, 0.04, 0), (0.96, 0.08, 0.96), 'dark')
body.box((0, 0.095, 0), (0.88, 0.03, 0.88), 'grey')
for k in range(6):
    t = -0.4 + k * 0.16
    for side in (-1, 1):
        body.box((t, 0.04, side * 0.485), (0.08, 0.06, 0.012), 'yellow')
        body.box((side * 0.485, 0.04, t), (0.012, 0.06, 0.08), 'yellow')
for sx in (-1, 1):
    for sz in (-1, 1):
        body.box((sx * 0.42, 0.125, sz * 0.42), (0.09, 0.03, 0.09), 'orange')
        body.cylinder((sx * 0.42, 0.142, sz * 0.42), 0.032, 0.006, 'grey', 16)
        body.cylinder((sx * 0.42, 0.153, sz * 0.42), 0.026, 0.018, 'steel', 6)
        body.cylinder((sx * 0.42, 0.168, sz * 0.42), 0.013, 0.016, 'grey', 10)
for t in (-0.2, 0.0, 0.2):
    for x, z in ((-0.462, t), (0.462, t), (t, -0.462)):
        body.cylinder((x, 0.082, z), 0.012, 0.006, 'steel', 6)
body.cylinder((0, 0.17, 0), 0.34, 0.12, 'steel', 24, cap_color='grey')
for k in range(12):
    a = 2 * math.pi * k / 12
    body.cylinder((math.cos(a) * 0.32, 0.235, math.sin(a) * 0.32), 0.016, 0.02, 'dark', 6)
body.cylinder((0, 0.255, 0), 0.3, 0.03, 'dark', 24)
body.cylinder((0, 0.5, 0), 0.26, 0.46, 'light', 24, cap_color='grey')
for y in (0.32, 0.4, 0.48, 0.56, 0.64):
    body.cylinder((0, y, 0), 0.272, 0.022, 'grey', 24, caps=False)
for k in range(4):
    a = math.radians(45 + k * 90)
    x, z = math.cos(a) * 0.265, math.sin(a) * 0.265
    yaw = -math.degrees(a) + 90
    body.box((x, 0.5, z), (0.06, 0.46, 0.04), 'orange', rot=(0, yaw, 0))


def radial_box(deg, r, y, size, color):
    a = math.radians(deg)
    body.box((math.cos(a) * r, y, math.sin(a) * r), size, color, rot=(0, -deg + 90, 0))


for deg in (-35, -25, -15, -5, 5, 15, 25, 35, 182, 192, 202):
    radial_box(deg, 0.275, 0.5, (0.012, 0.42, 0.042), 'grey')
    radial_box(deg, 0.292, 0.5, (0.016, 0.44, 0.01), 'dark')
TERMINAL = 158
radial_box(TERMINAL, 0.29, 0.56, (0.14, 0.12, 0.09), 'grey')
radial_box(TERMINAL, 0.338, 0.56, (0.15, 0.13, 0.01), 'dark')
radial_box(TERMINAL, 0.344, 0.585, (0.09, 0.03, 0.004), 'yellow')
for du in (-0.06, 0.06):
    for dy in (-0.05, 0.05):
        a = math.radians(TERMINAL)
        tx, tz = math.sin(a) * du, -math.cos(a) * du
        body.box((math.cos(a) * 0.345 + tx, 0.56 + dy, math.sin(a) * 0.345 + tz), (0.014, 0.014, 0.008), 'steel', rot=(0, -TERMINAL + 90, 0))
ta = math.radians(TERMINAL)
tx, tz = math.cos(ta) * 0.31, math.sin(ta) * 0.31
body.cylinder((tx, 0.49, tz), 0.026, 0.024, 'steel', 6)
body.cylinder((tx, 0.355, tz), 0.018, 0.27, 'black', 10, caps=False)
body.cylinder((tx, 0.235, tz), 0.026, 0.024, 'steel', 6)
gx, gz = -0.3, -0.25
ga = math.degrees(math.atan2(gz, gx))
radial_box(ga, 0.31, 0.42, (0.024, 0.024, 0.11), 'steel')
body.cylinder((gx, 0.42, -0.226), 0.03, 0.012, 'steel', 12, axis='z')
pa = math.degrees(math.atan2(0.24, -0.2))
for y in (0.4, 0.6):
    body.cylinder((-0.2, y, 0.24), 0.031, 0.02, 'steel', 12, caps=False)
    radial_box(pa, 0.276, y, (0.016, 0.016, 0.052), 'steel')
body.cylinder((-0.2, 0.335, 0.2575), 0.025, 0.085, 'orange', 10, axis='z')
body.cylinder((-0.2, 0.335, 0.27), 0.032, 0.012, 'steel', 12, axis='z')
radial_box(pa, 0.283, 0.668, (0.05, 0.03, 0.068), 'orange')
body.cylinder((0, 0.755, 0), 0.3, 0.05, 'dark', 24)
body.cylinder((0, 0.8, 0), 0.335, 0.05, 'dark', 24, cap_color='black')
for k in range(16):
    a = 2 * math.pi * k / 16
    body.box((math.cos(a) * 0.3, 0.845, math.sin(a) * 0.3), (0.13, 0.03, 0.035), 'grey', rot=(0, -math.degrees(a) + 90, 0))
for k in range(4):
    body.box((0, 0.875, 0), (0.6, 0.018, 0.025), 'dark', rot=(0, k * 45, 0))
body.cylinder((0, 0.88, 0), 0.06, 0.04, 'steel', 12)
body.box((0, 0.5, -0.29), (0.26, 0.24, 0.05), 'dark')
body.box((0, 0.54, -0.317), (0.18, 0.09, 0.008), 'screen')
body.cylinder((-0.07, 0.44, -0.318), 0.018, 0.012, 'green', 10, axis='z')
body.cylinder((0.07, 0.44, -0.318), 0.018, 0.012, 'red', 10, axis='z')
body.cylinder((-0.3, 0.42, -0.25), 0.06, 0.04, 'white', 16, axis='z', cap_color='white')
body.box((-0.3, 0.43, -0.273), (0.008, 0.045, 0.006), 'red', rot=(0, 0, -30))
body.box((0, 0.27, 0.34), (0.86, 0.3, 0.14), 'grey')
body.box((0, SOCKET_Y, PANEL_Z), (0.9, 0.34, 0.05), 'dark')
body.box((0, SOCKET_Y + 0.18, PANEL_Z + 0.005), (0.94, 0.025, 0.06), 'steel')
body.box((0, SOCKET_Y - 0.18, PANEL_Z + 0.005), (0.94, 0.025, 0.06), 'steel')
for x, col in SOCKETS:
    body.cylinder((x, SOCKET_Y, 0.4925), 0.085, 0.025, 'grey', 24, axis='z')
    body.cylinder((x, SOCKET_Y, 0.509), 0.068, 0.008, col, 24, axis='z')
    body.cylinder((x, SOCKET_Y, 0.515), 0.045, 0.004, 'black', 16, axis='z')
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        body.cylinder((x + math.cos(a) * 0.076, SOCKET_Y + math.sin(a) * 0.076, 0.507), 0.007, 0.008, 'steel', 6, axis='z')
    body.box((x, SOCKET_Y + 0.12, 0.483), (0.15, 0.022, 0.006), col)
body.box((0, SOCKET_Y, 0.483), (0.004, 0.2, 0.006), 'grey')
for x in (0.11, 0.33):
    body.cylinder((x * 0.6, 0.53, 0.28), 0.045, 0.2, 'blue', 14)
    body.cylinder((x * 0.6, 0.64, 0.24), 0.045, 0.1, 'blue', 14, axis='z', cap_color='blue')
    body.cylinder((x * 0.6, 0.429, 0.28), 0.06, 0.02, 'steel', 14)
    body.cylinder((x * 0.6, 0.445, 0.28), 0.052, 0.012, 'steel', 6)
    body.cylinder((x * 0.6, 0.54, 0.28), 0.048, 0.016, 'grey', 14, caps=False)
body.cylinder((-0.2, 0.5, 0.24), 0.025, 0.36, 'orange', 10)
body.cylinder((0, 0.004, 0), 0.14, 0.01, 'dark', 24, cap_color='steel')

fan = Mesh()
FAN_Y = 0.835
fan.cylinder((0, 0, 0), 0.05, 0.05, 'blue', 12, cap_color='white')
for k in range(6):
    a = 2 * math.pi * k / 6
    fan.box((math.cos(a) * 0.15, 0, math.sin(a) * 0.15), (0.2, 0.012, 0.085), 'cyan', rot=(25, -math.degrees(a), 0))
    fan.box((math.cos(a) * 0.062, 0, math.sin(a) * 0.062), (0.04, 0.026, 0.034), 'grey', rot=(0, -math.degrees(a), 0))

hose = Mesh()
hose.cylinder((0, -0.5, 0), 0.05, 1.0, 'rubber', 12, caps=False)

nozzle = Mesh()
nozzle.cylinder((0, 0.012, 0), 0.058, 0.044, 'steel', 14, caps=False)
nozzle.cylinder((0, 0.018, 0), 0.062, 0.014, 'grey', 14, caps=False)
nozzle.box((0.064, 0.018, 0), (0.016, 0.012, 0.02), 'steel')
nozzle.cylinder((0, -0.0275, 0), 0.085, 0.035, 'orange', 6, cap_color='orange')
nozzle.cylinder((0, -0.05, 0), 0.12, 0.01, 'dark', 20)
for k in range(8):
    a = 2 * math.pi * k / 8 + math.pi / 8
    nozzle.cylinder((math.cos(a) * 0.104, -0.041, math.sin(a) * 0.104), 0.007, 0.012, 'steel', 6)
    nozzle.cylinder((math.cos(a) * 0.115, -0.233, math.sin(a) * 0.115), 0.006, 0.012, 'steel', 6)
nozzle.cylinder((0, -0.1465, 0), 0.11, 0.183, 'rubber', 16, cap_color='black')
for y in (-0.09, -0.15, -0.21):
    nozzle.cylinder((0, y, 0), 0.114, 0.02, 'cyan', 16, caps=False)
for k in range(8):
    a = 2 * math.pi * k / 8
    nozzle.box((math.cos(a) * 0.112, -0.1465, math.sin(a) * 0.112), (0.02, 0.183, 0.02), 'black', rot=(0, -math.degrees(a), 0))
nozzle.cylinder((0, -0.245, 0), 0.12, 0.014, 'dark', 20)
nozzle.cylinder((0, -0.254, 0), 0.09, 0.008, 'grey', 16)
nozzle.cylinder((0, -0.258, 0), 0.045, 0.004, 'black', 12)
for k in range(4):
    a = 2 * math.pi * k / 4 + math.pi / 4
    nozzle.box((math.cos(a) * 0.098, -0.256, math.sin(a) * 0.098), (0.03, 0.008, 0.024), 'rubber', rot=(0, -math.degrees(a), 0))

if __name__ == '__main__':
    body.write(os.path.join(OUT, 'pump_body.bin'))
    fan.write(os.path.join(OUT, 'pump_fan.bin'))
    hose.write(os.path.join(OUT, 'pump_hose.bin'))
    nozzle.write(os.path.join(OUT, 'pump_nozzle.bin'))
    if PREVIEW:
        lines, n = ['mtllib pump.mtl', 'usemtl ink'], 1
        for mesh, off, name in ((body, (0, 0, 0), 'body'), (fan, (0, FAN_Y, 0), 'fan'), (nozzle, (0, -0.4, 0), 'nozzle')):
            l, n = mesh.write_obj(None, None, off, name, n)
            lines += l
        open(os.path.join(PREVIEW, 'pump.obj'), 'w').write('\n'.join(lines))
        tex = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'door_albedo.png'))
        open(os.path.join(PREVIEW, 'pump.mtl'), 'w').write('newmtl ink\nmap_Kd ' + tex.replace('\\', '/') + '\n')
