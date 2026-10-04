"""Lay preview renders out as a labelled contact sheet:  .venv/bin/python ArtSource/contact_sheet.py DIR PREFIX OUT.png"""
import glob
import os
import sys

from PIL import Image, ImageDraw, ImageFont

d, prefix, out = sys.argv[1], sys.argv[2], sys.argv[3]
files = sorted(glob.glob(os.path.join(d, prefix + "*.png")))
cols = int(sys.argv[4]) if len(sys.argv) > 4 else 4
cell = 320
rows = (len(files) + cols - 1) // cols
sheet = Image.new("RGB", (cols * cell, rows * (cell + 24)), (30, 26, 24))
dr = ImageDraw.Draw(sheet)
font = ImageFont.truetype("/usr/share/fonts/TTF/DejaVuSans.ttf", 15)
for i, f in enumerate(files):
    im = Image.open(f).convert("RGB")
    im.thumbnail((cell, cell))
    x, y = (i % cols) * cell, (i // cols) * (cell + 24)
    sheet.paste(im, (x, y + 24))
    dr.text((x + 6, y + 4), os.path.basename(f)[len(prefix):-4], font=font, fill=(230, 220, 200))
sheet.save(out)
print(out, len(files))
