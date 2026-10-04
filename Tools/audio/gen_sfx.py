"""All sound effects, ambience beds and voice syllable banks for Lost & Found, synthesized.

    .venv/bin/python Tools/audio/gen_sfx.py [name ...]

Writes 44.1 kHz 16-bit WAVs to Assets/Game/Resources/Audio. Names with _1.._n are random variants
picked by AudioDirector.
"""
import math
import zlib
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import synth as S  # noqa: E402
from synth import (SR, bandpass, click, env_exp, env_points, fade, formant_voice, highpass, knock, lowpass,  # noqa: E402
                   metal, mix, noise, normalize, reverb, resonator, sine, sweep_bandpass, t, write, stereo, loopable)

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Audio")
SFX = {}


def sfx(name, variants=1):
    def deco(fn):
        SFX[name] = (fn, variants)
        return fn
    return deco


def save(name, x, peak=0.85, room=0.0, room_dur=0.6):
    if room > 0:
        x = reverb(x, wet=room, dur=room_dur, damp=0.5, bright=7000, key="booth")
    write(os.path.join(OUT, name + ".wav"), fade(normalize(x, peak), 0.001, 0.01))


def friction(dur, lo, hi, rough=40, grain=0.5):
    """Sliding friction: band-limited noise with stick-slip roughness."""
    n = bandpass(noise(dur, "pink"), lo, hi)
    tt = t(dur)
    jitter = 1 + grain * np.sin(2 * np.pi * rough * tt + S.rng.uniform(0, 6)) * S.rng.uniform(0.5, 1.0, len(tt)) ** 0.2
    return n * jitter


# ----------------------------------------------------------------------------- drawers & furniture

@sfx("drawer_open", 3)
def drawer_open(v):
    d = 0.5
    slide = friction(d, 180, 1400, rough=35 + v * 7, grain=0.6) * env_points(d, [(0, 0), (0.04, 1), (0.32, 0.8), (0.4, 0.0), (d, 0)])
    stop = knock(150 + v * 12, 0.25, 0.05, noise_amt=0.3) * 0.7
    rattle = mix(*[(0.36 + S.rng.uniform(0, 0.08), knock(S.rng.uniform(900, 1600), 0.06, 0.01, noise_amt=0.2) * 0.12) for _ in range(3)])
    return mix(slide * 0.6, (0.36, stop), rattle)


@sfx("drawer_close", 3)
def drawer_close(v):
    d = 0.4
    slide = friction(d, 200, 1500, rough=45, grain=0.6) * env_points(d, [(0, 0), (0.03, 1), (0.22, 0.9), (0.26, 0), (d, 0)])
    thunk = knock(130 + v * 10, 0.3, 0.06, partials=((1, 1.0), (2.1, 0.6), (3.7, 0.3)), noise_amt=0.6)
    return mix(slide * 0.5, (0.24, thunk))


@sfx("drawer_thunk", 2)
def drawer_thunk(v):
    return knock(120 + v * 15, 0.25, 0.05, noise_amt=0.5)


@sfx("chair_swivel", 2)
def chair(v):
    d = 0.5
    creak = sweep_bandpass(friction(d, 300, 2000, rough=60, grain=0.9), 500 + v * 80, 900, q=6) * env_points(d, [(0, 0), (0.05, 1), (0.3, 0.6), (d, 0)])
    roll = lowpass(noise(d, "brown"), 300) * env_points(d, [(0, 0), (0.1, 0.8), (d, 0)])
    return creak * 0.5 + roll * 0.4


@sfx("iron_open")
def iron_open(_):
    d = 0.9
    scrape = friction(d, 90, 700, rough=25, grain=0.8) * env_points(d, [(0, 0), (0.08, 1), (0.6, 0.7), (0.7, 0), (d, 0)])
    squeal = sine(S.pitch_drop(980, 760, d, 2), d) * env_points(d, [(0, 0), (0.15, 0.25), (0.55, 0.12), (0.7, 0), (d, 0)])
    clank = metal([180, 433, 811], 0.6, [0.2, 0.12, 0.08], [1, 0.6, 0.3])
    return mix(scrape * 0.7, squeal * 0.2, (0.66, clank * 0.5))


