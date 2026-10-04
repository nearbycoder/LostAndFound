"""The score of Lost & Found, synthesized: an original 9-note "Ninefold" motif in D minor, arranged
per day (gentle jazz ballad with piano, upright bass and brushes; clarinet on Tuesday; celesta and
drone on Wednesday; strings on Thursday; everything on Friday), plus title, ledger, uncanny, ring
and finale cues.

    .venv/bin/python Tools/audio/gen_music.py [track ...]

Writes Assets/Game/Resources/Music/<track>.wav (stereo, 44.1 kHz).
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import synth as S  # noqa: E402
from synth import SR, bandpass, env_exp, highpass, lowpass, noise, normalize, reverb, sine, write  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Music")

NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8, "Ab": 8,
        "A": 9, "A#": 10, "Bb": 10, "B": 11}


def midi(name):
    """'A4' -> 69, 'Bb3' -> 58."""
    n, o = (name[:-1], int(name[-1])) if name[-1].isdigit() else (name, 4)
    return 12 * (o + 1) + NOTE[n]


def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)


# ----------------------------------------------------------------------------- instruments (mono)

def piano(m, dur, vel=0.8):
    f = hz(m)
    ring = min(4.5, 1.2 + 2.8 * (1 - (m - 40) / 60))   # low notes ring longer
    total = dur + 0.6
    tt = np.arange(int(total * SR)) / SR
    out = np.zeros_like(tt)
    B = 0.0004  # inharmonicity
    for k in range(1, 12):
        fk = f * k * math.sqrt(1 + B * k * k)
        if fk > 14000:
            break
        a = (1.0 / k ** 1.3) * (0.6 + 0.4 * vel) ** (k * 0.5)
        dec = ring / (1 + 0.35 * k)
        for det in (-0.6, 0.6):
            out += np.sin(2 * np.pi * (fk + det * k * 0.15) * tt) * np.exp(-tt / dec) * a * 0.5
    hammer = bandpass(S.rng.standard_normal(len(tt)), 1000, 6000) * np.exp(-tt / 0.008) * 0.05 * vel
    out = out + hammer
    damper = np.ones_like(tt)
    i = int(dur * SR)
    damper[i:] = np.exp(-(tt[i:] - dur) / 0.08)
    out = out * damper * np.clip(tt / 0.003, 0, 1)
    return lowpass(out, 2500 + 6000 * vel) * vel


def epiano(m, dur, vel=0.8):
    f = hz(m)
    total = dur + 0.5
    tt = np.arange(int(total * SR)) / SR
    idx = 1.6 * vel * np.exp(-tt / 0.6)
    tone = np.sin(2 * np.pi * f * tt + idx * np.sin(2 * np.pi * f * tt)) * np.exp(-tt / 1.8)
    tine = np.sin(2 * np.pi * f * 14 * tt) * np.exp(-tt / 0.03) * 0.08
    env = np.clip(tt / 0.004, 0, 1)
    rel = np.ones_like(tt)
    i = int(dur * SR)
    rel[i:] = np.exp(-(tt[i:] - dur) / 0.12)
    return (tone + tine) * env * rel * vel


def bass(m, dur, vel=0.85):
    f = hz(m)
    total = dur + 0.3
    x = S.karplus(f, total, damping=0.9975, bright=0.25)
    tt = np.arange(len(x)) / SR
    thump = np.sin(2 * np.pi * f * tt) * np.exp(-tt / 0.15) * 0.4
    y = lowpass(x * 0.8 + thump, 900)
    rel = np.ones_like(tt)
    i = int(dur * 0.95 * SR)
    rel[i:] = np.exp(-(tt[i:] - dur * 0.95) / 0.05)
    return y * rel * vel


def clarinet(m, dur, vel=0.7):
    f = hz(m)
    total = dur + 0.2
    tt = np.arange(int(total * SR)) / SR
    vib = 1 + 0.004 * np.sin(2 * np.pi * 5.2 * tt) * np.clip((tt - 0.25) / 0.3, 0, 1)
    ph = np.cumsum(f * vib) / SR
    out = np.zeros_like(tt)
    for k in range(1, 16, 2):
        if f * k > 9000:
            break
        out += np.sin(2 * np.pi * k * ph) / k ** 1.1
    out += 0.05 * np.sin(2 * np.pi * 2 * ph)
    breath = bandpass(S.rng.standard_normal(len(tt)), 1500, 5000) * 0.02
    env = np.clip(tt / 0.04, 0, 1) * np.where(tt < dur, 1.0, np.exp(-(tt - dur) / 0.06))
    return lowpass(out + breath, 3200) * env * vel * 0.6


def celesta(m, dur, vel=0.7):
    f = hz(m)
    total = dur + 1.6
    tt = np.arange(int(total * SR)) / SR
    out = (np.sin(2 * np.pi * f * tt) * np.exp(-tt / 1.4) + 0.35 * np.sin(2 * np.pi * f * 4 * tt) * np.exp(-tt / 0.25)
           + 0.15 * np.sin(2 * np.pi * f * 2.01 * tt) * np.exp(-tt / 0.8))
    return out * np.clip(tt / 0.002, 0, 1) * vel * 0.7


def musicbox(m, dur, vel=0.7):
    f = hz(m)
    total = 1.8
    tt = np.arange(int(total * SR)) / SR
    out = (np.sin(2 * np.pi * f * tt) * np.exp(-tt / 0.9) + 0.4 * np.sin(2 * np.pi * f * 3.0 * tt) * np.exp(-tt / 0.2)
           + 0.2 * np.sin(2 * np.pi * f * 5.4 * tt) * np.exp(-tt / 0.08))
    tick = highpass(S.rng.standard_normal(len(tt)), 4000) * np.exp(-tt / 0.002) * 0.15
    return (out + tick) * np.clip(tt / 0.001, 0, 1) * vel * 0.6


def strings(m, dur, vel=0.6):
    f = hz(m)
    total = dur + 0.9
    tt = np.arange(int(total * SR)) / SR
    out = np.zeros_like(tt)
    for det in (-0.12, 0.0, 0.11):
        vib = 1 + 0.003 * np.sin(2 * np.pi * (5.0 + det) * tt + det * 10)
        ph = np.cumsum(f * (1 + det / 100) * vib) / SR
        for k in range(1, 14):
            if f * k > 8000:
                break
            out += np.sin(2 * np.pi * k * ph) / k
    env = np.clip(tt / 0.45, 0, 1) ** 1.5 * np.where(tt < dur, 1.0, np.exp(-(tt - dur) / 0.35))
    return lowpass(out, 2200) * env * vel * 0.12


INSTR = {"piano": piano, "epiano": epiano, "bass": bass, "clarinet": clarinet, "celesta": celesta,
         "musicbox": musicbox, "strings": strings}


# ----------------------------------------------------------------------------- drums

def brush_swish(dur):
    n = bandpass(S.rng.standard_normal(int(dur * SR)), 2500, 9000)
    tt = np.arange(len(n)) / SR
    return n * np.sin(np.pi * np.clip(tt / dur, 0, 1)) ** 2 * 0.06


def brush_tap(vel=0.6):
    d = 0.12
    n = bandpass(S.rng.standard_normal(int(d * SR)), 1800, 7000)
    return n * env_exp(d, 0.025) * 0.3 * vel


def ride(vel=0.5):
    d = 0.9
    return S.metal([3150, 4420, 5390, 6870], d, [0.5, 0.35, 0.25, 0.2], [0.4, 0.3, 0.2, 0.15]) * 0.12 * vel


def kick_soft(vel=0.5):
    d = 0.3
    return sine(S.pitch_drop(90, 48, d, 25), d) * env_exp(d, 0.08) * 0.35 * vel


# ----------------------------------------------------------------------------- the score

CHORDS_A = [("D", "m9"), ("G", "m7"), ("F", "maj7"), ("Bb", "maj7"), ("E", "m7b5"), ("A", "7b9"), ("D", "m6"), ("A", "7")]
CHORDS_B = [("G", "m7"), ("C", "7"), ("F", "maj7"), ("D", "m7"), ("G", "m7"), ("A", "7"), ("D", "m9"), ("A", "7sus")]
QUALITY = {"m9": [0, 3, 7, 10, 14], "m7": [0, 3, 7, 10], "maj7": [0, 4, 7, 11], "m7b5": [0, 3, 6, 10], "7b9": [0, 4, 7, 10, 13],
           "m6": [0, 3, 7, 9], "7": [0, 4, 7, 10], "7sus": [0, 5, 7, 10]}

# the Ninefold motif (beats, duration, note): nine notes over two bars
MOTIF = [(0.0, 0.5, "A4"), (0.5, 1.0, "D5"), (1.5, 1.0, "F5"), (2.5, 0.5, "E5"), (3.0, 0.5, "D5"), (3.5, 1.5, "C5"),
         (5.0, 0.5, "A4"), (5.5, 1.0, "Bb4"), (6.5, 1.5, "A4")]
# answering phrases for the rest of each 8-bar section (original lines over the changes)
PHRASE_A2 = [(8.0, 1.0, "D5"), (9.0, 0.5, "E5"), (9.5, 0.5, "F5"), (10.0, 1.5, "A5"), (11.5, 0.5, "G5"), (12.0, 1.0, "F5"),
             (13.0, 1.0, "E5"), (14.0, 2.0, "F5")]
PHRASE_A3 = [(16.0, 0.5, "E5"), (16.5, 1.0, "D5"), (17.5, 0.5, "C5"), (18.0, 1.0, "Bb4"), (19.0, 1.0, "G4"), (20.0, 1.5, "C#5"),
             (21.5, 0.5, "E5"), (22.0, 2.0, "G5")]
PHRASE_A4 = [(24.0, 1.0, "F5"), (25.0, 0.5, "E5"), (25.5, 0.5, "D5"), (26.0, 1.0, "B4"), (27.0, 1.0, "D5"), (28.0, 1.5, "C#5"),
             (29.5, 0.5, "A4"), (30.0, 2.0, "D5")]
PHRASE_B = [(0.0, 1.5, "Bb4"), (1.5, 0.5, "D5"), (2.0, 1.0, "F5"), (3.0, 1.0, "E5"), (4.0, 2.0, "A5"), (6.0, 1.0, "G5"), (7.0, 1.0, "E5"),
            (8.0, 1.5, "F5"), (9.5, 0.5, "E5"), (10.0, 1.0, "D5"), (11.0, 1.0, "C5"), (12.0, 2.0, "A4"), (14.0, 2.0, "F4"),
            (16.0, 1.0, "G4"), (17.0, 1.0, "Bb4"), (18.0, 1.0, "D5"), (19.0, 1.0, "F5"), (20.0, 1.5, "E5"), (21.5, 0.5, "C#5"),
            (22.0, 2.0, "A4"), (24.0, 3.0, "D5"), (27.0, 1.0, "A4"), (28.0, 4.0, "E5")]


class Score:
    def __init__(self, bpm, swing=0.62):
        self.bpm = bpm
        self.swing = swing
        self.events = []  # (beat, dur_beats, instr, midi, vel, pan)
        self.drums = []   # (beat, kind, vel)

    def beat_time(self, b):
        """Swung eighths: off-beat 8ths are delayed."""
        whole = math.floor(b)
        frac = b - whole
        if abs(frac - 0.5) < 1e-6:
            frac = self.swing
        return (whole + frac) * 60.0 / self.bpm

    def note(self, beat, dur, instr, m, vel=0.7, pan=0.0):
        self.events.append((beat, dur, instr, m, vel, pan))

    def render(self, total_beats, room=0.35, tail=3.0):
        n = int((self.beat_time(total_beats) + tail) * SR)
        out = np.zeros((n, 2))
        for beat, dur, instr, m, vel, pan in self.events:
            t0 = self.beat_time(beat)
            dsec = dur * 60.0 / self.bpm
            x = INSTR[instr](m, dsec, vel)
            i = int((t0 + S.rng.uniform(0, 0.008)) * SR)
            end = min(n, i + len(x))
            l, r = math.cos((pan + 1) * math.pi / 4), math.sin((pan + 1) * math.pi / 4)
            out[i:end, 0] += x[: end - i] * l
            out[i:end, 1] += x[: end - i] * r
        for beat, kind, vel in self.drums:
            t0 = self.beat_time(beat)
            x = {"swish": lambda: brush_swish(60.0 / self.bpm * 1.8), "tap": lambda: brush_tap(vel), "ride": lambda: ride(vel),
                 "kick": lambda: kick_soft(vel)}[kind]()
            i = int(t0 * SR)
            end = min(n, i + len(x))
            pan = 0.25 if kind != "kick" else 0.0
            out[i:end, 0] += x[: end - i] * (1 - pan)
            out[i:end, 1] += x[: end - i] * (1 + pan) * 0.9
        wet = np.stack([reverb_fixed(out[:, 0], room), reverb_fixed(out[:, 1], room, key="hallR")], axis=-1)
        y = np.tanh(wet * 1.1) / 1.1
        return y


def reverb_fixed(x, wet, key="hallL"):
    y = reverb(x, wet=wet, dur=2.6, damp=0.6, bright=5200, key=key)
    if len(y) < len(x):
        y = np.pad(y, (0, len(x) - len(y)))
    return y[: len(x)]


def chord_tones(root, qual, octave=3):
    base = midi(root + str(octave))
    return [base + i for i in QUALITY[qual]]


def comp(score, beat0, chords, instr="piano", vel=0.45, pattern="charleston", octave=3, pan=-0.2):
    """Rootless-ish voicings in a gentle comping rhythm."""
    for bar, (root, qual) in enumerate(chords):
        tones = chord_tones(root, qual, octave)
        voicing = [t + 12 if t < midi("F3") else t for t in tones[1:]]  # drop the root, keep it in the left-hand middle
        voicing = sorted(voicing)[:4]
        b = beat0 + bar * 4
        hits = {"charleston": [(0.0, 1.4), (1.5, 1.0)], "two_four": [(1.0, 0.9), (3.0, 0.9)], "whole": [(0.0, 3.8)],
                "arp": [(0.0, 3.9), (0.5, 3.4), (1.0, 2.9), (1.5, 2.4)]}[pattern]
        for k, (off, d) in enumerate(hits):
            if pattern == "arp":
                score.note(b + off, d, instr, voicing[k % len(voicing)], vel * 0.9, pan)
            else:
                for vtone in voicing:
                    score.note(b + off, d, instr, vtone, vel * S.rng.uniform(0.85, 1.0), pan)


def walk(score, beat0, chords, vel=0.8, two_feel=False):
    """Walking bass: root, chord tones, chromatic approach into the next bar."""
    for bar, (root, qual) in enumerate(chords):
        r = midi(root + "2")
        if r < midi("E2"):
            r += 12
        tones = [r + i for i in QUALITY[qual][:4]]
        nxt_root = midi(chords[(bar + 1) % len(chords)][0] + "2")
        if nxt_root < midi("E2"):
            nxt_root += 12
        approach = nxt_root - 1 if nxt_root > tones[2] else nxt_root + 1
        line = [tones[0], tones[1] if bar % 2 else tones[2], tones[2] if bar % 2 else tones[3] - 12 if tones[3] - 12 > midi("E2") else tones[1], approach]
        b = beat0 + bar * 4
        if two_feel:
            score.note(b, 1.9, "bass", line[0], vel)
            score.note(b + 2, 1.9, "bass", line[2], vel * 0.9)
        else:
            for k in range(4):
                score.note(b + k, 0.95, "bass", line[k], vel * (1.0 if k == 0 else 0.85))


def brushes(score, beat0, bars, vel=0.6, ride_too=False):
    for bar in range(bars):
        b = beat0 + bar * 4
        score.drums.append((b, "swish", vel))
        score.drums.append((b + 2, "swish", vel))
        score.drums.append((b + 1, "tap", vel))
        score.drums.append((b + 3, "tap", vel))
        if ride_too:
            for k in (0, 1, 1.5, 2, 3, 3.5):
                score.drums.append((b + k, "ride", vel * (0.7 if k % 1 else 1.0)))
        if bar % 4 == 0:
            score.drums.append((b, "kick", vel * 0.6))


def melody(score, beat0, phrase, instr="piano", vel=0.65, transpose=0, pan=0.15):
    for beat, dur, name in phrase:
        score.note(beat0 + beat, dur, instr, midi(name) + transpose, vel * S.rng.uniform(0.9, 1.05), pan)


def section_a(score, b0, lead, vel=0.65, transpose=0):
    melody(score, b0, MOTIF, lead, vel, transpose)
    melody(score, b0, PHRASE_A2, lead, vel * 0.95, transpose)
    melody(score, b0, PHRASE_A3, lead, vel * 0.95, transpose)
    melody(score, b0, PHRASE_A4, lead, vel, transpose)


def form(score, lead="piano", comp_instr="piano", pattern="charleston", lead_vel=0.65, drums=True, two_feel_first=True,
         counter=None, pad=False, celesta_fills=False):
    """AABA, 32 bars x 4 beats. Bass in two for the first A, walking after."""
    beat = 0
    for sec, chords in enumerate([CHORDS_A + CHORDS_A[:0], CHORDS_A, CHORDS_B, CHORDS_A]):
        chords_full = chords if len(chords) == 8 else chords + CHORDS_A
        if sec == 2:
            melody(score, beat, PHRASE_B, lead, lead_vel * 0.95)
        else:
            section_a(score, beat, lead, lead_vel)
        comp(score, beat, chords_full, comp_instr, 0.42, pattern)
        walk(score, beat, chords_full, two_feel=(sec == 0 and two_feel_first))
        if drums:
            brushes(score, beat, 8, 0.55 if sec != 2 else 0.65, ride_too=(sec == 2))
        if pad:
            for bar, (root, qual) in enumerate(chords_full):
                for tone in chord_tones(root, qual, 3)[:3]:
                    score.note(beat + bar * 4, 3.9, "strings", tone + 12, 0.5, -0.4)
        if counter and sec in (1, 3):
            for bar, (root, qual) in enumerate(chords_full):
                tones = chord_tones(root, qual, 4)
                score.note(beat + bar * 4 + 2, 1.5, counter, tones[2], 0.4, 0.45)
                score.note(beat + bar * 4 + 3.5, 0.5, counter, tones[1], 0.35, 0.45)
        if celesta_fills and sec != 2:
            for bar in (3, 7):
                for k, iv in enumerate((0, 7, 12, 16)):
                    score.note(beat + bar * 4 + 2 + k * 0.5, 1.0, "celesta", midi("D5") + iv, 0.35, 0.5)
        beat += 32
    return beat


def track_day(bpm, **kw):
    sc = Score(bpm)
    total = form(sc, **kw)
    return sc.render(total, room=0.32)


def loop_cut(x, bpm, beats):
    """Trim to an exact number of beats and fold the reverb tail back over the start for a seamless loop."""
    n = int(beats * 60.0 / bpm * SR)
    if len(x) <= n:
        return x
    body = x[:n].copy()
    tail = x[n:]
    k = min(len(tail), n)
    body[:k] += tail[:k]
    return body


def title():
    sc = Score(66, swing=0.5)
    b = 0
    for rep in range(2):
        for beat, dur, name in MOTIF:
            sc.note(b + beat, dur, "musicbox", midi(name) + 12, 0.7, 0.1)
        for bar, (root, qual) in enumerate(CHORDS_A[:2]):
            tones = chord_tones(root, qual, 3)
            for k, tt in enumerate(tones[:4]):
                sc.note(b + bar * 4 + k * 0.75, 2.5, "celesta", tt + 12, 0.25, -0.3)
            sc.note(b + bar * 4, 3.9, "strings", tones[0] + 12, 0.45, 0)
            sc.note(b + bar * 4, 3.9, "strings", tones[2] + 12, 0.4, 0)
        b += 8
        for beat, dur, name in PHRASE_A4:
            sc.note(b + beat - 24, dur, "musicbox", midi(name) + 12, 0.65, 0.1)
        for bar, (root, qual) in enumerate(CHORDS_A[6:8]):
            tones = chord_tones(root, qual, 3)
            sc.note(b + bar * 4, 3.9, "strings", tones[0] + 12, 0.45, 0)
            sc.note(b + bar * 4, 3.9, "strings", tones[1] + 12, 0.4, 0)
        b += 8
    x = sc.render(b, room=0.5)
    return loop_cut(x, 66, b)


def ledger():
    sc = Score(80)
    cad = [("G", "m7"), ("A", "7b9"), ("D", "m9"), ("D", "m9")]
    comp(sc, 0, cad, "piano", 0.45, "arp")
    for beat, dur, name in [(0, 1, "D5"), (1, 1, "C5"), (2, 1, "Bb4"), (3, 1, "A4"), (4, 1.5, "C#5"), (5.5, 0.5, "E5"), (6, 2, "G5"), (8, 6, "F5")]:
        sc.note(beat, dur, "piano", midi(name), 0.6, 0.15)
    sc.note(8, 6, "celesta", midi("A5"), 0.4, 0.4)
    walk(sc, 0, cad, 0.7, two_feel=True)
    return sc.render(16, room=0.45, tail=3.5)


def uncanny():
    sc = Score(60, swing=0.5)
    for bar in range(10):
        b = bar * 4
        sc.note(b, 4.0, "strings", midi("D3"), 0.5, -0.3)
        sc.note(b, 4.0, "strings", midi("A3"), 0.4, 0.3)
        if bar % 2 == 0:
            frag = MOTIF[bar // 2 % 3 * 3: bar // 2 % 3 * 3 + 3]
            for beat, dur, name in frag:
                sc.note(b + beat % 4, 2.0, "celesta", midi(name) + 12, 0.45, 0.4)
        if bar % 3 == 1:
            sc.note(b + 2, 2.0, "celesta", midi("Eb6"), 0.25, -0.5)
    x = sc.render(40, room=0.6)
    return loop_cut(x, 60, 40)


def ring():
    sc = Score(56, swing=0.5)
    prog = [("D", "m9"), ("Bb", "maj7"), ("G", "m7"), ("A", "7sus"), ("F", "maj7"), ("C", "7"), ("D", "m9"), ("D", "m9")]
    for bar, (root, qual) in enumerate(prog):
        for tone in chord_tones(root, qual, 3)[:4]:
            sc.note(bar * 4, 4.0, "strings", tone + 12, 0.55 + bar * 0.04, 0)
        sc.note(bar * 4, 4.0, "bass", midi(root + "2") if midi(root + "2") >= midi("E2") else midi(root + "3"), 0.5)
    for beat, dur, name in MOTIF:
        sc.note(8 + beat, dur * 1.2, "musicbox", midi(name) + 12, 0.6, 0.2)
    for beat, dur, name in PHRASE_A4:
        sc.note(beat - 24 + 18, dur * 1.2, "piano", midi(name), 0.55, 0.1)
    return sc.render(32, room=0.55, tail=5.0)


def finale():
    sc = Score(80)
    total = form(sc, lead="clarinet", comp_instr="piano", pattern="charleston", lead_vel=0.7, counter="celesta", pad=True)
    # a final held chord
    for tone in chord_tones("D", "m9", 3):
        sc.note(total, 8, "strings", tone + 12, 0.6, 0)
        sc.note(total, 6, "piano", tone + 12, 0.5, -0.2)
    sc.note(total, 8, "bass", midi("D2") + 12, 0.7)
    sc.note(total, 6, "musicbox", midi("A5"), 0.5, 0.3)
    return sc.render(total + 8, room=0.38, tail=6.0)


TRACKS = {
    "title": lambda: title(),
    "day1": lambda: loop_cut(track_day(84, lead="piano", comp_instr="epiano", pattern="two_four", lead_vel=0.62), 84, 128),
    "day2": lambda: loop_cut(track_day(92, lead="clarinet", comp_instr="piano", pattern="charleston", lead_vel=0.7), 92, 128),
    "day3": lambda: loop_cut(track_day(76, lead="celesta", comp_instr="epiano", pattern="whole", lead_vel=0.7, drums=False, celesta_fills=False, pad=True), 76, 128),
    "day4": lambda: loop_cut(track_day(72, lead="piano", comp_instr="piano", pattern="arp", lead_vel=0.6, drums=True, pad=True), 72, 128),
    "day5": lambda: loop_cut(track_day(84, lead="clarinet", comp_instr="piano", pattern="charleston", lead_vel=0.68, counter="celesta", pad=True), 84, 128),
    "ledger": ledger,
    "uncanny": uncanny,
    "ring": ring,
    "finale": finale,
}


def main():
    only = sys.argv[1:]
    for name, fn in TRACKS.items():
        if only and name not in only:
            continue
        S.seed(abs(hash(name)) % 1000 if False else sum(map(ord, name)))
        x = fn()
        write(os.path.join(OUT, name + ".wav"), normalize(x, 0.85))
        print("music", name, f"{len(x) / SR:.1f}s")


if __name__ == "__main__":
    main()
