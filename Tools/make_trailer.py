#!/usr/bin/env python3
"""Cut the feature trailer, the README stills and the teaser loop from filmed takes.

Film the takes first (each plays real days through simulated mouse and keyboard, without the score):

    Tools/unity.sh film day1 -lafUntil 1                 # the title, then Monday (and Tuesday's morning)
    Tools/unity.sh film day2 -lafDay 2                   # Tuesday ... one take per day
    Tools/unity.sh film day3 -lafDay 3
    Tools/unity.sh film day4 -lafDay 4
    Tools/unity.sh film day5 -lafDay 5                   # Friday and the ending
    Tools/unity.sh film grey -lafDay 4 -lafUntil 5 -lafVerdicts 4.2=return:vell,5.5=return:vell
                                                         # Mr Vell gets the ring and the key: Grey Ninefold

then:

    .venv/bin/python Tools/make_trailer.py [trailer] [stills] [teaser]    (default: all three)

Every cut is anchored to an event in Recordings/<take>/markers.tsv, so re-filmed takes keep their cuts
on the same moments. The score is laid in here from Assets/Game/Resources/Music, ducked under the
game's own sound effects and voices. Output: docs/media/.
"""
import json
import math
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1]
REC = ROOT / "Recordings"
WORK = REC / "_trailer"
OUT = ROOT / "docs" / "media"
FONTS = ROOT / "ArtSource" / "fonts"
MUSIC = ROOT / "Assets" / "Game" / "Resources" / "Music"
TEX = ROOT / "Assets" / "Game" / "Resources" / "Textures"
PHOTOS = ROOT / "Assets" / "Game" / "Resources" / "Photos"

W, H, FPS, SR = 1920, 1080, 30, 48000
URL = "github.com/nearbycoder/LostAndFound"

# the game's palette and type (docs/PLAN.md, section 7)
INK = (30, 36, 64)
INK_SOFT = (58, 51, 48)
OXBLOOD = (122, 34, 34)
BRASS = (201, 161, 90)
CREAM = (247, 232, 196)
MANILA = (232, 212, 168)
F_SIGN = FONTS / "Limelight-Regular.ttf"
F_TITLE = FONTS / "IMFeENrm28P.ttf"
F_TITLE_I = FONTS / "IMFeENit28P.ttf"
F_TYPE = FONTS / "SpecialElite-Regular.ttf"
F_HAND = FONTS / "Caveat[wght].ttf"


def run(cmd, **kw):
    r = subprocess.run(cmd, **kw)
    if r.returncode != 0:
        sys.exit(f"failed: {' '.join(map(str, cmd))[:400]}")
    return r


def ffmpeg(*args):
    run(["nice", "-n", "10", "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", *map(str, args)])


# ----------------------------------------------------------------------------------------------- takes

class Take:
    def __init__(self, name):
        self.name = name
        self.dir = REC / name
        self.path = self.dir / "take.mkv"
        if not self.path.exists():
            sys.exit(f"missing take {self.path}: film it first (see the docstring)")
        self.events = []
        for line in (self.dir / "markers.tsv").read_text().splitlines():
            frame, _, ev = line.split("\t", 2)
            self.events.append((int(frame) / FPS, ev))

    def at(self, prefix, nth=0, after=0.0):
        hits = [t for t, ev in self.events if ev.startswith(prefix) and t >= after]
        if len(hits) <= nth:
            sys.exit(f"{self.name}: no marker '{prefix}' (#{nth}) after {after:.1f}s")
        return hits[nth]


TAKES = {}


def take(name):
    if name not in TAKES:
        TAKES[name] = Take(name)
    return TAKES[name]


# ----------------------------------------------------------------------------------------------- type and paper

def save_png(img, path):
    """Write a PNG only if it changed, so cached shot renders that use it stay valid."""
    import io
    buf = io.BytesIO()
    img.save(buf, "PNG")
    path = Path(path)
    if not path.exists() or path.read_bytes() != buf.getvalue():
        path.write_bytes(buf.getvalue())


def font(path, size, weight=None):
    f = ImageFont.truetype(str(path), size)
    if weight:
        f.set_variation_by_axes([weight])
    return f


def paper(size, base, grain=0.18):
    """A sheet of the game's paper texture, tinted."""
    tex = Image.open(TEX / "paper_a.png").convert("L")
    w, h = size
    tiled = Image.new("L", (w, h))
    for y in range(0, h, tex.height):
        for x in range(0, w, tex.width):
            tiled.paste(tex, (x, y))
    a = np.asarray(tiled, np.float32) / 255.0
    a = 1.0 - grain + grain * (a / max(a.mean(), 1e-3))
    rgb = np.clip(np.array(base, np.float32)[None, None, :] * a[..., None], 0, 255).astype(np.uint8)
    return Image.fromarray(rgb, "RGB").convert("RGBA")