@sfx("iron_close")
def iron_close(_):
    d = 0.5
    scrape = friction(d, 90, 700, rough=30, grain=0.8) * env_points(d, [(0, 0), (0.05, 1), (0.35, 0.8), (0.4, 0), (d, 0)])
    return mix(scrape * 0.6, (0.38, metal([140, 361, 702], 0.5, [0.25, 0.1, 0.06]) * 0.6))


@sfx("iron_slam")
def iron_slam(_):
    d = 1.6
    body = metal([96, 231, 389, 562, 870, 1240], d, [0.6, 0.45, 0.3, 0.2, 0.12, 0.08], [1, 0.8, 0.6, 0.5, 0.35, 0.25])
    thud = sine(S.pitch_drop(90, 45, d, 10), d) * env_exp(d, 0.18)
    crack = bandpass(noise(d), 800, 5000) * env_exp(d, 0.02)
    return reverb(body * 0.6 + thud * 1.0 + crack * 0.6, wet=0.35, dur=1.6, key="booth")


@sfx("iron_rattle")
def iron_rattle(_):
    parts = [(i * 0.07 + S.rng.uniform(0, 0.02), metal([400 + S.rng.uniform(-40, 40), 1020, 1700], 0.2, [0.06, 0.04, 0.03]) * 0.5) for i in range(4)]
    return mix(*parts)


@sfx("lock_clunk")
def lock_clunk(_):
    return mix(click(0.004, 5000), (0.03, knock(220, 0.25, 0.04, noise_amt=0.4)), (0.06, metal([600, 1500], 0.15, [0.04, 0.02]) * 0.4))


@sfx("key_turn")
def key_turn(_):
    parts = [(i * 0.05, click(0.003, 7000, 0.6)) for i in range(4)]
    parts.append((0.22, knock(300, 0.18, 0.03, noise_amt=0.6)))
    return mix(*parts)


@sfx("whoosh", 2)
def whoosh(v):
    d = 0.35
    return sweep_bandpass(noise(d, "pink"), 350 + v * 50, 2200, q=2.5) * env_points(d, [(0, 0), (0.15, 1), (d, 0)])


# ----------------------------------------------------------------------------- material foley

def soft_thud(f=90, d=0.18):
    return sine(S.pitch_drop(f * 1.6, f, d, 30), d) * env_exp(d, 0.04)


@sfx("leather_pick", 2)
def leather_pick(v):
    d = 0.22
    creak = bandpass(noise(d), 900, 3200) * env_points(d, [(0, 0), (0.03, 1), (d, 0)]) * (1 + 0.6 * np.sin(2 * np.pi * 70 * t(d)))
    return creak * 0.5


@sfx("leather_put", 2)
def leather_put(v):
    return mix(soft_thud(85 + v * 8) * 0.9, bandpass(noise(0.12), 700, 2500) * env_exp(0.12, 0.02) * 0.5)


@sfx("cloth_pick", 2)
def cloth_pick(v):
    d = 0.3
    return highpass(noise(d, "pink"), 1500) * env_points(d, [(0, 0), (0.05, 0.8), (0.12, 0.4), (0.2, 0.7), (d, 0)]) * 0.5


@sfx("cloth_put", 2)
def cloth_put(v):
    d = 0.25
    return mix(soft_thud(70, 0.15) * 0.5, highpass(noise(d, "pink"), 1200) * env_exp(d, 0.06) * 0.6)


@sfx("paper_pick", 2)
def paper_pick(v):
    d = 0.25
    crisp = highpass(noise(d), 2500) * env_points(d, [(0, 0), (0.02, 1), (0.08, 0.3), (0.12, 0.8), (d, 0)])
    return crisp * 0.45


@sfx("paper_put", 2)
def paper_put(v):
    d = 0.2
    return mix(highpass(noise(d), 1800) * env_exp(d, 0.03) * 0.5, soft_thud(110, 0.1) * 0.3)


@sfx("metal_pick", 2)
def metal_pick(v):
    return metal([2400 + v * 200, 4100, 6300], 0.35, [0.08, 0.05, 0.03], [0.6, 0.4, 0.3]) * 0.5


@sfx("metal_put", 2)
def metal_put(v):
    return mix(metal([1900 + v * 150, 3300, 5200], 0.4, [0.1, 0.06, 0.04], [0.6, 0.35, 0.2]) * 0.5, soft_thud(140, 0.1) * 0.4)


