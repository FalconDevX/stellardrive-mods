import os, wave
import numpy as np

RATE = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets')
rng = np.random.default_rng(7)

def band_noise(n, lo, hi):
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / RATE)
    shape = np.exp(-0.5 * ((np.log(np.maximum(f, 1)) - np.log(np.sqrt(lo * hi))) / (np.log(hi / lo) / 2)) ** 2)
    out = np.fft.irfft(spec * shape, n)
    return out / (np.abs(out).max() + 1e-9)

def tone(t, hz, harmonics, decay=1.3):
    s = np.zeros_like(t)
    for h in range(1, harmonics + 1):
        s += np.sin(2 * np.pi * hz * h * t + h * 0.7) / h ** decay
    return s

def soft_clip(x):
    return np.tanh(x)

def write(name, x, peak=0.8):
    x = x / (np.abs(x).max() + 1e-9) * peak
    data = (x * 32767).astype('<i2')
    with wave.open(os.path.join(OUT, name), 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())
    print(name, f'{len(x) / RATE:.1f} s')

def pump():
    seconds = 3.0
    n = int(RATE * seconds)
    t = np.arange(n) / RATE
    hum = tone(t, 55.0, 8, 1.0) * 0.5 * (0.85 + 0.15 * np.sin(2 * np.pi * 2.0 * t))
    blades = 0.6 + 0.4 * (0.5 + 0.5 * np.sin(2 * np.pi * 12.0 * t)) ** 2
    whoosh = band_noise(n, 250, 1400) * blades * 0.45
    flow = band_noise(n, 80, 500) * 0.35
    bubbles = np.zeros(n)
    for _ in range(int(seconds * 28)):
        start = int(rng.random() * n)
        dur = rng.uniform(0.02, 0.06)
        m = int(RATE * dur)
        tt = np.arange(m) / RATE
        f0 = rng.uniform(380, 1100)
        b = np.sin(2 * np.pi * f0 * (1 + 2.5 * tt) * tt) * np.exp(-tt / (dur / 4)) * rng.uniform(0.2, 0.7)
        idx = (start + np.arange(m)) % n
        bubbles[idx] += b
    x = soft_clip(hum * 1.3) * 0.55 + whoosh + flow + bubbles * 0.5
    write('pump_loop.wav', x, 0.75)

if __name__ == '__main__':
    pump()
