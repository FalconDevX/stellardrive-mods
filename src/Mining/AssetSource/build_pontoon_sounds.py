import os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_pump_sounds import RATE, band_noise, soft_clip, write, rng

def creaks(n, count, lo, hi, length):
    out = np.zeros(n)
    m = int(RATE * length)
    env = np.sin(np.linspace(0, np.pi, m)) ** 2
    for _ in range(count):
        start = int(rng.random() * n)
        idx = (start + np.arange(m)) % n
        out[idx] += band_noise(m, lo, hi) * env * rng.uniform(0.4, 1.0)
    return out

def inflate():
    seconds = 3.0
    n = int(RATE * seconds)
    t = np.arange(n) / RATE
    flutter = 0.8 + 0.2 * np.sin(2 * np.pi * 7.0 * t) * np.sin(2 * np.pi * 1.0 * t)
    hiss = band_noise(n, 700, 5000) * flutter * 0.55
    body = band_noise(n, 120, 600) * 0.3
    rubber = creaks(n, 5, 150, 700, 0.18) * 0.45
    x = soft_clip((hiss + body + rubber) * 1.3)
    write('pontoon_inflate_loop.wav', x, 0.7)

def deflate():
    seconds = 3.0
    n = int(RATE * seconds)
    t = np.arange(n) / RATE
    sputter = 0.6 + 0.4 * (0.5 + 0.5 * np.sin(2 * np.pi * 19.0 * t)) ** 3
    hiss = band_noise(n, 1500, 9000) * sputter * 0.6
    flap = band_noise(n, 90, 400) * (0.5 + 0.5 * np.sin(2 * np.pi * 19.0 * t)) ** 6 * 0.5
    rubber = creaks(n, 4, 120, 500, 0.25) * 0.35
    x = soft_clip((hiss + flap + rubber) * 1.3)
    write('pontoon_deflate_loop.wav', x, 0.7)

if __name__ == '__main__':
    inflate()
    deflate()