@sfx("wood_pick", 2)
def wood_pick(v):
    return knock(420 + v * 30, 0.12, 0.02, noise_amt=0.3) * 0.5


@sfx("wood_put", 2)
def wood_put(v):
    return knock(260 + v * 25, 0.18, 0.035, noise_amt=0.5) * 0.8


@sfx("glass_pick")
def glass_pick(_):
    return metal([3800, 5900], 0.25, [0.06, 0.04]) * 0.4


@sfx("glass_put")
def glass_put(_):
    return mix(metal([3100, 4800, 7200], 0.4, [0.12, 0.08, 0.05]) * 0.5, soft_thud(160, 0.08) * 0.3)


@sfx("ceramic_put")
def ceramic_put(_):
    return metal([2200, 3400, 5100], 0.35, [0.09, 0.06, 0.04]) * 0.5


# ----------------------------------------------------------------------------- parts: hinges, latches, winding

@sfx("hinge_open", 2)
def hinge_open(v):
    """Stick-slip creak: slow, irregular pulses (30-110 Hz) exciting wood/metal resonances."""
    d = 0.5
    n = int(d * SR)
    pulses = np.zeros(n)
    tpos = 0.0
    while tpos < d - 0.01:
        k = tpos / d
        rate = 35 + 75 * math.sin(math.pi * k) + v * 10
        tpos += (1.0 / rate) * S.rng.uniform(0.7, 1.3)
        i = int(tpos * SR)
        if i < n:
            pulses[i] = S.rng.uniform(0.5, 1.0)
    creak = resonator(pulses, 650 + v * 120, 18) + resonator(pulses, 1450 + v * 90, 22) * 0.7 + resonator(pulses, 2900, 25) * 0.35
    return creak * env_points(d, [(0, 0), (0.04, 1), (0.38, 0.8), (d, 0)]) * 0.8


@sfx("hinge_close", 2)
def hinge_close(v):
    return mix(hinge_open(v)[: int(0.2 * SR)] * 0.6, (0.18, knock(500, 0.1, 0.02, noise_amt=0.4) * 0.6))


@sfx("latch_open")
def latch_open(_):
    return mix(click(0.003, 6000), (0.02, metal([2600, 4300], 0.12, [0.03, 0.02]) * 0.5), (0.09, click(0.003, 5000, 0.7)))


@sfx("latch_close")
def latch_close(_):
    return mix(metal([2200, 3900], 0.15, [0.04, 0.02]) * 0.5, (0.01, click(0.004, 5000)))


@sfx("leather_open")
def leather_open(_):
    return mix(leather_pick(0) * 0.8, (0.08, bandpass(noise(0.15), 500, 1800) * env_exp(0.15, 0.04) * 0.4))


@sfx("leather_close")
def leather_close(_):
    return mix(soft_thud(110, 0.12) * 0.7, bandpass(noise(0.1), 900, 2500) * env_exp(0.1, 0.02) * 0.4)


@sfx("cloth_open")
def cloth_open(_):
    return mix(click(0.003, 4000, 0.5), (0.02, cloth_pick(0)))


@sfx("cloth_close")
def cloth_close(_):
    return mix(cloth_put(0), (0.08, click(0.003, 4000, 0.6)))


@sfx("paper_open")
def paper_open(_):
    return paper_pick(1)


@sfx("paper_close")
def paper_close(_):
    return paper_put(1)


@sfx("wind")
def wind(_):
    parts = [(i * 0.085, mix(click(0.002, 6000, 0.7), metal([3000 + (i % 2) * 400], 0.05, [0.01]) * 0.3)) for i in range(9)]
    return mix(*parts)


# ----------------------------------------------------------------------------- discovery, writing, paper

def bell_tone(f, d, decay=0.6, partials=((1, 1.0), (2.0, 0.3), (3.01, 0.15), (4.2, 0.06))):
    out = np.zeros(int(d * SR))
    for r, a in partials:
        out += sine(f * r, d) * env_exp(d, decay / r ** 0.6, 0.001) * a
    return out


@sfx("discover", 2)
def discover(v):
    base = [1318.5, 1567.98][v % 2]
    chime = mix(bell_tone(base, 1.4, 0.5), (0.07, bell_tone(base * 1.5, 1.3, 0.45) * 0.7))
    shimmer = highpass(noise(0.8), 6000) * env_points(0.8, [(0, 0), (0.05, 0.25), (0.8, 0)])
    return reverb(mix(chime * 0.6, shimmer * 0.15), wet=0.35, dur=1.5, key="sparkle")


