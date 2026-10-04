"""Readable detail textures for the lost objects: tickets, labels, stickers, engravings, notes,
dials. Photos are first drafted here and later overwritten by Blender renders (ArtSource/render_photos.py).

    .venv/bin/python Tools/textures/gen_item_textures.py

Writes Assets/Game/Resources/Textures/Items/<name>.png. Blender's tex_<name> materials and Unity's
MaterialLibrary both read from there.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen_textures import paper_base, tile_noise  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Textures", "Items")
FONTS = os.path.join(ROOT, "ArtSource", "fonts")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(15)


def F(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


HAND = "Caveat[wght].ttf"
AGNES = "Kalam-Regular.ttf"
TYPE = "SpecialElite-Regular.ttf"
MONO = "CourierPrime-Regular.ttf"
MONOB = "CourierPrime-Bold.ttf"
TITLE = "IMFeENrm28P.ttf"
TITLEI = "IMFeENit28P.ttf"
SIGN = "Limelight-Regular.ttf"
SCRIPT = "HomemadeApple-Regular.ttf"


def save(img, name):
    img.save(os.path.join(OUT, name + ".png"))


def card(w, h, color=(1.0, 0.97, 0.9), edge=0.18, seed=0):
    return Image.fromarray(paper_base(w, h, color, edge, seed=seed)).convert("RGBA")


def center_text(d, y, text, font, fill, w):
    tw = d.textlength(text, font=font)
    d.text(((w - tw) / 2, y), text, font=font, fill=fill)


def age(img, amount=0.25, seed=0):
    """Foxing, soft wear and a slight blur."""
    a = np.asarray(img.convert("RGBA"), np.float32)
    h, w = a.shape[:2]
    n = np.array(Image.fromarray((tile_noise(128, 6, 4, seed=seed) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), np.float32) / 255
    a[..., :3] *= (1 - amount * 0.35 + n[..., None] * amount * 0.35)
    spots = (rng.random((h, w)) > 0.9985).astype(np.float32)
    spots = np.asarray(Image.fromarray((spots * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2)), np.float32) / 255
    a[..., :3] *= (1 - spots[..., None] * 0.6 * amount)
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.5))


def ticket(name, lines, color=(0.86, 0.8, 0.6), uv_text=None):
    """Edmondson card ticket (horizontal)."""
    w, h = 600, 300
    img = card(w, h, color, 0.22, seed=len(name))
    d = ImageDraw.Draw(img)
    ink = (40, 32, 30, 255)
    d.rectangle([12, 12, w - 13, h - 13], outline=ink, width=3)
    d.line([(w * 0.72, 12), (w * 0.72, h - 12)], fill=ink, width=2)
    y = 26
    for text, font, size in lines:
        f = F(font, size)
        center_text(d, y, text, f, ink, int(w * 0.72))
        y += int(size * 1.12)
    d.text((w * 0.75, 40), "No.", font=F(TYPE, 30), fill=ink)
    d.text((w * 0.75, 80), f"{abs(hash(name)) % 9000 + 1000}", font=F(MONOB, 46), fill=ink)
    # clipper punch hole
    d.ellipse([w * 0.83, h - 90, w * 0.83 + 34, h - 56], fill=(0, 0, 0, 0))
    save(age(img, 0.3, seed=len(name)), name)
    if uv_text:
        uv = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        du = ImageDraw.Draw(uv)
        f = F(MONOB, 34)
        du.text((w * 0.735, h - 120), uv_text, font=f, fill=(255, 255, 255, 255))
        save(uv.filter(ImageFilter.GaussianBlur(1.2)), name + "_uv")


def cloth_label(name, lines, bg=(0.95, 0.92, 0.84), ink=(150, 40, 40), w=520, h=200):
    img = card(w, h, bg, 0.1, seed=len(name) + 3)
    # weave texture
    a = np.asarray(img, np.float32)
    yy, xx = np.mgrid[0:h, 0:w]
    weave = (np.sin(xx * 1.6) * np.sin(yy * 1.6)) * 6
    a[..., :3] += weave[..., None]
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    d.rectangle([8, 8, w - 9, h - 9], outline=ink + (255,), width=3)
    y = 30
    for text, font, size in lines:
        f = F(font, size)
        center_text(d, y, text, f, ink + (255,), w)
        y += int(size * 1.15)
    save(img.filter(ImageFilter.GaussianBlur(0.6)), name)


def note(name, text, font=HAND, size=54, w=600, h=420, ink=(28, 32, 70), color=(1.0, 0.98, 0.9), lines_bg=True, sign=None):
    img = card(w, h, color, 0.2, seed=len(name) + 7)
    d = ImageDraw.Draw(img)
    if lines_bg:
        for y in range(90, h - 20, 56):
            d.line([(30, y), (w - 30, y)], fill=(170, 190, 215, 255), width=2)
    f = F(font, size)
    y = 40
    for line in text.split("\n"):
        d.text((44, y), line, font=f, fill=ink + (255,))
        y += int(size * 1.05)
    if sign:
        fs = F(SCRIPT, 40)
        d.text((w - 60 - d.textlength(sign, font=fs), h - 90), sign, font=fs, fill=ink + (255,))
    save(age(img, 0.25, seed=len(name)), name)


def engraving(name, text, font=TITLEI, size=70, w=700, h=180, base=(201, 161, 90)):
    """Brass plate with engraved (dark, slightly bevelled) letters."""
    a = np.zeros((h, w, 4), np.float32)
    n = tile_noise(256, 8, 3, seed=len(name))
    n = np.array(Image.fromarray((n * 255).astype(np.uint8)).resize((w, h)), np.float32) / 255
    streak = np.repeat(np.asarray(Image.fromarray((rng.random((1, w)) * 255).astype(np.uint8)).resize((w, 1)), np.float32) / 255, h, 0)
    shade = 0.85 + n * 0.1 + streak * 0.05
    for i, c in enumerate(base):
        a[..., i] = c * shade
    a[..., 3] = 255
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    m = Image.new("L", (w, h), 0)
    dm = ImageDraw.Draw(m)
    f = F(font, size)
    tw = dm.textlength(text, font=f)
    dm.text(((w - tw) / 2, (h - size) / 2 - size * 0.15), text, font=f, fill=255)
    mask = np.asarray(m, np.float32) / 255
    hi = np.roll(np.roll(mask, 2, 0), 2, 1)
    arr = np.asarray(img, np.float32)
    arr[..., :3] *= (1 - mask[..., None] * 0.62)
    arr[..., :3] += (np.clip(hi - mask, 0, 1)[..., None] * 40)
    save(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)), name)


def sticker(name, title, sub, bg, fg, shape="oval"):
    w, h = 520, 360
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if shape == "oval":
        d.ellipse([6, 6, w - 7, h - 7], fill=bg + (255,), outline=fg + (255,), width=10)
        d.ellipse([28, 28, w - 29, h - 29], outline=fg + (255,), width=3)
    else:
        d.rounded_rectangle([6, 6, w - 7, h - 7], radius=30, fill=bg + (255,), outline=fg + (255,), width=10)
    center_text(d, 90, title, F(SIGN, 96), fg + (255,), w)
    center_text(d, 215, sub, F(TITLEI, 56), fg + (255,), w)
    # scuffs
    a = np.asarray(img, np.float32)
    n = tile_noise(128, 10, 3, seed=len(name))
    n = np.array(Image.fromarray((n * 255).astype(np.uint8)).resize((w, h)), np.float32) / 255
    wear = (n > 0.7).astype(np.float32) * 0.5
    a[..., :3] = a[..., :3] * (1 - wear[..., None]) + np.array([235, 225, 200], np.float32) * wear[..., None]
    save(Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)), name)


def dial(name, kind):
    S = 600
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    face = (240, 232, 210, 255) if kind != "compass" else (236, 228, 204, 255)
    d.ellipse([4, 4, S - 5, S - 5], fill=face, outline=(60, 50, 40, 255), width=8)
    c = S / 2
    if kind == "watch":
        numerals = ["XII", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI"]
        f = F(TITLE, 54)
        for i, nmr in enumerate(numerals):
            a = math.radians(i * 30 - 90)
            x, y = c + math.cos(a) * 225, c + math.sin(a) * 225
            tw = d.textlength(nmr, font=f)
            d.text((x - tw / 2, y - 32), nmr, font=f, fill=(30, 26, 24, 255))
        for i in range(60):
            a = math.radians(i * 6)
            r0 = 268 if i % 5 else 255
            d.line([(c + math.cos(a) * r0, c + math.sin(a) * r0), (c + math.cos(a) * 280, c + math.sin(a) * 280)], fill=(40, 34, 30, 255), width=3)
        center_text(d, c + 70, "NINEFOLD RLY", F(TYPE, 34), (120, 30, 30, 255), S)
        center_text(d, c - 120, "IX", F(SIGN, 40), (110, 90, 150, 255), S)
    else:
        f = F(SIGN, 80)
        for i, l in enumerate("NESW"):
            a = math.radians(i * 90 - 90)
            x, y = c + math.cos(a) * 215, c + math.sin(a) * 215
            tw = d.textlength(l, font=f)
            d.text((x - tw / 2, y - 46), l, font=f, fill=(30, 26, 24, 255) if l != "N" else (150, 30, 30, 255))
        for i in range(72):
            a = math.radians(i * 5)
            r0 = 262 if i % 2 else 248
            d.line([(c + math.cos(a) * r0, c + math.sin(a) * r0), (c + math.cos(a) * 282, c + math.sin(a) * 282)], fill=(40, 34, 30, 255), width=3)
        # a compass rose
        for k in range(8):
            a = math.radians(k * 45)
            r = 150 if k % 2 == 0 else 90
            pts = [(c + math.cos(a) * r, c + math.sin(a) * r), (c + math.cos(a + 0.2) * 30, c + math.sin(a + 0.2) * 30), (c, c), (c + math.cos(a - 0.2) * 30, c + math.sin(a - 0.2) * 30)]
            d.polygon(pts, fill=(160, 130, 80, 255) if k % 2 == 0 else (120, 100, 70, 255))
    save(age(img, 0.2, seed=len(name)), name)


def photo_placeholder(name, caption, scene, w=400, h=500, border=24, oval=False):
    """Drafted sepia photograph (overwritten by Blender renders when available)."""
    img = Image.new("RGB", (w, h), (238, 230, 214))
    inner = Image.new("RGB", (w - 2 * border, h - 2 * border - (50 if caption else 0)), (120, 100, 78))
    d = ImageDraw.Draw(inner)
    iw, ih = inner.size
    # soft sky gradient
    for y in range(ih):
        t = y / ih
        d.line([(0, y), (iw, y)], fill=(int(150 - 50 * t), int(128 - 44 * t), int(100 - 36 * t)))
    scene(d, iw, ih)
    inner = inner.filter(ImageFilter.GaussianBlur(1.1))
    img.paste(inner, (border, border))
    if caption:
        dd = ImageDraw.Draw(img)
        f = F(HAND, 40)
        center_text(dd, h - border - 50, caption, f, (40, 36, 60), w)
    img = age(img.convert("RGBA"), 0.35, seed=len(name))
    if oval:
        m = Image.new("L", (w, h), 0)
        ImageDraw.Draw(m).ellipse([0, 0, w - 1, h - 1], fill=255)
        img.putalpha(m)
    save(img, name)


def scene_dog(d, w, h):
    gy = int(h * 0.72)
    d.rectangle([0, gy, w, h], fill=(96, 80, 60))
    # dachshund: long body, short legs, long snout, floppy ear
    d.ellipse([w * 0.18, gy - 90, w * 0.78, gy - 30], fill=(58, 40, 28))
    for x in (0.24, 0.34, 0.62, 0.71):
        d.rectangle([w * x, gy - 40, w * x + 16, gy - 2], fill=(52, 36, 25))
    d.ellipse([w * 0.70, gy - 130, w * 0.88, gy - 70], fill=(60, 42, 30))
    d.polygon([(w * 0.84, gy - 112), (w * 0.98, gy - 96), (w * 0.84, gy - 84)], fill=(62, 44, 30))
    d.ellipse([w * 0.72, gy - 118, w * 0.80, gy - 66], fill=(44, 30, 22))
    d.ellipse([w * 0.80, gy - 114, w * 0.825, gy - 104], fill=(230, 220, 200))
    d.line([(w * 0.18, gy - 70), (w * 0.06, gy - 100)], fill=(58, 40, 28), width=8)


def scene_person(d, w, h, hat=False, tone=(70, 56, 44)):
    d.rectangle([0, h * 0.75, w, h], fill=(80, 66, 52))
    d.ellipse([w * 0.3, h * 0.55, w * 0.7, h * 1.2], fill=tone)
    d.ellipse([w * 0.36, h * 0.22, w * 0.64, h * 0.56], fill=(170, 140, 112))
    d.ellipse([w * 0.34, h * 0.16, w * 0.66, h * 0.34], fill=(200, 196, 190))  # grey hair
    d.ellipse([w * 0.43, h * 0.36, w * 0.47, h * 0.39], fill=(40, 30, 24))
    d.ellipse([w * 0.53, h * 0.36, w * 0.57, h * 0.39], fill=(40, 30, 24))
    d.arc([w * 0.44, h * 0.41, w * 0.56, h * 0.49], 20, 160, fill=(80, 40, 30), width=3)


def scene_couple(d, w, h):
    d.rectangle([0, h * 0.7, w, h], fill=(90, 76, 60))
    # station clock at 9:40 above them
    cx, cy, r = w * 0.5, h * 0.2, w * 0.13
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(220, 210, 190), outline=(50, 40, 30), width=5)
    for ang, length in ((math.radians(-90 + 9 * 30 + 20), 0.55), (math.radians(-90 + 240), 0.85)):
        d.line([(cx, cy), (cx + math.cos(ang) * r * length, cy + math.sin(ang) * r * length)], fill=(30, 24, 20), width=5)
    for x0, tone, hat in ((0.22, (60, 70, 56), True), (0.55, (110, 70, 70), False)):
        d.ellipse([w * x0, h * 0.5, w * (x0 + 0.24), h * 1.0], fill=tone)
        d.ellipse([w * (x0 + 0.05), h * 0.36, w * (x0 + 0.19), h * 0.52], fill=(176, 146, 118))
        if hat:
            d.rectangle([w * (x0 + 0.04), h * 0.33, w * (x0 + 0.2), h * 0.37], fill=(60, 64, 50))
            d.rectangle([w * (x0 + 0.07), h * 0.28, w * (x0 + 0.17), h * 0.35], fill=(60, 64, 50))
        else:
            d.ellipse([w * (x0 + 0.03), h * 0.33, w * (x0 + 0.21), h * 0.42], fill=(90, 60, 40))
    # a faint third figure in the background (the secret)
    d.ellipse([w * 0.83, h * 0.45, w * 0.95, h * 0.72], fill=(100, 88, 72))
    d.ellipse([w * 0.86, h * 0.38, w * 0.92, h * 0.46], fill=(130, 112, 92))


def main():
    # 1 wallet
    photo_placeholder("dog_photo", "Biscuit", scene_dog, 400, 300, 16)
    ticket("ticket_harwick", [("NINEFOLD & DISTRICT RLY", TYPE, 28), ("HARWICK", SIGN, 58), ("to", TITLEI, 36), ("NINEFOLD JN.", SIGN, 44), ("THIRD CLASS  ·  SINGLE", TYPE, 28)])
    # 2 scarf
    cloth_label("label_nan", [("Knitted with love", AGNES, 56), ("— Nan —", AGNES, 52)])
    # 3 duck umbrella
    cloth_label("namestrip_bramble", [("B. BRAMBLE", MONOB, 92)], bg=(0.93, 0.93, 0.88), ink=(30, 30, 60), w=600, h=160)
    sticker("sweet_wrapper", "SHERBET", "lemon", (232, 210, 80), (170, 60, 40), shape="rect")
    # 4 black umbrella collar
    engraving("collar_ninefold", "NINEFOLD RLY", font=SIGN, size=80, w=800, h=160)
    engraving("collar_ninefold_alt", "T. HALE · STATION MASTER", font=TITLE, size=50, w=800, h=160)
    # 5 suitcase
    sticker("sticker_lisbon", "LISBOA", "1931", (230, 196, 120), (150, 40, 40))
    sticker("sticker_vienna", "WIEN", "Opernball 1934", (200, 220, 205), (40, 80, 60), shape="rect")
    note("dance_card", "Opernball, Wien\nWaltz ........ T.\nPolka ....... T.\nLast dance . T.", font=TITLEI, size=56, color=(0.98, 0.95, 0.9), lines_bg=False)
    engraving("latch_om", "O. M.", font=TITLEI, size=90, w=360, h=160)
    # 6 compass
    dial("compass_dial", "compass")
    engraving("compass_lid", "Find what is lost", font=TITLEI, size=60, w=700, h=160)
    note("compass_map", "  IX\n /  \\\n— Ninefold —", font=TITLE, size=60, w=420, h=420, lines_bg=False, color=(0.95, 0.9, 0.78))
    # 7 locket
    photo_placeholder("locket_gran", None, lambda d, w, h: scene_person(d, w, h), 300, 380, 0, oval=True)
    note("locket_note", "For Cecily,\nwho never\nloses anything.\n— Gran", font=AGNES, size=60, w=520, h=440, lines_bg=False)
    # 8 pocket watch
    dial("watch_face", "watch")
    engraving("watch_engraving", "Station Master · Ninefold", font=TITLEI, size=60, w=800, h=160, base=(200, 196, 190))
    # 9 rabbit
    cloth_label("rabbit_tag", [("HOPS", SIGN, 80), ("OKAFOR", MONOB, 56)], bg=(0.97, 0.95, 0.9), ink=(40, 60, 120), w=400, h=240)
    # 10 briefcase
    note("racing_form", "NEWMARKET  2.30\n(1) Lucky Biscuit\n(2) Ninefold Lass  O\n(3) Copper Kettle\n(4) Late Train  O", font=MONO, size=40, w=600, h=420, lines_bg=False, ink=(30, 30, 30), color=(0.92, 0.9, 0.84))
    engraving("lining_df", "D. F.", font=TITLEI, size=100, w=360, h=180, base=(160, 40, 50))
    note("betting_slip", "D. FINCH  turf accountant\n£2 on Late Train\nWON  ·  5 to 1", font=TYPE, size=40, w=560, h=300, lines_bg=False, color=(1.0, 0.96, 0.85), ink=(40, 30, 30))
    # 11 the photograph from tomorrow
    photo_placeholder("photo_couple", None, scene_couple, 420, 520, 24)
    note("photo_back", "NINEFOLD STUDIO\nDeveloped\nThurs 18 Oct 1962", font=TYPE, size=38, w=420, h=520, lines_bg=False, color=(0.96, 0.94, 0.88), ink=(90, 40, 120))
    # 12 frosted tin
    engraving("tin_lid", "Lt. A. Penhallow · 1917", font=TITLE, size=62, w=800, h=200, base=(110, 120, 90))
    note("letters_mabel", "Miss Mabel Hart\n14 Canal Row\nNinefold", font=HAND, size=66, w=600, h=380, lines_bg=False, color=(0.95, 0.9, 0.8))
    # 13 lunch tin
    note("note_mavis", "Sidney —\nAsk about the raise!\nLove, Mavis xx", font=AGNES, size=56, w=600, h=380)
    note("child_drawing", "  DAD\n  [=]=[=]=>", font=HAND, size=90, w=600, h=420, lines_bg=False, color=(1.0, 1.0, 0.97), ink=(200, 40, 40))
    # 14 ring
    engraving("ring_engraving", "A — always the 9:40 — T", font=SCRIPT, size=46, w=900, h=120, base=(222, 190, 100))
    ticket("ticket_1921", [("NINEFOLD & WESTERN RLY", TYPE, 28), ("NINEFOLD", SIGN, 56), ("to", TITLEI, 34), ("HARWICK", SIGN, 50), ("FIRST CLASS  ·  9.40", TYPE, 30)], color=(0.9, 0.82, 0.7), uv_text="14.X.21")
    # 15 violin
    cloth_label("violin_label", [("L. DELACROIX", TITLE, 64), ("New Orleans", TITLEI, 56)], bg=(0.9, 0.84, 0.7), ink=(60, 40, 30))
    note("setlist", "SET\n1. Last Train Home\n2. Ninefold Blues\n3. Mabel's Waltz", font=HAND, size=56, w=520, h=420, lines_bg=False)
    photo_placeholder("lou_mother", "Mama", lambda d, w, h: scene_person(d, w, h), 300, 380, 18)
    # 16 record
    S = 600
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([0, 0, S - 1, S - 1], fill=(160, 40, 40, 255))
    d.ellipse([S / 2 - 18, S / 2 - 18, S / 2 + 18, S / 2 + 18], fill=(20, 20, 20, 255))
    center_text(d, 120, "NINEFOLD", F(SIGN, 64), (240, 220, 160, 255), S)
    center_text(d, 360, "LOU DELACROIX", F(TYPE, 40), (240, 220, 160, 255), S)
    center_text(d, 410, "& the Ninefold Five", F(TITLEI, 40), (240, 220, 160, 255), S)
    center_text(d, 200, "Last Train Home", F(TITLEI, 58), (250, 240, 220, 255), S)
    save(img, "record_label")
    note("record_sleeve", "Recorded at\nNinefold Studios\nMarch 1963", font=TYPE, size=54, w=600, h=600, lines_bg=False, color=(0.9, 0.86, 0.76), ink=(40, 30, 30))
    note("sleeve_signature", "To Edie,\nmy leading lady\n— L.", font=SCRIPT, size=48, w=600, h=420, lines_bg=False, color=(0.9, 0.86, 0.76))
    # 17 spectacles
    engraving("spec_case", "E. Plum · Optician to the Great Western · 1889", font=TITLEI, size=40, w=900, h=150, base=(90, 60, 50))
    note("timetable_1903", "GREAT WESTERN\nWinter 1903\nNinefold  9.40\nHarwick   10.15", font=MONO, size=44, w=520, h=420, lines_bg=False, color=(0.92, 0.88, 0.76), ink=(40, 30, 30))
    # 18 birdcage
    engraving("cage_plate", "A. L.", font=TITLEI, size=90, w=360, h=180)
    # 19 snow globe
    engraving("globe_base", "Souvenir of Ninefold", font=TITLEI, size=56, w=800, h=150, base=(120, 60, 50))
    uv = Image.new("RGBA", (800, 150), (0, 0, 0, 0))
    center_text(ImageDraw.Draw(uv), 40, "Fri 21 Dec 1962", F(MONOB, 64), (255, 255, 255, 255), 800)
    save(uv.filter(ImageFilter.GaussianBlur(1.0)), "globe_base_uv")
    # 20 black wallet
    note("library_card", "NINEFOLD PUBLIC LIBRARY\nReader: M. QUILL\nNo. 0471", font=TYPE, size=44, w=600, h=360, lines_bg=False, color=(0.85, 0.88, 0.95), ink=(30, 30, 60))
    note("iou_note", "I.O.U. one queen\n— A.", font=HAND, size=60, w=520, h=300)
    # 21 chess set
    note("chess_note", "Your move, Arthur.\n— M.", font=AGNES, size=60, w=560, h=300)
    # 23 hatbox
    w, h = 1024, 256
    img = Image.new("RGBA", (w, h), (0, 0, 0, 255))
    d = ImageDraw.Draw(img)
    for i in range(0, w, 64):
        d.rectangle([i, 0, i + 31, h], fill=(220, 200, 170, 255))
        d.rectangle([i + 32, 0, i + 63, h], fill=(150, 60, 70, 255))
    save(age(img, 0.3), "hatbox_stripes")
    ticket("theatre_stub", [("THEATRE ROYAL", TYPE, 34), ("TWELFTH NIGHT", SIGN, 46), ("with", TITLEI, 32), ("Miss Edie Larkspur", TITLEI, 46), ("STALLS  ·  ROW F", TYPE, 30)], color=(0.95, 0.85, 0.85))
    note("love_letter", "My dearest,\nI never sent this.\nI never could.", font=SCRIPT, size=44, w=600, h=420, lines_bg=False)
    # 25 iron key tag
    note("key_tag", "Iron Drawer\nDo not lend.\n— A.P.", font=AGNES, size=60, w=420, h=420, lines_bg=False, color=(0.95, 0.85, 0.65))
    # documents presented by claimants
    note("chit_vell", "Hold for Mr V.\n— A. Pell", font=AGNES, size=64, w=600, h=380, lines_bg=False, color=(0.95, 0.93, 0.86))
    uv = Image.new("RGBA", (600, 380), (0, 0, 0, 0))
    ImageDraw.Draw(uv).text((40, 230), "NEVER TO VELL — A.P.", font=F(AGNES, 46), fill=(255, 255, 255, 255))
    save(uv.filter(ImageFilter.GaussianBlur(1.0)), "chit_vell_uv")
    note("certificate_crane", "CERTIFICATE OF DESCENT\nVictor Crane is the great-grandson\nof the late Lt. A. Penhallow\nNinefold Registry", font=TYPE, size=34, w=700, h=480, lines_bg=False, color=(0.96, 0.93, 0.84), ink=(40, 30, 30))
    note("requisition_vell", "NINEFOLD JUNCTION\nREQUISITION ORDER\nRelease to bearer:\n— the Station Master's watch\n— the brass compass\n— the Iron Drawer key\nSigned, Station Master", font=TYPE, size=34, w=700, h=600, lines_bg=False, color=(0.95, 0.94, 0.9), ink=(30, 30, 40))
    uv = Image.new("RGBA", (700, 600), (0, 0, 0, 0))
    du = ImageDraw.Draw(uv)
    du.text((60, 470), "dated Sat 20 Oct 1962", font=F(MONOB, 40), fill=(255, 255, 255, 255))
    du.text((60, 520), "signed: E. VELL", font=F(MONOB, 44), fill=(255, 255, 255, 255))
    save(uv.filter(ImageFilter.GaussianBlur(1.0)), "requisition_vell_uv")
    print("item textures written to", OUT)


if __name__ == "__main__":
    main()
