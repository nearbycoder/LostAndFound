"""Small numpy synthesis toolkit for Lost & Found: oscillators, envelopes, filters, convolution
reverb, Karplus-Strong strings, FM, formant voices and helpers to write 16-bit WAVs.
Everything is deterministic (seeded) so rebuilding the audio gives identical files."""
import math
import zlib
import os
import wave

import numpy as np
from scipy import signal

SR = 44100
rng = np.random.default_rng(1962)


def seed(n):
    global rng
    rng = np.random.default_rng(n)


def t(dur):
    return np.arange(int(dur * SR)) / SR


def silence(dur):
    return np.zeros(int(dur * SR))


# ----------------------------------------------------------------------------- oscillators

def sine(freq, dur, phase=0.0):
    if np.isscalar(freq):
        return np.sin(2 * np.pi * freq * t(dur) + phase)
    ph = 2 * np.pi * np.cumsum(freq) / SR
    return np.sin(ph + phase)


def saw(freq, dur, harmonics=40):
    """Band-limited sawtooth by additive synthesis (freq scalar)."""
    tt = t(dur)
    out = np.zeros_like(tt)
    for k in range(1, harmonics + 1):
        if k * freq > SR / 2:
            break
        out += np.sin(2 * np.pi * k * freq * tt) / k
    return out * (2 / np.pi)


def square(freq, dur, harmonics=30):
    tt = t(dur)
    out = np.zeros_like(tt)
    for k in range(1, harmonics * 2, 2):
        if k * freq > SR / 2:
            break
        out += np.sin(2 * np.pi * k * freq * tt) / k
    return out * (4 / np.pi)