@sfx("discover_secret")
def discover_secret(_):
    notes = [987.77, 1318.5, 1760.0, 2093.0]
    parts = [(i * 0.09, bell_tone(f, 1.8, 0.7) * (0.8 - i * 0.1)) for i, f in enumerate(notes)]
    shimmer = highpass(noise(1.4), 5000) * env_points(1.4, [(0, 0), (0.3, 0.3), (1.4, 0)])
    return reverb(mix(*parts) + mix(shimmer * 0.2, length=len(mix(*parts))), wet=0.45, dur=2.2, key="sparkle")


@sfx("pen_scratch", 3)
def pen_scratch(v):
    d = 0.55
    tt = t(d)
    strokes = np.clip(np.sin(2 * np.pi * (6 + v) * tt + S.rng.uniform(0, 6)), 0, 1) ** 2
    scratch = bandpass(noise(d), 2500, 9000) * strokes * env_points(d, [(0, 0), (0.03, 1), (0.45, 0.8), (d, 0)])
    return scratch * 0.5


@sfx("tick_soft")
def tick_soft(_):
    return click(0.003, 5000, 0.6)


@sfx("type_key", 3)
def type_key(v):
    return mix(click(0.003, 4000 + v * 500), (0.008, knock(700 + v * 60, 0.06, 0.01, noise_amt=0.4) * 0.5))


def rustle(d, lo=1500, density=1.0):
    n = highpass(noise(d), lo)
    gate = np.clip(lowpass(S.rng.standard_normal(len(n)) * density, 25) * 6, 0, 1)
    return n * gate


@sfx("paper_slide")
def paper_slide(_):
    d = 0.35
    return bandpass(noise(d, "pink"), 900, 6000) * env_points(d, [(0, 0), (0.05, 1), (0.25, 0.6), (d, 0)]) * 0.5


@sfx("paper_lift")
def paper_lift(_):
    return rustle(0.25, 2000) * env_exp(0.25, 0.08) * 0.5


@sfx("paper_unfold")
def paper_unfold(_):
    d = 0.6
    return rustle(d, 1200, 1.4) * env_points(d, [(0, 0), (0.05, 1), (0.4, 0.7), (d, 0)]) * 0.6


@sfx("paper_fold")
def paper_fold(_):
    d = 0.35
    return rustle(d, 1500, 1.2) * env_exp(d, 0.1) * 0.5


@sfx("paper_tear", 2)
def paper_tear(v):
    d = 0.32
    crackle = mix(*[(i * d / 30 + S.rng.uniform(0, 0.004), click(0.002, 3000 + S.rng.uniform(0, 3000), S.rng.uniform(0.3, 1.0))) for i in range(30)])
    body = bandpass(noise(d), 1500, 7000) * env_points(d, [(0, 0), (0.02, 0.6), (0.28, 0.5), (d, 0)])
    return mix(crackle, body * 0.4)


@sfx("paper_spike")
def paper_spike(_):
    return mix(knock(900, 0.08, 0.01, noise_amt=0.6) * 0.4, (0.01, rustle(0.2, 2000) * env_exp(0.2, 0.05) * 0.4))


@sfx("tag_flip", 2)
def tag_flip(v):
    return mix(click(0.002, 5000, 0.4), (0.01, rustle(0.12, 2500) * env_exp(0.12, 0.03) * 0.4))


@sfx("newspaper")
def newspaper(_):
    d = 0.8
    return rustle(d, 900, 1.6) * env_points(d, [(0, 0), (0.05, 1), (0.5, 0.6), (d, 0)]) * 0.7


# ----------------------------------------------------------------------------- stamps, printer, bell, tray

@sfx("stamp_pick")
def stamp_pick(_):
    return mix(knock(520, 0.1, 0.015, noise_amt=0.3) * 0.5, (0.04, knock(780, 0.08, 0.01, noise_amt=0.2) * 0.3))


