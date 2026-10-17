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
        body.cylinder((sx * 0.42, 0.15, sz * 0.42), 0.02, 0.03, 'steel', 8)
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
body.cylinder((-0.2, 0.5, 0.24), 0.025, 0.36, 'orange', 10)
body.cylinder((0, 0.004, 0), 0.14, 0.01, 'black', 16)

fan = Mesh()
FAN_Y = 0.835
fan.cylinder((0, 0, 0), 0.05, 0.05, 'blue', 12, cap_color='white')
for k in range(6):
    a = 2 * math.pi * k / 6
    fan.box((math.cos(a) * 0.15, 0, math.sin(a) * 0.15), (0.2, 0.012, 0.085), 'cyan', rot=(25, -math.degrees(a), 0))

hose = Mesh()
hose.cylinder((0, -0.5, 0), 0.05, 1.0, 'rubber', 12, caps=False)

nozzle = Mesh()
nozzle.cylinder((0, -0.025, 0), 0.075, 0.05, 'orange', 14)
nozzle.cylinder((0, -0.15, 0), 0.11, 0.2, 'rubber', 16, cap_color='black')
for y in (-0.09, -0.15, -0.21):
    nozzle.cylinder((0, y, 0), 0.114, 0.02, 'cyan', 16, caps=False)
for k in range(8):
    a = 2 * math.pi * k / 8
    nozzle.box((math.cos(a) * 0.112, -0.15, math.sin(a) * 0.112), (0.02, 0.19, 0.02), 'black', rot=(0, -math.degrees(a), 0))
nozzle.cylinder((0, -0.255, 0), 0.08, 0.01, 'dark', 14)

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
