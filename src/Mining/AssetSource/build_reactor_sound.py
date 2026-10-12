import os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_pump_sounds import RATE, band_noise, tone, soft_clip, write, rng

def reactor():
    seconds = 4.0
    n = int(RATE * seconds)
    t = np.arange(n) / RATE
    pulse = 0.8 + 0.2 * np.sin(2 * np.pi * 0.5 * t)
    hum = (tone(t, 50.0, 9, 0.9) * 0.55 + np.sin(2 * np.pi * 100.0 * t) * 0.25) * pulse
    whine = (np.sin(2 * np.pi * 880.0 * t) * 0.09 + np.sin(2 * np.pi * 1320.25 * t) * 0.05) * (0.85 + 0.15 * np.sin(2 * np.pi * 1.5 * t))
    blades = 0.65 + 0.35 * (0.5 + 0.5 * np.sin(2 * np.pi * 24.0 * t)) ** 2
    rush = band_noise(n, 400, 2500) * blades * 0.22
    flow = band_noise(n, 70, 400) * 0.3
    ticks = np.zeros(n)
    click = band_noise(int(RATE * 0.004), 2500, 9000) * np.exp(-np.linspace(0, 5, int(RATE * 0.004)))
    for _ in range(int(seconds * 9)):
        start = int(rng.random() * n)
        idx = (start + np.arange(len(click))) % n
        ticks[idx] += click * rng.uniform(0.4, 1.0)
    x = soft_clip(hum * 1.4) * 0.6 + whine + rush + flow + ticks * 0.3
    write('reactor_loop.wav', x, 0.75)

if __name__ == '__main__':
    reactor()