@sfx("stamp_thump", 3)
def stamp_thump(v):
    d = 0.5
    sub = sine(S.pitch_drop(120 + v * 6, 52, d, 18), d) * env_exp(d, 0.09, 0.0008)
    body = knock(240 + v * 15, d, 0.05, partials=((1, 1.0), (1.9, 0.5), (3.3, 0.25)), noise_amt=0.2)
    slap = bandpass(noise(d), 1200, 5500) * env_exp(d, 0.012)
    squelch = bandpass(noise(0.12), 300, 900) * env_exp(0.12, 0.03)
    x = mix(sub * 1.0, body * 0.55, slap * 0.55, (0.004, squelch * 0.25))
    return reverb(x, wet=0.12, dur=0.5, key="booth")


@sfx("stamp_rack")
def stamp_rack(_):
    return mix(knock(600, 0.1, 0.012, noise_amt=0.3) * 0.5, (0.035, knock(450, 0.1, 0.015, noise_amt=0.3) * 0.4))


@sfx("nope")
def nope(_):
    return mix(soft_thud(140, 0.1) * 0.6, (0.12, soft_thud(110, 0.12) * 0.6))


@sfx("printer")
def printer(_):
    d = 1.45
    tt = t(d)
    gate = (np.sin(2 * np.pi * 11 * tt) > 0.2).astype(float)
    gate = lowpass(gate, 200)
    motor = (S.square(150, d, 8) * 0.5 + S.saw(300, d, 10) * 0.2) * gate
    motor = bandpass(motor, 200, 3000)
    clicks = mix(*[(k / 11.0, click(0.002, 4000, 0.7)) for k in range(int(d * 11) - 2)], length=len(tt))
    whine = sine(1900 + 60 * np.sin(2 * np.pi * 0.7 * tt), d) * 0.04
    env = env_points(d, [(0, 0), (0.05, 1), (1.2, 1), (1.32, 0), (d, 0)])
    ding = bell_tone(2637, 0.6, 0.25) * 0.25
    return mix((motor * 0.5 + clicks * 0.6 + whine) * env, (1.3, ding))


@sfx("bell")
def counter_bell(_):
    d = 2.2
    f = 1760
    tone = metal([f, f * 2.76, f * 5.40, f * 8.93, f * 1.002], d, [0.9, 0.5, 0.25, 0.12, 0.85], [1, 0.45, 0.2, 0.1, 0.6])
    strike = click(0.003, 6000, 0.8)
    return reverb(mix(tone * 0.7, strike * 0.4), wet=0.25, dur=1.4, key="booth")


@sfx("tray_clink", 2)
def tray_clink(v):
    return metal([1180 + v * 60, 2900, 4400], 0.5, [0.15, 0.08, 0.05], [0.7, 0.4, 0.25]) * 0.5


@sfx("tray_slide")
def tray_slide(_):
    d = 0.4
    scrape = friction(d, 900, 3500, rough=80, grain=0.5) * env_points(d, [(0, 0), (0.05, 1), (0.3, 0.8), (d, 0)])
    return resonator(scrape, 1800, 6) * 0.3 + scrape * 0.3


@sfx("receive")
def receive(_):
    return mix(cloth_pick(1) * 0.6, (0.1, soft_thud(100, 0.1) * 0.3))


# ----------------------------------------------------------------------------- lamp, uncanny

@sfx("lamp_click")
def lamp_click(_):
    return mix(click(0.004, 4000), (0.05, click(0.003, 5000, 0.7)), (0.07, metal([3200], 0.05, [0.01]) * 0.3))


@sfx("uv_on")
def uv_on(_):
    d = 1.0
    hum = (sine(100, d) * 0.5 + sine(200, d) * 0.3 + sine(300, d) * 0.15 + S.saw(100, d, 30) * 0.08) * env_points(d, [(0, 0), (0.15, 1), (d, 0.0)])
    zap = bandpass(noise(0.2), 2000, 8000) * env_exp(0.2, 0.03)
    shimmer = sine(1500, d) * env_points(d, [(0, 0), (0.2, 0.15), (d, 0)])
    return mix(hum * 0.6, zap * 0.5, shimmer * 0.4)


@sfx("uv_off")
def uv_off(_):
    d = 0.4
    return sine(S.pitch_drop(200, 60, d, 6), d) * env_exp(d, 0.1) * 0.5


