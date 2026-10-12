# Pompa wody 1x1 (wzorowana na modelu "AquaCore Pump Station" od użytkownika) w stylu rysunku z gry.
# Układ części: środek podstawy w (0, 0, 0), kostka 1x1x1, przód = -Z.
#   lewy bok (-X): wejście wody (rura), prawy bok (+X): dwa wyjścia wody, tył (+Z): skrzynka sterowania,
#   gniazda prądu i sygnału. Na górze kratka z wirnikiem (osobna siatka - kręci się przy pompowaniu).
#   Wąż wysuwa się w dół ze środka podstawy (siatka węża długości 1 m skalowana w grze + głowica ssąca).
# Wynik: Assets/pump_body.bin, pump_fan.bin, pump_hose.bin, pump_nozzle.bin (tekstura door_albedo.png).
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from inkpoly import Mesh

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets')
PREVIEW = sys.argv[2] if len(sys.argv) > 2 else None

SOCKET_Y = 0.3
body = Mesh()
# --- podstawa: ciemna płyta z żółtymi pasami ostrzegawczymi i pomarańczowymi narożnikami ---
body.box((0, 0.05, 0), (0.96, 0.1, 0.96), 'dark')
body.box((0, 0.115, 0), (0.86, 0.03, 0.86), 'grey')
for k in range(6):
    t = -0.4 + k * 0.16
    for side in (-1, 1):
        body.box((t, 0.05, side * 0.485), (0.08, 0.07, 0.012), 'yellow')
        body.box((side * 0.485, 0.05, t), (0.012, 0.07, 0.08), 'yellow')
for sx in (-1, 1):
    for sz in (-1, 1):
        body.box((sx * 0.43, 0.135, sz * 0.43), (0.08, 0.04, 0.08), 'orange')
        body.cylinder((sx * 0.43, 0.165, sz * 0.43), 0.018, 0.03, 'steel', 8)
# --- obudowa: biały walec z ciemnymi opaskami i pomarańczowymi obejmami ---
body.cylinder((0, 0.47, 0), 0.3, 0.62, 'light', 20, cap_color='grey')
body.cylinder((0, 0.19, 0), 0.315, 0.07, 'dark', 20)
body.cylinder((0, 0.74, 0), 0.315, 0.06, 'dark', 20)
for k in range(4):
    a = math.radians(45 + k * 90)
    x, z = math.cos(a) * 0.305, math.sin(a) * 0.305
    yaw = -math.degrees(a) + 90
    body.box((x, 0.47, z), (0.07, 0.5, 0.05), 'orange', rot=(0, yaw, 0))
    for y in (0.3, 0.64):
        body.box((x * 1.06, y, z * 1.06), (0.035, 0.035, 0.02), 'steel', rot=(0, yaw, 0))
# tabliczka z kroplą wody (przód) i manometr
body.box((0, 0.5, -0.3), (0.2, 0.22, 0.03), 'blue')
body.cylinder((0, 0.475, -0.318), 0.045, 0.012, 'white', 16, axis='z')          # kropla: kółko + ostry czubek
body.box((0, 0.51, -0.318), (0.064, 0.064, 0.012), 'white', rot=(0, 0, 45))
body.cylinder((-0.17, 0.6, -0.26), 0.055, 0.04, 'white', 14, axis='z', cap_color='white')
body.box((-0.17, 0.61, -0.283), (0.008, 0.04, 0.006), 'red', rot=(0, 0, -30))
# --- góra: kołnierz, kratka nad wirnikiem ---
body.cylinder((0, 0.8, 0), 0.335, 0.06, 'dark', 20, cap_color='black')
for k in range(16):
    a = 2 * math.pi * k / 16
    body.box((math.cos(a) * 0.3, 0.845, math.sin(a) * 0.3), (0.13, 0.03, 0.035), 'grey', rot=(0, -math.degrees(a) + 90, 0))
for k in range(4):
    body.box((0, 0.875, 0), (0.6, 0.018, 0.025), 'dark', rot=(0, k * 45, 0))
body.cylinder((0, 0.88, 0), 0.06, 0.04, 'steel', 12)
# --- wejście wody (lewy bok) ---
body.cylinder((-0.38, SOCKET_Y, 0), 0.07, 0.2, 'dark', 12, axis='x')
body.cylinder((-0.44, SOCKET_Y, 0), 0.08, 0.035, 'blue', 14, axis='x')
body.cylinder((-0.48, SOCKET_Y, 0), 0.11, 0.04, 'orange', 16, axis='x', cap_color='dark')
body.box((-0.47, SOCKET_Y + 0.15, 0), (0.04, 0.07, 0.16), 'cyan')          # oznaczenie: wejście
# --- dwa wyjścia (prawy bok): kolektor i dwa kołnierze ---
body.box((0.33, SOCKET_Y, 0), (0.12, 0.16, 0.56), 'grey')
for z in (-0.2, 0.2):
    body.cylinder((0.42, SOCKET_Y, z), 0.065, 0.12, 'dark', 12, axis='x')
    body.cylinder((0.445, SOCKET_Y, z), 0.075, 0.03, 'cyan', 14, axis='x')
    body.cylinder((0.48, SOCKET_Y, z), 0.105, 0.04, 'orange', 16, axis='x', cap_color='dark')
# --- tył: skrzynka sterowania z ekranem i lampkami, gniazda prądu i sygnału ---
body.box((0, 0.5, 0.36), (0.4, 0.34, 0.14), 'dark')
body.box((0, 0.56, 0.432), (0.24, 0.12, 0.01), 'screen')
body.box((-0.12, 0.42, 0.432), (0.05, 0.05, 0.012), 'green')
body.box((0.12, 0.42, 0.432), (0.05, 0.05, 0.012), 'red')
body.box((0, 0.42, 0.432), (0.1, 0.03, 0.012), 'yellow')
body.cylinder((0.19, 0.5, 0.3), 0.025, 0.36, 'black', 8)                    # kabel od skrzynki w dół
body.box((0.15, SOCKET_Y - 0.09, 0.47), (0.14, 0.04, 0.05), 'yellow')     # podpis pod gniazdem prądu
body.box((-0.15, SOCKET_Y - 0.09, 0.47), (0.14, 0.04, 0.05), 'white')     # podpis pod gniazdem sygnału
# --- otwór na wąż w podstawie (od spodu) ---
body.cylinder((0, 0.004, 0), 0.14, 0.01, 'black', 16)

fan = Mesh()  # środek wirnika w (0, 0, 0); w grze ustawiony na wysokości FAN_Y
FAN_Y = 0.835
fan.cylinder((0, 0, 0), 0.05, 0.05, 'blue', 12, cap_color='white')
for k in range(6):
    a = 2 * math.pi * k / 6
    fan.box((math.cos(a) * 0.15, 0, math.sin(a) * 0.15), (0.2, 0.012, 0.085), 'cyan', rot=(25, -math.degrees(a), 0))

hose = Mesh()  # wąż: od (0, 0, 0) w dół na 1 m (skalowany w grze w osi Y)
hose.cylinder((0, -0.5, 0), 0.05, 1.0, 'rubber', 12, caps=False)

nozzle = Mesh()  # głowica ssąca: góra w (0, 0, 0), wisi w dół na 0.26 m
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