def shadowed(img, offset=(8, 12), blur=14, opacity=0.5, pad=40):
    """img (RGBA) with a soft drop shadow, padded."""
    w, h = img.size
    out = Image.new("RGBA", (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    sh = Image.new("RGBA", out.size, (0, 0, 0, 0))
    alpha = img.split()[3].point(lambda v: int(v * opacity))
    sh.paste((10, 8, 6, 255), (pad + offset[0], pad + offset[1]), alpha)
    sh = sh.filter(ImageFilter.GaussianBlur(blur))
    out.alpha_composite(sh)
    out.alpha_composite(img, (pad, pad))
    return out


def text_w(f, s):
    return f.getbbox(s)[2] - f.getbbox(s)[0]


def wrap(f, text, width):
    """Greedy word wrap to a pixel width."""
    lines, cur = [], ""
    for w in text.split():
        t = (cur + " " + w).strip()
        if cur and text_w(f, t) > width:
            lines.append(cur)
            cur = w
        else:
            cur = t
    return lines + ([cur] if cur else [])


def luggage_tag(head, sub, number, text_max=500):
    """A manila luggage tag: the caption plate for each feature beat. It stays narrow (the text column is
    at most text_max px) so that, in the top-left corner, it clears the claimant's face and dialogue."""
    S = 2  # supersample
    size = 66
    while size > 50 and text_w(font(F_TITLE, size), head) > text_max:
        size -= 2
    fh, fs, fn = font(F_TITLE, size * S), font(F_TYPE, 27 * S), font(F_TYPE, 22 * S)
    subs = wrap(fs, sub, text_max * S) if sub else []
    cut, eyelet = 52 * S, 136 * S
    tw = max([text_w(fh, head)] + [text_w(fs, x) for x in subs])
    w = tw + eyelet + 70 * S
    head_h = int(size * 1.25) * S
    h = 30 * S + head_h + (len(subs) * 36 * S + 14 * S if subs else 0) + 26 * S
    tag = paper((w, h), MANILA)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).polygon([(cut, 0), (w, 0), (w, h), (cut, h), (0, h - cut), (0, cut)], fill=255)
    d = ImageDraw.Draw(tag)
    # a red rule, like the game's tags
    d.line([(eyelet - 26 * S, 22 * S), (eyelet - 26 * S, h - 22 * S)], fill=(176, 70, 52), width=2 * S)
    # the brass eyelet and its hole
    cx, cy, r = 58 * S, h // 2, 27 * S
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=BRASS)
    d.ellipse([cx - r + 5 * S, cy - r + 5 * S, cx + r - 5 * S, cy + r - 5 * S], fill=(150, 112, 52))
    d.ellipse([cx - 12 * S, cy - 12 * S, cx + 12 * S, cy + 12 * S], fill=(24, 20, 18))
    # text
    x, y = eyelet, 24 * S
    d.text((x, y), head, font=fh, fill=INK)
    y += head_h + 10 * S
    for line in subs:
        d.text((x + 2 * S, y), line, font=fs, fill=INK_SOFT)
        y += 36 * S
    if number:
        n = f"No. {number:02d}"
        d.text((w - text_w(fn, n) - 22 * S, 10 * S), n, font=fn, fill=OXBLOOD)
    tag.putalpha(mask)
    # the string through the eyelet, trailing off to the left
    canvas = Image.new("RGBA", (w + 400 * S, h + 60 * S), (0, 0, 0, 0))
    canvas.alpha_composite(tag, (400 * S, 30 * S))
    dc = ImageDraw.Draw(canvas)
    sx, sy = 400 * S + cx, 30 * S + cy
    curve = [(sx - k * 400 * S / 20, sy + 26 * S * math.sin(k / 20 * math.pi) + k * 1.2 * S) for k in range(21)]
    dc.line(curve, fill=(150, 40, 36), width=4 * S, joint="curve")
    canvas = canvas.resize((canvas.width // S, canvas.height // S), Image.LANCZOS)
    canvas = canvas.rotate(1.2, resample=Image.BICUBIC, expand=True)
    return shadowed(canvas)


def caption_frame(head, sub, number, path, corner="tl"):
    """A full 1920x1080 transparent frame with the tag at top left ("tl") or lower left ("bl"); its
    string runs off screen. The game's own hints sit bottom centre and dialogue top right."""
    tag = luggage_tag(head, sub, number)
    frame = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    frame.alpha_composite(tag, (-330, 6 if corner == "tl" else H - tag.height - 130))
    save_png(frame, path)


def stamp_frame(kind, path, angle=-8):
    """A verdict stamp imprint (the game's own stamp texture), inked in its colour, for the stamps beat."""
    # the game's ink colours, the iron one lifted so it reads over a dark desk
    col = {"return": (47, 107, 58), "refuse": (122, 34, 34), "seal": (150, 160, 182)}[kind]
    m = Image.open(TEX / f"stamp_{kind}.png").getchannel("A")
    ink = Image.new("RGBA", m.size, col + (255,))
    ink.putalpha(m.point(lambda v: int(v * 0.92)))
    ink = ink.resize((int(m.width * 0.9), int(m.height * 0.9)), Image.LANCZOS).rotate(angle, Image.BICUBIC, expand=True)
    frame = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    frame.alpha_composite(ink, ((W - ink.width) // 2, (H - ink.height) // 2))
    save_png(frame, path)


def centered_text(lines, path, shade=0.42):
    """Big centred intertitle lines: [(text, fontpath, size, colour, gap_after)]."""
    frame = Image.new("RGBA", (W, H), (0, 0, 0, int(255 * shade)))
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    total = sum(sz + gap for _, _, sz, _, gap in lines)
    y = (H - total) // 2
    for text, fp, sz, col, gap in lines:
        f = font(fp, sz)
        bb = d.textbbox((0, 0), text, font=f)
        d.text(((W - (bb[2] - bb[0])) // 2 - bb[0], y), text, font=f, fill=col)
        y += sz + gap
    glow = layer.filter(ImageFilter.GaussianBlur(10))
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    shadow.paste((0, 0, 0, 255), (0, 0), layer.split()[3])
    shadow = shadow.filter(ImageFilter.GaussianBlur(14))
    frame.alpha_composite(shadow)
    frame.alpha_composite(shadow)
    frame.alpha_composite(Image.blend(Image.new("RGBA", (W, H), (0, 0, 0, 0)), glow, 0.35))
    frame.alpha_composite(layer)
    save_png(frame, path)


def logo_frame(path, tagline=None, url=None):
    """The title: LOST & FOUND in the station-sign face, with the game's subtitle."""
    S = 2
    layer = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    f = font(F_SIGN, 190 * S)
    title = "Lost & Found"
    bb = d.textbbox((0, 0), title, font=f)
    tw = bb[2] - bb[0]
    y0 = (H * S) // 2 - (230 if tagline else 170) * S
    d.text(((W * S - tw) // 2 - bb[0], y0), title, font=f, fill=CREAM)
    rule_y = y0 + 250 * S
    d.line([((W * S) // 2 - 330 * S, rule_y), ((W * S) // 2 + 330 * S, rule_y)], fill=BRASS, width=3 * S)
    for dx in (-342, 342):
        cx = (W * S) // 2 + dx * S
        d.polygon([(cx - 9 * S, rule_y), (cx, rule_y - 9 * S), (cx + 9 * S, rule_y), (cx, rule_y + 9 * S)], fill=BRASS)
    fs = font(F_TITLE_I, 46 * S)
    sub = "The Lost Property Office  ·  Ninefold Junction  ·  October 1962"
    sb = d.textbbox((0, 0), sub, font=fs)
    d.text(((W * S - (sb[2] - sb[0])) // 2 - sb[0], rule_y + 30 * S), sub, font=fs, fill=(226, 208, 176))
    y = rule_y + 130 * S
    if tagline:
        ft = font(F_TITLE, 60 * S)
        tb = d.textbbox((0, 0), tagline, font=ft)
        d.text(((W * S - (tb[2] - tb[0])) // 2 - tb[0], y), tagline, font=ft, fill=CREAM)
        y += 120 * S
    if url:
        fu = font(F_TYPE, 40 * S)
        ub = d.textbbox((0, 0), url, font=fu)
        d.text(((W * S - (ub[2] - ub[0])) // 2 - ub[0], y), url, font=fu, fill=BRASS)
    layer = layer.resize((W, H), Image.LANCZOS)
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    glow.paste((255, 196, 120, 255), (0, 0), layer.split()[3].point(lambda v: int(v * 0.5)))
    glow = glow.filter(ImageFilter.GaussianBlur(18))
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    shadow.paste((0, 0, 0, 255), (0, 0), layer.split()[3])
    shadow = shadow.filter(ImageFilter.GaussianBlur(8))
    frame = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    frame.alpha_composite(shadow)
    frame.alpha_composite(glow)
    frame.alpha_composite(layer)
    save_png(frame, path)


def handwritten(text, path, y=0.80, size=78, x=None):
    """A line in the clerk's handwriting (the cold open): centred, or from x if given."""
    centered = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(centered)
    f = font(F_HAND, size, weight=600)
    bb = d.textbbox((0, 0), text, font=f)
    x = (W - (bb[2] - bb[0])) // 2 - bb[0] if x is None else x
    yy = int(H * y) - (bb[3] - bb[1]) // 2
    sh = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(sh).text((x + 3, yy + 4), text, font=f, fill=(0, 0, 0, 230))
    centered.alpha_composite(sh.filter(ImageFilter.GaussianBlur(6)))
    d.text((x, yy), text, font=f, fill=CREAM)
    save_png(centered, path)


# ----------------------------------------------------------------------------------------------- shots

def clip(take_name, start, dur, overlays=(), speed=1.0, xf=0.4, trans="fade", gain=1.0, zoom=0.0, label=""):
    """A piece of a take. start is seconds into the take (use take(...).at(marker) + offset)."""
    return dict(kind="clip", take=take_name, start=start, dur=dur, overlays=list(overlays), speed=speed,
                xf=xf, trans=trans, gain=gain, zoom=zoom, label=label)


def card(dur, bg, overlays=(), xf=0.6, trans="fade", label=""):
    """A title or end card: overlays on a blurred, darkened still from a take (bg=(take, seconds))."""
    return dict(kind="card", bg=bg, dur=dur, overlays=list(overlays), xf=xf, trans=trans, label=label)


def ov(png, t_in=0.35, t_out=None, fade_in=0.45, fade_out=0.35, slide=70, pop=False):
    return dict(png=png, t_in=t_in, t_out=t_out, fade_in=fade_in, fade_out=fade_out, slide=slide, pop=pop)


def overlay_filters(seg, first_input):
    """filter_complex parts that animate each overlay PNG (slide in and fade, or a stamp's thump)."""
    parts, last = [], "base"
    for k, o in enumerate(seg["overlays"]):
        idx = first_input + k
        t_out = o["t_out"] if o["t_out"] is not None else seg["dur"] - o["fade_out"] - 0.15
        chain = f"[{idx}:v]format=rgba,fade=t=in:st={o['t_in']:.3f}:d={o['fade_in']:.3f}:alpha=1"
        if t_out < seg["dur"]:
            chain += f",fade=t=out:st={t_out:.3f}:d={o['fade_out']:.3f}:alpha=1"
        if o["pop"]:
            # a stamp: arrives big and lands with a thump
            k0 = f"min(1,max(0,(t-{o['t_in']:.3f})/0.16))"
            chain += f",scale=w='iw*(1.35-0.35*{k0})':h='ih*(1.35-0.35*{k0})':eval=frame"
            pos = "x='(W-w)/2':y='(H-h)/2'"
        else:
            k0 = f"min(1,max(0,(t-{o['t_in']:.3f})/0.55))"
            pos = f"x='-{o['slide']}*pow(1-{k0},3)':y=0"
        parts.append(chain + f"[o{k}]")
        parts.append(f"[{last}][o{k}]overlay={pos}:eval=frame:format=auto[l{k}]")
        last = f"l{k}"
    return parts, last


def render_segment(i, seg):
    out = WORK / f"seg_{i:02d}.mp4"
    # reuse the render if nothing about the shot (or its take or overlays) has changed
    deps = [take(seg["take"]).path] if seg["kind"] == "clip" else [take(seg["bg"][0]).path]
    deps += [Path(o["png"]) for o in seg["overlays"]]
    key = json.dumps([seg, [(str(p), p.stat().st_mtime) for p in deps]], sort_keys=True, default=str)
    stamp = out.with_suffix(".json")
    if out.exists() and stamp.exists() and stamp.read_text() == key:
        return out
    inputs, fc = [], []
    dur = seg["dur"]
    if seg["kind"] == "clip":
        tk = take(seg["take"])
        inputs += ["-ss", f"{seg['start']:.3f}", "-t", f"{dur * seg['speed'] + 0.1:.3f}", "-i", tk.path]
        v = f"[0:v]setpts=(PTS-STARTPTS)/{seg['speed']},fps={FPS},scale={W}:{H}"
        if seg["zoom"]:
            z = seg["zoom"]
            v += f",scale=w='{W}*(1+{z}*t/{dur})':h=-2:eval=frame,crop={W}:{H}"
        fc.append(v + ",format=yuv420p[base]")
    else:
        tk, t = seg["bg"]
        inputs += ["-ss", f"{t:.3f}", "-i", take(tk).path]
        fc.append(f"[0:v]trim=end_frame=1,setpts=PTS-STARTPTS,scale={W}:{H},gblur=sigma=22,eq=brightness=-0.09:saturation=0.8,"
                  f"loop=loop={int(dur * FPS) + 5}:size=1:start=0,fps={FPS},"
                  f"scale=w='{W}*(1.04+0.04*t/{dur})':h=-2:eval=frame,crop={W}:{H},"
                  f"vignette=PI/3.2,format=yuv420p[base]")
    for o in seg["overlays"]:
        inputs += ["-loop", "1", "-t", f"{dur + 0.1:.3f}", "-i", o["png"]]
    parts, last = overlay_filters(seg, 1)
    fc += parts
    fc.append(f"[{last}]trim=duration={dur:.3f},setpts=PTS-STARTPTS,format=yuv420p[v]")
    ffmpeg(*inputs, "-filter_complex", ";".join(fc), "-map", "[v]", "-an", "-r", FPS,
           "-c:v", "libx264", "-preset", "veryfast", "-crf", "12", out)
    stamp.write_text(key)
    return out


def segment_audio(seg):
    n = int(round(seg["dur"] * SR))
    if seg["kind"] != "clip":
        return np.zeros((n, 2), np.float32)
    tk = take(seg["take"])
    af = f"atempo={seg['speed']}" if seg["speed"] != 1.0 else "anull"
    r = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-ss", f"{seg['start']:.3f}",
                        "-t", f"{seg['dur'] * seg['speed'] + 0.2:.3f}", "-i", str(tk.path), "-vn", "-af", af,
                        "-ac", "2", "-ar", str(SR), "-f", "f32le", "-"], capture_output=True, check=True)
    a = np.frombuffer(r.stdout, np.float32).reshape(-1, 2)
    if len(a) < n:
        a = np.concatenate([a, np.zeros((n - len(a), 2), np.float32)])
    return a[:n] * seg["gain"]


# ----------------------------------------------------------------------------------------------- music

def load_music(name):
    r = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-i", str(MUSIC / f"{name}.wav"),
                        "-ac", "2", "-ar", str(SR), "-f", "f32le", "-"], capture_output=True, check=True)
    return np.frombuffer(r.stdout, np.float32).reshape(-1, 2).copy()


def music_bed(total, cues):
    """cues: (track, src_seconds, at_seconds, until_seconds, fade_in, fade_out, gain)."""
    bed = np.zeros((int(total * SR) + SR, 2), np.float32)
    cache = {}
    for track, src, at, until, fin, fout, gain in cues:
        until = min(until, total)
        if until <= at:
            continue
        if track not in cache:
            cache[track] = load_music(track)
        m = cache[track]
        n = int((until - at) * SR)
        s0 = int(src * SR)
        piece = m[s0:s0 + n]
        if len(piece) < n:  # loop the track if the cue outlasts it
            reps = int(math.ceil(n / max(1, len(m))) + 1)
            piece = np.concatenate([m[s0:]] + [m] * reps)[:n]
        env = np.ones(n, np.float32)
        a, b = int(fin * SR), int(fout * SR)
        if a > 0:
            env[:a] = np.sin(np.linspace(0, math.pi / 2, a)) ** 2
        if b > 0:
            env[-b:] *= np.cos(np.linspace(0, math.pi / 2, b)) ** 2
        i0 = int(at * SR)
        bed[i0:i0 + n] += piece * (env * gain)[:, None]
    return bed[: int(total * SR)]


def envelope(x, attack=0.008, release=0.35):
    """A peak follower on a mono signal (for ducking)."""
    mono = np.abs(x).max(axis=1)
    # 5 ms blocks keep this fast in numpy
    blk = int(SR * 0.005)
    nb = len(mono) // blk + 1
    padded = np.concatenate([mono, np.zeros(nb * blk - len(mono), np.float32)])
    peaks = padded.reshape(nb, blk).max(axis=1)
    out = np.zeros(nb, np.float32)
    ka, kr = math.exp(-0.005 / attack), math.exp(-0.005 / release)
    e = 0.0
    for i, p in enumerate(peaks):
        k = ka if p > e else kr
        e = k * e + (1 - k) * p
        out[i] = e
    return np.repeat(out, blk)[: len(mono)]


def mix(game, bed, duck_db=9.0, music_gain=0.55):
    env = envelope(game)
    db = 20 * np.log10(np.maximum(env, 1e-5))
    # duck the music as the game gets loud (from -36 dBFS, fully by -14)
    amt = np.clip((db + 36.0) / 22.0, 0.0, 1.0)
    duck = 10 ** (-duck_db * amt / 20.0)
    out = game + bed * (duck * music_gain)[:, None]
    # loudness: aim for about -16 dB RMS with -1 dBFS peaks (a gentle tanh limiter takes the rest)
    rms = math.sqrt(float(np.mean(out ** 2)) + 1e-12)
    out *= 10 ** (-16.0 / 20.0) / rms
    ceiling = 10 ** (-1.0 / 20.0)
    out = np.where(np.abs(out) > 0.7 * ceiling,
                   np.sign(out) * (0.7 * ceiling + 0.3 * ceiling * np.tanh((np.abs(out) - 0.7 * ceiling) / (0.3 * ceiling))),
                   out)
    return out.astype(np.float32)


# ----------------------------------------------------------------------------------------------- assembly

def assemble(segs, name, cues, target_mb=37.0):
    """cues is a list of music cues, or a function (starts, total) -> cues, so the score can follow
    the cut (see music_bed)."""
    WORK.mkdir(parents=True, exist_ok=True)
    starts, t = [], 0.0
    for k, s in enumerate(segs):
        if k > 0:
            t -= s["xf"]
        starts.append(t)
        t += s["dur"]
    total = t
    print(f"{name}: {len(segs)} shots, {total:.1f}s")
    files = []
    for k, s in enumerate(segs):
        print(f"  [{k:02d}] {starts[k]:6.2f}s  {s['dur']:5.2f}s  {s.get('label', '')}")
        files.append(render_segment(k, s))

    # video: one xfade chain
    inputs, fc, last = [], [], "0:v"
    for f in files:
        inputs += ["-i", f]
    for k in range(1, len(segs)):
        xf = max(segs[k]["xf"], 1.0 / FPS)
        fc.append(f"[{last}][{k}:v]xfade=transition={segs[k]['trans']}:duration={xf:.3f}:offset={starts[k]:.3f}[x{k}]")
        last = f"x{k}"
    joined = WORK / f"{name}_video.mp4"
    ffmpeg(*inputs, "-filter_complex", ";".join(fc) if fc else "null", "-map", f"[{last}]" if fc else "0:v",
           "-c:v", "libx264", "-preset", "veryfast", "-crf", "12", "-r", FPS, joined)

    # audio: the takes' own sound, crossfaded on the same timeline, then the score ducked beneath it
    game = np.zeros((int(total * SR) + SR, 2), np.float32)
    for k, s in enumerate(segs):
        a = segment_audio(s)
        n = len(a)
        env = np.ones(n, np.float32)
        fi = int(max(s["xf"], 0.03) * SR) if k > 0 else int(0.05 * SR)
        fo = int(max(segs[k + 1]["xf"], 0.03) * SR) if k + 1 < len(segs) else int(0.3 * SR)
        env[:fi] = np.sin(np.linspace(0, math.pi / 2, fi)) ** 2
        env[-fo:] *= np.cos(np.linspace(0, math.pi / 2, fo)) ** 2
        i0 = int(starts[k] * SR)
        game[i0:i0 + n] += a * env[:, None]
    game = game[: int(total * SR)]
    bed = music_bed(total, cues(starts, total) if callable(cues) else cues)
    audio = mix(game, bed)
    wav = WORK / f"{name}_mix.f32"
    audio.tofile(wav)

    out = OUT / f"{name}.mp4"
    OUT.mkdir(parents=True, exist_ok=True)
    # two-pass to a size budget; a light temporal denoise takes the film grain out of the bitrate bill
    audio_kbps = 192
    target_kbps = int(target_mb * 8e6 / 1000 / total) - audio_kbps
    vf = f"hqdn3d=1.2:1.0:4:4,fade=t=out:st={total - 1.2:.3f}:d=1.2,format=yuv420p"
    common = ["-i", joined, "-vf", vf, "-c:v", "libx264", "-preset", "slower", "-profile:v", "high",
              "-b:v", f"{target_kbps}k", "-maxrate", f"{int(target_kbps * 1.8)}k", "-bufsize", f"{target_kbps * 3}k",
              "-g", 60, "-passlogfile", WORK / f"{name}_2pass"]
    ffmpeg(*common, "-pass", 1, "-an", "-f", "null", "/dev/null")
    ffmpeg(*common[:1], joined, "-f", "f32le", "-ar", SR, "-ac", 2, "-i", wav, *common[2:], "-pass", 2,
           "-map", "0:v", "-map", "1:a", "-c:a", "aac", "-b:a", f"{audio_kbps}k", "-movflags", "+faststart", "-shortest", out)
    print(f"  encoded two-pass at {target_kbps} kb/s: {out.stat().st_size / 1e6:.1f} MB")
    json.dump({"total": total, "shots": [{"at": round(starts[k], 2), "dur": s["dur"], "label": s.get("label", "")}
                                         for k, s in enumerate(segs)]},
              open(WORK / f"{name}_timeline.json", "w"), indent=1)
    return out, starts, total


# ----------------------------------------------------------------------------------------------- stills and teaser

def still(take_name, t, name, quality=95):
    """One 1920x1080 frame of a take, saved as a high-quality JPEG for the README."""
    png = WORK / f"still_{name}.png"
    ffmpeg("-ss", f"{t:.3f}", "-i", take(take_name).path, "-frames:v", 1, png)
    im = Image.open(png).convert("RGB")
    out = OUT / f"{name}.jpg"
    q = quality
    while True:
        im.save(out, quality=q, optimize=True, progressive=True)
        if out.stat().st_size <= 1_400_000 or q <= 70:
            break
        q -= 4
    print(f"  {out.relative_to(ROOT)}  {out.stat().st_size / 1e6:.2f} MB  (q{q})")
    return out


def poster(src, out):
    """The trailer's poster frame with a play button, for the README (GitHub won't play the MP4 inline)."""
    im = Image.open(src).convert("RGBA")
    shade = Image.new("RGBA", im.size, (0, 0, 0, 70))
    im.alpha_composite(shade)
    S = 4
    r = 92
    btn = Image.new("RGBA", (r * 2 * S + 40 * S, r * 2 * S + 40 * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(btn)
    c = btn.width // 2
    d.ellipse([c - r * S, c - r * S, c + r * S, c + r * S], fill=(20, 18, 16, 170), outline=CREAM + (255,), width=6 * S)
    tri = [(c - 28 * S, c - 44 * S), (c - 28 * S, c + 44 * S), (c + 48 * S, c)]
    d.polygon(tri, fill=CREAM + (255,))
    btn = btn.resize((btn.width // S, btn.height // S), Image.LANCZOS)
    im.alpha_composite(btn, ((im.width - btn.width) // 2, (im.height - btn.height) // 2))
    f = font(F_TYPE, 34)
    label = "WATCH THE TRAILER"
    d = ImageDraw.Draw(im)
    tw = text_w(f, label)
    y = im.height // 2 + r + 34
    d.text(((im.width - tw) // 2 + 2, y + 3), label, font=f, fill=(0, 0, 0, 200))
    d.text(((im.width - tw) // 2, y), label, font=f, fill=CREAM)
    im.convert("RGB").save(out, quality=88, optimize=True, progressive=True)
    print(f"  {out.relative_to(ROOT)}  {out.stat().st_size / 1e6:.2f} MB")


def teaser(shots, name="teaser", width=1280, fps=20, xf=0.5):
    """A short silent loop for the top of the README: shots crossfade, and the end fades back into the start."""
    segs = [clip(t, s, d, xf=xf) for t, s, d in shots]
    files = [render_segment(90 + k, s) for k, s in enumerate(segs)]
    starts, t = [], 0.0
    for k, s in enumerate(segs):
        if k:
            t -= xf
        starts.append(t)
        t += s["dur"]
    total = t
    inputs, fc, last = [], [], "0:v"
    for f in files:
        inputs += ["-i", f]
    for k in range(1, len(files)):
        fc.append(f"[{last}][{k}:v]xfade=transition=fade:duration={xf}:offset={starts[k]:.3f}[x{k}]")
        last = f"x{k}"
    # loop seam: the last half second dissolves into the first
    fc.append(f"[{last}]split[a][b];[a]trim=start={xf}:end={total - xf},setpts=PTS-STARTPTS[body];"
              f"[b]split[c][d];[c]trim=end={xf},setpts=PTS-STARTPTS[head];[d]trim=start={total - xf},setpts=PTS-STARTPTS[tail];"
              f"[tail][head]xfade=transition=fade:duration={xf}:offset=0[seam];[body][seam]concat=n=2:v=1:a=0,"
              f"fps={fps},scale={width}:-2:flags=lanczos[v]")
    loop_mp4 = WORK / f"{name}_loop.mp4"
    ffmpeg(*inputs, "-filter_complex", ";".join(fc), "-map", "[v]", "-c:v", "libx264", "-crf", "14", "-preset", "veryfast", loop_mp4)
    out = OUT / f"{name}.webp"
    OUT.mkdir(parents=True, exist_ok=True)
    for q in (90, 84, 78, 70, 62):
        ffmpeg("-i", loop_mp4, "-c:v", "libwebp_anim", "-quality", q, "-compression_level", 6, "-loop", 0, "-an", out)
        if out.stat().st_size <= 7_500_000:
            break
    print(f"  {out.relative_to(ROOT)}  {out.stat().st_size / 1e6:.2f} MB, {total - xf:.1f}s loop")
    return out


# ----------------------------------------------------------------------------------------------- the trailer

def caps():
    """Render every caption, card and stamp the trailer uses into WORK; returns {name: path}."""
    WORK.mkdir(parents=True, exist_ok=True)
    c = {}

    def tag(key, head, sub, n, corner="tl"):
        c[key] = WORK / f"cap_{key}.png"
        caption_frame(head, sub, n, c[key], corner)

    tag("desk", "Run the Lost Property desk", "NINEFOLD JUNCTION · OCTOBER 1962", 1)
    tag("listen", "Listen to every claim", "WHAT THEY TELL YOU IS WRITTEN ON THE CLAIM SLIP", 2)
    tag("search", "Search the drawers", "AND THE SHELF. EVERY STRAY HAS A TAG: WHERE, WHEN, WHICH TRAIN", 3)
    tag("turn", "Turn it over", "IN YOUR HANDS. OPEN LIDS, LATCHES AND CLASPS", 4)
    tag("find", "Find what's hidden", "EVERY DISCOVERY GOES ON THE SLIP", 5)
    tag("liars", "Catch the liars", "THEY ONLY KNOW WHAT THEY COULD SEE", 6)
    tag("two", "Two claim one?", "LET THE OBJECT DECIDE", 7, "bl")
    tag("stamp", "Stamp the claim", "RETURN  ·  REFUSE  ·  SEAL", 8)
    tag("hum", "If it hums, it's home", "SOME THINGS KNOW THEIR OWNERS", 9)
    tag("vell", "The Grey Gentleman", "GETS NOTHING. HE KNOWS EVERY DETAIL, BUT NOTHING HUMS FOR HIM", 11)
    tag("tomorrow", "Dated tomorrow?", "THEN IT ISN'T LOST YET: SEAL IT IN THE IRON DRAWER", 12)
    tag("frost", "Frost on the glass", "THEY'VE GONE ON AHEAD. COLD THINGS GO ONLY TO THE COLD", 13)
    tag("lamp", "Agnes's blue lamp", "SHOWS WHAT INK TRIES TO HIDE", 14)
    tag("rules", "A new rule each morning", "IN THE HAND OF AGNES, WHO RAN THIS DESK FOR 41 YEARS", 10)
    tag("ledger", "The Day Ledger", "EVERY EVENING, AND THE GAZETTE REPORTS WHAT YOU CHANGED", 15, "bl")
    tag("grey", "Choices carry", "THROUGH THE WEEK. GIVE MR VELL WHAT HE WANTS AND THE STATION GREYS", 16)
    for k in ("return", "refuse", "seal"):
        c["stamp_" + k] = WORK / f"stamp_{k}.png"
        stamp_frame(k, c["stamp_" + k], angle={"return": -7, "refuse": 5, "seal": -3}[k])
    c["logo"] = WORK / "logo.png"
    logo_frame(c["logo"])
    c["end"] = WORK / "end.png"
    logo_frame(c["end"], tagline="Some belongings should never be returned.", url=URL)
    c["ring1"] = WORK / "hand_ring1.png"
    handwritten("You return a wedding ring...", c["ring1"], y=0.12, x=90)
    c["ring2"] = WORK / "hand_ring2.png"
    handwritten("...and every photograph on your desk changes.", c["ring2"])
    for key, text in (("five", "Five days."), ("objects", "Twenty-five objects."), ("cast", "Twenty-five faces at the window."),
                      ("endings", "Three endings.")):
        c[key] = WORK / f"inter_{key}.png"
        centered_text([(text, F_TITLE, 132, CREAM, 0)], c[key])
    return c


def cold_open(c):
    """Thursday's last case: Thomas, the ring, and the photographs changing."""
    d4 = take("day4")
    ring = d4.at("pickup ring_box", after=d4.at("arrive 4.5"))
    stamp = d4.at("stamp 4.5")
    photos = d4.at("photos-begin")
    S = [
        clip("day4", ring + 0.1, 3.4, [ov(c["ring1"], t_in=0.5, slide=0, fade_in=0.8)], xf=0, label="Thomas: is that it?"),
        clip("day4", stamp - 1.0, 1.5, xf=0.2, label="RETURN"),
        clip("day4", stamp + 3.6, 3.1, xf=0.3, label="Thomas: not any longer"),
        clip("day4", photos + 2.8, 2.8, [ov(c["ring2"], t_in=0.3, slide=0, fade_in=0.7, t_out=99)], xf=0.5, label="photograph: 1921"),
        clip("day4", photos + 6.2, 2.6, [ov(c["ring2"], t_in=0.0, slide=0, fade_in=0.01, fade_out=0.6)], xf=0.35, label="photograph: 41 years"),
    ]
    return S


def mechanics(c):
    """Monday and Tuesday: how a case plays, beat by beat."""
    d1, d2 = take("day1"), take("day2")
    S = []
    S.append(clip("day1", d1.at("bell 1.1") - 0.2, 4.6, [ov(c["desk"])], label="desk: ring for Walter"))
    S.append(clip("day1", d1.at("arrive 1.1") + 7.8, 4.1, [ov(c["listen"])], label="listen: claims underlined"))
    S.append(clip("day1", d1.at("turn Cabinet") + 0.2, 5.0, [ov(c["search"])], label="search: drawer A, the tag"))
    S.append(clip("day1", d1.at("pickup wallet_brown") + 0.1, 5.3, [ov(c["turn"])], label="turn it over, open the flap"))
    S.append(clip("day1", d1.at("discover wallet_brown.photo") - 2.2, 4.0, [ov(c["find"])], label="find: Biscuit"))
    strip = d1.at("part umbrella_duck.hinge") + 0.3
    S.append(clip("day1", strip, 3.0, [ov(c["liars"])], label="liars: the name strip"))
    S.append(clip("day1", d1.at("ask namestrip") + 1.6, 4.7, xf=0.3, label="Reggie bluffs"))
    S.append(clip("day2", d2.at("arrive 2.2") + 15.2, 3.8, [ov(c["two"])], label="two claim one: the twins"))
    S.append(clip("day2", d2.at("discover locket.note") - 1.8, 3.4, xf=0.3, label="the note in the locket"))
    # stamps: three slams, each with its imprint
    for k, (tk, ev, kind) in enumerate([("day1", "stamp 1.1", "return"), ("day1", "stamp 1.3", "refuse"), ("day2", "stamp 2.4", "seal")]):
        t = take(tk).at(ev)
        lead = 1.8 if k == 0 else 1.0
        o = [ov(c["stamp_" + kind], t_in=lead - 0.32, fade_in=0.06, fade_out=0.3, pop=True)]
        # one caption held across the three cuts: it fades in on the first and out on the last
        hold = 99 if k < 2 else None
        if k == 0:
            o.insert(0, ov(c["stamp"], t_in=0.2, t_out=hold))
        else:
            o.insert(0, ov(c["stamp"], t_in=0.0, fade_in=0.01, slide=0, t_out=hold))
        S.append(clip(tk, t - lead, lead + 0.85, o, xf=0.12 if k else 0.35, label=f"stamp {kind}"))
    return S


def uncanny(c):
    """The rules that bend: the hum, the Grey Gentleman, tomorrow, frost, the blue lamp."""
    d1, d2, d3, d4 = take("day1"), take("day2"), take("day3"), take("day4")
    S = []
    S.append(clip("day1", d1.at("pickup suitcase") - 0.1, 4.3, [ov(c["hum"])], label="the suitcase hums for Mrs Marsh"))
    rule5 = max(t for t, e in d1.events if e == "note")   # Tuesday's rules: four, then five
    S.append(clip("day1", rule5 + 5.1, 4.0, [ov(c["rules"])], label="Rule five, in Agnes's hand"))
    S.append(clip("day2", d2.at("arrive 2.4") + 10.0, 4.2, [ov(c["vell"])], label="Mr Vell: you have her eyes"))
    S.append(clip("day3", d3.at("discover photograph.stamp") - 2.0, 4.2, [ov(c["tomorrow"])], label="a photograph developed tomorrow"))
    S.append(clip("day3", d3.at("arrive 3.5") + 2.8, 4.3, [ov(c["frost"])], label="the window frosts: Lt Penhallow"))
    S.append(clip("day4", d4.at("lamp-on chit_vell") - 0.1, 5.0, [ov(c["lamp"])], label="the blue lamp: NEVER TO VELL"))
    return S


def systems(c):
    """The evening, and choices that carry."""
    d1, g = take("day1"), take("grey")
    S = []
    S.append(clip("day1", d1.at("ledger 1") + 1.5, 4.7, [ov(c["ledger"])], label="the Day Ledger and the Gazette"))
    given = g.at("stamp 4.2")
    S.append(clip("grey", given + 2.8, 4.4, [ov(c["grey"])], label="Vell: forty-one years she waited"))
    S.append(clip("grey", g.at("arrive 4.5") + 3.1, 3.0, xf=0.35, label="Thomas: gone? to a gentleman in grey?"))
    return S


def montage(c):
    """Five days, twenty-five objects, twenty-five faces, three endings: cut on the beat (80 bpm)."""
    beat = 60.0 / 80.0
    d = {n: take(n) for n in ("day1", "day2", "day3", "day4", "day5", "grey")}
    S = []

    def inter(key, bg):
        S.append(clip(bg[0], bg[1], 2 * beat, [ov(c[key], t_in=0.0, slide=0, fade_in=0.12, fade_out=0.1)], xf=0.04,
                      trans="fade", label="intertitle " + key))

    def cut(tk, t, n=1.0, label=""):
        S.append(clip(tk, t, n * beat, xf=0.04, label=label))

    # the stakes: Mr Vell's last request
    v = d["day5"].at("arrive 5.5")
    S.append(clip("day5", v + 17.7, 3.5, xf=0.5, label="Vell: the key, please"))
    # five days: each day's card
    inter("five", ("day1", d["day1"].at("done 1.1") + 0.3))
    for tk, ev in (("day1", "day 1"), ("day1", "day 2"), ("day2", "day 3"), ("day3", "day 4"), ("day4", "day 5")):
        cut(tk, d[tk].at(ev) + 0.7, 1.0, label="day card " + ev)
    # twenty-five objects, in the hands
    inter("objects", ("day2", d["day2"].at("bell 2.3") - 2.0))
    for tk, item in (("day1", "scarf_red"), ("day2", "toy_rabbit"), ("day4", "violin_case"),
                     ("day4", "record"), ("day5", "birdcage"), ("day5", "snow_globe")):
        cut(tk, d[tk].at("pickup " + item) + 0.75, 0.75, label="object " + item)
    # twenty-five faces at the window
    inter("cast", ("day3", d["day3"].at("bell 3.2") - 2.0))
    for tk, who in (("day2", "arrive 2.1"), ("day2", "arrive 2.5"), ("day3", "arrive 3.1"),
                    ("day4", "arrive 4.3"), ("day4", "arrive 4.4"), ("day5", "arrive 5.1")):
        cut(tk, d[tk].at(who) + 3.0, 0.75, label="face " + who)
    # three endings
    inter("endings", ("day4", d["day4"].at("bell 4.2") - 2.0))
    cut("day5", d["day5"].at("ending") + 5.0, 2.0, label="ending: the 9:40")
    cut("grey", d["grey"].at("ending") + 4.0, 2.0, label="ending: grey ninefold")
    return S


def trailer():
    c = caps()
    S = cold_open(c)
    title = card(4.6, ("day1", 3.0), [ov(c["logo"], t_in=0.5, slide=0, fade_in=1.1, fade_out=0.6)], xf=0.9, trans="fadeblack",
                 label="TITLE")
    S.append(title)
    first_mech = len(S)
    S += mechanics(c)
    S[first_mech]["xf"], S[first_mech]["trans"] = 0.7, "fadeblack"
    first_unc = len(S)
    S += uncanny(c)
    first_sys = len(S)
    S += systems(c)
    first_mont = len(S)
    S += montage(c)
    S.append(card(5.8, ("day5", take("day5").at("bell 5.1") - 2.0), [ov(c["end"], t_in=0.4, slide=0, fade_in=1.0, fade_out=0.01, t_out=99)],
                  xf=0.6, trans="fadeblack", label="END CARD"))

    def cues(starts, total):
        t_mech, t_unc = starts[first_mech], starts[first_unc]
        t_sys, t_mont, t_end = starts[first_sys], starts[first_mont], starts[-1]
        # the finale's last held chord (about 96 s into the track) lands on the end card
        fin_src = 96.0 - (t_end - t_mont)
        return [
            ("ring", 6.0, 0.0, t_mech + 1.0, 1.5, 2.0, 0.95),          # cold open and title: the swell lands on the title
            ("day2", 0.0, t_mech - 0.2, t_unc + 1.2, 1.0, 1.6, 0.85),   # how it plays: the jazz
            ("uncanny", 0.0, t_unc - 0.4, t_sys + 0.8, 1.2, 1.4, 1.0),  # the strange rules
            ("day5", 8.0, t_sys - 0.2, t_mont + 0.6, 0.8, 1.0, 0.85),   # the evening
            ("finale", fin_src, t_mont - 0.3, total, 0.4, 2.5, 1.0),    # the montage and the end card
        ]

    return assemble(S, "trailer", cues)




def stills():
    """The README screenshots: one frame each from the takes, chosen by marker."""
    d = {n: take(n) for n in ("day1", "day2", "day3", "day4")}
    shots = [
        ("screenshot_title", "day1", d["day1"].at("title") + 3.4),
        ("screenshot_drawers", "day1", d["day1"].at("tag wallet_brown") + 1.5),
        ("screenshot_inspect", "day1", d["day1"].at("discover wallet_brown.photo") + 0.7),
        ("screenshot_liar", "day1", d["day1"].at("ask namestrip") + 5.0),
        ("screenshot_hum", "day1", d["day1"].at("pickup suitcase") + 0.9),
        ("screenshot_vell", "day2", d["day2"].at("discover pocket_watch.hands") - 0.7),
        ("screenshot_frost", "day3", d["day3"].at("arrive 3.5") + 7.0),
        ("screenshot_lamp", "day4", d["day4"].at("discover chit_vell.hidden") - 0.6),
        ("screenshot_photographs", "day4", d["day4"].at("photos-begin") + 7.4),
        ("screenshot_ledger", "day1", d["day1"].at("ledger 1") + 6.2),
    ]
    WORK.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)
    return [still(tk, t, name) for name, tk, t in shots]


def teaser_loop():
    """About 8 seconds for the top of the README: the drawer, the object in the hands, the stamp, a photograph changing."""
    d1, d4 = take("day1"), take("day4")
    return teaser([
        ("day1", d1.at("drawer Drawer_A") - 0.85, 3.4),
        ("day1", d1.at("part wallet_brown.hinge") - 0.6, 2.4),
        ("day1", d1.at("stamp 1.1") - 1.1, 1.8),
        ("day4", d4.at("photos-begin") + 6.0, 2.8),
    ])


if __name__ == "__main__":
    what = sys.argv[1:] or ["trailer", "stills", "teaser"]
    if "stills" in what:
        stills()
    if "teaser" in what:
        teaser_loop()
    if "trailer" in what:
        out, starts, total = trailer()
        # the README's poster: the title card, with a play button
        frame = WORK / "poster_src.png"
        ffmpeg("-ss", f"{starts[5] + 2.4:.2f}", "-i", out, "-frames:v", 1, frame)
        poster(frame, OUT / "trailer_poster.jpg")
