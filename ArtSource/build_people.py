"""The commuters: a modular character generator. Each character is a spec (build, skin, hair, hat,
moustache, coat, accessories...) assembled from stylized parts and exported with the pieces the game
animates: Body (torso, arms) > Head (pivot at the neck) > EyeL/EyeR, BrowL/BrowR, Mouth, Hat; HandL/HandR.

    blender -b -P ArtSource/build_people.py [-- --only walter,odile] [--preview /tmp/p]

Unity space, metres; feet at the origin, facing -z (towards the booth).
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib"))
import bmesh  # noqa: E402
import bpy  # noqa: E402
import mathutils  # noqa: E402
from laf import (V, Model, box, cbox, cyl, empty, export_fbx, lathe, mat, rod, sphere, torus, ellipsoid,  # noqa: E402
                 reset_scene, setup_preview, render, sweep, hull, loft, transformed, rotate_about, args_after_dashes,
                 text_mesh, shade)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Models", "People")

EYE = mat("eye", "141018")
MOUTH = mat("paint", "4A1E1E")
BRASS = mat("brass", "C9A15A")


def section(cx, cy, cz, hw, hd, r=0.6, steps=10):
    """Rounded-rectangle-ish horizontal cross-section (superellipse) at height cy."""
    pts = []
    n = steps * 4
    for i in range(n):
        a = 2 * math.pi * i / n
        c, s = math.cos(a), math.sin(a)
        e = 2.0 / (2.0 + 2.0 * r)  # exponent: r=0 ellipse, larger = boxier
        x = math.copysign(abs(c) ** e, c) * hw
        z = math.copysign(abs(s) ** e, s) * hd
        pts.append((cx + x, cy, cz + z))
    return pts


def torso_shape(spec):
    """List of (y, half-width, half-depth) rings from hips to shoulders."""
    b = spec.get("build", "average")
    w = {"slim": 0.82, "average": 1.0, "round": 1.25, "broad": 1.2, "tiny": 0.9, "thin": 0.78, "huge": 1.45, "child": 0.72}[b]
    belly = {"round": 1.18, "huge": 1.1}.get(b, 1.0)
    return [
        (0.80, 0.17 * w, 0.11 * w),
        (0.95, 0.165 * w * belly, 0.12 * w * belly),
        (1.10, 0.17 * w * belly, 0.125 * w * belly),
        (1.25, 0.18 * w, 0.115 * w),
        (1.36, 0.19 * w, 0.10 * w),
        (1.42, 0.15 * w, 0.085 * w),
        (1.45, 0.07, 0.06),
    ]


def build_body(spec):
    coat = spec["coat"]
    body = Model("Body")
    rings = torso_shape(spec)
    secs = [section(0, y, 0, hw, hd, r=0.8) for (y, hw, hd) in rings]
    body.add(loft(secs), mat(spec.get("coat_mat", "cloth"), coat))
    # skirt / coat tails / legs
    w = rings[0][1]
    if spec.get("skirt"):
        body.add(lathe([(w * 1.25, 0.35), (w * 1.05, 0.62), (w * 0.98, 0.82)], (0, 0, 0), segments=32, cap_bottom=True, cap_top=False), mat("cloth", spec.get("skirt")))
        legs_bottom = 0.0
        for sx in (-1, 1):
            body.add(rod((sx * 0.06, 0.06, 0), (sx * 0.06, 0.38, 0), 0.04), mat("cloth", spec.get("stockings", "3A2E2A")))
    else:
        trousers = mat("cloth", spec.get("trousers", shade(coat, 0.7)))
        for sx in (-1, 1):
            body.add(rod((sx * 0.075, 0.06, 0), (sx * 0.08, 0.84, 0), 0.062, radius2=0.075), trousers)
        if spec.get("long_coat"):
            body.add(lathe([(w * 1.12, 0.45), (w * 1.05, 0.62), (w * 0.99, 0.82)], (0, 0, 0), segments=32, cap_bottom=False, cap_top=False), mat(spec.get("coat_mat", "cloth"), coat))
    for sx in (-1, 1):
        body.add(ellipsoid((sx * 0.075, 0.035, -0.04), (0.055, 0.04, 0.11)), mat("leather", spec.get("shoes", "2A1A12")))
    # arms: sleeves hanging from the shoulders
    sw = rings[4][1]
    for sx in (-1, 1):
        sh = V(sx * (sw + 0.01), 1.37, 0)
        elbow = V(sx * (sw + 0.05), 1.10, 0.01)
        wrist = V(sx * (sw + 0.04), 0.88, -0.03)
        body.add(sweep([sh, sh + V(sx * 0.02, -0.06, 0), elbow, wrist], 0.055, radius_end=0.045, segments=14), mat(spec.get("coat_mat", "cloth"), coat))
        body.add(sphere(sh + V(0, 0.0, 0), 0.06), mat(spec.get("coat_mat", "cloth"), coat))
        if spec.get("cuffs"):
            body.add(torus(wrist + V(0, 0.01, 0), 0.044, 0.008, axis="y", segments=16), mat("cloth", spec["cuffs"]))
    # collar / shirt front / tie
    shirt = mat("cloth", spec.get("shirt", "EDE6D6"))
    body.add(hull([(-0.06, 1.44, -0.07), (0.06, 1.44, -0.07), (0.0, 1.27, -0.118 * (1.2 if spec.get("build") == "round" else 1)), (-0.07, 1.42, -0.04), (0.07, 1.42, -0.04)]), shirt)
    if spec.get("tie"):
        tie = mat("cloth", spec["tie"])
        wide = 1.6 if spec.get("tie_wide") else 1.0
        body.add(hull([(-0.012, 1.43, -0.085), (0.012, 1.43, -0.085), (-0.022 * wide, 1.28, -0.122), (0.022 * wide, 1.28, -0.122), (0.0, 1.25, -0.124)]), tie)
    if spec.get("bowtie"):
        bt = mat("cloth", spec["bowtie"])
        for sx in (-1, 1):
            body.add(hull([(0, 1.425, -0.09), (sx * 0.04, 1.44, -0.09), (sx * 0.04, 1.405, -0.09), (0, 1.425, -0.08)]), bt)
    if spec.get("scarf"):
        sc = mat("knit", spec["scarf"])
        body.add(torus((0, 1.43, -0.005), 0.085, 0.03, axis="y", segments=24), sc)
        body.add(sweep([(0.05, 1.42, -0.09), (0.06, 1.3, -0.13), (0.055, 1.18, -0.135)], 0.025, segments=10, flat=0.45), sc)
    # lapels
    lap = mat(spec.get("coat_mat", "cloth"), shade(coat, 0.85))
    for sx in (-1, 1):
        body.add(hull([(sx * 0.03, 1.44, -0.085), (sx * 0.11, 1.4, -0.075), (sx * 0.02, 1.2, -0.125), (sx * 0.035, 1.44, -0.095)]), lap)
    for k in range(spec.get("buttons", 3)):
        body.add(sphere((0.0 if spec.get("double") is None else 0.035, 1.18 - k * 0.09, -0.135 - (0.02 if spec.get("build") == "round" and k < 2 else 0)), 0.011, scale=(1, 1, 0.5)), mat("brass", "C9A15A") if spec.get("brass_buttons") else mat("plastic", "2A2420"))
    if spec.get("stole"):
        st = mat("fur", spec["stole"])
        body.add(torus((0, 1.41, 0.0), 0.15, 0.05, axis="y", segments=28, scale=(1.05, 1, 0.75)), st)
    if spec.get("flower"):
        body.add(sphere((-0.11, 1.32, -0.105), 0.022), mat("cloth", spec["flower"]))
        body.add(sphere((-0.11, 1.32, -0.12), 0.01), mat("cloth", "F2E2A0"))
    if spec.get("badge"):
        body.add(cyl((0.1, 1.3, -0.11), 0.022, 0.006, axis="z", segments=16), BRASS)
    if spec.get("satchel"):
        body.add(sweep([(-0.16, 1.4, -0.05), (0.0, 1.1, -0.15), (0.16, 0.95, -0.1)], 0.012, segments=6, flat=0.4), mat("leather", spec["satchel"]))
        body.add(cbox((0.17, 0.92, -0.08), (0.06, 0.16, 0.2), bevel=0.02), mat("leather", spec["satchel"]))
    if spec.get("flour"):
        for sx, y in ((-1, 1.38), (1, 1.36), (-1, 1.2)):
            body.add(ellipsoid((sx * 0.14, y, -0.07), (0.05, 0.012, 0.04)), mat("cloth", "F4F0E6"))
    if spec.get("apron"):
        body.add(hull([(-0.14, 1.25, -0.13), (0.14, 1.25, -0.13), (-0.17, 0.75, -0.16), (0.17, 0.75, -0.16), (0, 1.0, -0.17)]), mat("cloth", spec["apron"]))
    return body


def build_head(spec):
    skin = mat("skin", spec["skin"])
    base = spec.get("head", (0.105, 0.125, 0.11))
    k = 1.24  # stylized: big heads read well through the window
    hw, hh, hd = base[0] * k, base[1] * k, base[2] * k
    hc = V(0, 1.47 + hh * 0.95, 0)
    head = Model("Head")
    head.add(rod((0, 1.41, 0), (0, hc.y - hh * 0.55, 0), 0.05), skin)
    jaw = spec.get("jaw", 1.0)
    head.add(ellipsoid(hc, (hw, hh, hd), segments=32, rings=20), skin)
    head.add(ellipsoid(hc + V(0, -hh * 0.45, -hd * 0.12), (hw * 0.85 * jaw, hh * 0.5, hd * 0.82), segments=28, rings=16), skin)
    # ears
    ear = spec.get("ears", 1.0)
    for sx in (-1, 1):
        head.add(ellipsoid(hc + V(sx * hw * 0.98, -0.005, 0.01), (0.018 * ear, 0.032 * ear, 0.022 * ear)), skin)
    # nose
    nose = spec.get("nose", "round")
    nz = -hd * 0.98
    nsk = mat("skin", shade(spec["skin"], 0.96))
    if nose == "round":
        head.add(sphere(hc + V(0, -0.024, nz), 0.03), nsk)
    elif nose == "button":
        head.add(sphere(hc + V(0, -0.022, nz), 0.021), nsk)
    elif nose == "long":
        head.add(hull([hc + V(0, 0.03, nz + 0.01), hc + V(-0.014, -0.035, nz), hc + V(0.014, -0.035, nz), hc + V(0, -0.04, nz - 0.04)], bevel=0.006), nsk)
    elif nose == "hook":
        head.add(hull([hc + V(0, 0.025, nz + 0.01), hc + V(-0.012, -0.04, nz + 0.005), hc + V(0.012, -0.04, nz + 0.005), hc + V(0, -0.005, nz - 0.035), hc + V(0, -0.045, nz - 0.02)], bevel=0.006), nsk)
    elif nose == "big":
        head.add(sphere(hc + V(0, -0.03, nz - 0.006), 0.04, scale=(1, 0.9, 1)), nsk)
    # cheeks
    if spec.get("rosy"):
        for sx in (-1, 1):
            head.add(sphere(hc + V(sx * hw * 0.55, -0.035, -hd * 0.78), 0.022, scale=(1, 0.7, 0.4)), mat("skin", spec["rosy"]))
    # hair
    hair_col = spec.get("hair_col", "3A2A20")
    hair = mat("hair", hair_col)
    style = spec.get("hair", "short")
    if style in ("short", "slick", "ginger"):
        head.add(ellipsoid(hc + V(0, 0.03, 0.01), (hw * 1.04, hh * 0.86, hd * 1.04), segments=28, rings=16), hair,
                 transform=None)
        if style == "slick":
            head.add(ellipsoid(hc + V(0, 0.07, -0.02), (hw * 0.9, 0.03, hd * 0.95)), hair)
    elif style == "bald":
        for sx in (-1, 1):
            head.add(ellipsoid(hc + V(sx * hw * 0.9, -0.0, 0.03), (0.03, 0.05, 0.07)), hair)
        head.add(ellipsoid(hc + V(0, -0.01, hd * 0.85), (hw * 0.8, 0.05, 0.03)), hair)
    elif style == "bob":
        head.add(ellipsoid(hc + V(0, 0.01, 0.02), (hw * 1.16, hh * 0.98, hd * 1.08), segments=28, rings=16), hair)
        head.add(ellipsoid(hc + V(0, hh * 0.5, -hd * 0.72), (hw * 0.98, hh * 0.24, hd * 0.38)), hair)
    elif style == "bun":
        head.add(ellipsoid(hc + V(0, 0.03, 0.01), (hw * 1.05, hh * 0.9, hd * 1.05)), hair)
        head.add(sphere(hc + V(0, 0.07, hd * 0.95), 0.05), hair)
    elif style == "curls":
        for i in range(14):
            a = 2 * math.pi * i / 14
            head.add(sphere(hc + V(math.cos(a) * hw * 0.85, 0.06 + 0.03 * math.sin(a * 3), math.sin(a) * hd * 0.8), 0.04), hair)
        head.add(ellipsoid(hc + V(0, 0.06, 0), (hw * 0.95, hh * 0.7, hd * 0.95)), hair)
    elif style == "braids":
        head.add(ellipsoid(hc + V(0, 0.03, 0.01), (hw * 1.05, hh * 0.88, hd * 1.05)), hair)
        for sx in (-1, 1):
            head.add(sweep([hc + V(sx * hw * 0.9, -0.02, 0.02), hc + V(sx * hw * 1.05, -0.12, 0.0), hc + V(sx * hw * 1.0, -0.2, -0.02)], 0.022, segments=10), hair)
            head.add(sphere(hc + V(sx * hw * 1.0, -0.215, -0.02), 0.015), mat("cloth", spec.get("ribbon", "C03030")))
    elif style == "platinum":
        for i in range(10):
            a = math.pi * (0.05 + 0.9 * i / 9)
            head.add(sphere(hc + V(math.cos(a) * hw * 1.0, 0.0 + math.sin(a) * hh * 0.7, 0.01), 0.04), hair)
        head.add(ellipsoid(hc + V(0, 0.04, 0.02), (hw * 1.02, hh * 0.8, hd * 1.02)), hair)
    elif style == "wild":
        head.add(ellipsoid(hc + V(0, 0.05, 0.01), (hw * 1.2, hh * 0.9, hd * 1.15)), hair)
    # facial hair
    fh = spec.get("moustache")
    fhc = mat("hair", spec.get("moustache_col", hair_col))
    my = hc.y - hh * 0.34
    if fh == "walrus":
        head.add(ellipsoid(V(0, my, nz + 0.01), (0.055, 0.018, 0.02)), fhc)
        for sx in (-1, 1):
            head.add(ellipsoid(V(sx * 0.04, my - 0.012, nz + 0.014), (0.022, 0.024, 0.015)), fhc)
    elif fh == "pencil":
        head.add(box((-0.03, my + 0.003, nz + 0.012), (0.03, my + 0.008, nz + 0.018)), fhc)
    elif fh == "handlebar":
        head.add(ellipsoid(V(0, my, nz + 0.012), (0.035, 0.012, 0.014)), fhc)
        for sx in (-1, 1):
            head.add(sweep([(sx * 0.03, my, nz + 0.014), (sx * 0.06, my + 0.004, nz + 0.02), (sx * 0.075, my + 0.02, nz + 0.03)], 0.007, radius_end=0.002, segments=8), fhc)
    elif fh == "huge":
        head.add(ellipsoid(V(0, my, nz + 0.005), (0.075, 0.022, 0.024)), fhc)
        for sx in (-1, 1):
            head.add(sweep([(sx * 0.05, my, nz + 0.01), (sx * 0.09, my - 0.005, nz + 0.03), (sx * 0.11, my + 0.015, nz + 0.05)], 0.014, radius_end=0.004, segments=8), fhc)
    if spec.get("beard") == "goatee":
        head.add(ellipsoid(V(0, hc.y - 0.11, nz + 0.03), (0.022, 0.03, 0.02)), fhc)
    if spec.get("beard") == "muttonchops":
        for sx in (-1, 1):
            head.add(ellipsoid(hc + V(sx * hw * 0.8, -0.06, -0.02), (0.03, 0.06, 0.05)), fhc)
    if spec.get("glasses") in ("round", "owl"):
        r = 0.026 if spec["glasses"] == "round" else 0.032
        gm = mat("brass" if spec.get("glasses_brass", True) else "paint", "B8964E" if spec.get("glasses_brass", True) else "1A1A1A")
        for sx in (-1, 1):
            head.add(torus(V(sx * hw * 0.4, hc.y + 0.014, nz + 0.02), r * 1.25, 0.004, axis="z", segments=24), gm)
            head.add(cyl(V(sx * hw * 0.4, hc.y + 0.014, nz + 0.02), r * 1.25, 0.001, axis="z", segments=24), mat("glass", "C8DCE4"))
        head.add(rod((-hw * 0.2, hc.y + 0.016, nz + 0.02), (hw * 0.2, hc.y + 0.016, nz + 0.02), 0.003), gm)
    if spec.get("glasses") == "monocle":
        head.add(torus(V(-hw * 0.4, hc.y + 0.014, nz + 0.02), 0.032, 0.0035, axis="z", segments=24), BRASS)
        head.add(sweep([(-hw * 0.4 - 0.03, hc.y + 0.0, nz + 0.018), (-hw * 0.7, hc.y - 0.12, nz + 0.0), (-0.07, 1.36, -0.1)], 0.0015, segments=5), BRASS)
    if spec.get("glasses") == "shades":
        for sx in (-1, 1):
            head.add(ellipsoid(V(sx * hw * 0.4, hc.y + 0.012, nz + 0.018), (0.036, 0.024, 0.008)), mat("glass", "101418"))
        head.add(rod((-hw * 0.2, hc.y + 0.016, nz + 0.018), (hw * 0.2, hc.y + 0.016, nz + 0.018), 0.003), BLACKISH)
    lids = spec.get("lids", 0.0)
    if lids > 0:
        for sx in (-1, 1):
            c = V(sx * hw * 0.4, hc.y + 0.012 + 0.021 * spec.get("eye_size", 1.0) * (1 - lids * 0.9), -hd * 0.9)
            head.add(ellipsoid(c, (0.021 * spec.get("eye_size", 1.0), 0.016 * spec.get("eye_size", 1.0), 0.012)), mat("skin", shade(spec["skin"], 0.92)))
    return head, hc, (hw, hh, hd), nz


BLACKISH = mat("paint", "1A1A1A")


def build_face(head_obj, spec, hc, dims, nz):
    hw, hh, hd = dims
    eye_y = hc.y + 0.012 + spec.get("eye_y", 0.0)
    eye_x = hw * spec.get("eye_x", 0.4)
    es = spec.get("eye_size", 1.0)
    for name, sx in (("EyeL", -1), ("EyeR", 1)):
        e = Model(name)
        c = V(sx * eye_x, eye_y, -hd * 0.9)
        e.add(ellipsoid(c, (0.016 * es, 0.021 * es, 0.01)), EYE)
        e.add(sphere(c + V(0.005 * es, 0.007 * es, -0.009), 0.0042 * es), mat("emit", "FFFFFF"))
        e.build(origin=c, parent=head_obj)
    brow_col = mat("hair", spec.get("brow_col", spec.get("hair_col", "3A2A20")))
    bt = spec.get("brow", 1.0)
    for name, sx in (("BrowL", -1), ("BrowR", 1)):
        b = Model(name)
        c = V(sx * eye_x, eye_y + 0.038 * es, -hd * 0.93)
        ang = spec.get("brow_angle", 8) * sx
        b.add(cbox(c, (0.036 * bt, 0.009 * bt, 0.01)), brow_col, transform=rotate_about(c, "Z", ang))
        b.build(origin=c, parent=head_obj)
    mo = Model("Mouth")
    c = V(0, hc.y - hh * 0.52 + spec.get("mouth_y", 0.0), -hd * 0.975)
    mw = spec.get("mouth_w", 1.0)
    mo.add(ellipsoid(c, (0.03 * mw, 0.008, 0.008)), MOUTH)
    if spec.get("smile"):
        for sx in (-1, 1):
            mo.add(sphere(c + V(sx * 0.03 * mw, 0.006, 0.001), 0.0055), MOUTH)
    mo.build(origin=c, parent=head_obj)


def build_hat(head_obj, spec, hc, dims):
    hat = spec.get("hat")
    if not hat:
        return
    hw, hh, hd = dims
    col = spec.get("hat_col", "3A3530")
    hm = mat(spec.get("hat_mat", "felt"), col)
    band = mat("cloth", spec.get("hat_band", shade(col, 0.6)))
    top = hc.y + hh * 0.72
    h = Model("Hat")
    if hat == "flatcap":
        h.add(ellipsoid(V(0, top, 0.01), (hw * 1.1, 0.04, hd * 1.12)), hm)
        h.add(ellipsoid(V(0, top - 0.008, -hd * 0.9), (hw * 0.95, 0.012, 0.06)), hm)
    elif hat == "trilby":
        h.add(torus(V(0, top - 0.005, 0), hw * 1.25, 0.012, axis="y", segments=40, scale=(1, 0.25, 1.05)), hm)
        h.add(cyl(V(0, top - 0.005, 0), hw * 1.45, 0.006, segments=40), hm)
        h.add(lathe([(hw * 0.95, top - 0.005), (hw * 0.92, top + 0.06), (hw * 0.7, top + 0.1), (0.001, top + 0.09)], (0, 0, 0), segments=32), hm)
        h.add(torus(V(0, top + 0.015, 0), hw * 0.95, 0.014, axis="y", segments=32, scale=(1, 0.8, 1)), band)
    elif hat == "bowler":
        h.add(cyl(V(0, top - 0.008, 0), hw * 1.25, 0.008, segments=40, bevel=0.003), hm)
        h.add(lathe([(hw * 0.96, top - 0.005), (hw * 1.0, top + 0.05), (hw * 0.8, top + 0.11), (0.001, top + 0.125)], (0, 0, 0), segments=36), hm)
        h.add(torus(V(0, top + 0.01, 0), hw * 0.97, 0.01, axis="y", segments=32), band)
    elif hat == "beret":
        h.add(ellipsoid(V(0.02, top + 0.005, 0.0), (hw * 1.15, 0.032, hd * 1.15)), hm)
        h.add(sphere(V(0.02, top + 0.04, 0), 0.008), hm)
    elif hat == "porter":
        h.add(cyl(V(0, top + 0.02, 0), hw * 1.05, 0.06, segments=36, bevel=0.006), hm)
        h.add(ellipsoid(V(0, top - 0.01, -hd * 0.95), (hw * 0.85, 0.01, 0.055)), mat("plastic", "141414"))
        h.add(cyl(V(0, top + 0.03, -hw * 1.05), 0.018, 0.006, axis="z", segments=16), BRASS)
        h.add(torus(V(0, top + 0.0, 0), hw * 1.05, 0.008, axis="y", segments=32), band)
    elif hat == "custodian":
        h.add(lathe([(hw * 1.1, top - 0.02), (hw * 1.05, top + 0.06), (hw * 0.8, top + 0.16), (hw * 0.3, top + 0.21), (0.001, top + 0.22)], (0, 0, 0), segments=36), hm)
        h.add(sphere(V(0, top + 0.225, 0), 0.016), mat("silver", "C0C0C8"))
        h.add(cyl(V(0, top + 0.08, -hw * 1.0), 0.03, 0.006, axis="z", segments=20), mat("silver", "C0C0C8"))
        h.add(torus(V(0, top - 0.015, 0), hw * 1.1, 0.01, axis="y", segments=32), mat("leather", "1A1A1A"))
    elif hat == "feathered":
        h.add(cyl(V(0, top - 0.01, 0), hw * 2.0, 0.012, segments=48, bevel=0.004), hm)
        h.add(lathe([(hw * 1.0, top - 0.005), (hw * 0.96, top + 0.07), (hw * 0.6, top + 0.1), (0.001, top + 0.1)], (0, 0, 0), segments=36), hm)
        h.add(torus(V(0, top + 0.02, 0), hw * 1.0, 0.016, axis="y", segments=32), band)
        for k, (dx, ang) in enumerate(((0.06, 30), (0.08, 55), (0.04, 10))):
            h.add(sweep([(dx, top + 0.05, 0.04), (dx + 0.06, top + 0.14 + k * 0.02, 0.07), (dx + 0.12 + k * 0.03, top + 0.2 + k * 0.03, 0.02)], 0.022, radius_end=0.004, segments=10, flat=0.3), mat("fur", spec.get("feather", "E8E0F0")))
        h.add(sphere(V(0.07, top + 0.04, -0.05), 0.02), mat("cloth", spec.get("hat_flower", "C05070")))
    elif hat == "pillbox":
        h.add(cyl(V(0.02, top + 0.02, 0), hw * 0.85, 0.05, segments=36, bevel=0.006), hm)
        h.add(sweep([(-hw * 0.9, top + 0.02, -hd * 0.6), (0.0, top - 0.03, -hd * 1.15), (hw * 0.9, top + 0.02, -hd * 0.6)], 0.03, segments=8, flat=0.08), mat("cloth", spec.get("veil", "2A2A2A")))
    elif hat == "peaked":
        h.add(cyl(V(0, top + 0.025, 0), hw * 1.08, 0.055, segments=36, bevel=0.006), hm)
        h.add(cyl(V(0, top + 0.06, 0.01), hw * 1.15, 0.012, segments=36), hm)
        h.add(ellipsoid(V(0, top - 0.005, -hd * 0.95), (hw * 0.85, 0.01, 0.055)), mat("leather", "2A1A10"))
        h.add(cyl(V(0, top + 0.035, -hw * 1.08), 0.016, 0.006, axis="z", segments=16), BRASS)
    elif hat == "tophat":
        h.add(cyl(V(0, top - 0.008, 0), hw * 1.4, 0.008, segments=40), hm)
        h.add(cyl(V(0, top + 0.09, 0), hw * 0.95, 0.19, segments=36, bevel=0.004), hm)
        h.add(torus(V(0, top + 0.02, 0), hw * 0.96, 0.012, axis="y", segments=32), band)
    elif hat == "porkpie":
        h.add(cyl(V(0, top - 0.005, 0), hw * 1.35, 0.008, segments=40), hm)
        h.add(cyl(V(0, top + 0.035, 0), hw * 0.98, 0.075, segments=36, bevel=0.01), hm)
        h.add(torus(V(0, top + 0.012, 0), hw * 0.99, 0.012, axis="y", segments=32), band)
    elif hat == "beanie":
        h.add(ellipsoid(V(0, top - 0.005, 0.0), (hw * 1.1, 0.07, hd * 1.1)), mat("knit", col))
        h.add(torus(V(0, top - 0.04, 0), hw * 1.08, 0.016, axis="y", segments=32), mat("knit", shade(col, 0.8)))
    elif hat == "cloche":
        h.add(lathe([(hw * 1.25, top - 0.06), (hw * 1.15, top + 0.02), (hw * 0.85, top + 0.08), (0.001, top + 0.09)], (0, 0, 0), segments=36), hm)
        h.add(torus(V(0, top - 0.0, 0), hw * 1.12, 0.012, axis="y", segments=32), band)
    elif hat == "headscarf":
        h.add(ellipsoid(V(0, top - 0.03, 0.01), (hw * 1.18, hh * 0.7, hd * 1.2)), mat("cloth", col))
        h.add(sphere(V(0, hc.y - 0.12, -hd * 0.5), 0.018), mat("cloth", col))
    elif hat == "bun_net":
        pass
    h.build(parent=head_obj)


def build_hands(body_obj, spec):
    skin = mat("leather" if spec.get("gloves") else "skin", spec.get("gloves", spec["skin"]))
    sw = torso_shape(spec)[4][1]
    for name, sx in (("HandL", -1), ("HandR", 1)):
        c = V(sx * (sw + 0.04), 0.845, -0.04)
        hnd = Model(name)
        hnd.add(ellipsoid(c, (0.028, 0.045, 0.035)), skin)
        hnd.add(ellipsoid(c + V(-sx * 0.022, 0.005, -0.012), (0.012, 0.025, 0.012)), skin)
        hnd.build(origin=c, parent=body_obj)


def build_character(cid, spec):
    """Every part is a direct child of the root (FBX hierarchies under moved parents come out
    mangled); the game re-parents Head under Body and the face parts under Head on load."""
    root = empty(cid)
    body = build_body(spec)
    body.build(origin=(0, 0.95, 0), parent=root)
    head, hc, dims, nz = build_head(spec)
    head.build(origin=(0, 1.45, 0), parent=root)
    build_face(root, spec, hc, dims, nz)
    build_hat(root, spec, hc, dims)
    build_hands(root, spec)
    return root


# ----------------------------------------------------------------------------- the cast

CAST = {
    "gus": dict(build="slim", skin="E6B08A", hair="ginger", hair_col="B5562A", ears=1.5, nose="button", rosy="E58C70",
                coat="22304A", coat_mat="cloth", trousers="1E2638", hat="porter", hat_col="8A2420", hat_band="1A1A1A",
                brass_buttons=True, buttons=4, cuffs="8A2420", tie="1A1A1A"),
    "walter": dict(lids=0.18, build="round", skin="E8B48E", hair="bald", hair_col="9A928A", moustache="walrus", moustache_col="C8C0B4",
                   nose="big", rosy="E89080", coat="6A5038", hat="flatcap", hat_col="7A6448", head=(0.115, 0.125, 0.115),
                   flour=True, apron="EDE6D6", shirt="E8E2D4", brow=1.3, brow_col="B8B0A4", buttons=0),
    "clementine": dict(build="slim", skin="F2CCAE", hair="bob", hair_col="1E1614", nose="button", glasses="round",
                       coat="C8A040", coat_mat="knit", shirt="F4EEE2", hat="beret", hat_col="7A2232", skirt="3A3A4A",
                       eye_size=1.15, mouth_w=0.8, brow_angle=-6, buttons=4),
    "reggie": dict(lids=0.38, build="average", skin="DDAA84", hair="slick", hair_col="141010", moustache="pencil", nose="long",
                   coat="4A5468", tie="E07A20", tie_wide=True, hat="trilby", hat_col="6A5038", hat_band="1A1A1A",
                   smile=True, mouth_w=1.2, brow_angle=14, flower="C02020", buttons=2),
    "odile": dict(build="tiny", skin="EEC8AC", hair="bun", hair_col="E8E4DE", nose="button", rosy="E8A0A0", coat="6A3A5A",
                  hat="feathered", hat_col="4A2A5A", feather="E8D8F0", stole="D8CCB8", skirt="4A2A3A", eye_size=0.9,
                  smile=True, buttons=0, height=0.86),
    "bramble": dict(build="broad", skin="E2AE8A", hair="short", hair_col="4A3A2A", moustache="huge", moustache_col="5A4030",
                    nose="big", coat="1C2440", hat="custodian", hat_col="1C2440", brass_buttons=True, buttons=5, rosy="E09080",
                    brow=1.4),
    "rosalind": dict(build="slim", skin="F2D0B8", hair="bob", hair_col="B07040", nose="button", coat="8AC8B0", hat="cloche",
                     hat_col="5AA890", skirt="5A7A70", ribbon="C03030", smile=True, eye_size=1.1, buttons=3, scarf="C03030"),
    "cecily": dict(build="slim", skin="F2D0B8", hair="bob", hair_col="B07040", nose="button", coat="8AC8B0", hat="cloche",
                   hat_col="5AA890", skirt="5A7A70", smile=True, eye_size=1.1, buttons=3, scarf="3050B0"),
    "haversham": dict(build="round", skin="F0C0A0", hair="bald", hair_col="6A5A4A", glasses="round", glasses_brass=False,
                      nose="round", coat="3A3A3E", bowtie="8A2A5A", shirt="F4F0E8", rosy="F0A090", brow_angle=-12, mouth_w=0.7),
    "vell": dict(lids=0.5, build="thin", skin="D0CCC8", hair="slick", hair_col="5A5A5E", nose="long", coat="5A5C60", long_coat=True,
                 hat="bowler", hat_col="4A4C50", hat_band="2A2C30", gloves="6A6C70", smile=True, mouth_w=1.6,
                 eye_size=0.75, head=(0.095, 0.135, 0.1), brow_angle=18, shirt="DCDCDC", tie="3A3C40", buttons=2, height=1.1),
    "pip": dict(build="child", skin="7A4A30", hair="braids", hair_col="141010", ribbon="E0C030", nose="button", coat="2A3A6A",
                skirt="3A3A3A", satchel="7A4A28", smile=True, eye_size=1.25, head=(0.1, 0.115, 0.1), height=0.74, tie="C0302A",
                buttons=2),
    "dora": dict(lids=0.12, build="broad", skin="E0A884", hair="curls", hair_col="8A3A22", nose="round", coat="8A6A3A", hat="flatcap",
                 hat_col="5A6A3A", rosy="E09070", smile=True, mouth_w=1.3, scarf="E0C040", buttons=3),
    "harriet": dict(build="slim", skin="F0C8A4", hair="short", hair_col="6A4028", nose="button", coat="A8B8C8", coat_mat="knit",
                    hat="headscarf", hat_col="C86A5A", skirt="4A5A6A", eye_size=1.1, buttons=4),
    "crane": dict(lids=0.42, build="average", skin="ECC0A0", hair="slick", hair_col="2A2420", nose="hook", glasses="monocle",
                  coat="5A2238", coat_mat="velvet", scarf="D8C8A0", brow_angle=16, mouth_w=0.9, buttons=2),
    "thomas": dict(build="slim", skin="E8BC98", hair="short", hair_col="6A4A2A", nose="round", coat="7A6A4A", hat="flatcap",
                   hat_col="5A4A34", flower="E04060", tie="4A3A2A", eye_size=1.05, smile=True, buttons=3),
    "penhallow": dict(lids=0.3, build="average", skin="D8C8BC", hair="short", hair_col="4A3A2A", moustache="pencil", nose="long",
                      coat="6A6A48", hat="peaked", hat_col="5A5A3C", brass_buttons=True, buttons=4, tie="5A5A3C"),
    "sid": dict(build="huge", skin="D89870", hair="short", hair_col="2A1E14", nose="big", coat="2A2A2A", hat="beanie",
                hat_col="3A4A6A", rosy="D08060", smile=True, mouth_w=1.3, head=(0.12, 0.125, 0.115), buttons=4),
    "lou": dict(lids=0.0, build="average", skin="6A3E28", hair="short", hair_col="141010", beard="goatee", nose="round", glasses="shades",
                coat="2A2E4A", hat="porkpie", hat_col="1A1A1A", hat_band="C0A040", tie="C0A040", smile=True, buttons=2),
    "spratt": dict(lids=0.3, build="thin", skin="E4D0BC", hair="wild", hair_col="3A3428", nose="long", coat="1E1E22", bowtie="1E1E22",
                   shirt="E0D8C8", brow_angle=-10, mouth_w=0.8, eye_size=0.85),
    "edie": dict(build="slim", skin="F6D8C4", hair="platinum", hair_col="F0E4B8", nose="button", coat="B83A5A",
                 hat="pillbox", hat_col="1A1A1A", stole="F0E8E0", skirt="7A2A3A", gloves="F0F0F0", smile=True,
                 eye_size=1.1, brow_angle=12, buttons=0),
    "plum": dict(lids=0.55, build="round", skin="D4CCC4", hair="bald", hair_col="D8D4CC", beard="muttonchops", moustache_col="D8D4CC",
                 nose="big", coat="2A2A30", long_coat=True, hat="tophat", hat_col="1E1E22", brass_buttons=False, buttons=4),
    "lark": dict(build="average", skin="ECC4A4", hair="wild", hair_col="C8C4BC", glasses="owl", nose="hook", coat="7A6A4A",
                 hat="flatcap", hat_col="6A5A40", brow=1.6, brow_col="E0DCD4", bowtie="3A6A4A", eye_size=1.2, buttons=3),
    "agnes_old": dict(build="slim", skin="EEC8B0", hair="bun", hair_col="D8D4D0", nose="button", glasses="round", coat="5A6A7A",
                      coat_mat="knit", skirt="3A4A5A", smile=True, rosy="E8A8A0", eye_size=1.0, buttons=5),
    "thomas_old": dict(build="slim", skin="E8C0A0", hair="bald", hair_col="D8D4CC", nose="round", coat="6A5A44", hat="flatcap",
                       hat_col="5A4A34", flower="E04060", moustache="walrus", moustache_col="D8D4CC", smile=True, buttons=3),
    "corporal": dict(build="broad", skin="E2B48E", hair="short", hair_col="3A2A1A", nose="round", coat="6A6A48", hat="peaked",
                     hat_col="5A5A3C", brass_buttons=True, buttons=4, smile=True),
}


def bounds(obj):
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ mathutils.Vector(c) for o in [obj] + list(obj.children_recursive) if o.type == "MESH" for c in o.bound_box]
    mn = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx


def main():
    args = args_after_dashes()
    only = args[args.index("--only") + 1].split(",") if "--only" in args else None
    prev = args[args.index("--preview") + 1] if "--preview" in args else None
    for cid, spec in CAST.items():
        if only and cid not in only:
            continue
        reset_scene()
        root = build_character(cid, spec)
        export_fbx(os.path.join(OUT, cid + ".fbx"), [root])
        if prev:
            # chest-up portrait from the booth's point of view
            setup_preview((0, 1.45, 0), 0.32, yaw=12, pitch=4)
            render(os.path.join(prev, f"person_{cid}.png"))
        print("built", cid)


if __name__ == "__main__":
    main()