@sfx("hum")
def hum(_):
    """Warm major chord with slow beating, exactly loopable (4 s, all partials whole cycles)."""
    d = 4.0
    out = np.zeros(int(d * SR))
    for f, a in ((73.5, 0.45), (110.25, 0.3), (147.0, 0.55), (185.25, 0.35), (220.5, 0.3), (294.0, 0.3),
                 (370.5, 0.22), (441.0, 0.16), (588.0, 0.1), (741.0, 0.06)):
        for det in (-0.25, 0.25):
            out += sine(f + det, d) * a
    trem = 1 + 0.12 * np.sin(2 * np.pi * 1.0 * t(d))
    # a soft "oo" choir colour so the hum carries on small speakers
    x = out * trem
    x = x * 0.6 + resonator(x, 420, 4) * 0.25 + resonator(x, 900, 5) * 0.12
    return x


@sfx("frost_creep")
def frost_creep(_):
    d = 1.8
    grains = []
    for i in range(140):
        tt = (i / 140) ** 0.7 * d * 0.95
        grains.append((tt, metal([S.rng.uniform(3000, 9000)], 0.04, [0.008]) * S.rng.uniform(0.2, 0.7)))
    air = highpass(noise(d), 4000) * env_points(d, [(0, 0), (1.2, 0.25), (d, 0)])
    return reverb(mix(*grains) + mix(air * 0.3, length=len(mix(*grains))), wet=0.4, dur=1.6, key="frost")


@sfx("photo_change")
def photo_change(_):
    d = 2.6
    harm = np.zeros(int(d * SR))
    for f, a in ((523.25, 0.5), (659.25, 0.35), (783.99, 0.3), (1046.5, 0.2)):
        harm += sine(f, d) * a
    harm *= env_points(d, [(0, 0), (0.9, 1), (1.6, 0.8), (d, 0)])
    rev_cym = bandpass(noise(d), 3000, 12000) * env_points(d, [(0, 0), (0.9, 0.5), (0.95, 0), (d, 0)])
    sparkle = mix(*[(1.0 + i * 0.07, bell_tone(1046.5 * (1.5 ** (i % 3)), 1.2, 0.4) * 0.3) for i in range(5)], length=int(d * SR))
    return reverb(harm * 0.5 + rev_cym * 0.3 + sparkle, wet=0.5, dur=2.5, key="sparkle")


# ----------------------------------------------------------------------------- people

def step(v):
    d = 0.25
    heel = sine(S.pitch_drop(160, 80, d, 40), d) * env_exp(d, 0.025)
    tap = bandpass(noise(d), 1500, 6000) * env_exp(d, 0.008)
    scuff = bandpass(noise(d), 600, 3000) * env_points(d, [(0, 0), (0.05, 0), (0.07, 0.2), (0.15, 0)])
    return heel * 0.8 + tap * 0.5 * (0.7 + 0.3 * v) + scuff * 0.3


@sfx("footsteps", 3)
def footsteps(v):
    parts = []
    for i in range(6):
        parts.append((i * (0.42 + v * 0.03) + S.rng.uniform(0, 0.02), step(S.rng.uniform(0.6, 1.0)) * (0.5 + 0.5 * math.sin(math.pi * (i + 0.5) / 6))))
    return reverb(mix(*parts), wet=0.45, dur=2.0, key="hall")


@sfx("emote_happy", 2)
def emote_happy(v):
    d = 0.35
    return formant_voice(220 + v * 30, d, "a", contour=0.6) * env_points(d, [(0, 0), (0.03, 1), (0.25, 0.7), (d, 0)]) * 0.6


@sfx("emote_huff")
def emote_huff(_):
    d = 0.4
    return bandpass(noise(d), 300, 2500) * env_points(d, [(0, 0), (0.03, 1), (d, 0)]) * 0.6


@sfx("emote_gasp")
def emote_gasp(_):
    d = 0.45
    return sweep_bandpass(noise(d), 800, 2500, q=3) * env_points(d, [(0, 0), (0.25, 1), (d, 0)]) * 0.6


@sfx("emote_sigh")
def emote_sigh(_):
    d = 0.9
    return sweep_bandpass(noise(d), 1800, 500, q=3) * env_points(d, [(0, 0), (0.1, 1), (d, 0)]) * 0.5