def noise(dur, color="white"):
    n = rng.standard_normal(int(dur * SR))
    if color == "pink":
        b, a = [0.049922035, -0.095993537, 0.050612699, -0.004408786], [1, -2.494956002, 2.017265875, -0.522189400]
        n = signal.lfilter(b, a, n) * 3.5
    elif color == "brown":
        n = np.cumsum(n)
        n = n - signal.savgol_filter(n, 2001 if len(n) > 2001 else (len(n) // 2) * 2 - 1, 2) if len(n) > 50 else n
        n /= (np.max(np.abs(n)) + 1e-9)
    return n


# ----------------------------------------------------------------------------- envelopes

def env_exp(dur, decay, attack=0.002):
    tt = t(dur)
    e = np.exp(-tt / max(decay, 1e-4))
    if attack > 0:
        a = np.clip(tt / attack, 0, 1)
        e *= a
    return e


def env_adsr(dur, a=0.01, d=0.1, s=0.7, r=0.2):
    n = int(dur * SR)
    out = np.zeros(n)
    na, nd, nr = int(a * SR), int(d * SR), int(r * SR)
    ns = max(0, n - na - nd - nr)
    seg = [np.linspace(0, 1, na, endpoint=False), np.linspace(1, s, nd, endpoint=False), np.full(ns, s), np.linspace(s, 0, nr)]
    e = np.concatenate(seg)
    out[:min(n, len(e))] = e[:n]
    return out


def env_points(dur, pts):
    """Piecewise-linear envelope from [(time, level), ...]."""
    tt = t(dur)
    xs, ys = zip(*pts)
    return np.interp(tt, xs, ys)


def fade(x, fin=0.005, fout=0.02):
    x = x.copy()
    ni, no = min(int(fin * SR), len(x) // 2), min(int(fout * SR), len(x) // 2)
    if ni > 0:
        x[:ni] *= np.linspace(0, 1, ni)
    if no > 0:
        x[-no:] *= np.linspace(1, 0, no)
    return x


# ----------------------------------------------------------------------------- filters

def lowpass(x, cutoff, order=2):
    b, a = signal.butter(order, min(cutoff, SR / 2 - 100) / (SR / 2), "low")
    return signal.lfilter(b, a, x)


def highpass(x, cutoff, order=2):
    b, a = signal.butter(order, max(cutoff, 10) / (SR / 2), "high")
    return signal.lfilter(b, a, x)


def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [max(lo, 10) / (SR / 2), min(hi, SR / 2 - 100) / (SR / 2)], "band")
    return signal.lfilter(b, a, x)


def resonator(x, freq, q=20.0):
    """Two-pole resonant bandpass (peaky): good for formants and body resonance."""
    w0 = 2 * np.pi * freq / SR
    alpha = np.sin(w0) / (2 * q)
    b = [alpha, 0, -alpha]
    a = [1 + alpha, -2 * np.cos(w0), 1 - alpha]
    return signal.lfilter(b, a, x)


def sweep_bandpass(x, f0, f1, q=3.0, blocks=64):
    """Bandpass whose centre moves from f0 to f1 over the sound (block-wise)."""
    out = np.zeros_like(x)
    n = len(x)
    bs = max(1, n // blocks)
    zi = None
    for i in range(0, n, bs):
        k = i / max(1, n - 1)
        f = f0 * (f1 / f0) ** k
        lo, hi = f / (1 + 1 / q), f * (1 + 1 / q)
        b, a = signal.butter(2, [max(lo, 20) / (SR / 2), min(hi, SR / 2 - 200) / (SR / 2)], "band")
        if zi is None or len(zi) != max(len(a), len(b)) - 1:
            zi = signal.lfilter_zi(b, a) * 0
        seg, zi = signal.lfilter(b, a, x[i:i + bs], zi=zi)
        out[i:i + bs] = seg
    return out


# ----------------------------------------------------------------------------- space

_ir_cache = {}


def impulse(dur=2.0, damp=0.5, bright=6000, early=True, key="hall"):
    """Synthetic reverb impulse: early reflections + exponentially decaying filtered noise."""
    ck = (dur, damp, bright, early, key)
    if ck in _ir_cache:
        return _ir_cache[ck]
    r = np.random.default_rng(zlib.crc32(key.encode()) % 1000)
    n = int(dur * SR)
    tail = r.standard_normal(n) * np.exp(-np.arange(n) / SR / (dur * damp / 3))
    tail = lowpass(tail, bright)
    ir = np.zeros(n)
    ir += tail * 0.3
    if early:
        for k in range(10):
            d = int(r.uniform(0.007, 0.08) * SR)
            ir[d] += r.uniform(0.2, 0.6) * (1 if r.random() < 0.5 else -1)
    ir[0] = 1.0
    _ir_cache[ck] = ir
    return ir


def reverb(x, wet=0.25, dur=1.8, damp=0.5, bright=6000, key="hall"):
    """Convolution reverb; keeps the whole tail and lets it die out smoothly (no cut)."""
    ir = impulse(dur, damp, bright, key=key)
    y = signal.fftconvolve(x, ir)
    dry = np.zeros(len(y))
    dry[: len(x)] = x
    wy = y / (np.max(np.abs(y)) + 1e-9) * (np.max(np.abs(x)) + 1e-9)
    out = dry * (1 - wet * 0.5) + wy * wet
    # trim trailing near-silence, then a gentle fade
    env = np.abs(out)
    thresh = np.max(env) * 0.0015
    idx = np.nonzero(env > thresh)[0]
    end = min(len(out), (idx[-1] if len(idx) else len(out)) + int(0.05 * SR))
    out = out[:end]
    nf = min(len(out) // 3, int(0.25 * SR))
    if nf > 0:
        out[-nf:] *= np.linspace(1, 0, nf) ** 2
    return out


def delay(x, time, feedback=0.35, mix=0.3, repeats=5):
    d = int(time * SR)
    out = np.zeros(len(x) + d * repeats)
    out[: len(x)] += x
    g = mix
    for k in range(1, repeats + 1):
        out[k * d: k * d + len(x)] += x * g
        g *= feedback
    return out


def stereo(x, pan=0.0, width=0.0):
    """Mono -> stereo with constant-power pan and optional Haas widening."""
    l = x * math.cos((pan + 1) * math.pi / 4)
    r = x * math.sin((pan + 1) * math.pi / 4)
    if width > 0:
        d = int(width * 0.012 * SR)
        r = np.concatenate([np.zeros(d), r])[: len(l)]
    return np.stack([l, r], axis=-1)


# ----------------------------------------------------------------------------- building blocks

def mix(*parts, length=None):
    """Sum signals (mono or stereo arrays, or (offset_seconds, signal) tuples)."""
    items = []
    for p in parts:
        if isinstance(p, tuple):
            items.append(p)
        else:
            items.append((0.0, p))
    stereo_out = any(np.ndim(s) == 2 for _, s in items)
    n = length if length else max(int(o * SR) + len(s) for o, s in items)
    out = np.zeros((n, 2)) if stereo_out else np.zeros(n)
    for o, s in items:
        i = int(o * SR)
        if stereo_out and np.ndim(s) == 1:
            s = np.stack([s, s], axis=-1)
        end = min(n, i + len(s))
        if end > i:
            out[i:end] += s[: end - i]
    return out


def normalize(x, peak=0.9):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def pitch_drop(f0, f1, dur, curve=8.0):
    tt = t(dur)
    return f1 + (f0 - f1) * np.exp(-tt * curve)


def click(dur=0.004, bright=6000, amp=1.0):
    n = noise(dur)
    return highpass(n, bright * 0.3) * env_exp(dur, dur / 3, 0.0002) * amp


def knock(freq=300, dur=0.12, decay=0.03, partials=((1, 1.0), (2.3, 0.5), (3.9, 0.25)), noise_amt=0.5):
    out = np.zeros(int(dur * SR))
    for ratio, amp in partials:
        out += sine(freq * ratio, dur) * env_exp(dur, decay / ratio ** 0.5) * amp
    out += bandpass(noise(dur), freq, freq * 6) * env_exp(dur, 0.006) * noise_amt
    return out


def metal(freqs, dur, decays, amps=None, attack=0.0005):
    out = np.zeros(int(dur * SR))
    amps = amps or [1.0] * len(freqs)
    for f, d, a in zip(freqs, decays, amps):
        out += sine(f * (1 + rng.uniform(-0.002, 0.002)), dur) * env_exp(dur, d, attack) * a
    return out


def karplus(freq, dur, damping=0.996, bright=0.5):
    """Plucked string (Karplus-Strong) as an IIR comb: y[n] = x[n] + d/2 (y[n-p] + y[n-p-1])."""
    n = int(dur * SR)
    p = max(2, int(round(SR / freq)))
    exc = np.zeros(n)
    burst = rng.uniform(-1, 1, p)
    if p > 20:
        burst = lowpass(burst, 1000 + 8000 * bright)
    exc[:p] = burst
    a = np.zeros(p + 2)
    a[0] = 1.0
    a[p] -= 0.5 * damping
    a[p + 1] -= 0.5 * damping
    return signal.lfilter([1.0], a, exc)


def fm(freq, dur, ratio=1.0, index=2.0, index_decay=0.3):
    tt = t(dur)
    idx = index * np.exp(-tt / index_decay)
    mod = np.sin(2 * np.pi * freq * ratio * tt) * idx
    return np.sin(2 * np.pi * freq * tt + mod)


VOWELS = {
    "a": (800, 1200, 2500), "e": (480, 1900, 2600), "i": (300, 2300, 3000),
    "o": (500, 850, 2400), "u": (330, 800, 2300), "ae": (650, 1700, 2500), "uh": (600, 1100, 2400),
}


def formant_voice(f0, dur, vowel="a", formant_shift=1.0, breath=0.05, vibrato=0.0, contour=0.0, jitter=0.01):
    """Glottal pulse train through three formant resonators."""
    tt = t(dur)
    f = f0 * (1 + contour * (tt / max(dur, 1e-3) - 0.4)) * (1 + vibrato * np.sin(2 * np.pi * 5.5 * tt))
    f = f * (1 + jitter * rng.standard_normal(len(tt)).cumsum() / max(1, len(tt)) * 50)
    ph = np.cumsum(f) / SR
    pulse = (ph % 1.0)
    src = (1 - pulse) ** 3 - 0.25  # skewed glottal-ish wave
    src = src + breath * rng.standard_normal(len(tt))
    f1, f2, f3 = (x * formant_shift for x in VOWELS.get(vowel, VOWELS["a"]))
    out = resonator(src, f1, 8) * 1.0 + resonator(src, f2, 10) * 0.6 + resonator(src, f3, 12) * 0.3
    return out


# ----------------------------------------------------------------------------- output

def to_int16(x):
    x = np.clip(x, -1.0, 1.0)
    return (x * 32767).astype(np.int16)


def write(path, x, peak=None):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if peak is not None:
        x = normalize(x, peak)
    x = np.nan_to_num(x)
    ch = 1 if np.ndim(x) == 1 else 2
    data = to_int16(x)
    with wave.open(path, "wb") as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def loopable(x, xfade=1.0):
    """Make a seamless loop by crossfading the tail into the head."""
    n = int(xfade * SR)
    if np.ndim(x) == 1:
        head, tail = x[:n], x[-n:]
        k = np.linspace(0, 1, n)
        body = x[n:-n] if len(x) > 2 * n else x[n:]
        start = tail * (1 - k) + head * k
        return np.concatenate([start, body])
    k = np.linspace(0, 1, n)[:, None]
    head, tail = x[:n], x[-n:]
    return np.concatenate([tail * (1 - k) + head * k, x[n:-n]])
