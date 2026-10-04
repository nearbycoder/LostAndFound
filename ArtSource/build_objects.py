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
    # two layers folded over at the right end
    m.add(rrect_slab((0, t / 2, 0), L, Wd, t, 0.012, plane="xz", bevel=0.004), RED)
    m.add(rrect_slab((0.02, t * 1.5 + 0.001, 0), L - 0.04, Wd, t, 0.012, plane="xz", bevel=0.004), RED)
    m.add(torus((L / 2 - 0.004, t + 0.0005, 0), t * 0.55, t * 0.5, axis="z", segments=20, ring_segments=10, arc=0.5, start=-0.25, scale=(1, 1, Wd / (t * 1.05) * 0.0 + 1)), RED,
          transform=mathutils.Matrix.Translation((L / 2 - 0.004, t + 0.0005, 0)) @ mathutils.Matrix.Diagonal((1, 1, Wd / 0.0266, 1)) @ mathutils.Matrix.Translation((-(L / 2 - 0.004), -(t + 0.0005), 0)))
    # cream stripes near the ends
    for x in (-0.1, -0.075):
        m.add(box((x - 0.008, 0.0, -Wd / 2 + 0.002), (x + 0.008, t + 0.0004, Wd / 2 - 0.002)), CREAM)
        m.add(box((x + 0.02 - 0.008, t + 0.001, -Wd / 2 + 0.002), (x + 0.02 + 0.008, 2 * t + 0.0014, Wd / 2 - 0.002)), CREAM)
    # fringe
    for k in range(9):
        z = -Wd / 2 + 0.01 + k * (Wd - 0.02) / 8
        m.add(sweep([(-L / 2 + 0.004, t * 0.5, z), (-L / 2 - 0.018, t * 0.35, z + 0.002), (-L / 2 - 0.034, 0.002, z - 0.001)], 0.0022, segments=6), RED)
        m.add(sweep([(-L / 2 + 0.024, t * 1.6, z), (-L / 2 + 0.004, t * 1.4, z - 0.002), (-L / 2 - 0.012, t * 1.1, z + 0.002)], 0.002, segments=6), RED)
    # the moth hole: a frayed dark gap in the top layer, near the fringe
    m.add(sphere((-0.085, 2 * t + 0.0009, 0.022), 0.0055, scale=(1.3, 0.1, 1), segments=12, rings=6), DARK)
    for k in range(6):
        a = k * math.pi / 3
        m.add(rod((-0.085 + math.cos(a) * 0.006, 2 * t + 0.001, 0.022 + math.sin(a) * 0.006), (-0.085 + math.cos(a) * 0.009, 2 * t + 0.0014, 0.022 + math.sin(a) * 0.009), 0.0008, segments=5), RED)
    # Nan's label sewn on the underside, and a little knitted C in the corner
    add_decal(m, (0.07, 0.0, 0.0), (0, -1, 0), (0, 0, 1), 0.06, 0.024, "label_nan")
    m.add(text_mesh("C", (0.105, 2 * t + 0.0012, -0.04), 0.014, depth=0.0012, plane="xz"), CREAM)
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


BUILDERS = {
    "wallet_brown": wallet_brown,
    "scarf_red": scarf_red,
    "umbrella_duck": umbrella_duck,
    "umbrella_black": umbrella_black,
    "suitcase": suitcase,
    "compass": compass,
    "hatbox": hatbox,
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