VOICE_BANKS = {
    # bank: (f0, formant shift, breath, vibrato, extra)
    "low": (105, 0.88, 0.04, 0.0, None),
    "mid": (150, 1.0, 0.04, 0.0, None),
    "high": (235, 1.15, 0.05, 0.0, None),
    "child": (300, 1.3, 0.05, 0.0, None),
    "old": (210, 1.1, 0.12, 0.035, None),
    "grey": (88, 0.85, 0.03, 0.0, "grey"),
    "cold": (120, 0.95, 0.25, 0.015, "cold"),
}


def gen_voices():
    vowels = ["a", "e", "i", "o", "u", "ae", "uh", "a"]
    for bank, (f0, fs, breath, vib, extra) in VOICE_BANKS.items():
        S.seed(zlib.crc32(bank.encode()) % 10000)
        for i, vw in enumerate(vowels):
            d = 0.075 + S.rng.uniform(0, 0.035)
            v = formant_voice(f0 * S.rng.uniform(0.95, 1.08), d, vw, formant_shift=fs, breath=breath, vibrato=vib, contour=S.rng.uniform(-0.15, 0.2))
            # a consonant-ish onset for half of them
            if i % 2 == 0:
                v[: int(0.012 * SR)] += highpass(noise(0.012), 2500) * 0.3
            v = v * env_points(d, [(0, 0), (0.008, 1), (d * 0.7, 0.8), (d, 0)])
            if extra == "grey":
                v = np.concatenate([v[::-1] * np.linspace(0, 0.3, len(v)), v])  # a whisper of it reversed before
                v = reverb(v, wet=0.3, dur=0.6, key="grey")
            if extra == "cold":
                v = reverb(S.delay(v, 0.09, 0.4, 0.35, 3), wet=0.5, dur=1.0, key="cold")
            v = lowpass(v, 6000)
            save(f"voice_{bank}_{i + 1}", v, peak=0.7)


# ----------------------------------------------------------------------------- UI and transitions

@sfx("ui_hover")
def ui_hover(_):
    return mix(click(0.002, 6000, 0.4), (0.005, rustle(0.06, 3000) * env_exp(0.06, 0.015) * 0.3))


@sfx("ui_click")
def ui_click(_):
    return mix(knock(900, 0.08, 0.012, noise_amt=0.3) * 0.5, click(0.003, 5000, 0.6))


@sfx("day_sting")
def day_sting(_):
    d = 3.0
    chord = mix(*[(i * 0.05, S.karplus(f, d, 0.9985, 0.6) * 0.5) for i, f in enumerate((146.83, 220.0, 293.66, 369.99, 440.0))])
    bell = mix((0.3, bell_tone(880, 2.0, 0.8) * 0.3))
    return reverb(mix(chord, bell), wet=0.4, dur=2.5, key="hall")


@sfx("shutter_down")
def shutter_down(_):
    d = 1.6
    parts = []
    k = 0.0
    i = 0
    while k < 1.2:
        parts.append((k, metal([S.rng.uniform(500, 900) * (1 - k * 0.2), S.rng.uniform(1500, 2500)], 0.12, [0.03, 0.02]) * 0.5 + 0))
        k += 0.045 + S.rng.uniform(0, 0.02)
        i += 1
    roll = lowpass(noise(1.25, "brown"), 400) * 0.5
    slam = knock(110, 0.4, 0.08, noise_amt=0.6)
    return reverb(mix(*parts, roll, (1.22, slam)), wet=0.3, dur=1.4, key="booth")


@sfx("ledger_tick", 2)
def ledger_tick(v):
    return mix(pen_scratch(v)[: int(0.15 * SR)] * 0.8, (0.05, bell_tone(1568 + v * 100, 0.6, 0.25) * 0.25))


@sfx("ledger_half")
def ledger_half(_):
    return mix(pen_scratch(1)[: int(0.2 * SR)] * 0.7, (0.05, bell_tone(1174, 0.5, 0.2) * 0.2))


@sfx("ledger_cross")
def ledger_cross(_):
    return mix(pen_scratch(0)[: int(0.12 * SR)], (0.16, pen_scratch(2)[: int(0.12 * SR)]), (0.05, soft_thud(110, 0.12) * 0.3))


@sfx("brass_stamp")
def brass_stamp(_):
    return mix(stamp_thump(1) * 0.7, (0.02, bell_tone(1318.5, 0.9, 0.35) * 0.35))


# ----------------------------------------------------------------------------- ambience

