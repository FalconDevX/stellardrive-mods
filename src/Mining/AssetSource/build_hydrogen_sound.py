import os, sys, subprocess
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_pump_sounds import RATE, band_noise, tone, soft_clip, write, rng

HERE = os.path.dirname(os.path.abspath(__file__))


def recorded(seconds):
    path = os.path.join(HERE, 'hydrogen_elevenlabs.mp3')
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', path, '-ac', '1', '-ar', str(RATE), '-f', 's16le', '-'], capture_output=True, check=True).stdout
    x = np.frombuffer(raw, '<i2').astype(np.float64) / 32768
    n = int(RATE * seconds)
    fade = int(RATE * 0.05)
    ramp = np.linspace(0, 1, fade)
    looped = x[fade:].copy()
    looped[-fade:] = x[-fade:] * (1 - ramp) + x[:fade] * ramp
    x = np.interp(np.linspace(0, len(looped), n, endpoint=False), np.arange(len(looped)), looped)
    return x / (np.abs(x).max() + 1e-9)


def synth(n, seconds):
    t = np.arange(n) / RATE
    swell = 0.82 + 0.1 * np.sin(2 * np.pi * (1 / seconds) * 3 * t) + 0.08 * np.sin(2 * np.pi * (1 / seconds) * 7 * t + 1.3)
    flutter = 0.8 + 0.2 * np.sin(2 * np.pi * 17.0 * t) * np.sin(2 * np.pi * (1 / seconds) * 4 * t + 0.4)
    roar = band_noise(n, 28, 180) * swell * flutter
    body = band_noise(n, 140, 900) * (0.75 + 0.25 * np.sin(2 * np.pi * (1 / seconds) * 13 * t + 2.0))
    hiss = band_noise(n, 2600, 9500) * (0.9 + 0.1 * np.sin(2 * np.pi * (1 / seconds) * 18 * t))
    jet = band_noise(n, 900, 2600)
    crackle = np.zeros(n)
    for _ in range(int(seconds * 140)):
        m = int(RATE * rng.uniform(0.002, 0.008))
        burst = band_noise(m, 500, 4000) * np.exp(-np.linspace(0, 6, m)) * rng.uniform(0.3, 1.0)
        idx = (int(rng.random() * n) + np.arange(m)) % n
        crackle[idx] += burst
    whine = (np.sin(2 * np.pi * 1150.0 * t) * 0.6 + np.sin(2 * np.pi * 2300.0 * t) * 0.25) * (0.85 + 0.15 * np.sin(2 * np.pi * (1 / seconds) * 3 * t))
    thrum = tone(t, 38.0, 6, 1.1) * 0.5
    low = soft_clip(roar * 1.8 + thrum * 0.6)
    return low, body, jet, hiss, crackle, whine


def hydrogen():
    seconds = 6.0
    n = int(RATE * seconds)
    rec = recorded(seconds)
    low, body, jet, hiss, crackle, whine = synth(n, seconds)
    x = rec * 1.0 + low * 0.3 + body * 0.15 + jet * 0.12 + hiss * 0.1 + crackle * 0.2 + whine * 0.02
    write('hydrogen_loop.wav', soft_clip(x * 1.1), 0.8)


if __name__ == '__main__':
    hydrogen()
