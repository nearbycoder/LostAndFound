"""The lost objects. One builder per object; each exports Assets/Game/Resources/Models/Objects/<id>.fbx.

    blender -b -P ArtSource/build_objects.py [-- --only wallet_brown,suitcase] [--preview /tmp/p]

Conventions (Unity space, metres): origin at the bottom centre of the object in its natural resting
pose. Hotspots are empties named HS_<detailId> whose local +Y points out of the surface (the game
only lets you notice a detail that faces you and isn't hidden). Moving parts are child objects with
their origin on the hinge, named as in objects.json ("Lid", "Flap", "Strap", "Needle"...).
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib"))
import bmesh  # noqa: E402
import bpy  # noqa: E402
import mathutils  # noqa: E402
import numpy as np  # noqa: E402
import sdf  # noqa: E402
from laf import (V, Model, box, cbox, cyl, empty, export_fbx, lathe, mat, rod, rrect_slab, sphere, torus,  # noqa: E402
                 reset_scene, setup_preview, render, text_mesh, rotate_about, transformed, sweep, slab,
                 planar_uv, args_after_dashes, tex, ellipsoid, hull, rrect_outline)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Models", "Objects")
FONT_TITLE = os.path.join(ROOT, "ArtSource", "fonts", "IMFeENit28P.ttf")

BRASS = mat("brass", "C9A15A")
BRASS_D = mat("brass", "9C7A3E")
STEEL = mat("steel", "B0B3B8")
BLACK = mat("paint", "141414")
PAPER = mat("paper", "EFE4CC")


# ----------------------------------------------------------------------------- helpers

def decal(center, normal, up, w, h, offset=0.0006):
    """A textured quad lying on a surface: faces `normal`, with the texture's up along `up`."""
    n = V(normal).normalized()
    u_up = V(up)
    u_up = (u_up - n * u_up.dot(n)).normalized()
    f = -n                          # the viewer looks along -normal
    right = u_up.cross(f)           # Unity: right = up x forward
    c = V(center) + n * offset
    bm = bmesh.new()
    vs = [bm.verts.new(c + right * sx * w / 2 + u_up * sy * h / 2) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    face = bm.faces.new(vs)
    bm.normal_update()
    if face.normal.dot(n) < 0:
        bmesh.ops.reverse_faces(bm, faces=[face])
    uv = bm.loops.layers.uv.verify()
    for loop in face.loops:
        d = loop.vert.co - c
        loop[uv].uv = (d.dot(right) / w + 0.5, d.dot(u_up) / h + 0.5)
    bm.normal_update()
    return bm, False


def band_uv(bm, center, axis, length, start_angle=0.0):
    """Cylindrical UVs around an axis ('x', 'y' or 'z'): u = angle, v = along the axis."""
    uvl = bm.loops.layers.uv.verify()
    c = V(center)
    ai = "xyz".index(axis)
    for f in bm.faces:
        us = []
        for loop in f.loops:
            p = loop.vert.co - c
            a, b = [p[i] for i in range(3) if i != ai]
            ang = (math.atan2(b, a) - start_angle) / (2 * math.pi) % 1.0
            us.append(ang)
        # keep faces from straddling the seam
        if max(us) - min(us) > 0.5:
            us = [u + 1.0 if u < 0.5 else u for u in us]
        for loop, u in zip(f.loops, us):
            p = loop.vert.co - c
            loop[uvl].uv = (u, p[ai] / length + 0.5)


def add_decal(model, center, normal, up, w, h, name, offset=0.0006):
    model.add(decal(center, normal, up, w, h, offset), tex(name), uv=False)


ROOT_OBJ = [None]


def hs(name, pos, up, parent, origin=(0, 0, 0), part=None):
    """Hotspot empty slightly above the surface, +Y outwards. Always parented to the object's root
    (zero location); hotspots that ride on a moving part are named HS_<id>__<Part> and the game
    re-parents them onto that part."""
    p = V(pos) + V(up).normalized() * 0.0015
    root = ROOT_OBJ[0] if ROOT_OBJ[0] is not None else parent
    return empty("HS_" + name + ("__" + part if part else ""), p, parent=root, up=up)


def spindle(x0, x1, rmax, lobes=8, steps=22, seg=48, cx=0.0, cy=0.0, cz=0.0):
    """A furled umbrella canopy along x: radius swells from the tip, with folds."""
    bm = bmesh.new()
    rings = []
    for i in range(steps + 1):
        t = i / steps
        x = x0 + (x1 - x0) * t
        prof = math.sin(min(1.0, t * 1.15) * math.pi * 0.62) ** 0.9 * (1 - max(0, t - 0.85) * 4.5)
        prof = max(prof, 0.12)
        r = rmax * prof
        ring = []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            rr = r * (1 + 0.13 * math.cos(lobes * a) * min(1, t * 3))
            ring.append(bm.verts.new((x, cy + math.cos(a) * rr, cz + math.sin(a) * rr)))
        rings.append(ring)
    for r0, r1 in zip(rings[:-1], rings[1:]):
        for k in range(seg):
            bm.faces.new((r0[k], r0[(k + 1) % seg], r1[(k + 1) % seg], r1[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm, True


def hollow_box(mn, mx, wall, open_top=True, bevel=0.0):
    """Five-sided tray made of slabs, open at +y (or at -y when open_top is False, for lids)."""
    mn, mx = V(mn), V(mx)
    parts = [box(mn, (mx.x, mn.y + wall, mx.z), bevel) if open_top else box((mn.x, mx.y - wall, mn.z), mx, bevel)]
    parts.append(box(mn, (mn.x + wall, mx.y, mx.z), bevel))
    parts.append(box((mx.x - wall, mn.y, mn.z), mx, bevel))
    parts.append(box(mn, (mx.x, mx.y, mn.z + wall), bevel))
    parts.append(box((mn.x, mn.y, mx.z - wall), mx, bevel))
    return parts


# ----------------------------------------------------------------------------- objects

def wallet_brown():
    L1, L2 = mat("leather", "6B3A22"), mat("leather", "8A5232")
    W, D, T = 0.115, 0.088, 0.008
    m = Model("wallet_brown")
    m.add(rrect_slab((0, T / 2, 0), W, D, T, 0.008, plane="xz", bevel=0.002), L1)
    # inside: card slots, the photo window, a ticket stub peeking out
    for k in range(3):
        m.add(rrect_slab((-0.03, T + 0.0006 + k * 0.0003, -0.02 + k * 0.012), 0.05, 0.03, 0.001, 0.003, plane="xz"), L2)
    add_decal(m, (-0.03, T + 0.0022, 0.02), (0, 1, 0), (0, 0, 1), 0.044, 0.024, "ticket_harwick")
    m.add(rrect_slab((0.029, T + 0.0005, 0.0), 0.05, 0.068, 0.001, 0.004, plane="xz"), L2)
    add_decal(m, (0.029, T + 0.0012, 0.004), (0, 1, 0), (0, 0, 1), 0.042, 0.032, "dog_photo")
    m.add(rrect_slab((0.029, T + 0.0016, 0.004), 0.044, 0.034, 0.0003, 0.002, plane="xz"), mat("glass", "DDE8EE"))
    # pressed clover tucked by the photo
    for k in range(4):
        a = k * math.pi / 2 + 0.4
        m.add(sphere((0.046 + math.cos(a) * 0.004, T + 0.0016, -0.026 + math.sin(a) * 0.004), 0.0035, scale=(1, 0.15, 1), segments=10, rings=5), mat("cloth", "4A7A3A"))
    # stitching along the outer edge
    for sx in (-1, 1):
        m.add(box((sx * W / 2 - 0.004 * sx - 0.0006, T - 0.0004, -D / 2 + 0.005), (sx * W / 2 - 0.004 * sx + 0.0006, T + 0.0002, D / 2 - 0.005)), mat("cloth", "C9A877"))
    obj = m.build()
    hs("photo", (0.029, T + 0.0018, 0.004), (0, 1, 0), obj)
    hs("ticket", (-0.03, T + 0.0025, 0.02), (0, 1, 0), obj)
    hs("clover", (0.046, T + 0.0018, -0.026), (0, 1, 0), obj)

    # the cover folds over from the left edge (hinge along z)
    hinge = V(-W / 2, T, 0)
    f = Model("Flap")
    f.add(rrect_slab((0, T + T / 2, 0), W, D, T, 0.008, plane="xz", bevel=0.002), L1)
    for sx in (-1, 1):
        f.add(box((sx * W / 2 - 0.004 * sx - 0.0006, 2 * T - 0.0001, -D / 2 + 0.005), (sx * W / 2 - 0.004 * sx + 0.0006, 2 * T + 0.0005, D / 2 - 0.005)), mat("cloth", "C9A877"))
    f.add(box((-W / 2 + 0.005, 2 * T - 0.0001, D / 2 - 0.0046), (W / 2 - 0.005, 2 * T + 0.0005, D / 2 - 0.0034)), mat("cloth", "C9A877"))
    # brass corner protectors on the outside
    for cz_ in (-1, 1):
        f.add(hull([(W / 2 - 0.016, 2 * T - 0.001, cz_ * (D / 2 - 0.001)), (W / 2 - 0.001, 2 * T - 0.001, cz_ * (D / 2 - 0.001)),
                    (W / 2 - 0.001, 2 * T - 0.001, cz_ * (D / 2 - 0.016)), (W / 2 - 0.016, 2 * T + 0.0012, cz_ * (D / 2 - 0.001)),
                    (W / 2 - 0.001, 2 * T + 0.0012, cz_ * (D / 2 - 0.001)), (W / 2 - 0.001, 2 * T + 0.0012, cz_ * (D / 2 - 0.016))]), BRASS)
    # embossed initials on the inside of the cover (faces down while closed)
    f.add(transformed(text_mesh("W.B.", (0, T - 0.0002, 0.0), 0.016, depth=0.0008, plane="xz", font=FONT_TITLE),
                      rotate_about((0, T, 0), "Z", 180)), mat("leather", "3E2012"))
    fobj = f.build(origin=hinge, parent=obj)
    hs("initials", (0, T - 0.0006, 0), (0, -1, 0), obj, part="Flap")
    return obj


def scarf_red():
    RED, CREAM, DARK = mat("knit", "B8302A"), mat("knit", "E8DCC0"), mat("paint", "2A1010")
    m = Model("scarf_red")
    L, Wd, t = 0.27, 0.11, 0.013
    hole = np.array((-0.085, 2 * t, 0.022))
    # two soft knitted layers, folded over at the right end, with ribs, a gentle sag and a moth hole
    lower = sdf.rbox((0, t / 2, 0), (L / 2, t / 2, Wd / 2), t * 0.48)
    upper = sdf.rbox((0.02, t * 1.5 + 0.0005, 0), ((L - 0.04) / 2, t / 2, Wd / 2), t * 0.48)
    fold = sdf.capsule((L / 2 - t * 0.9, t, -Wd / 2 + t * 0.5), (L / 2 - t * 0.9, t, Wd / 2 - t * 0.5), t * 1.02)
    body = sdf.union(lower, upper, fold, k=0.004)
    body = sdf.warp(body, lambda p: p + np.stack([0.004 * np.sin(p[:, 2] * 30), 0.003 * np.sin(p[:, 0] * 23 + 1) * np.cos(p[:, 2] * 19), 0 * p[:, 0]], 1))
    body = sdf.displace(body, lambda p: -0.0016 * np.sin(p[:, 0] * 31 + 2) * np.cos(p[:, 2] * 27))
    body = sdf.subtract(body, sdf.ellipsoid(hole + np.array((0, 0.002, 0)), (0.0075, t * 0.55, 0.006)), k=0.0015)

    def stripes(p):
        x = p[:, 0] - np.where(p[:, 1] > t + 0.0005, 0.02, 0.0)
        return np.minimum(np.abs(x + 0.1), np.abs(x + 0.075)) - 0.008
    red_part, cream_part = sdf.mesh(body, (-L / 2 - 0.01, -0.004, -Wd / 2 - 0.01), (L / 2 + 0.01, 2 * t + 0.008, Wd / 2 + 0.01),
                                    0.0011, smooth=1, tris=14000, regions=[stripes])
    m.add(red_part, RED)
    m.add(cream_part, CREAM)
    m.add(sphere(hole + V(0, -t * 0.52, 0), 0.0052, scale=(1.3, 0.12, 1), segments=12, rings=6), DARK)
    # fringe: little twisted tassels hanging off both layers
    rng = np.random.default_rng(5)
    tassels = []
    for k in range(9):
        z = -Wd / 2 + 0.01 + k * (Wd - 0.02) / 8
        j = rng.normal(size=3) * 0.002
        tassels.append(sdf.chain([np.array((-L / 2 + 0.006, t * 0.5, z)), np.array((-L / 2 - 0.016, t * 0.3, z + j[0])), np.array((-L / 2 - 0.033, 0.0022, z - 0.001 + j[1]))],
                                 [0.0025, 0.0021, 0.0016]))
        tassels.append(sdf.chain([np.array((-L / 2 + 0.026, t * 1.5, z)), np.array((-L / 2 + 0.006, t * 1.25, z + j[2])), np.array((-L / 2 - 0.012, t * 0.95, z + 0.002))],
                                 [0.0023, 0.002, 0.0015]))
    fr = sdf.displace(sdf.union(*tassels), lambda p: 0.0004 * np.sin(p[:, 0] * 1800))
    m.add(sdf.mesh(fr, (-L / 2 - 0.045, -0.002, -Wd / 2), (-L / 2 + 0.035, 2 * t, Wd / 2), 0.0007, smooth=1, tris=3000), RED)
    # Nan's label sewn on the underside, and a little knitted C in the corner
    add_decal(m, (0.07, 0.0, 0.0), (0, -1, 0), (0, 0, 1), 0.06, 0.024, "label_nan")
    m.add(text_mesh("C", (0.105, 2 * t + 0.0022, -0.04), 0.014, depth=0.0016, plane="xz"), CREAM)
    obj = m.build()
    hs("hole", (-0.085, 2 * t + 0.001, 0.022), (0, 1, 0), obj)
    hs("label", (0.07, -0.0008, 0.0), (0, -1, 0), obj)
    hs("initial", (0.105, 2 * t + 0.0015, -0.04), (0, 1, 0), obj)
    return obj


def umbrella(name, canopy_hex, handle):
    CAN = mat("cloth", canopy_hex)
    m = Model(name)
    r = 0.034
    cy = r + 0.004
    m.add(rod((-0.44, cy, 0), (0.30, cy, 0), 0.0045, segments=10), STEEL)
    m.add(spindle(-0.38, 0.22, r, cy=cy), CAN)
    m.add(lathe([(0.0001, 0.0), (0.006, 0.004), (0.006, 0.03)], (-0.44, cy, 0), axis="x", segments=16), BRASS)
    m.add(torus((0.22, cy, 0), 0.007, 0.003, axis="x", segments=20), STEEL)
    obj = m.build()
    return obj, cy


def umbrella_duck():
    obj, cy = umbrella("umbrella_duck", "2F5A3A", "duck")
    WOOD, YELLOW, EYE = mat("wood", "C08A4A"), mat("paint", "E0A030"), mat("eye", "101010")
    h = Model("DuckHandle")
    # the neck curls up from the shaft into a carved duck's head
    h.add(sweep([(0.29, cy, 0), (0.33, cy + 0.004, 0), (0.36, cy + 0.02, 0), (0.375, cy + 0.045, 0)], 0.013, radius_end=0.016, segments=16), WOOD)
    h.add(ellipsoid((0.383, cy + 0.065, 0), (0.026, 0.024, 0.021)), WOOD)
    h.add(ellipsoid((0.41, cy + 0.062, 0), (0.02, 0.006, 0.011)), YELLOW)
    # the chip in the beak
    h.add(sphere((0.428, cy + 0.0655, 0.006), 0.004, scale=(1, 0.6, 1), segments=10, rings=6), mat("wood", "6A4A2A"))
    for sz in (-1, 1):
        h.add(sphere((0.393, cy + 0.072, sz * 0.017), 0.0035), EYE)
    hobj = h.build(parent=obj)
    hs("beak", (0.428, cy + 0.067, 0.008), (0.3, 0.5, 0.8), obj)
    # name strip under the strap; the strap flips off
    m2 = Model("Canopy_Label")
    add_decal(m2, (0.02, cy + 0.034, 0.0), (0, 1, 0), (-1, 0, 0), 0.07, 0.019, "namestrip_bramble", offset=0.0012)
    add_decal(m2, (-0.16, cy + 0.0235, 0.012), (0, 0.8, 0.6), (1, 0, 0), 0.03, 0.016, "sweet_wrapper", offset=0.0008)
    m2.build(parent=obj)
    hs("namestrip", (0.02, cy + 0.036, 0.0), (0, 1, 0), obj)
    hs("wrapper", (-0.16, cy + 0.025, 0.013), (0, 0.8, 0.6), obj)
    st = Model("Strap")
    hinge = V(0.02, cy + 0.033, -0.022)
    st.add(box((-0.03, cy + 0.0345, -0.024), (0.07, cy + 0.0385, 0.024), bevel=0.0015), mat("cloth", "24452D"))
    st.add(cyl((0.06, cy + 0.039, 0.0), 0.006, 0.002, segments=16), BRASS)
    st.build(origin=hinge, parent=obj)
    return obj


def umbrella_black():
    obj, cy = umbrella("umbrella_black", "1A1A1E", "crook")
    WOOD = mat("wood", "3A2214")
    h = Model("CrookHandle")
    pts = [(0.29, cy, 0), (0.36, cy, 0)] + [(0.36 + math.sin(a) * 0.04, cy + 0.04 - math.cos(a) * 0.04, 0) for a in [math.pi * i / 10 for i in range(1, 11)]]
    pts.append((0.35, cy + 0.08, 0))
    h.add(sweep(pts, 0.011, segments=14), WOOD)
    h.build(parent=obj)
    # the brass collar carries the railway stamp (texture swaps after the ring is returned)
    c = Model("Collar")
    bm, _ = cyl((0.30, cy, 0), 0.0125, 0.018, axis="x", segments=40)
    band_uv(bm, (0.30, cy, 0), "x", 0.018, start_angle=math.pi / 2)
    c.add((bm, None), tex("collar_ninefold"), uv=False)
    c.build(parent=obj)
    hs("stamp", (0.30, cy + 0.0125, 0), (0, 1, 0), obj)
    # A+T scratched into the ferrule
    f = Model("Ferrule")
    f.add(text_mesh("A+T", (-0.425, cy + 0.0058, 0.0), 0.004, depth=0.0003, plane="xz"), mat("brass", "6E5228"))
    f.build(parent=obj)
    hs("ferrule", (-0.425, cy + 0.006, 0.0), (0, 1, 0), obj)
    return obj


def suitcase():
    HIDE, BAND, LIN = mat("leather", "7A5232"), mat("leather", "4E3020"), mat("cloth", "6E5A7E")
    W, D, H1, H2, wall = 0.56, 0.40, 0.11, 0.06, 0.012
    m = Model("suitcase")
    for part in hollow_box((-W / 2, 0, -D / 2), (W / 2, H1, D / 2), wall, bevel=0.006):
        m.add(part, HIDE)
    m.add(box((-W / 2 + wall, wall, -D / 2 + wall), (W / 2 - wall, wall + 0.002, D / 2 - wall)), LIN)
    # leather bands and brass corners
    for x in (-0.17, 0.17):
        m.add(box((x - 0.018, -0.001, -D / 2 - 0.002), (x + 0.018, H1, D / 2 + 0.002)), BAND)
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.add(sphere((sx * (W / 2 - 0.01), 0.01, sz * (D / 2 - 0.01)), 0.02, scale=(1, 0.8, 1)), BRASS)
    # front: latch plates with O.M. engraved between them, and the handle
    for x in (-0.15, 0.15):
        m.add(cbox((x, H1 - 0.012, -D / 2 - 0.003), (0.03, 0.024, 0.006), bevel=0.002), BRASS)
    m.add(cbox((0.0, H1 - 0.02, -D / 2 - 0.002), (0.07, 0.03, 0.004), bevel=0.002), BRASS)
    add_decal(m, (0.0, H1 - 0.02, -D / 2 - 0.0042), (0, 0, -1), (0, 1, 0), 0.062, 0.026, "latch_om")
    m.add(sweep([(-0.06, H1 * 0.55, -D / 2 - 0.004), (-0.05, H1 * 0.55, -D / 2 - 0.03), (0.05, H1 * 0.55, -D / 2 - 0.03), (0.06, H1 * 0.55, -D / 2 - 0.004)], 0.008, segments=10, flat=0.6), BAND)
    add_decal(m, (0.17 - 0.09, H1 * 0.5, -D / 2 - 0.0005), (0, 0, -1), (0.1, 1, 0), 0.11, 0.075, "sticker_vienna")
    # inside: the lavender sachet
    m.add(ellipsoid((0.12, wall + 0.018, 0.05), (0.05, 0.016, 0.035)), mat("cloth", "8A70B0"))
    m.add(torus((0.12, wall + 0.032, 0.05), 0.012, 0.003, axis="y", segments=16), mat("cloth", "E8DCC0"))
    obj = m.build()
    hs("initials", (0.0, H1 - 0.02, -D / 2 - 0.005), (0, 0, -1), obj)
    hs("sachet", (0.12, wall + 0.035, 0.05), (0, 1, 0), obj)

    hinge = V(0, H1, D / 2)
    lid = Model("Lid")
    for part in hollow_box((-W / 2, H1, -D / 2), (W / 2, H1 + H2, D / 2), wall, open_top=False, bevel=0.006):
        lid.add(part, HIDE)
    for x in (-0.17, 0.17):
        lid.add(box((x - 0.018, H1, -D / 2 - 0.002), (x + 0.018, H1 + H2 + 0.001, D / 2 + 0.002)), BAND)
    lid.add(box((-W / 2 + wall, H1 + H2 - wall - 0.002, -D / 2 + wall), (W / 2 - wall, H1 + H2 - wall, D / 2 - wall)), LIN)
    add_decal(lid, (-0.12, H1 + H2, 0.04), (0, 1, 0), (0.25, 0, 1), 0.14, 0.097, "sticker_lisbon")
    # dance card tucked into the lid lining
    add_decal(lid, (0.15, H1 + H2 - wall - 0.0025, 0.05), (0, -1, 0), (0, 0, 1), 0.06, 0.045, "dance_card")
    lobj = lid.build(origin=hinge, parent=obj)
    hs("stickers", (-0.12, H1 + H2 + 0.0006, 0.04), (0, 1, 0), obj, part="Lid")
    hs("dancecard", (0.15, H1 + H2 - wall - 0.003, 0.05), (0, -1, 0), obj, part="Lid")
    return obj


def compass():
    m = Model("compass")
    R, H = 0.03, 0.012
    m.add(lathe([(0.0, 0.0), (R - 0.002, 0.0), (R, 0.002), (R, H - 0.001), (R - 0.003, H), (R - 0.004, H - 0.002), (0.0, H - 0.002)], (0, 0, 0), segments=48), BRASS)
    bm, _ = cyl((0, H - 0.0018, 0), R - 0.0045, 0.0004, segments=48)
    planar_uv(bm, (0, H, 0), (1, 0, 0), (0, 0, 1), 2 * (R - 0.0045), 2 * (R - 0.0045))
    m.add((bm, None), tex("compass_dial"), uv=False)
    m.add(cyl((0, H - 0.0006, 0), R - 0.004, 0.0008, segments=48), mat("glass", "DDEEF2"))
    # bow at the back
    m.add(torus((0, H / 2, R + 0.006), 0.007, 0.0018, axis="x", segments=20), BRASS)
    # a tiny map engraved on the underside
    add_decal(m, (0, 0.0, 0), (0, -1, 0), (0, 0, 1), 0.04, 0.04, "compass_map", offset=0.0004)
    obj = m.build()
    n = Model("Needle")
    n.add(hull([(0, H - 0.0013, -0.02), (0.0025, H - 0.0013, 0), (-0.0025, H - 0.0013, 0), (0, H - 0.0011, -0.02), (0, H - 0.0011, 0.0)]), mat("paint", "9A2420"))
    n.add(hull([(0, H - 0.0013, 0.02), (0.0025, H - 0.0013, 0), (-0.0025, H - 0.0013, 0), (0, H - 0.0011, 0.02)]), mat("steel", "C8CCD2"))
    n.add(cyl((0, H - 0.0012, 0), 0.0018, 0.0008, segments=12), BRASS)
    nobj = n.build(origin=(0, H, 0), parent=obj)
    nobj.rotation_euler = (0, 0, math.radians(-128))  # it never points north
    hs("needle", (0, H - 0.0005, 0), (0, 1, 0), obj)
    hs("map", (0, -0.0006, 0), (0, -1, 0), obj)
    hinge = V(0, H, R)
    lid = Model("Lid")
    lid.add(lathe([(0.0, H), (R - 0.004, H), (R, H + 0.0015), (R - 0.001, H + 0.004), (R - 0.006, H + 0.006), (0.0, H + 0.0065)], (0, 0, 0), segments=48), BRASS)
    lid.add(torus((0, H + 0.0035, 0), R - 0.008, 0.0008, axis="y", segments=40), BRASS_D)
    add_decal(lid, (0, H - 0.0001, 0), (0, -1, 0), (0, 0, -1), 0.046, 0.0105, "compass_lid", offset=0.0003)
    lobj = lid.build(origin=hinge, parent=obj)
    hs("engraving", (0, H - 0.0004, 0), (0, -1, 0), obj, part="Lid")
    return obj


def hatbox():
    LIDC, RIB = mat("card", "E6D7B8"), mat("velvet", "6A2232")
    R, Hb = 0.17, 0.17
    m = Model("hatbox")
    # open tube: striped outside, plain card inside, with a floor
    bm, _ = lathe([(R, 0.0), (R, Hb)], (0, 0, 0), segments=48, cap_bottom=False, cap_top=False)
    band_uv(bm, (0, Hb / 2, 0), "y", Hb)
    m.add((bm, None), tex("hatbox_stripes"), uv=False)
    m.add(lathe([(R - 0.004, Hb), (R - 0.004, 0.004)], (0, 0, 0), segments=48, cap_bottom=False, cap_top=False), mat("card", "D8C8A8"))
    m.add(torus((0, Hb, 0), R - 0.002, 0.0022, axis="y", segments=48), mat("card", "D8C8A8"))
    m.add(cyl((0, 0.002, 0), R, 0.004, segments=48), mat("card", "C8B898"))
    # the hat inside: plum velvet cloche with an ostrich feather
    m.add(lathe([(0.001, Hb - 0.06), (0.07, Hb - 0.06), (0.09, Hb - 0.045), (0.085, Hb - 0.04), (0.068, Hb - 0.03), (0.06, Hb + 0.01), (0.04, Hb + 0.035), (0.001, Hb + 0.042)], (0, 0, 0), segments=40), mat("velvet", "5A2A4A"))
    m.add(torus((0, Hb - 0.012, 0), 0.064, 0.008, axis="y", segments=40), mat("velvet", "2A1A2A"))
    m.add(sweep([(0.05, Hb + 0.0, 0.03), (0.09, Hb + 0.04, 0.05), (0.12, Hb + 0.07, 0.03), (0.14, Hb + 0.08, -0.01)], 0.01, radius_end=0.003, segments=10, flat=0.35), mat("fur", "F0E8D8"))
    add_decal(m, (-0.07, Hb - 0.044, -0.08), (0, 1, 0), (0.2, 0, 1), 0.07, 0.035, "theatre_stub", offset=0.0)
    m.add(box((-0.11, Hb - 0.05, 0.02), (-0.06, Hb - 0.0475, 0.09)), mat("paper", "E8E0D0"))
    add_decal(m, (-0.085, Hb - 0.0472, 0.055), (0, 1, 0), (0, 0, 1), 0.045, 0.06, "love_letter", offset=0.0)
    m.add(cyl((0, Hb - 0.055, 0), R - 0.006, 0.004, segments=48), mat("cloth", "C8B898"))
    obj = m.build()
    hs("hat", (0.0, Hb + 0.043, 0.0), (0, 1, 0), obj)
    hs("stub", (-0.07, Hb - 0.043, -0.08), (0, 1, 0), obj)
    hs("letter", (-0.085, Hb - 0.0465, 0.055), (0, 1, 0), obj)
    lid = Model("Lid")
    lid.add(lathe([(0.0, Hb - 0.005), (R + 0.006, Hb - 0.005), (R + 0.006, Hb + 0.04), (R + 0.002, Hb + 0.044), (0.0, Hb + 0.044)], (0, 0, 0), segments=48), LIDC)
    lid.add(box((-0.012, Hb + 0.044, -R - 0.007), (0.012, Hb + 0.047, R + 0.007)), RIB)
    lid.add(box((-0.012, Hb - 0.02, -R - 0.009), (0.012, Hb + 0.047, -R - 0.006)), RIB)
    lid.add(box((-0.012, Hb - 0.02, R + 0.006), (0.012, Hb + 0.047, R + 0.009)), RIB)
    lid.add(sweep([(0, Hb + 0.047, -0.03), (0, Hb + 0.075, -0.015), (0, Hb + 0.08, 0.0), (0, Hb + 0.075, 0.015), (0, Hb + 0.047, 0.03)], 0.005, segments=8, flat=0.5), RIB)
    lid.build(origin=(0, Hb, 0), parent=obj)
    return obj



# ----------------------------------------------------------------------------- Tuesday onwards

SILVER = mat("silver", "C8CACC")
GOLD = mat("gold", "D9B060")


def oval(prim, sz):
    """Stretch a primitive along z (lathed circles into ovals)."""
    return transformed(prim, mathutils.Matrix.Diagonal((1.0, 1.0, sz, 1.0)))


def locket():
    R, H = 0.016, 0.0042
    m = Model("locket")
    m.add(oval(lathe([(0.0, 0.0), (R * 0.8, 0.0003), (R, 0.0022), (R, H), (R - 0.0012, H), (R - 0.0012, 0.0012), (0.0, 0.0012)], (0, 0, 0), segments=40), 1.25), SILVER)
    # the folded note and a lock of hair tied with blue thread, in the back half
    m.add(box((-0.009, 0.0012, -0.012), (0.009, 0.0019, 0.008)), PAPER)
    add_decal(m, (0.0, 0.0019, -0.002), (0, 1, 0), (0, 0, 1), 0.017, 0.016, "locket_note", offset=0.0002)
    m.add(sweep([(-0.004, 0.0022, 0.011), (0.0, 0.0026, 0.0125), (0.006, 0.0022, 0.011), (0.009, 0.0021, 0.0085)], 0.0012, radius_end=0.0004, segments=8), mat("hair", "8A6A48"))
    m.add(torus((0.0, 0.0024, 0.0125), 0.0016, 0.0004, axis="x", segments=12), mat("cloth", "3050A0"))
    # bail and a coil of fine chain
    m.add(torus((0, H * 0.5, -R * 1.25 - 0.0025), 0.0026, 0.0008, axis="x", segments=16), SILVER)
    for k in range(9):
        a = k * 0.9
        m.add(torus((math.cos(a) * 0.006 - 0.004, 0.0006, -R * 1.25 - 0.009 - math.sin(a) * 0.004), 0.0016, 0.0004, axis="x" if k % 2 else "z", segments=10), SILVER)
    obj = m.build()
    hs("note", (0.0, 0.002, -0.002), (0, 1, 0), obj)
    hs("hair", (0.002, 0.0027, 0.0115), (0, 1, 0), obj)
    hinge = V(-R, H, 0)
    lid = Model("Lid")
    lid.add(oval(lathe([(0.0, H + 0.0035), (R * 0.8, H + 0.0032), (R, H + 0.0015), (R, H), (0.0, H)], (0, 0, 0), segments=40), 1.25), SILVER)
    # engraved ivy trailing across the front
    def dome(x, z):   # height of the lid's surface (matches the lathe profile)
        r = math.hypot(x, z / 1.25) / R
        return H + (0.0035 - 0.0003 * (r / 0.8) ** 2 if r < 0.8 else 0.0032 - (r - 0.8) / 0.2 * 0.0017)
    pts = [(-0.011 + i * 0.0022, 0.0, 0.007 * math.sin(i * 0.7)) for i in range(11)]
    pts = [(x, dome(x, z) - 0.0001, z) for x, _, z in pts]
    lid.add(sweep(pts, 0.00035, segments=6, flat=0.5), mat("silver", "7C7E80"))
    for i in range(1, 10, 2):
        x, _, z = pts[i]
        lz = z + 0.0018 * (1 if i % 4 == 1 else -1)
        lid.add(sphere((x + 0.001, dome(x + 0.001, lz) - 0.00005, lz), 0.0013, scale=(1, 0.12, 0.6), segments=8, rings=4), mat("silver", "7C7E80"))
    # Gran's portrait inside the lid
    bm, _ = oval(cyl((0, H - 0.0002, 0), R - 0.002, 0.0003, segments=40), 1.25)
    planar_uv(bm, (0, H, 0), (-1, 0, 0), (0, 0, 1), 2 * (R - 0.002), 2.5 * (R - 0.002))
    lid.add((bm, None), tex("locket_gran"), uv=False)
    lid.build(origin=hinge, parent=obj)
    hs("ivy", (0.0, H + 0.0036, 0.0), (0, 1, 0), obj, part="Lid")
    hs("photo", (0.0, H - 0.0006, 0.0), (0, -1, 0), obj, part="Lid")
    return obj


def pocket_watch():
    R, H = 0.024, 0.011
    m = Model("pocket_watch")
    # open-topped case: an inner lip steps down to the dial
    m.add(lathe([(0.0, 0.0), (R - 0.004, 0.0), (R, 0.003), (R, H - 0.002), (R - 0.001, H), (R - 0.0022, H), (R - 0.0022, H - 0.0024), (0.0, H - 0.0024)], (0, 0, 0), segments=48, cap_top=False), SILVER)
    bm, _ = cyl((0, H - 0.0016, 0), R - 0.0022, 0.0004, segments=48)
    planar_uv(bm, (0, H, 0), (1, 0, 0), (0, 0, 1), 2 * (R - 0.0022), 2 * (R - 0.0022))
    m.add((bm, None), tex("watch_face"), uv=False)
    m.add(cyl((0, H - 0.0005, 0), R - 0.0018, 0.0006, segments=48), mat("glass", "DDEEF2"))
    # crown and bow at twelve o'clock (towards the viewer)
    m.add(cyl((0, H / 2, -R - 0.003), 0.0028, 0.006, axis="z", segments=16), SILVER)
    m.add(torus((0, H / 2, -R - 0.01), 0.0062, 0.0013, axis="y", segments=24), SILVER)
    # a glass window in the back shows the movement: one gear is engraved IX
    m.add(cyl((0, -0.0001, 0.006), 0.009, 0.0004, segments=32), mat("glass", "DDEEF2"))
    m.add(cyl((0, 0.0012, 0.006), 0.0085, 0.0008, segments=24), BRASS_D)
    for k in range(12):
        a = k * math.pi / 6
        m.add(cbox((math.cos(a) * 0.0088, 0.0012, 0.006 + math.sin(a) * 0.0088), (0.0016, 0.0008, 0.0012)), BRASS_D)
    m.add(transformed(text_mesh("IX", (0, 0.0006, 0.006), 0.006, depth=0.0003, plane="xz", font=FONT_TITLE), rotate_about((0, 0.0006, 0.006), "Z", 180)), mat("brass", "5A4020"))
    obj = m.build()
    hs("gear", (0, -0.0004, 0.006), (0, -1, 0), obj)
    # the hands (the game turns them backwards, all the time)
    hn = Model("Spin_Hands")
    hn.add(hull([(-0.0008, H - 0.0011, 0.0), (0.0008, H - 0.0011, 0.0), (0, H - 0.0011, -0.016), (0, H - 0.0009, -0.008)]), BLACK)
    hn.add(hull([(-0.001, H - 0.0013, 0.0), (0.001, H - 0.0013, 0.0), (0.0, H - 0.0013, 0.011), (0, H - 0.0011, 0.006)]), BLACK)
    hn.add(cyl((0, H - 0.0011, 0), 0.0012, 0.0006, segments=12), BLACK)
    hn.build(origin=(0, H, 0), parent=obj)
    hs("hands", (0.004, H - 0.0002, -0.004), (0, 1, 0), obj)
    hinge = V(0, H, R)
    lid = Model("Lid")
    lid.add(lathe([(0.0, H), (R - 0.001, H), (R, H + 0.0015), (R - 0.002, H + 0.004), (0.0, H + 0.005)], (0, 0, 0), segments=48), SILVER)
    lid.add(torus((0, H + 0.0035, 0), R - 0.007, 0.0008, axis="y", segments=40), mat("silver", "8C8E90"))
    add_decal(lid, (0, H - 0.0001, 0), (0, -1, 0), (0, 0, -1), 0.04, 0.008, "watch_engraving", offset=0.0003)
    lid.build(origin=hinge, parent=obj)
    hs("engraving", (0, H - 0.0004, 0), (0, -1, 0), obj, part="Lid")
    return obj


def toy_rabbit():
    FUR, PINK = mat("fur", "A8998A"), mat("felt", "D8A0A0")
    m = Model("toy_rabbit")
    P = lambda *a: np.array(a, dtype=float)  # noqa: E731
    body = sdf.ellipsoid(P(0, 0.058, 0.004), (0.05, 0.058, 0.044))
    head = sdf.ellipsoid(P(0, 0.135, -0.012), (0.043, 0.04, 0.04))
    cheeks = sdf.ellipsoid(P(0, 0.122, -0.04), (0.03, 0.02, 0.016))
    ear_r = sdf.capsule(P(0.016, 0.165, -0.006), P(0.03, 0.235, 0.004), 0.013, 0.01)
    ear_l = sdf.capsule(P(-0.016, 0.165, -0.006), P(-0.024, 0.2, 0.0), 0.013, 0.011)  # chewed short
    ear_l = sdf.subtract(ear_l, sdf.sphere(P(-0.03, 0.208, -0.004), 0.011), sdf.sphere(P(-0.018, 0.212, 0.006), 0.008), k=0.002)
    arms = [sdf.capsule(P(sx * 0.04, 0.085, -0.02), P(sx * 0.03, 0.04, -0.042), 0.014) for sx in (-1, 1)]
    feet = [sdf.ellipsoid(P(sx * 0.03, 0.012, -0.03), (0.02, 0.013, 0.034)) for sx in (-1, 1)]
    tail = sdf.sphere(P(0, 0.035, 0.05), 0.016)
    f = sdf.union(body, head, cheeks, ear_r, ear_l, *arms, *feet, tail, k=0.008)
    f = sdf.displace(f, lambda p: 0.0007 * np.sin(p[:, 0] * 900) * np.sin(p[:, 1] * 800 + p[:, 2] * 700))
    inner_ear = sdf.union(sdf.capsule(P(0.03, 0.17, -0.015), P(0.04, 0.235, -0.006), 0.009),
                          sdf.capsule(P(-0.03, 0.17, -0.015), P(-0.036, 0.2, -0.01), 0.009))
    fur, pink = sdf.mesh(f, (-0.075, -0.002, -0.07), (0.075, 0.25, 0.07), 0.0022, smooth=1, tris=16000, regions=[inner_ear])
    m.add(fur, FUR)
    m.add(pink, PINK)
    # one black bead eye; the other is a brown coat button sewn on in a hurry
    m.add(sphere((0.017, 0.143, -0.047), 0.006), mat("eye", "0A0A0A"))
    m.add(cyl((-0.017, 0.143, -0.049), 0.0075, 0.003, axis="z", segments=20), mat("plastic", "5A3A22"))
    for dx in (-0.002, 0.002):
        m.add(cyl((-0.017 + dx, 0.143, -0.051), 0.0011, 0.002, axis="z", segments=8), mat("paint", "1A1A1A"))
    m.add(sweep([(-0.019, 0.143, -0.0515), (-0.015, 0.143, -0.0515)], 0.0005, segments=6), mat("cloth", "E0D8C0"))
    m.add(ellipsoid((0, 0.127, -0.054), (0.006, 0.0045, 0.004)), PINK)
    m.add(sweep([(-0.006, 0.117, -0.052), (0.0, 0.114, -0.054), (0.006, 0.117, -0.052)], 0.0008, segments=6), mat("cloth", "4A2A2A"))
    # a little ribbon bow and the name tag sewn under the right foot
    m.add(torus((0, 0.098, -0.014), 0.034, 0.004, axis="y", segments=32, scale=(1, 1, 1.05)), mat("cloth", "3A60A0"))
    add_decal(m, (0.03, -0.0004, -0.03), (0, -1, 0), (0, 0, -1), 0.026, 0.016, "rabbit_tag", offset=0.0)
    obj = m.build()
    hs("ear", (-0.026, 0.212, -0.004), (-0.6, 0.6, -0.4), obj)
    hs("tag", (0.03, -0.0012, -0.03), (0, -1, 0), obj)
    hs("button", (-0.017, 0.143, -0.052), (0, 0, -1), obj)
    return obj


def briefcase():
    HIDE, LIN = mat("leather", "4A2A1A"), mat("cloth", "7A2A30")
    W, D, H1, H2, wall = 0.42, 0.30, 0.06, 0.04, 0.009
    m = Model("briefcase")
    for part in hollow_box((-W / 2, 0, -D / 2), (W / 2, H1, D / 2), wall, bevel=0.005):
        m.add(part, HIDE)
    m.add(box((-W / 2 + wall, wall, -D / 2 + wall), (W / 2 - wall, wall + 0.002, D / 2 - wall)), LIN)
    # the racing form, folded, and an egg-and-cress sandwich in greaseproof paper
    m.add(box((-0.18, wall + 0.002, -0.12), (0.0, wall + 0.006, 0.02)), mat("paper", "E8E2D2"))
    add_decal(m, (-0.09, wall + 0.0062, -0.05), (0, 1, 0), (0, 0, 1), 0.17, 0.12, "racing_form", offset=0.0002)
    for k, (yb, col) in enumerate(((wall + 0.004, "E8D2A0"), (wall + 0.014, "E8D2A0"))):
        m.add(hull([(0.04, yb, 0.03), (0.15, yb, 0.03), (0.04, yb, 0.12), (0.04, yb + 0.009, 0.03), (0.15, yb + 0.009, 0.03), (0.04, yb + 0.009, 0.12)], bevel=0.003), mat("cloth", col))
    m.add(hull([(0.045, wall + 0.0125, 0.035), (0.142, wall + 0.0125, 0.035), (0.045, wall + 0.0125, 0.113), (0.045, wall + 0.0145, 0.035), (0.142, wall + 0.0145, 0.035), (0.045, wall + 0.0145, 0.113)]), mat("cloth", "E8D070"))
    m.add(hull([(0.05, wall + 0.0145, 0.04), (0.13, wall + 0.0145, 0.04), (0.05, wall + 0.0145, 0.1), (0.05, wall + 0.0158, 0.04), (0.13, wall + 0.0158, 0.04), (0.05, wall + 0.0158, 0.1)]), mat("cloth", "5A8A3A"))
    m.add(box((0.03, wall + 0.002, 0.02), (0.17, wall + 0.003, 0.135)), mat("paper", "F2EEE0"))
    # brass clasp and handle on the front
    m.add(cbox((0.0, H1 - 0.008, -D / 2 - 0.003), (0.05, 0.02, 0.006), bevel=0.002), BRASS)
    m.add(sweep([(-0.05, H1 * 0.6, -D / 2 - 0.003), (-0.04, H1 * 0.6, -D / 2 - 0.028), (0.04, H1 * 0.6, -D / 2 - 0.028), (0.05, H1 * 0.6, -D / 2 - 0.003)], 0.007, segments=10, flat=0.6), HIDE)
    obj = m.build()
    hs("form", (-0.09, wall + 0.0068, -0.05), (0, 1, 0), obj)
    hs("sandwich", (0.08, wall + 0.017, 0.07), (0, 1, 0), obj)
    hinge = V(0, H1, D / 2)
    lid = Model("Lid")
    for part in hollow_box((-W / 2, H1, -D / 2), (W / 2, H1 + H2, D / 2), wall, open_top=False, bevel=0.005):
        lid.add(part, HIDE)
    lid.add(box((-W / 2 + wall, H1 + H2 - wall - 0.002, -D / 2 + wall), (W / 2 - wall, H1 + H2 - wall, D / 2 - wall)), LIN)
    add_decal(lid, (0.1, H1 + H2 - wall - 0.0025, 0.06), (0, -1, 0), (0, 0, 1), 0.06, 0.03, "lining_df")
    # a pocket in the lid with a slip of paper poking out
    lid.add(box((-0.19, H1 + H2 - wall - 0.006, -0.02), (-0.04, H1 + H2 - wall - 0.002, 0.12)), mat("leather", "3A2014"))
    add_decal(lid, (-0.115, H1 + H2 - wall - 0.0064, -0.035), (0, -1, 0), (0, 0, 1), 0.07, 0.038, "betting_slip")
    lid.add(cbox((0.0, H1 + 0.006, -D / 2 - 0.003), (0.03, 0.012, 0.005), bevel=0.002), BRASS)
    lid.build(origin=hinge, parent=obj)
    hs("initials", (0.1, H1 + H2 - wall - 0.003, 0.06), (0, -1, 0), obj, part="Lid")
    hs("slip", (-0.115, H1 + H2 - wall - 0.007, -0.035), (0, -1, 0), obj, part="Lid")
    return obj


def wallet_black():
    L1, L2 = mat("leather", "1C1A1A"), mat("leather", "2E2A28")
    W, D, T = 0.11, 0.085, 0.007
    m = Model("wallet_black")
    m.add(rrect_slab((0, T / 2, 0), W, D, T, 0.006, plane="xz", bevel=0.0015), L1)
    m.add(rrect_slab((0.028, T + 0.0005, 0.0), 0.048, 0.066, 0.001, 0.004, plane="xz"), L2)
    add_decal(m, (0.028, T + 0.0012, 0.008), (0, 1, 0), (0, 0, 1), 0.044, 0.028, "library_card")
    m.add(rrect_slab((-0.028, T + 0.0005, 0.0), 0.046, 0.066, 0.001, 0.004, plane="xz"), L2)
    # the folded IOU tucked deep in the empty note pocket
    m.add(box((-0.04, T + 0.0011, 0.02), (-0.016, T + 0.0015, 0.034)), PAPER)
    add_decal(m, (-0.028, T + 0.0016, 0.027), (0, 1, 0), (0, 0, 1), 0.022, 0.013, "iou_note", offset=0.0002)
    obj = m.build()
    hs("card", (0.028, T + 0.0016, 0.008), (0, 1, 0), obj)
    hs("empty", (-0.028, T + 0.0012, -0.012), (0, 1, 0), obj)
    hs("iou", (-0.028, T + 0.0018, 0.027), (0, 1, 0), obj)
    hinge = V(-W / 2, T, 0)
    f = Model("Flap")
    f.add(rrect_slab((0, T + T / 2, 0), W, D, T, 0.006, plane="xz", bevel=0.0015), L1)
    f.add(box((-W / 2 + 0.004, 2 * T - 0.0001, -D / 2 + 0.004), (W / 2 - 0.004, 2 * T + 0.0004, -D / 2 + 0.005)), mat("cloth", "4A4A4A"))
    f.build(origin=hinge, parent=obj)
    return obj


def chess_set():
    WOOD, DARK = mat("wood", "9A6A3A"), mat("darkwood", "4A2A16")
    S, H1, H2, wall = 0.17, 0.025, 0.012, 0.006
    m = Model("chess_set")
    for part in hollow_box((-S / 2, 0, -S / 2), (S / 2, H1, S / 2), wall, bevel=0.002):
        m.add(part, WOOD)
    sq = (S - 2 * wall) / 8
    y0 = H1 - 0.004
    for i in range(8):
        for j in range(8):
            x0 = -S / 2 + wall + i * sq
            z0 = -S / 2 + wall + j * sq
            m.add(box((x0, y0 - 0.002, z0), (x0 + sq, y0, z0 + sq)), mat("wood", "E2C898") if (i + j) % 2 else DARK)
    IV, EB, CORK = mat("plastic", "EDE4D0"), mat("plastic", "1E1A18"), mat("cork", "B08858")
    def piece(i, j, kind, col):
        x = -S / 2 + wall + (i + 0.5) * sq
        z = -S / 2 + wall + (j + 0.5) * sq
        h = {"p": 0.012, "r": 0.015, "n": 0.016, "b": 0.018, "q": 0.021, "k": 0.023}[kind]
        m.add(lathe([(0.0, y0), (0.0068, y0), (0.0068, y0 + 0.002), (0.0045, y0 + 0.004), (0.0032, y0 + h * 0.65), (0.0045, y0 + h * 0.72), (0.002, y0 + h * 0.8), (0.0001, y0 + h * 0.82)], (x, 0, z), segments=16), col)
        if kind in "pqkb":
            m.add(sphere((x, y0 + h * 0.86, z), 0.0034 if kind != "p" else 0.0038, segments=12, rings=8), col)
        if kind == "n":
            m.add(hull([(x - 0.002, y0 + h * 0.6, z + 0.003), (x + 0.002, y0 + h * 0.6, z + 0.003), (x, y0 + h * 1.05, z + 0.002), (x, y0 + h * 0.85, z - 0.006)]), col)
        if kind == "r":
            m.add(cyl((x, y0 + h * 0.86, z), 0.0045, 0.004, segments=12), col)
        return (x, y0 + h, z)
    for i, j, k in [(0, 0, "r"), (4, 0, "k"), (5, 1, "p"), (6, 1, "p"), (7, 1, "p"), (2, 2, "n"), (3, 3, "p"), (4, 2, "b"), (3, 0, "q"), (7, 0, "r")]:
        piece(i, j, k, IV)
    for i, j, k in [(0, 7, "r"), (6, 7, "k"), (5, 6, "p"), (6, 6, "p"), (7, 5, "p"), (4, 4, "p"), (2, 5, "b"), (3, 7, "q"), (7, 7, "r")]:
        piece(i, j, k, EB)
    cork = piece(5, 5, "n", CORK)   # the missing black knight, carved from a cork
    obj = m.build()
    hs("game", (-S / 2 + wall + 3.5 * sq, y0 + 0.016, -S / 2 + wall + 3.5 * sq), (0, 1, 0), obj)
    hs("knight", cork, (0, 1, 0), obj)
    hinge = V(0, H1, S / 2)
    lid = Model("Lid")
    for part in hollow_box((-S / 2, H1, -S / 2), (S / 2, H1 + H2, S / 2), wall, open_top=False, bevel=0.002):
        lid.add(part, WOOD)
    lid.add(box((-0.05, H1 + H2 - wall - 0.0015, -0.03), (0.05, H1 + H2 - wall, 0.03)), PAPER)
    add_decal(lid, (0.0, H1 + H2 - wall - 0.0016, 0.0), (0, -1, 0), (0, 0, 1), 0.09, 0.05, "chess_note", offset=0.0002)
    lid.add(cbox((0, H1 + H2 * 0.4, -S / 2 - 0.002), (0.02, 0.008, 0.004)), BRASS)
    lid.build(origin=hinge, parent=obj)
    hs("note", (0.0, H1 + H2 - wall - 0.002, 0.0), (0, -1, 0), obj, part="Lid")
    return obj


def photograph():
    W, D, T = 0.105, 0.13, 0.0012
    m = Model("photograph")
    m.add(box((-W / 2, 0, -D / 2), (W / 2, T, D / 2)), mat("card", "F2EEE2"))
    add_decal(m, (0, T, 0), (0, 1, 0), (0, 0, 1), W - 0.002, D - 0.002, "photo_couple", offset=0.0002)
    add_decal(m, (0, 0, 0), (0, -1, 0), (0, 0, 1), W - 0.002, D - 0.002, "photo_back", offset=0.0002)
    obj = m.build()
    hs("clock", (0.012, T + 0.0004, -0.036), (0, 1, 0), obj)
    hs("stamp", (0.0, -0.0004, 0.01), (0, -1, 0), obj)
    hs("figure", (-0.03, T + 0.0004, -0.012), (0, 1, 0), obj)
    return obj


def frost_patches(model, pts, y, r):
    rng = np.random.default_rng(len(pts))
    for (x, z) in pts:
        for _ in range(3):
            dx, dz = rng.normal(size=2) * r * 0.5
            model.add(ellipsoid((x + dx, y, z + dz), (r * rng.uniform(0.6, 1.2), 0.0012, r * rng.uniform(0.5, 1.0)), segments=14, rings=6), mat("frost", "E8F2FF"))


def frosted_tin():
    TIN, TIN_D = mat("paint", "5E6A48"), mat("paint", "3C4430")
    W, D, H1, H2 = 0.2, 0.14, 0.06, 0.018
    m = Model("frosted_tin")
    m.add(rrect_slab((0, H1 / 2, 0), W, D, H1, 0.02, plane="xz"), TIN)
    m.add(rrect_slab((0, H1 + 0.0005, 0), W - 0.01, D - 0.01, 0.001, 0.016, plane="xz"), mat("paint", "2A2A22"))
    # inside, visible once the lid is off: letters tied with string, a harmonica, a pressed poppy
    m.add(box((-0.085, H1 - 0.012, -0.05), (0.02, H1 + 0.0012, 0.05)), mat("paper", "E2D6B8"))
    add_decal(m, (-0.032, H1 + 0.0013, 0.0), (0, 1, 0), (0, 0, 1), 0.1, 0.064, "letters_mabel", offset=0.0002)
    m.add(box((-0.04, H1 - 0.012, -0.0505), (-0.036, H1 + 0.0025, 0.0505)), mat("cloth", "B09060"))
    m.add(cbox((0.058, H1 - 0.004, -0.01), (0.03, 0.012, 0.1), bevel=0.002), STEEL)
    for k in range(10):
        m.add(box((0.0435, H1 - 0.006, -0.055 + k * 0.009), (0.0445, H1 - 0.002, -0.049 + k * 0.009)), mat("paint", "1A1A1A"))
    for k in range(5):
        a = k * 2 * math.pi / 5
        m.add(ellipsoid((0.06 + math.cos(a) * 0.006, H1 + 0.0025, 0.052 + math.sin(a) * 0.006), (0.006, 0.0006, 0.004)), mat("velvet", "B02020"))
    m.add(sphere((0.06, H1 + 0.003, 0.052), 0.0022, scale=(1, 0.4, 1)), mat("paint", "141414"))
    frost_patches(m, [(-0.09, -0.06), (0.09, 0.06), (0.095, -0.05)], H1 * 0.6, 0.012)
    obj = m.build()
    hs("letters", (-0.032, H1 + 0.0016, 0.0), (0, 1, 0), obj)
    hs("harmonica", (0.058, H1 + 0.0025, -0.01), (0, 1, 0), obj)
    hs("poppy", (0.06, H1 + 0.004, 0.052), (0, 1, 0), obj)
    lid = Model("Lid")
    lid.add(rrect_slab((0, H1 + H2 / 2 - 0.004, 0), W + 0.006, D + 0.006, H2, 0.022, plane="xz", bevel=0.002), TIN)
    add_decal(lid, (0, H1 + H2 - 0.004, 0), (0, 1, 0), (0, 0, 1), 0.16, 0.04, "tin_lid", offset=0.0004)
    lid.add(box((-0.09, H1 + H2 - 0.0042, -0.055), (0.09, H1 + H2 - 0.0035, -0.052)), TIN_D)
    frost_patches(lid, [(-0.07, 0.04), (0.06, 0.035), (0.02, -0.045), (-0.08, -0.04)], H1 + H2 - 0.003, 0.016)
    lid.build(origin=(0, H1, 0), parent=obj)
    hs("lid", (0, H1 + H2 - 0.0035, 0), (0, 1, 0), obj, part="Lid")
    return obj


def lunch_tin():
    TIN = mat("paint", "8A3A2A")
    W, D, H1 = 0.2, 0.12, 0.07
    m = Model("lunch_tin")
    for part in hollow_box((-W / 2, 0, -D / 2), (W / 2, H1, D / 2), 0.003, bevel=0.003):
        m.add(part, TIN)
    # a doorstep corned-beef sandwich and Mavis's note
    for yb in (0.004, 0.024):
        m.add(cbox((-0.03, yb + 0.009, 0.0), (0.11, 0.018, 0.09), bevel=0.006), mat("cloth", "E6CC98"))
    m.add(cbox((-0.03, 0.0225, 0.0), (0.104, 0.005, 0.086)), mat("cloth", "B05A4A"))
    m.add(box((0.035, 0.004, -0.045), (0.09, 0.006, 0.045)), PAPER)
    add_decal(m, (0.0625, 0.0062, 0.0), (0, 1, 0), (-1, 0, 0), 0.085, 0.052, "note_mavis", offset=0.0002)
    # the handle on the lid side
    obj = m.build()
    hs("sandwich", (-0.03, 0.043, 0.0), (0, 1, 0), obj)
    hs("note", (0.0625, 0.0068, 0.0), (0, 1, 0), obj)
    hinge = V(0, H1, D / 2)
    lid = Model("Lid")
    r = D / 2 + 0.003
    dome = lathe([(r, 0.0), (r, 0.008), (r * 0.92, 0.022), (r * 0.7, 0.034), (r * 0.35, 0.041), (0.0, 0.043)], (0, 0, 0), segments=40)
    lid.add(transformed(dome, mathutils.Matrix.Translation((0, H1, 0)) @ mathutils.Matrix.Diagonal(((W / 2 + 0.003) / r, 1, 1, 1))), TIN)
    lid.add(sweep([(-0.04, H1 + 0.04, 0), (-0.035, H1 + 0.058, 0), (0.035, H1 + 0.058, 0), (0.04, H1 + 0.04, 0)], 0.0035, segments=8), STEEL)
    lid.add(box((-0.06, H1 + 0.002, -0.04), (0.06, H1 + 0.004, 0.04)), mat("paper", "FBF8F0"))
    add_decal(lid, (0.0, H1 + 0.0042, 0.0), (0, 1, 0), (-1, 0, 0), 0.1, 0.07, "child_drawing", offset=0.0)
    lid.build(origin=hinge, parent=obj)
    hs("drawing", (0.0, H1 + 0.005, 0.0), (0, 1, 0), obj, part="Lid")
    return obj


def seashell():
    m = Model("seashell")
    P = lambda *a: np.array(a, dtype=float)  # noqa: E731
    parts = []
    n = 26
    for i in range(n):
        t = i / (n - 1)
        a = t * 4.2 * math.pi
        r = 0.032 * (1 - t) ** 1.1 + 0.002
        c = P(math.cos(a) * r * 0.55 - 0.02 + t * 0.075, 0.03 * (1 - t) + 0.006 + t * 0.012, math.sin(a) * r * 0.55)
        parts.append(sdf.ellipsoid(c, (r * 0.95, r * 0.8, r * 0.9)))
    body = sdf.union(*parts, k=0.008)
    body = sdf.subtract(body, sdf.ellipsoid(P(-0.035, 0.03, -0.004), (0.022, 0.018, 0.012)), k=0.004)
    body = sdf.displace(body, lambda p: 0.0008 * np.sin(np.arctan2(p[:, 2], p[:, 0] + 0.02) * 22))
    pink = sdf.sphere(P(-0.04, 0.03, -0.01), 0.02)
    cream, inner = sdf.mesh(body, (-0.075, -0.004, -0.045), (0.07, 0.07, 0.045), 0.0016, smooth=1, tris=12000, regions=[pink])
    m.add(cream, mat("ceramic", "E8D8C0"))
    m.add(inner, mat("ceramic", "E8A898"))
    for k in range(9):
        a = k * 0.7
        m.add(sphere((-0.044 + math.cos(a) * 0.006, 0.02 + math.sin(a) * 0.004, -0.006 + k * 0.001), 0.0014, segments=6, rings=4), mat("paint", "D8C090"))
    obj = m.build()
    hs("sea", (0.0, 0.065, 0.0), (0, 1, 0), obj)
    hs("sand", (-0.044, 0.026, -0.012), (-0.6, 0.3, -0.7), obj)
    return obj


def ring_box():
    VEL, CUSH = mat("velvet", "5A1A2A"), mat("velvet", "E8DCC8")
    S, H1, H2 = 0.05, 0.026, 0.02
    m = Model("ring_box")
    # the 1921 ticket the box was found sitting on (with the blue date under the lamp)
    tk = (0.012, 0.0, 0.024)
    m.add(box((tk[0] - 0.03, 0.0, tk[2] - 0.016), (tk[0] + 0.03, 0.0008, tk[2] + 0.016)), mat("card", "E0CCA8"))
    add_decal(m, (tk[0], 0.0008, tk[2]), (0, 1, 0), (0, 0, 1), 0.058, 0.03, "ticket_1921", offset=0.0002)
    m.add(rrect_slab((0, 0.0008 + H1 / 2, -0.004), S, S, H1, 0.006, plane="xz", bevel=0.003), VEL)
    m.add(rrect_slab((0, 0.0008 + H1 - 0.004, -0.004), S - 0.006, S - 0.006, 0.006, 0.004, plane="xz", bevel=0.002), CUSH)
    m.add(box((-0.016, 0.0008 + H1 - 0.0012, -0.0048), (0.016, 0.0008 + H1 + 0.0002, -0.0032)), mat("velvet", "8A7A6A"))
    # the ring standing in its slot
    rc = (0, 0.0008 + H1 + 0.008, -0.004)
    m.add(torus(rc, 0.0085, 0.0016, axis="z", segments=40, ring_segments=12), GOLD)
    bm, _ = lathe([(0.0068, -0.0012), (0.0068, 0.0012)], (0, 0, 0), segments=40, cap_bottom=False, cap_top=False)
    bm.normal_update()
    if sum(f.normal.dot(f.calc_center_median().normalized()) for f in bm.faces) > 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    band_uv(bm, (0, 0, 0), "y", 0.0024)
    m.add(transformed((bm, None), mathutils.Matrix.Translation(rc) @ mathutils.Matrix.Rotation(math.radians(90), 4, "X")), tex("ring_engraving"), uv=False)
    # forget-me-not pressed at the cushion's edge
    for k in range(5):
        a = k * 2 * math.pi / 5
        m.add(sphere((0.016 + math.cos(a) * 0.0018, 0.0008 + H1 + 0.0002, 0.012 + math.sin(a) * 0.0018), 0.0016, scale=(1, 0.2, 1), segments=8, rings=4), mat("cloth", "6A8AD8"))
    m.add(sphere((0.016, 0.0008 + H1 + 0.0004, 0.012), 0.0007), mat("paint", "E8D040"))
    obj = m.build()
    uv = Model("UV_ticket")
    add_decal(uv, (tk[0], 0.0008, tk[2]), (0, 1, 0), (0, 0, 1), 0.058, 0.03, "ticket_1921_uv", offset=0.0005)
    uv.build(parent=obj)
    hs("engraving", (0, rc[1] - 0.0066, -0.004), (0, 1, 0), obj)
    hs("ticket", (tk[0] + 0.012, 0.0012, tk[2] + 0.004), (0, 1, 0), obj)
    hs("date", (tk[0] - 0.012, 0.0012, tk[2] - 0.004), (0, 1, 0), obj)
    hs("flower", (0.016, 0.0008 + H1 + 0.0008, 0.012), (0, 1, 0), obj)
    hinge = V(0, 0.0008 + H1, -0.004 + S / 2)
    lid = Model("Lid")
    lid.add(rrect_slab((0, 0.0008 + H1 + H2 / 2, -0.004), S, S, H2, 0.006, plane="xz", bevel=0.004), VEL)
    lid.add(rrect_slab((0, 0.0008 + H1 + 0.0015, -0.004), S - 0.006, S - 0.006, 0.002, 0.004, plane="xz"), mat("velvet", "F0E8DC"))
    lid.build(origin=hinge, parent=obj)
    return obj


def violin_outline(cx, cz, scale):
    pts = []
    for k in range(64):
        t = 2 * math.pi * k / 64
        x = math.cos(t)
        z = math.sin(t)
        # a fiddle silhouette: wide lower bout, waist, narrower upper bout
        w = 1.0 - 0.28 * math.exp(-((x - 0.05) ** 2) / 0.02) - 0.18 * (x > 0) * x
        pts.append((cx + x * scale * 1.0, cz + z * scale * 0.42 * w))
    return pts


def violin_case():
    SHELL, LIN = mat("leather", "1E1E22"), mat("velvet", "2A4A3A")
    L, Wc, H1, H2 = 0.66, 0.24, 0.07, 0.05
    m = Model("violin_case")
    outline = violin_outline(0, 0, L / 2)
    m.add(slab(outline, 0.0, H1, plane="xz", bevel=0.008), SHELL)
    m.add(slab(violin_outline(0, 0, L / 2 - 0.015), H1 - 0.002, H1 + 0.0005, plane="xz"), LIN)
    # the violin resting in the plush
    VW = mat("wood", "A0501E")
    vb = violin_outline(-0.06, 0, 0.18)
    m.add(slab(vb, H1, H1 + 0.03, plane="xz", bevel=0.01), VW)
    m.add(box((0.11, H1 + 0.012, -0.012), (0.28, H1 + 0.026, 0.012)), mat("darkwood", "1E140E"))
    m.add(sphere((0.295, H1 + 0.02, 0.0), 0.016, scale=(1.2, 0.8, 0.9)), VW)
    m.add(box((-0.14, H1 + 0.03, -0.035), (-0.12, H1 + 0.046, 0.035)), mat("darkwood", "1E140E"))
    for k in range(4):
        z = -0.006 + k * 0.004
        m.add(box((-0.2, H1 + 0.0315, z - 0.0004), (0.29, H1 + 0.0322, z + 0.0004)), STEEL)
    m.add(box((-0.045, H1 + 0.0305, -0.022), (-0.035, H1 + 0.04, 0.022)), mat("wood", "E0C090"))
    for sz in (-1, 1):
        m.add(transformed(text_mesh("f", (-0.04, H1 + 0.0302, sz * 0.032), 0.04, depth=0.0006, plane="xz", font=FONT_TITLE), mathutils.Matrix.Identity(4)), mat("paint", "120A06"))
    add_decal(m, (-0.04, H1 + 0.0303, 0.032), (0, 1, 0), (1, 0, 0), 0.03, 0.012, "violin_label", offset=0.0004)
    for x in (-0.14, 0.14):
        m.add(cbox((x, H1 - 0.01, -Wc / 2 + 0.01), (0.025, 0.02, 0.008), bevel=0.002), STEEL)
    m.add(sweep([(-0.05, H1 * 0.55, -0.1), (-0.04, H1 * 0.55, -0.125), (0.04, H1 * 0.55, -0.125), (0.05, H1 * 0.55, -0.1)], 0.007, segments=10, flat=0.6), SHELL)
    obj = m.build()
    hs("label", (-0.04, H1 + 0.0315, 0.032), (0, 1, 0), obj)
    hinge = V(0, H1, 0.11)
    lid = Model("Lid")
    lid.add(slab(outline, H1, H1 + H2, plane="xz", bevel=0.01), SHELL)
    lid.add(slab(violin_outline(0, 0, L / 2 - 0.015), H1 - 0.0005, H1 + 0.001, plane="xz"), LIN)
    add_decal(lid, (0.12, H1 - 0.0006, 0.0), (0, -1, 0), (-1, 0, 0), 0.07, 0.056, "setlist", offset=0.0003)
    add_decal(lid, (-0.18, H1 - 0.0006, 0.02), (0, -1, 0), (-1, 0, 0), 0.05, 0.063, "lou_mother", offset=0.0003)
    lid.build(origin=hinge, parent=obj)
    hs("setlist", (0.12, H1 - 0.0012, 0.0), (0, -1, 0), obj, part="Lid")
    hs("mother", (-0.18, H1 - 0.0012, 0.02), (0, -1, 0), obj, part="Lid")
    return obj


def record():
    S, T = 0.31, 0.003
    m = Model("record")
    m.add(box((-S / 2, 0, -S / 2), (S / 2, T, S / 2)), mat("card", "2A3A5A"))
    # sleeve art: a big hole showing the label, and the title
    add_decal(m, (0.0, T, 0.0), (0, 1, 0), (0, 0, 1), 0.1, 0.1, "record_label", offset=0.0004)
    m.add(torus((0, T + 0.0003, 0), 0.052, 0.0012, axis="y", segments=48), mat("paint", "E8D8A0"))
    m.add(text_mesh("LAST TRAIN HOME", (0, T + 0.0004, -0.11), 0.022, depth=0.0005, plane="xz", font=FONT_TITLE), mat("paint", "E8D8A0"))
    m.add(text_mesh("LOU DELACROIX", (0, T + 0.0004, 0.105), 0.016, depth=0.0005, plane="xz"), mat("paint", "E8D8A0"))
    add_decal(m, (0.0, 0.0, 0.0), (0, -1, 0), (0, 0, 1), 0.26, 0.26, "record_sleeve", offset=0.0003)
    add_decal(m, (0.09, T, -0.105), (0, 1, 0), (0.15, 0, 1), 0.07, 0.05, "sleeve_signature", offset=0.0006)
    # the disc, slid half out of the right side
    m.add(cyl((0.12, T + 0.0012, 0.0), 0.149, 0.0016, segments=64), mat("plastic", "0E0E10"))
    for r in (0.07, 0.09, 0.11, 0.13):
        m.add(torus((0.12, T + 0.002, 0.0), r, 0.0003, axis="y", segments=64), mat("plastic", "2A2A2E"))
    obj = m.build()
    hs("label", (0.0, T + 0.0008, 0.0), (0, 1, 0), obj)
    hs("date", (0.0, -0.0008, 0.03), (0, -1, 0), obj)
    hs("signature", (0.09, T + 0.0008, -0.105), (0, 1, 0), obj)
    return obj


def opera_glove():
    SATIN = mat("cloth", "EEE6D8")
    m = Model("opera_glove")
    P = lambda *a: np.array(a, dtype=float)  # noqa: E731
    arm = sdf.capsule(P(-0.17, 0.012, 0.0), P(0.0, 0.009, 0.0), 0.026, 0.017)
    palm = sdf.rbox(P(0.04, 0.009, 0.0), (0.035, 0.007, 0.03), 0.006)
    fingers = []
    for k, (dz, ln) in enumerate(((-0.022, 0.052), (-0.008, 0.062), (0.007, 0.058), (0.021, 0.046))):
        fingers.append(sdf.capsule(P(0.07, 0.008, dz), P(0.07 + ln, 0.006, dz * 1.15), 0.0072, 0.0058))
    thumb = sdf.capsule(P(0.03, 0.008, -0.03), P(0.06, 0.006, -0.065), 0.008, 0.0065)
    f = sdf.union(arm, palm, *fingers, thumb, k=0.007)
    f = sdf.warp(f, lambda p: p + np.stack([0 * p[:, 0], 0.003 * np.sin(p[:, 0] * 40) * (p[:, 0] < 0), 0 * p[:, 0]], 1))
    f = sdf.displace(f, lambda p: -0.0012 * np.maximum(0, np.sin(p[:, 0] * 120 + np.sin(p[:, 2] * 60))) * (p[:, 0] < 0.0))
    cuff = sdf.subtract(f, sdf.halfspace(P(-0.172, 0, 0), P(1, 0, 0)))
    m.add(sdf.mesh(cuff, (-0.2, -0.02, -0.08), (0.15, 0.04, 0.05), 0.0016, smooth=1, tris=12000), SATIN)
    m.add(text_mesh("E.L.", (-0.13, 0.034, -0.004), 0.016, depth=0.0008, plane="xz", font=FONT_TITLE, rot=90), mat("cloth", "B08840"))
    m.add(ellipsoid((0.112, 0.0128, -0.009), (0.007, 0.0006, 0.0045)), mat("paint", "B01828"))
    m.add(ellipsoid((0.105, 0.013, -0.006), (0.005, 0.0006, 0.003)), mat("paint", "C02030"))
    # a rose petal tucked into the open cuff
    m.add(ellipsoid((-0.172, 0.014, 0.008), (0.003, 0.009, 0.007)), mat("velvet", "D06070"))
    obj = m.build()
    hs("monogram", (-0.13, 0.036, -0.004), (0, 1, 0), obj)
    hs("lipstick", (0.11, 0.0138, -0.008), (0, 1, 0), obj)
    hs("petal", (-0.176, 0.016, 0.008), (-1, 0.2, 0), obj)
    return obj


def spectacles():
    CASE, LIN = mat("leather", "4A2A26"), mat("velvet", "3A2A4A")
    L, Wd, H1, H2 = 0.15, 0.06, 0.018, 0.016
    m = Model("spectacles")
    m.add(transformed(lathe([(0.0, 0.0), (Wd / 2 - 0.004, 0.0), (Wd / 2, 0.006), (Wd / 2, H1)], (0, 0, 0), segments=40, cap_top=False), mathutils.Matrix.Diagonal((L / Wd, 1, 1, 1))), CASE)
    m.add(transformed(cyl((0, H1 - 0.012, 0), Wd / 2 - 0.002, 0.002, segments=40), mathutils.Matrix.Diagonal((L / Wd, 1, 1, 1))), LIN)
    # the folded 1903 timetable under the specs
    m.add(box((-0.045, H1 - 0.011, -0.018), (0.03, H1 - 0.0102, 0.018)), PAPER)
    add_decal(m, (-0.008, H1 - 0.0101, 0.0), (0, 1, 0), (0, 0, 1), 0.06, 0.03, "timetable_1903", offset=0.0002)
    # round wire spectacles, lenses frosted over
    for sx in (-1, 1):
        m.add(torus((sx * 0.028, H1 - 0.006, 0), 0.017, 0.0012, axis="y", segments=32), BRASS_D)
        m.add(cyl((sx * 0.028, H1 - 0.006, 0), 0.0165, 0.0012, segments=32), mat("frost", "E8F2FF"))
        m.add(rod((sx * 0.045, H1 - 0.006, 0.0), (sx * 0.05, H1 - 0.0065, 0.026), 0.0008), BRASS_D)
    m.add(sweep([(-0.011, H1 - 0.006, 0), (0, H1 - 0.003, 0), (0.011, H1 - 0.006, 0)], 0.001, segments=6), BRASS_D)
    frost_patches(m, [(-0.05, -0.02), (0.06, 0.015)], H1 * 0.5, 0.008)
    obj = m.build()
    hs("lenses", (0.028, H1 - 0.005, 0.0), (0, 1, 0), obj)
    hs("timetable", (-0.034, H1 - 0.0095, 0.012), (0, 1, 0), obj)
    hinge = V(0, H1, Wd / 2)
    lid = Model("Lid")
    lid.add(transformed(oval(lathe([(Wd / 2, H1), (Wd / 2, H1 + H2 - 0.006), (Wd / 2 - 0.004, H1 + H2), (0.0, H1 + H2)], (0, 0, 0), segments=40, cap_bottom=False), 1.0), mathutils.Matrix.Diagonal((L / Wd, 1, 1, 1))), CASE)
    lid.add(transformed(cyl((0, H1 + 0.001, 0), Wd / 2 - 0.002, 0.002, segments=40), mathutils.Matrix.Diagonal((L / Wd, 1, 1, 1))), mat("velvet", "E8DCC8"))
    add_decal(lid, (0.0, H1 - 0.0001, 0.0), (0, -1, 0), (0, 0, -1), 0.13, 0.022, "spec_case", offset=0.0003)
    frost_patches(lid, [(-0.04, 0.0), (0.03, -0.012)], H1 + H2, 0.01)
    lid.build(origin=hinge, parent=obj)
    hs("case", (0.0, H1 - 0.0006, 0.0), (0, -1, 0), obj, part="Lid")
    return obj


def birdcage():
    B = BRASS
    R, Hc = 0.09, 0.2
    m = Model("birdcage")
    m.add(lathe([(0.0, 0.0), (R + 0.012, 0.0), (R + 0.014, 0.008), (R + 0.006, 0.022), (0.0, 0.022)], (0, 0, 0), segments=48), mat("darkwood", "3A2216"))
    m.add(torus((0, 0.022, 0), R, 0.003, axis="y", segments=48), B)
    # bars rising into a dome
    for k in range(24):
        a = 2 * math.pi * k / 24
        pts = [(math.cos(a) * R, 0.022, math.sin(a) * R), (math.cos(a) * R, Hc * 0.7, math.sin(a) * R)]
        for j in range(1, 7):
            t = j / 6
            rr = R * math.cos(t * math.pi / 2)
            pts.append((math.cos(a) * rr, Hc * 0.7 + Hc * 0.3 * math.sin(t * math.pi / 2), math.sin(a) * rr))
        m.add(sweep(pts, 0.0014, segments=6), B)
    m.add(torus((0, Hc * 0.35, 0), R, 0.002, axis="y", segments=48), B)
    m.add(torus((0, Hc * 0.7, 0), R, 0.0025, axis="y", segments=48), B)
    m.add(torus((0, Hc + 0.012, 0), 0.016, 0.003, axis="z", segments=24), B)
    # perch and the clockwork canary
    m.add(rod((-0.06, 0.08, 0.0), (0.06, 0.08, 0.0), 0.003), mat("wood", "C09060"))
    Y = mat("paint", "E8C030")
    m.add(ellipsoid((0.0, 0.098, 0.0), (0.014, 0.016, 0.022)), Y)
    m.add(sphere((0.0, 0.118, -0.016), 0.011), Y)
    m.add(hull([(-0.0025, 0.118, -0.026), (0.0025, 0.118, -0.026), (0.0, 0.1195, -0.034), (0.0, 0.1165, -0.03)]), mat("paint", "D07020"))
    m.add(hull([(-0.008, 0.095, 0.018), (0.008, 0.095, 0.018), (0.0, 0.09, 0.045), (0.0, 0.1, 0.04)]), Y)
    for sx in (-1, 1):
        m.add(sphere((sx * 0.007, 0.121, -0.022), 0.002), mat("eye", "0A0A0A"))
    # a real canary feather on the cage floor
    m.add(sweep([(0.03, 0.0235, 0.04), (0.042, 0.024, 0.048), (0.052, 0.0235, 0.05)], 0.003, radius_end=0.0008, segments=6, flat=0.2), Y)
    # A.L. plate underneath
    add_decal(m, (0.0, 0.0, 0.0), (0, -1, 0), (0, 0, 1), 0.06, 0.03, "cage_plate", offset=0.0004)
    obj = m.build()
    hs("song", (0.0, 0.122, -0.02), (0, 0.4, -1), obj)
    hs("plate", (0.0, -0.0008, 0.0), (0, -1, 0), obj)
    hs("feather", (0.042, 0.025, 0.048), (0, 1, 0), obj)
    # the wind key at the back of the base, and the little door at the front
    key = Model("Key")
    key.add(rod((0, 0.011, R + 0.012), (0, 0.011, R + 0.024), 0.0018), B)
    key.add(hull([(-0.012, 0.004, R + 0.024), (0.012, 0.004, R + 0.024), (-0.012, 0.018, R + 0.024), (0.012, 0.018, R + 0.024),
                  (-0.012, 0.004, R + 0.027), (0.012, 0.004, R + 0.027), (-0.012, 0.018, R + 0.027), (0.012, 0.018, R + 0.027)], bevel=0.003), B)
    key.build(origin=(0, 0.011, R + 0.012), parent=obj)
    door = Model("Door")
    for k in range(4):
        x = -0.018 + k * 0.012
        door.add(rod((x, 0.03, -R - 0.002), (x, 0.09, -R - 0.002), 0.0014), B)
    door.add(rod((-0.02, 0.09, -R - 0.002), (0.02, 0.09, -R - 0.002), 0.0016), B)
    door.add(rod((-0.02, 0.03, -R - 0.002), (0.02, 0.03, -R - 0.002), 0.0016), B)
    door.build(origin=(-0.02, 0.06, -R - 0.002), parent=obj)
    return obj


def snow_globe():
    m = Model("snow_globe")
    BASE = mat("darkwood", "4A2418")
    m.add(lathe([(0.0, 0.0), (0.05, 0.0), (0.052, 0.006), (0.046, 0.03), (0.036, 0.034), (0.0, 0.034)], (0, 0, 0), segments=48), BASE)
    bm, _ = lathe([(0.0518, 0.009), (0.0472, 0.027)], (0, 0, 0), segments=48, cap_bottom=False, cap_top=False)
    band_uv(bm, (0, 0.018, 0), "y", 0.018, start_angle=-math.pi / 2)
    m.add((bm, None), tex("globe_base"), uv=False)
    # the station inside, with drifts of snow
    S = mat("paint", "C8B8A0")
    m.add(cbox((0, 0.046, 0.004), (0.04, 0.022, 0.016)), S)
    m.add(hull([(-0.022, 0.057, -0.006), (0.022, 0.057, -0.006), (-0.022, 0.057, 0.014), (0.022, 0.057, 0.014), (0, 0.068, 0.004)]), mat("paint", "6A3A2A"))
    m.add(cbox((0, 0.074, 0.004), (0.006, 0.014, 0.006)), S)
    m.add(cyl((0, 0.075, -0.0005), 0.0035, 0.0008, axis="z", segments=16), mat("paint", "F0E8D0"))
    for k in range(3):
        m.add(cbox((-0.012 + k * 0.012, 0.047, -0.0042), (0.006, 0.008, 0.001)), mat("emit", "F0C060"))
    # the tiny figure at the Lost Property window
    m.add(cbox((0.012, 0.0405, -0.008), (0.0025, 0.007, 0.0025)), mat("paint", "2A3A5A"))
    m.add(sphere((0.012, 0.0455, -0.008), 0.0018), mat("skin", "E0B090"))
    m.add(cyl((0, 0.0355, 0), 0.04, 0.003, segments=40), mat("paint", "F4F6FA"))
    rng = np.random.default_rng(3)
    for _ in range(40):
        a, r = rng.uniform(0, 2 * math.pi), math.sqrt(rng.uniform(0, 1)) * 0.036
        m.add(sphere((math.cos(a) * r, 0.0375, math.sin(a) * r), 0.0011, segments=6, rings=4), mat("paint", "FFFFFF"))
    m.add(sphere((0, 0.072, 0), 0.042, segments=40, rings=24), mat("glass", "E8F0F4"))
    obj = m.build()
    uv = Model("UV_base")
    bm2, _ = lathe([(0.0521, 0.009), (0.0475, 0.027)], (0, 0, 0), segments=48, cap_bottom=False, cap_top=False)
    band_uv(bm2, (0, 0.018, 0), "y", 0.018, start_angle=-math.pi / 2)
    uv.add((bm2, None), tex("globe_base_uv"), uv=False)
    uv.build(parent=obj)
    hs("snow", (0.0, 0.114, 0.0), (0, 1, 0), obj)
    hs("base", (0.0, 0.019, -0.0505), (0, 0, -1), obj)
    hs("date", (0.006, 0.016, -0.051), (0, 0, -1), obj)
    hs("figure", (0.012, 0.049, -0.011), (0, 0.3, -1), obj)
    return obj


def iron_key():
    IRON = mat("iron", "3A3634")
    m = Model("iron_key")
    m.add(torus((-0.045, 0.006, 0), 0.016, 0.005, axis="y", segments=32), IRON)
    m.add(rod((-0.03, 0.006, 0), (0.06, 0.006, 0), 0.004), IRON)
    m.add(cbox((0.05, 0.006, 0.011), (0.016, 0.006, 0.016), bevel=0.001), IRON)
    m.add(cbox((0.046, 0.006, 0.02), (0.006, 0.006, 0.006)), IRON)
    m.add(text_mesh("1921", (-0.045, 0.0112, -0.016), 0.006, depth=0.0004, plane="xz"), mat("iron", "8A8480"))
    # red ribbon through the bow, and Agnes's tag
    m.add(sweep([(-0.058, 0.004, 0), (-0.075, 0.003, 0.01), (-0.09, 0.002, 0.02), (-0.1, 0.002, 0.012)], 0.004, segments=8, flat=0.25), mat("velvet", "A01818"))
    m.add(box((-0.13, 0.0, 0.0), (-0.095, 0.0008, 0.035)), mat("card", "E8D0A0"))
    add_decal(m, (-0.1125, 0.0008, 0.0175), (0, 1, 0), (0, 0, 1), 0.034, 0.034, "key_tag", offset=0.0002)
    obj = m.build()
    hs("tag", (-0.1125, 0.0012, 0.0175), (0, 1, 0), obj)
    hs("year", (-0.045, 0.012, -0.016), (0, 1, 0), obj)
    return obj


def document(name, w, d, texname, uvname=None, color="F0EAD8", fold=True):
    m = Model(name)
    m.add(box((-w / 2, 0, -d / 2), (w / 2, 0.0008, d / 2)), mat("paper", color))
    if fold:
        m.add(box((-w / 2, 0.0008, -0.0004), (w / 2, 0.0011, 0.0004)), mat("paper", shade_hex(color, 0.85)))
    add_decal(m, (0, 0.0008, 0), (0, 1, 0), (0, 0, 1), w - 0.004, d - 0.004, texname, offset=0.0002)
    obj = m.build()
    if uvname:
        uv = Model("UV_" + name)
        add_decal(uv, (0, 0.0008, 0), (0, 1, 0), (0, 0, 1), w - 0.004, d - 0.004, uvname, offset=0.0006)
        uv.build(parent=obj)
    return obj


def shade_hex(h, f):
    return "".join(f"{min(255, int(int(h[i:i + 2], 16) * f)):02X}" for i in (0, 2, 4))


def chit_vell():
    obj = document("chit_vell", 0.12, 0.076, "chit_vell", "chit_vell_uv", fold=False)
    hs("text", (0.0, 0.0012, -0.01), (0, 1, 0), obj)
    hs("hidden", (0.0, 0.0012, 0.02), (0, 1, 0), obj)
    hs("back", (0.0, -0.0004, 0.0), (0, -1, 0), obj)
    return obj


def certificate_crane():
    obj = document("certificate_crane", 0.18, 0.124, "certificate_crane")
    hs("text", (0.0, 0.0012, -0.02), (0, 1, 0), obj)
    hs("ink", (0.05, 0.0012, 0.04), (0, 1, 0), obj)
    return obj


def requisition_vell():
    obj = document("requisition_vell", 0.15, 0.129, "requisition_vell", "requisition_vell_uv")
    hs("text", (0.0, 0.0012, -0.03), (0, 1, 0), obj)
    hs("hidden", (-0.02, 0.0012, 0.045), (0, 1, 0), obj)
    hs("seal", (0.05, 0.0012, 0.04), (0, 1, 0), obj)
    return obj


BUILDERS = {
    "wallet_brown": wallet_brown,
    "scarf_red": scarf_red,
    "umbrella_duck": umbrella_duck,
    "umbrella_black": umbrella_black,
    "suitcase": suitcase,
    "compass": compass,
    "hatbox": hatbox,
    "locket": locket,
    "pocket_watch": pocket_watch,
    "toy_rabbit": toy_rabbit,
    "briefcase": briefcase,
    "wallet_black": wallet_black,
    "chess_set": chess_set,
    "photograph": photograph,
    "frosted_tin": frosted_tin,
    "lunch_tin": lunch_tin,
    "seashell": seashell,
    "ring_box": ring_box,
    "violin_case": violin_case,
    "record": record,
    "opera_glove": opera_glove,
    "spectacles": spectacles,
    "birdcage": birdcage,
    "snow_globe": snow_globe,
    "iron_key": iron_key,
    "chit_vell": chit_vell,
    "certificate_crane": certificate_crane,
    "requisition_vell": requisition_vell,
}


def bounds_center(obj):
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ mathutils.Vector(c) for o in [obj] + list(obj.children_recursive) if o.type == "MESH" for c in o.bound_box]
    mn = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    cb = (mn + mx) / 2
    return (-cb.x, cb.z, -cb.y), (mx - mn).length / 2


def open_parts(obj, amount=1.0):
    """For previews: swing hinged parts open (Blender axes: Unity x = -x, Unity z = -y)."""
    for c in obj.children_recursive:
        if c.name in ("Lid", "Strap"):
            c.rotation_euler = (math.radians(100 * amount), 0, 0)
        if c.name == "Flap":
            c.rotation_euler = (0, math.radians(175 * amount), 0)


def main():
    args = args_after_dashes()
    only = args[args.index("--only") + 1].split(",") if "--only" in args else None
    prev = args[args.index("--preview") + 1] if "--preview" in args else None
    for name, fn in BUILDERS.items():
        if only and name not in only:
            continue
        reset_scene()
        ROOT_OBJ[0] = None
        obj = fn()
        export_fbx(os.path.join(OUT, name + ".fbx"), [obj])
        if prev:
            c, r = bounds_center(obj)
            setup_preview(c, max(r, 0.03), yaw=30, pitch=35)
            render(os.path.join(prev, f"obj_{name}.png"))
            open_parts(obj)
            render(os.path.join(prev, f"obj_{name}_open.png"))
        print("built", name)


if __name__ == "__main__":
    main()