def gen_ambience():
    S.seed(77)
    d = 34.0
    n = int(d * SR)
    # crowd murmur: many distant formant voices
    crowd = np.zeros(n)
    for _ in range(260):
        tt = S.rng.uniform(0, d - 0.4)
        f0 = S.rng.choice([110, 140, 180, 220])
        syl = formant_voice(f0 * S.rng.uniform(0.9, 1.1), S.rng.uniform(0.08, 0.2), S.rng.choice(list("aeiou")), breath=0.1)
        k = np.arange(len(syl)) / SR
        syl *= np.interp(k, [0, 0.01, k[-1]], [0, 1, 0]) * S.rng.uniform(0.2, 1.0)
        i = int(tt * SR)
        crowd[i:i + len(syl)] += syl[: n - i]
    crowd = lowpass(crowd, 1800) * 0.5 + lowpass(noise(d, "pink"), 900) * 0.15
    # distant footsteps
    steps = np.zeros(n)
    for _ in range(90):
        i = int(S.rng.uniform(0, d - 0.3) * SR)
        s = step(S.rng.uniform(0.3, 0.8)) * S.rng.uniform(0.05, 0.2)
        steps[i:i + len(s)] += s[: n - i]
    # a train arriving and the station announcer's chime
    train = lowpass(noise(d, "brown"), 120) * env_points(d, [(0, 0.05), (8, 0.1), (14, 0.6), (20, 0.5), (24, 0.08), (d, 0.05)])
    brake = sweep_bandpass(noise(4.0), 3000, 1800, q=20) * env_points(4.0, [(0, 0), (0.5, 0.3), (3.0, 0.2), (4.0, 0)])
    chime = mix(bell_tone(784, 1.6, 0.5), (0.45, bell_tone(659.25, 1.6, 0.5)), (0.9, bell_tone(523.25, 2.0, 0.6)))
    bed = mix(crowd, steps, train * 0.8, (18.0, brake * 0.25), (27.0, chime * 0.18), length=n)
    bed = reverb(bed, wet=0.55, dur=3.0, damp=0.6, bright=5000, key="hall")[:n]
    left = bed
    right = np.roll(bed, int(0.013 * SR)) * 0.95 + np.roll(bed, int(7.3 * SR)) * 0.05
    st = np.stack([left, right], axis=-1)
    st = loopable(st, 2.0)
    write(os.path.join(OUT, "amb_concourse.wav"), normalize(st, 0.6))

    # the station clock, ticking (exactly 4 s loop)
    d = 4.0
    clk = np.zeros(int(d * SR))
    for k in range(4):
        tk = knock(1800 if k % 2 == 0 else 1500, 0.08, 0.008, noise_amt=0.4) * 0.5
        i = int(k * SR)
        clk[i:i + len(tk)] += tk
    clk = reverb(clk, wet=0.5, dur=1.2, key="clock")[: int(d * SR)]
    write(os.path.join(OUT, "amb_clock.wav"), normalize(clk, 0.25))

    # rain on the glass roof (Friday evening)
    d = 20.0
    rain = highpass(noise(d, "pink"), 800) * 0.4 + lowpass(noise(d, "pink"), 400) * 0.2
    drops = np.zeros(int(d * SR))
    for _ in range(1600):
        i = int(S.rng.uniform(0, d - 0.05) * SR)
        dr = metal([S.rng.uniform(2000, 6000)], 0.03, [0.006]) * S.rng.uniform(0.05, 0.3)
        drops[i:i + len(dr)] += dr[: len(drops) - i]
    r = loopable(stereo(reverb(rain + drops, wet=0.3, dur=1.5, key="rain")[: int(d * SR)], 0, 0.6), 1.5)
    write(os.path.join(OUT, "amb_rain.wav"), normalize(r, 0.5))


def main():
    only = sys.argv[1:]
    for name, (fn, variants) in SFX.items():
        if only and name not in only:
            continue
        for v in range(variants):
            S.seed(zlib.crc32(name.encode()) % 100000 + v)
            x = fn(v)
            out = name if variants == 1 else f"{name}_{v + 1}"
            save(out, x)
    if not only or "voices" in only:
        gen_voices()
    if not only or "ambience" in only:
        gen_ambience()
    print("sfx written to", OUT)


if __name__ == "__main__":
    main()
