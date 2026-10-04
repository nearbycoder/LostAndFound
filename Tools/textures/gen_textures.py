"""Generates every texture the game uses: tiling material sets (albedo detail + normal map), paper
backgrounds, stamp imprints, frost, UI sprites and cursors. Pure numpy + PIL, deterministic.

    .venv/bin/python Tools/textures/gen_textures.py

Outputs into Assets/Game/Resources/Textures (and /UI). Albedo maps are near-white detail (the
material colour is a tint); normal maps are OpenGL convention (green up), as Unity expects.
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Textures")
UI = os.path.join(OUT, "UI")
FONTS = os.path.join(ROOT, "ArtSource", "fonts")
os.makedirs(UI, exist_ok=True)
rng = np.random.default_rng(1962)


# ----------------------------------------------------------------------------- noise helpers

def tile_noise(n, freq, octaves=4, persistence=0.5, seed=0):
    """Seamless value noise via random lattice + periodic bicubic-ish (cosine) interpolation."""
    r = np.random.default_rng(seed)
    out = np.zeros((n, n), np.float32)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        f = freq * (2 ** o)
        grid = r.random((f, f)).astype(np.float32)
        xs = np.arange(n) * f / n
        x0 = np.floor(xs).astype(int)
        t = xs - x0
        t = t * t * (3 - 2 * t)
        x1 = (x0 + 1) % f
        a = grid[x0][:, x0] * (1 - t)[None, :] + grid[x0][:, x1] * t[None, :]
        b = grid[x1][:, x0] * (1 - t)[None, :] + grid[x1][:, x1] * t[None, :]
        layer = a * (1 - t)[:, None] + b * t[:, None]
        out += layer * amp
        total += amp
        amp *= persistence
    return out / total


def norm01(a):
    a = a - a.min()
    return a / max(1e-6, a.max())


def blur_wrap(a, radius):
    """Gaussian blur with wrap-around (keeps tiling)."""
    if radius <= 0:
        return a
    k = int(radius * 3) | 1
    xs = np.arange(k) - k // 2
    g = np.exp(-xs ** 2 / (2 * radius * radius))
    g /= g.sum()
    out = a.copy()
    for axis in (0, 1):
        acc = np.zeros_like(out)
        for i, w in zip(xs, g):
            acc += np.roll(out, i, axis=axis) * w
        out = acc
    return out


def normal_from_height(h, strength):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    nx, ny, nz = -dx * strength, dy * strength, np.ones_like(h)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    n = np.stack([nx / l, ny / l, nz / l], -1)
    return ((n * 0.5 + 0.5) * 255).astype(np.uint8)


def save_rgb(arr, name, folder=OUT):
    Image.fromarray(arr).save(os.path.join(folder, name + ".png"))


def albedo(h, lo, hi, tint=(1, 1, 1)):
    v = lo + (hi - lo) * h
    rgb = np.stack([v * tint[0], v * tint[1], v * tint[2]], -1)
    return (np.clip(rgb, 0, 1) * 255).astype(np.uint8)


def material(name, height, alb, strength):
    save_rgb(alb, name + "_a")
    save_rgb(normal_from_height(height, strength), name + "_n")


N = 512


def gen_materials():
    # wood: long grain with growth rings and pores
    y = np.linspace(0, 1, N, endpoint=False)
    warp = tile_noise(N, 3, 3, seed=1) * 2.0
    rings = np.sin((y[:, None] * 22 + warp * 3.5) * 2 * math.pi) * 0.5 + 0.5
    fine = tile_noise(N, 64, 2, seed=2)
    streak = blur_wrap(rng.random((N, N)).astype(np.float32), 0.6)
    streak = blur_wrap(np.repeat(streak[:, :1], N, axis=1) * 0 + streak, 0) * 0.3
    grain = norm01(rings * 0.55 + fine * 0.25 + tile_noise(N, 8, 4, seed=3) * 0.4)
    pores = (tile_noise(N, 128, 1, seed=4) > 0.82).astype(np.float32) * 0.25
    h = norm01(grain - pores)
    material("wood", h, albedo(h, 0.72, 1.0), 2.0)

    # leather: pebbled cells
    pts = rng.random((900, 2)) * N
    yy, xx = np.mgrid[0:N, 0:N]
    d = np.full((N, N), 1e9, np.float32)
    for px, py in pts:
        dx = np.abs(xx - px)
        dx = np.minimum(dx, N - dx)
        dy = np.abs(yy - py)
        dy = np.minimum(dy, N - dy)
        d = np.minimum(d, (dx * dx + dy * dy).astype(np.float32))
    cells = norm01(np.sqrt(d))
    h = norm01(1 - cells ** 0.6 + tile_noise(N, 32, 3, seed=5) * 0.3)
    material("leather", h, albedo(h, 0.8, 1.0), 3.5)

    # brushed metal: horizontal streaks
    s = rng.random((N, N)).astype(np.float32)
    s = blur_wrap(s, 0.5)
    k = np.ones(41) / 41
    s = np.real(np.fft.ifft(np.fft.fft(s, axis=1) * np.fft.fft(np.pad(k, (0, N - 41)), axis=0)[None, :], axis=1)).astype(np.float32)
    h = norm01(s * 0.7 + tile_noise(N, 6, 3, seed=6) * 0.3)
    material("brushed", h, albedo(h, 0.86, 1.0), 1.2)

    # iron: hammered dents and pits
    dents = tile_noise(N, 10, 3, seed=7)
    pits = (tile_noise(N, 90, 2, seed=8) > 0.78).astype(np.float32)
    h = norm01(dents - pits * 0.15 + tile_noise(N, 40, 2, seed=9) * 0.15)
    material("iron", h, albedo(h, 0.7, 1.0), 4.0)

    # paper: fibres
    fib = np.zeros((N, N), np.float32)
    img = Image.new("L", (N, N), 0)
    dr = ImageDraw.Draw(img)
    for _ in range(1600):
        x, yv = rng.random() * N, rng.random() * N
        a = rng.random() * math.pi
        l = 4 + rng.random() * 14
        dr.line([(x, yv), (x + math.cos(a) * l, yv + math.sin(a) * l)], fill=int(40 + rng.random() * 80), width=1)
    fib = np.asarray(img, np.float32) / 255
    h = norm01(tile_noise(N, 16, 4, seed=10) * 0.6 + blur_wrap(fib, 0.7) * 0.6)
    material("paper", h, albedo(h, 0.9, 1.0), 1.5)

    # cloth: plain weave
    u = np.arange(N) / N * 96 * 2 * math.pi
    warp_ = (np.sin(u)[None, :] * 0.5 + 0.5)
    weft = (np.sin(u)[:, None] * 0.5 + 0.5)
    checker = ((np.floor(np.arange(N) / N * 96)[None, :] + np.floor(np.arange(N) / N * 96)[:, None]) % 2)
    h = norm01(np.where(checker > 0, warp_, weft) + tile_noise(N, 24, 3, seed=11) * 0.5)
    material("cloth", h, albedo(h, 0.8, 1.0), 2.5)

    # felt: fuzzy
    h = norm01(blur_wrap(rng.random((N, N)).astype(np.float32), 1.2) * 0.6 + tile_noise(N, 20, 4, seed=12) * 0.6)
    material("felt", h, albedo(h, 0.84, 1.0), 2.0)

    # knit: V stitches
    yy, xx = np.mgrid[0:N, 0:N] / N
    cols, rows = 32, 40
    cx = (xx * cols) % 1.0
    cy = (yy * rows) % 1.0
    v = np.abs(cx - 0.5) * 2
    stitch = np.exp(-((cy - (0.15 + 0.7 * v)) ** 2) / 0.02) * (1 - np.abs(v - 0.5) * 0.6)
    h = norm01(stitch + tile_noise(N, 30, 2, seed=13) * 0.2)
    material("knit", h, albedo(h, 0.7, 1.0), 4.0)

    # plaster / generic paint
    h = norm01(tile_noise(N, 12, 5, seed=14))
    material("plaster", h, albedo(h, 0.92, 1.0), 1.0)

    # frost: crystalline
    cryst = np.zeros((N, N), np.float32)
    img = Image.new("L", (N, N), 0)
    dr = ImageDraw.Draw(img)
    for _ in range(260):
        x, yv = rng.random() * N, rng.random() * N
        for k in range(6):
            a = k * math.pi / 3 + rng.random() * 0.3
            l = 6 + rng.random() * 22
            dr.line([(x, yv), (x + math.cos(a) * l, yv + math.sin(a) * l)], fill=200, width=1)
    cryst = blur_wrap(np.asarray(img, np.float32) / 255, 0.6)
    h = norm01(cryst + tile_noise(N, 30, 3, seed=15) * 0.4)
    material("frost", h, albedo(h, 0.85, 1.0), 3.0)


# ----------------------------------------------------------------------------- paper & imprints

def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def paper_base(w, h, color, edge=0.18, seed=0):
    n = np.array(Image.fromarray((tile_noise(256, 8, 5, seed=seed) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), np.float32) / 255
    yy, xx = np.mgrid[0:h, 0:w]
    ex = np.minimum(xx, w - 1 - xx) / (w * 0.5)
    ey = np.minimum(yy, h - 1 - yy) / (h * 0.5)
    vign = np.clip(np.minimum(ex, ey) * 6, 0, 1) * edge + (1 - edge)
    shade = (0.93 + n * 0.07) * vign
    rgb = np.stack([shade * color[0], shade * color[1], shade * color[2]], -1)
    return np.clip(rgb * 255, 0, 255).astype(np.uint8)


def gen_papers():
    # the claim slip: cream form with a ruled border and faint rules (text is rendered in-game)
    w, h = 680, 940
    img = Image.fromarray(paper_base(w, h, (1.0, 0.98, 0.93), 0.12, seed=21))
    d = ImageDraw.Draw(img)
    ink = (120, 60, 50)
    d.rectangle([22, 22, w - 23, h - 23], outline=ink, width=3)
    d.rectangle([30, 30, w - 31, h - 31], outline=ink, width=1)
    d.line([(30, 150), (w - 31, 150)], fill=ink, width=2)
    for yv in range(212, h - 140, 46):
        d.line([(46, yv), (w - 47, yv)], fill=(150, 170, 200), width=1)
    d.line([(30, h - 140), (w - 31, h - 140)], fill=ink, width=1)
    f = font("SpecialElite-Regular.ttf", 20)
    d.text((44, h - 128), "STAMP HERE  ·  ONE STAMP PER CLAIM", font=f, fill=(150, 90, 80))
    img = img.filter(ImageFilter.GaussianBlur(0.4))
    img.save(os.path.join(OUT, "slip_paper.png"))

    # manila tag
    w, h = 256, 440
    tag = Image.fromarray(paper_base(w, h, (1.0, 0.93, 0.78), 0.25, seed=22))
    d = ImageDraw.Draw(tag)
    d.line([(20, 120), (w - 20, 120)], fill=(170, 120, 80), width=2)
    for yv in range(170, h - 20, 44):
        d.line([(24, yv), (w - 24, yv)], fill=(190, 150, 110), width=1)
    tag.save(os.path.join(OUT, "tag_paper.png"))

    # stamps: rubber imprints with grunge (white, tinted by ink colour in-game)
    for key, word in (("return", "RETURNED"), ("refuse", "REFUSED"), ("seal", "SEALED")):
        W, H = 600, 290
        im = Image.new("L", (W, H), 0)
        d = ImageDraw.Draw(im)
        d.rounded_rectangle([10, 10, W - 11, H - 11], radius=26, outline=255, width=14)
        d.rounded_rectangle([34, 34, W - 35, H - 35], radius=16, outline=255, width=4)
        f = font("SpecialElite-Regular.ttf", 120 if len(word) <= 7 else 108)
        tw = d.textlength(word, font=f)
        d.text(((W - tw) / 2, 70), word, font=f, fill=255)
        f2 = font("SpecialElite-Regular.ttf", 30)
        sub = "NINEFOLD LOST PROPERTY" if key != "seal" else "THE IRON DRAWER  ·  IX"
        tw = d.textlength(sub, font=f2)
        d.text(((W - tw) / 2, 205), sub, font=f2, fill=255)
        a = np.asarray(im, np.float32) / 255
        grunge = tile_noise(256, 12, 4, seed=hash(key) % 100)
        grunge = np.array(Image.fromarray((grunge * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC), np.float32) / 255
        speck = (rng.random((H, W)) > 0.93).astype(np.float32)
        a = a * np.clip(grunge * 1.6 - 0.05, 0, 1) * (1 - speck * 0.8)
        a = np.clip(a * 1.25, 0, 1)
        rgba = np.zeros((H, W, 4), np.uint8)
        rgba[..., :3] = 255
        rgba[..., 3] = (a * 255).astype(np.uint8)
        Image.fromarray(rgba).save(os.path.join(OUT, f"stamp_{key}.png"))

    # soft shadow blob
    S = 128
    yy, xx = np.mgrid[0:S, 0:S] / (S - 1) * 2 - 1
    a = np.clip(1 - np.sqrt(xx ** 2 + yy ** 2), 0, 1) ** 1.6
    rgba = np.zeros((S, S, 4), np.uint8)
    rgba[..., 3] = (a * 255).astype(np.uint8)
    Image.fromarray(rgba).save(os.path.join(OUT, "soft_shadow.png"))

    # frosted glass: crystals creeping in from the edges, clear centre
    W = 512
    yy, xx = np.mgrid[0:W, 0:W] / (W - 1)
    edge = np.minimum(np.minimum(xx, 1 - xx), np.minimum(yy, 1 - yy))
    n = tile_noise(W, 6, 5, seed=31)
    cryst = np.asarray(Image.open(os.path.join(OUT, "frost_a.png")).convert("L"), np.float32) / 255
    a = np.clip((0.32 - edge + (n - 0.5) * 0.22) * 4.0, 0, 1)
    a = np.clip(a * (0.6 + cryst * 0.6), 0, 1)
    rgba = np.zeros((W, W, 4), np.uint8)
    rgba[..., 0] = 235
    rgba[..., 1] = 245
    rgba[..., 2] = 255
    rgba[..., 3] = (np.clip(0.25 + a * 0.75, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(rgba).save(os.path.join(OUT, "frost_glass.png"))


# ----------------------------------------------------------------------------- UI sprites

def rgba_from(img_l, color):
    a = np.asarray(img_l, np.float32) / 255
    rgba = np.zeros((*a.shape, 4), np.uint8)
    rgba[..., 0], rgba[..., 1], rgba[..., 2] = color
    rgba[..., 3] = (a * 255).astype(np.uint8)
    return Image.fromarray(rgba)


def paper_sprite(w, h, radius, name, rough=True, lines=None, seed=0):
    base = paper_base(w, h, (1.0, 1.0, 1.0), 0.1, seed=seed)
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle([1, 1, w - 2, h - 2], radius=radius, fill=255)
    if rough:
        m = np.asarray(mask, np.float32) / 255
        n = np.array(Image.fromarray((tile_noise(128, 16, 3, seed=seed + 5) * 255).astype(np.uint8)).resize((w, h)), np.float32) / 255
        m = np.clip(m * 1.0 - (1 - m) * 0 - ((m < 1) * (n - 0.5) * 0.6), 0, 1)
        mask = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))
    img = Image.fromarray(base).convert("RGBA")
    if lines:
        d = ImageDraw.Draw(img)
        lines(d, w, h)
    img.putalpha(mask)
    img.save(os.path.join(UI, name + ".png"))


def gen_ui():
    paper_sprite(128, 128, 18, "paper_card", seed=40)
    paper_sprite(128, 48, 12, "nameplate", rough=False, seed=41)

    def ledger_lines(d, w, h):
        for yv in range(140, h - 60, 52):
            d.line([(60, yv), (w - 60, yv)], fill=(170, 190, 215, 255), width=2)
        d.line([(w - 260, 60), (w - 260, h - 60)], fill=(200, 120, 110, 255), width=2)
        d.line([(80, 60), (80, h - 60)], fill=(200, 120, 110, 255), width=2)
    paper_sprite(1500, 900, 30, "ledger_page", rough=True, lines=ledger_lines, seed=42)

    # luggage tag card with a reinforced hole
    w, h = 330, 250
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    c = 52
    d.polygon([(c, 0), (w, 0), (w, h), (c, h), (0, h - c), (0, c)], fill=255)
    d.ellipse([18, h / 2 - 14, 46, h / 2 + 14], fill=0)
    base = Image.fromarray(paper_base(w, h, (1, 1, 1), 0.2, seed=43)).convert("RGBA")
    dd = ImageDraw.Draw(base)
    dd.ellipse([12, h / 2 - 20, 52, h / 2 + 20], outline=(170, 130, 70, 255), width=5)
    for yv in range(80, h - 10, 40):
        dd.line([(70, yv), (w - 16, yv)], fill=(180, 140, 100, 255), width=1)
    base.putalpha(m.filter(ImageFilter.GaussianBlur(0.7)))
    base.save(os.path.join(UI, "tag_card.png"))

    # note paper with a torn bottom edge and a bit of tape
    w, h = 520, 400
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    pts = [(0, 0), (w, 0), (w, h - 20)]
    for i in range(30, -1, -1):
        x = w * i / 30
        pts.append((x, h - 20 + rng.random() * 16))
    pts.append((0, h - 20))
    d.polygon(pts, fill=255)
    base = Image.fromarray(paper_base(w, h, (1, 1, 1), 0.15, seed=44)).convert("RGBA")
    dd = ImageDraw.Draw(base)
    for yv in range(86, h - 30, 38):
        dd.line([(30, yv), (w - 30, yv)], fill=(175, 195, 220, 255), width=1)
    dd.line([(70, 0), (70, h)], fill=(220, 150, 150, 255), width=1)
    base.putalpha(m)
    tape = Image.new("RGBA", (130, 40), (240, 230, 190, 150))
    base.alpha_composite(tape.rotate(-8, expand=True), (w // 2 - 70, -6))
    base.save(os.path.join(UI, "note_paper.png"))

    # gazette clipping
    w, h = 600, 300
    base = Image.fromarray(paper_base(w, h, (1, 1, 1), 0.2, seed=45)).convert("RGBA")
    dd = ImageDraw.Draw(base)
    dd.line([(20, 52), (w - 20, 52)], fill=(40, 40, 40, 255), width=2)
    dd.line([(20, 56), (w - 20, 56)], fill=(40, 40, 40, 255), width=1)
    for k in range(3):
        x0 = 30 + k * 190
        for yv in range(200, h - 20, 9):
            dd.line([(x0, yv), (x0 + 170, yv)], fill=(120, 115, 105, 90), width=2)
    base.save(os.path.join(UI, "gazette.png"))

    # photo border
    w, h = 300, 360
    base = Image.fromarray(paper_base(w, h, (1, 1, 1), 0.1, seed=46)).convert("RGBA")
    base.save(os.path.join(UI, "photo_border.png"))

    S = 128
    yy, xx = np.mgrid[0:S, 0:S] / (S - 1) * 2 - 1
    r = np.sqrt(xx ** 2 + yy ** 2)
    ring = np.clip(1 - np.abs(r - 0.82) / 0.08, 0, 1)
    rgba_from(Image.fromarray((ring * 255).astype(np.uint8)), (255, 255, 255)).save(os.path.join(UI, "ring.png"))
    ang = np.arctan2(yy, xx)
    star = np.clip((np.abs(np.cos(ang * 2)) ** 18) * (1 - r) * 2.2 + np.clip(1 - r * 3.5, 0, 1), 0, 1)
    rgba_from(Image.fromarray((star * 255).astype(np.uint8)), (255, 255, 255)).save(os.path.join(UI, "glint.png"))

    # inked marks for the ledger
    def mark(name, draw):
        im = Image.new("L", (160, 160), 0)
        d = ImageDraw.Draw(im)
        draw(d)
        im = im.filter(ImageFilter.GaussianBlur(1.2))
        a = np.asarray(im, np.float32) / 255
        a = a * (0.75 + tile_noise(160, 10, 3, seed=len(name)) * 0.4)
        rgba_from(Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)), (255, 255, 255)).save(os.path.join(UI, name + ".png"))
    mark("mark_tick", lambda d: d.line([(28, 86), (66, 126), (136, 30)], fill=255, width=16, joint="curve"))
    mark("mark_cross", lambda d: (d.line([(34, 34), (128, 128)], fill=255, width=16), d.line([(128, 34), (34, 128)], fill=255, width=16)))
    mark("mark_half", lambda d: (d.ellipse([28, 28, 132, 132], outline=255, width=14), d.line([(40, 80), (120, 80)], fill=255, width=12)))
    mark("mark_dash", lambda d: d.line([(36, 80), (124, 80)], fill=255, width=14))

    # brass seal stamp
    S = 160
    yy, xx = np.mgrid[0:S, 0:S] / (S - 1) * 2 - 1
    r = np.sqrt(xx ** 2 + yy ** 2)
    disc = np.clip((1 - r) * 30, 0, 1)
    shade = 0.75 + 0.25 * (-xx - yy) / 2
    rim = np.clip(1 - np.abs(r - 0.85) / 0.06, 0, 1)
    col = np.stack([0.85 * shade + rim * 0.1, 0.66 * shade + rim * 0.08, 0.34 * shade], -1)
    rgba = np.zeros((S, S, 4), np.uint8)
    rgba[..., :3] = (np.clip(col, 0, 1) * 255).astype(np.uint8)
    rgba[..., 3] = (disc * 255).astype(np.uint8)
    im = Image.fromarray(rgba)
    d = ImageDraw.Draw(im)
    f = font("Limelight-Regular.ttf", 70)
    tw = d.textlength("IX", font=f)
    d.text(((S - tw) / 2 + 2, 38), "IX", font=f, fill=(110, 80, 30, 255))
    d.text(((S - tw) / 2, 36), "IX", font=f, fill=(250, 220, 150, 255))
    im.save(os.path.join(UI, "brass_stamp.png"))

    # speech bubble tail (points left; flipped in-game)
    im = Image.new("L", (92, 80), 0)
    ImageDraw.Draw(im).polygon([(92, 6), (92, 74), (0, 52)], fill=255)
    rgba_from(im.filter(ImageFilter.GaussianBlur(0.8)), (255, 255, 255)).save(os.path.join(UI, "bubble_tail.png"))

    # quill (continue marker) and chevron
    im = Image.new("L", (64, 64), 0)
    d = ImageDraw.Draw(im)
    d.polygon([(12, 20), (52, 20), (32, 50)], fill=255)
    rgba_from(im.filter(ImageFilter.GaussianBlur(0.6)), (255, 255, 255)).save(os.path.join(UI, "quill.png"))
    im = Image.new("L", (60, 96), 0)
    d = ImageDraw.Draw(im)
    d.line([(44, 10), (14, 48), (44, 86)], fill=255, width=10, joint="curve")
    rgba_from(im.filter(ImageFilter.GaussianBlur(0.8)), (255, 255, 255)).save(os.path.join(UI, "chevron.png"))


def gen_cursors():
    """48x48 cursors: dark ink outline with a cream fill, so they read on any background."""
    def cursor(name, draw):
        S = 48 * 4
        fill = Image.new("L", (S, S), 0)
        draw(ImageDraw.Draw(fill), 4)
        outline = fill.filter(ImageFilter.MaxFilter(13))
        fill_s = fill.resize((48, 48), Image.LANCZOS)
        out_s = outline.resize((48, 48), Image.LANCZOS)
        rgba = np.zeros((48, 48, 4), np.uint8)
        f = np.asarray(fill_s, np.float32) / 255
        o = np.asarray(out_s, np.float32) / 255
        col = np.array([248, 238, 214], np.float32)
        ink = np.array([28, 22, 30], np.float32)
        rgb = ink[None, None, :] * (1 - f[..., None]) + col[None, None, :] * f[..., None]
        rgba[..., :3] = rgb.astype(np.uint8)
        rgba[..., 3] = (np.clip(np.maximum(o, f), 0, 1) * 255).astype(np.uint8)
        Image.fromarray(rgba).save(os.path.join(UI, name + ".png"))

    def arrow(d, k):
        d.polygon([(6 * k, 4 * k), (6 * k, 34 * k), (14 * k, 27 * k), (20 * k, 40 * k), (25 * k, 38 * k), (19 * k, 25 * k), (29 * k, 25 * k)], fill=255)

    def hand(d, k):
        d.rounded_rectangle([18 * k, 4 * k, 26 * k, 26 * k], radius=4 * k, fill=255)       # index finger
        d.rounded_rectangle([12 * k, 20 * k, 38 * k, 42 * k], radius=7 * k, fill=255)      # palm
        for x in (26, 31):
            d.rounded_rectangle([x * k, 16 * k, (x + 6) * k, 28 * k], radius=3 * k, fill=255)
        d.rounded_rectangle([7 * k, 22 * k, 15 * k, 34 * k], radius=4 * k, fill=255)      # thumb

    def grab(d, k):
        d.rounded_rectangle([10 * k, 14 * k, 38 * k, 40 * k], radius=9 * k, fill=255)
        for x in (12, 18, 24, 30):
            d.rounded_rectangle([x * k, 9 * k, (x + 6) * k, 20 * k], radius=3 * k, fill=255)

    def magnifier(d, k):
        d.ellipse([6 * k, 6 * k, 32 * k, 32 * k], fill=255)
        d.line([(28 * k, 28 * k), (42 * k, 42 * k)], fill=255, width=7 * k)

    def look(d, k):
        d.ellipse([18 * k, 18 * k, 30 * k, 30 * k], fill=255)
        d.ellipse([6 * k, 6 * k, 42 * k, 42 * k], outline=255, width=3 * k)

    cursor("cursor_arrow", arrow)
    cursor("cursor_hand", hand)
    cursor("cursor_grab", grab)
    cursor("cursor_magnifier", magnifier)
    cursor("cursor_look", look)


if __name__ == "__main__":
    gen_materials()
    gen_papers()
    gen_ui()
    gen_cursors()
    print("textures written to", OUT)
