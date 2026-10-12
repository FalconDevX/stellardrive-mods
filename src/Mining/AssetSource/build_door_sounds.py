# Dźwięki dużych drzwi (ciężkie, wolne): w grze wysokość spada z rozmiarem drzwi (Content/BigDoorModel - BigDoorVisuals).
#   bigdoor_start.wav - odryglowanie: głuche uderzenie, trzask zamka, syk hydrauliki,
#   bigdoor_loop.wav  - jazda skrzydeł (pętla): niskie buczenie silnika, dudnienie po prowadnicy, zgrzyt zębatek,
#   bigdoor_stop.wav  - dojazd: ciężkie uderzenie z metalicznym pogłosem.
# Wynik w Assets/. Podmiana: UserData/Mining/bigdoor_start.wav, bigdoor.wav (pętla), bigdoor_stop.wav.
import os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_pump_sounds import RATE, band_noise, tone, soft_clip, write, rng


def env(t, attack, decay):
    return np.minimum(t / attack, 1.0) * np.exp(-t / decay)


def thud(t, hz, decay):
    # głuche uderzenie: opadający ton + krótki szum
    f = hz * (1 + 0.6 * np.exp(-t / 0.03))
    return np.sin(2 * np.pi * np.cumsum(f) / RATE) * env(t, 0.003, decay)


def ring(t, partials, decay):
    # metaliczny pogłos: nieharmoniczne alikwoty
    return sum(np.sin(2 * np.pi * f * t + i) * a * np.exp(-t / (decay * (1 - 0.12 * i))) for i, (f, a) in enumerate(partials))


def start():
    n = int(RATE * 1.4)
    t = np.arange(n) / RATE
    latch = band_noise(n, 1500, 6000) * env(t, 0.001, 0.015) * 0.6
    bang = thud(t, 55.0, 0.25) * 0.9 + band_noise(n, 60, 400) * env(t, 0.002, 0.08) * 0.5
    clank = ring(t, [(310, 0.25), (487, 0.18), (733, 0.12)], 0.25)
    hiss_t = np.maximum(t - 0.12, 0)
    hiss = band_noise(n, 2500, 8000) * np.where(t > 0.12, env(hiss_t, 0.05, 0.4), 0) * 0.35
    x = soft_clip((bang + clank + latch) * 1.2) * 0.8 + hiss
    write('bigdoor_start.wav', x, 0.8)


def loop():
    seconds = 3.0
    n = int(RATE * seconds)
    t = np.arange(n) / RATE
    motor = tone(t, 40.0, 10, 0.9) * 0.5 * (0.9 + 0.1 * np.sin(2 * np.pi * 1.0 * t))
    rumble = band_noise(n, 25, 180) * (0.75 + 0.25 * np.sin(2 * np.pi * (2.0 / 3.0) * t + 1)) * 0.6
    grind = band_noise(n, 300, 1600) * (0.5 + 0.5 * np.sin(2 * np.pi * 7.0 * t) ** 8) * 0.18
    clacks = np.zeros(n)
    click = band_noise(int(RATE * 0.02), 600, 3000) * np.exp(-np.linspace(0, 7, int(RATE * 0.02)))
    for k in range(int(seconds * 6)):
        start_i = int(k * RATE / 6.0)
        idx = (start_i + np.arange(len(click))) % n
        clacks[idx] += click * (0.6 + 0.4 * rng.random())
    x = soft_clip(motor * 1.5) * 0.5 + rumble + grind + clacks * 0.25
    write('bigdoor_loop.wav', x, 0.75)


def stop():
    n = int(RATE * 2.2)
    t = np.arange(n) / RATE
    slam = thud(t, 42.0, 0.45) * 1.0 + band_noise(n, 40, 300) * env(t, 0.002, 0.15) * 0.7
    metal = ring(t, [(196, 0.3), (311, 0.22), (452, 0.16), (689, 0.1), (1013, 0.06)], 0.6)
    rattle = band_noise(n, 800, 4000) * env(t, 0.005, 0.12) * 0.25
    x = soft_clip((slam + metal + rattle) * 1.3) * 0.85
    write('bigdoor_stop.wav', x, 0.85)


if __name__ == '__main__':
    start()
    loop()
    stop()
