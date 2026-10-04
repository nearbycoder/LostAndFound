"""Age the raw photo renders into prints: sepia for 1921, silver for 1951, faded colour slides and
a Polaroid for 1962, with grain, vignette, dust and soft focus. Writes Assets/Game/Resources/Photos/.

    .venv/bin/python Tools/textures/finish_photos.py
"""
import os
import zlib

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RAW = os.path.join(ROOT, "ArtSource", "_renders", "photos")
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Photos")
ITEMS = os.path.join(ROOT, "Assets", "Game", "Resources", "Textures", "Items")


def rng_for(name):
    return np.random.default_rng(zlib.crc32(name.encode()))


def load(name):
    return np.asarray(Image.open(os.path.join(RAW, name + ".png")).convert("RGB"), np.float32) / 255.0


def luma(a):
    return a[..., 0] * 0.3 + a[..., 1] * 0.59 + a[..., 2] * 0.11


def vignette(a, amount):
    h, w = a.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.sqrt(((xx - w / 2) / (w / 2)) ** 2 + ((yy - h / 2) / (h / 2)) ** 2)
    v = 1 - amount * np.clip(d - 0.45, 0, 1) ** 1.6
    return a * v[..., None]


def grain(a, rng, amount):
    n = rng.normal(0, amount, a.shape[:2]).astype(np.float32)
    n = np.asarray(Image.fromarray(((n + 0.5) * 255).clip(0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)), np.float32) / 255 - 0.5
    return a + n[..., None]


def dust(img, rng, count, light=True):
    d = ImageDraw.Draw(img)
    w, h = img.size
    col = (240, 232, 215) if light else (40, 32, 26)
    for _ in range(count):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(0.6, 2.2)
        d.ellipse([x - r, y - r, x + r, y + r], fill=col)
    for _ in range(count // 6):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        L = rng.uniform(20, 120)
        a = rng.uniform(-0.3, 0.3) + np.pi / 2
        d.line([(x, y), (x + np.cos(a) * L, y + np.sin(a) * L)], fill=col, width=1)
    return img


def tone(a, shadows, highlights, contrast=0.8, lift=0.06):
    l = luma(a)
    l = (l - 0.5) * contrast + 0.5 + lift
    l = np.clip(l, 0, 1)[..., None]
    return np.array(shadows) * (1 - l) + np.array(highlights) * l


def to_img(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))


def sepia(name, out):
    rng = rng_for(out)
    a = load(name)
    a = tone(a, (0.16, 0.1, 0.06), (0.98, 0.9, 0.74), contrast=0.85, lift=0.04)
    a = vignette(a, 0.7)
    a = grain(a, rng, 0.06)
    img = to_img(a).filter(ImageFilter.GaussianBlur(1.1))
    img = dust(img, rng, 60)
    save(img, out)


def silver(name, out):
    rng = rng_for(out)
    a = load(name)
    a = tone(a, (0.08, 0.08, 0.09), (0.95, 0.94, 0.9), contrast=0.95, lift=0.02)
    a = vignette(a, 0.5)
    a = grain(a, rng, 0.05)
    img = to_img(a).filter(ImageFilter.GaussianBlur(0.8))
    save(dust(img, rng, 30), out)


def faded_colour(name, out, warm=(1.06, 0.98, 0.86), sat=0.75, lift=0.08, vig=0.45, blur=0.7):
    rng = rng_for(out)
    a = load(name)
    l = luma(a)[..., None]
    a = l + (a - l) * sat
    a = a * np.array(warm) + lift * np.array((0.9, 0.85, 1.0))
    a = vignette(a, vig)
    a = grain(a, rng, 0.035)
    img = to_img(a).filter(ImageFilter.GaussianBlur(blur))
    save(dust(img, rng, 12), out)
    return img


def polaroid(name, out):
    rng = rng_for(out)
    a = load(name)
    l = luma(a)[..., None]
    a = l + (a - l) * 0.7
    a = a * np.array((1.02, 1.0, 0.92)) + np.array((0.02, 0.05, 0.08)) * (1 - l)   # cyan shadows
    a = (a - 0.5) * 0.85 + 0.55
    a = grain(a, rng, 0.03)
    pic = to_img(a).filter(ImageFilter.GaussianBlur(0.9)).resize((632, 632), Image.LANCZOS)
    card = Image.new("RGB", (704, 856), (238, 234, 222))
    shade = np.asarray(card, np.float32) / 255 * (0.96 + 0.04 * rng.random((856, 704, 1)))
    card = to_img(shade)
    card.paste(pic, (36, 36))
    d = ImageDraw.Draw(card)
    d.rectangle([36, 36, 667, 667], outline=(200, 196, 186))
    save(card, out)


def snapshot_with_caption(name, out, caption, size=(400, 300), border=16, cap_h=50):
    """A small bordered snapshot with a handwritten caption under the picture (Walter's Biscuit)."""
    from PIL import ImageFont
    rng = rng_for(out)
    w, h = size
    a = load(name)
    a = tone(a, (0.1, 0.075, 0.06), (0.98, 0.93, 0.83), contrast=1.15, lift=0.0)
    a = vignette(a, 0.4)
    a = grain(a, rng, 0.04)
    iw, ih = w - 2 * border, h - 2 * border - cap_h
    pic = to_img(a).filter(ImageFilter.GaussianBlur(0.6))
    # crop to the picture's aspect, centred, then fit
    pw, ph = pic.size
    target = iw / ih
    if pw / ph > target:
        nw = int(ph * target)
        pic = pic.crop(((pw - nw) // 2, 0, (pw + nw) // 2, ph))
    else:
        nh = int(pw / target)
        pic = pic.crop((0, (ph - nh) // 2, pw, (ph + nh) // 2))
    pic = pic.resize((iw, ih), Image.LANCZOS)
    card = Image.new("RGB", (w, h), (238, 231, 216))
    card.paste(pic, (border, border))
    d = ImageDraw.Draw(card)
    font = ImageFont.truetype(os.path.join(ROOT, "ArtSource", "fonts", "Caveat[wght].ttf"), 42)
    tw = d.textlength(caption, font=font)
    d.text(((w - tw) / 2, h - border - cap_h + 2), caption, font=font, fill=(40, 36, 64))
    card = dust(card, rng, 4)   # a few specks, no scratches across so small a print
    os.makedirs(ITEMS, exist_ok=True)
    card.save(os.path.join(ITEMS, out + ".png"))
    print("photo", out, card.size)


def save(img, out):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, out + ".png"))
    print("photo", out, img.size)


def main():
    for v in ("before", "after"):
        sepia(f"staff1921_{v}", f"staff1921_{v}")
        sepia(f"platform9_{v}", f"platform9_{v}")
        silver(f"mum_{v}", f"mum_{v}")
        faded_colour(f"retirement_{v}", f"retirement_{v}")
        polaroid(f"polaroid_{v}", f"polaroid_{v}")
    faded_colour("ending_nine40", "ending_nine40", warm=(1.1, 1.0, 0.82), sat=0.85, lift=0.06)
    faded_colour("ending_longwait", "ending_longwait", warm=(1.02, 0.98, 0.92), sat=0.6, lift=0.1, vig=0.6)
    faded_colour("ending_grey", "ending_grey", warm=(0.92, 0.95, 1.0), sat=0.0, lift=0.02, vig=0.7)
    if os.path.exists(os.path.join(RAW, "dog.png")):
        snapshot_with_caption("dog", "dog_photo", "Biscuit")


if __name__ == "__main__":
    main()
