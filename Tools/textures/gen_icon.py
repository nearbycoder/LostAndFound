"""The application icon: a manila intake tag on Agnes's green baize, lit by the desk lamp, with a red
string. Pure numpy + PIL, deterministic.

    .venv/bin/python Tools/textures/gen_icon.py

Writes Assets/Game/Icon/app_icon.png (1024 x 1024, transparent outside the rounded square, which macOS
expects of an app icon). ProjectSetup sets it as the default icon for every platform.
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Game", "Icon")
FONTS = os.path.join(ROOT, "ArtSource", "fonts")
S = 2048            # drawn at twice the size, then downsampled
FINAL = 1024


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def noise(n, scale, seed):
    r = np.random.default_rng(seed)
    small = r.random((max(2, n // scale), max(2, n // scale))).astype(np.float32)
    return np.asarray(Image.fromarray((small * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC), np.float32) / 255


def baize():
    """Green felt with a warm pool of lamplight from the upper left."""
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32) / S
    felt = 0.9 + 0.06 * noise(S, 6, 1) + 0.04 * noise(S, 64, 2)
    base = np.array([0.10, 0.22, 0.18], np.float32)
    light = np.exp(-(((xx - 0.30) ** 2) + ((yy - 0.26) ** 2)) / 0.16)
    warm = np.array([1.0, 0.78, 0.45], np.float32)
    rgb = base[None, None, :] * felt[..., None] * (0.55 + 0.9 * light[..., None]) + warm * (0.10 * light[..., None])
    vign = 1.0 - 0.45 * np.clip(np.hypot(xx - 0.5, yy - 0.5) / 0.72, 0, 1) ** 2
    return np.clip(rgb * vign[..., None], 0, 1)


def tag_layer():
    """A manila tag with a chamfered end, a reinforced eyelet, ruled lines and the words, drawn upright."""
    w, h = 1080, 1500
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    shape = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(shape)
    c = 230
    d.polygon([(c, 0), (w - c, 0), (w, c), (w, h), (0, h), (0, c)], fill=255)
    d.ellipse([w / 2 - 70, 120, w / 2 + 70, 260], fill=0)
    shape = shape.filter(ImageFilter.GaussianBlur(1.5))

    n = noise(w if w > h else h, 8, 7)[:h, :w]
    shade = 0.9 + 0.1 * n
    paper = np.stack([shade * 0.93, shade * 0.80, shade * 0.56], -1)
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    paper *= (0.92 + 0.08 * (1 - yy))[..., None]
    tag = Image.fromarray((np.clip(paper, 0, 1) * 255).astype(np.uint8)).convert("RGBA")
    dd = ImageDraw.Draw(tag)
    dd.ellipse([w / 2 - 105, 85, w / 2 + 105, 295], outline=(176, 128, 62, 255), width=26)
    dd.line([(90, 420), (w - 90, 420)], fill=(168, 112, 74, 255), width=8)
    for yv in range(1160, h - 60, 110):
        dd.line([(110, yv), (w - 110, yv)], fill=(186, 146, 104, 255), width=5)
    ink = (34, 30, 52, 255)
    f = font("Limelight-Regular.ttf", 250)
    for i, word in enumerate(("Lost &", "Found")):
        tw = dd.textlength(word, font=f)
        dd.text(((w - tw) / 2, 470 + i * 300), word, font=f, fill=ink)
    f2 = font("SpecialElite-Regular.ttf", 92)
    no = "No. 09"
    dd.text(((w - dd.textlength(no, font=f2)) / 2, 1210), no, font=f2, fill=(120, 44, 36, 255))
    tag.putalpha(shape)
    return tag


def main():
    os.makedirs(OUT, exist_ok=True)
    bg = Image.fromarray((baize() * 255).astype(np.uint8)).convert("RGBA")

    # the red string runs from the eyelet up and out of the frame
    string = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sd = ImageDraw.Draw(string)
    pts = [(1172 + 240 * t + 90 * math.sin(t * 3.0), 596 - 660 * t) for t in np.linspace(0, 1, 40)]
    sd.line(pts, fill=(150, 30, 28, 255), width=34, joint="curve")
    sd.line([(x - 6, y - 4) for x, y in pts], fill=(200, 70, 60, 255), width=10, joint="curve")

    tag = tag_layer().rotate(-11, resample=Image.BICUBIC, expand=True)
    tx, ty = (S - tag.width) // 2 + 40, (S - tag.height) // 2 + 120
    shadow = Image.new("RGBA", tag.size, (0, 0, 0, 0))
    shadow.putalpha(tag.getchannel("A").point(lambda a: int(a * 0.55)))
    shadow = shadow.filter(ImageFilter.GaussianBlur(28))
    bg.alpha_composite(shadow, (tx + 38, ty + 54))
    bg.alpha_composite(tag, (tx, ty))
    bg.alpha_composite(string)

    # the rounded square, with a little room around it, as macOS draws icons
    mask = Image.new("L", (S, S), 0)
    inset, radius = 200, 370
    ImageDraw.Draw(mask).rounded_rectangle([inset, inset, S - inset, S - inset], radius=radius, fill=255)
    rim = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(rim).rounded_rectangle([inset, inset, S - inset, S - inset], radius=radius, outline=(201, 161, 90, 160), width=10)
    bg.alpha_composite(rim)
    bg.putalpha(mask)
    soft = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    soft.putalpha(mask.filter(ImageFilter.GaussianBlur(30)).point(lambda a: int(a * 0.5)))
    out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    out.alpha_composite(soft, (0, 24))
    out.alpha_composite(bg)
    out = out.resize((FINAL, FINAL), Image.LANCZOS)
    path = os.path.join(OUT, "app_icon.png")
    out.save(path)
    print("icon written to", path)


if __name__ == "__main__":
    main()
