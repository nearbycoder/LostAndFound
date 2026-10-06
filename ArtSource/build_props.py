"""Desk props: banker's lamp, rubber stamps and rack, ticket printer, spike, counter bell, flip
calendar, photo frames, tea cup, inkwell and pen, ledger.

    blender -b -P ArtSource/build_props.py [-- --only lamp,bell] [--preview /tmp/p]

Each prop is authored at the origin (bottom centre on y=0) facing the player (-z) and exported to
Assets/Game/Resources/Models/Props/<Name>.fbx. Moving parts and anchors are named children.
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib"))
import bpy  # noqa: E402
from laf import (V, Model, slab, box, cbox, cyl, empty, export_fbx, lathe, mat, rod, rrect_slab, sphere, torus,  # noqa: E402
                 reset_scene, setup_preview, render, text_mesh, grid_quad, rotate_about, transformed, sweep,
                 planar_uv, args_after_dashes, arc_points, hull)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Models", "Props")
FONT_SIGN = os.path.join(ROOT, "ArtSource", "fonts", "Limelight-Regular.ttf")
FONT_TYPE = os.path.join(ROOT, "ArtSource", "fonts", "SpecialElite-Regular.ttf")

BRASS = mat("brass", "C9A15A")
BRASS_D = mat("brass", "9C7A3E")
WALNUT = mat("wood", "6B4329")
WALNUT_D = mat("wood", "4A2E1E")
IRON = mat("iron", "2B2B2E")
STEEL = mat("steel", "A8ABB0")
PAPER = mat("paper", "EFE4CC")
GREEN_GLASS = mat("plastic", "1E5A3C")
CREAM = mat("paint", "EADFC4")
ENAMEL = mat("paint", "2E4A3E")
ENAMEL_D = mat("paint", "1F332B")
BLACK = mat("paint", "141414")
RUBBER = mat("rubber", "2A2222")


def photo_quad(m, center, w, h, tilt=0.0, pivot=None, oval=False):
    """The picture inside a frame: a quad (or oval) facing -z with 0..1 UVs (the game sets its texture)."""
    import mathutils
    q = Model("PHOTO")
    if oval:
        bm, _ = cyl(center, 1.0, 0.0004, axis="z", segments=48)
        c = V(center)
        bm.transform(mathutils.Matrix.Translation(c) @ mathutils.Matrix.Diagonal((w / 2, h / 2, 1, 1)) @ mathutils.Matrix.Translation(-c))
    else:
        bm, _ = grid_quad(center, w, h, plane="xy")
    planar_uv(bm, center, (1, 0, 0), (0, 1, 0), w, h)
    q.add((bm, False), mat("paper", "FFFFFF"), uv=False, transform=rotate_about(pivot or center, "X", tilt) if tilt else None)
    return q


def lamp():
    m = Model("Lamp")
    # weighted brass base with a stepped edge
    m.add(rrect_slab((0, 0.012, 0), 0.20, 0.14, 0.024, 0.04, plane="xz", bevel=0.006), BRASS)
    m.add(rrect_slab((0, 0.029, 0), 0.16, 0.10, 0.012, 0.03, plane="xz", bevel=0.004), BRASS_D)
    m.add(lathe([(0.022, 0.035), (0.02, 0.05), (0.012, 0.06)], (0, 0, 0.03), segments=24), BRASS)
    # stem rising at the back, then a swan-neck to the shade
    path = [(0, 0.06, 0.03), (0, 0.20, 0.03), (0, 0.30, 0.025), (0, 0.345, 0.0), (0, 0.355, -0.03)]
    m.add(sweep(path, 0.009, segments=14), BRASS)
    m.add(sphere((0, 0.205, 0.03), 0.016), BRASS)
    # pull-chain switch
    m.add(sweep([(0.05, 0.33, -0.03), (0.055, 0.30, -0.035), (0.056, 0.26, -0.036)], 0.0015, segments=6), BRASS_D)
    m.add(sphere((0.056, 0.255, -0.036), 0.006), BRASS)
    obj = m.build()

    # the emerald shade: a half-cylinder shell along x, opening downwards, slightly forward
    s = Model("LampShade")
    cz, cy, L, R, t = -0.04, 0.37, 0.27, 0.078, 0.004
    outer = []
    for i in range(25):
        a = math.pi * i / 24
        outer.append((math.cos(a) * R, math.sin(a) * R))
    # build as a loft of half-ring sections along x
    import bmesh
    bm = bmesh.new()
    rings = []
    for xi in range(9):
        x = -L / 2 + L * xi / 8
        ring = []
        for (u, v) in outer:
            ring.append(bm.verts.new((x, cy + v * 0.78, cz + u)))
        for (u, v) in reversed(outer):
            ring.append(bm.verts.new((x, cy + v * 0.78 - t, cz + u * (R - t) / R)))
        rings.append(ring)
    n = len(rings[0])
    for a, b in zip(rings[:-1], rings[1:]):
        for k in range(n):
            bm.faces.new((a[k], b[k], b[(k + 1) % n], a[(k + 1) % n]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    s.add((bm, True), GREEN_GLASS)
    # cream inner lining, brass end caps and a rim
    half = [(cz + math.cos(math.pi * i / 24) * (R + 0.003), cy + math.sin(math.pi * i / 24) * (R + 0.003) * 0.78) for i in range(25)]
    for sx in (-1, 1):
        x0 = sx * L / 2
        s.add(slab(half, min(x0, x0 + sx * 0.006), max(x0, x0 + sx * 0.006), plane="zy", bevel=0.0015), BRASS)
    s.add(box((-L / 2, cy - 0.004, cz - R - 0.004), (L / 2, cy + 0.0, cz - R + 0.004)), BRASS)
    s.add(box((-L / 2, cy - 0.004, cz + R - 0.004), (L / 2, cy + 0.0, cz + R + 0.004)), BRASS)
    shade = s.build(parent=obj)
    b = Model("Bulb")
    b.add(sphere((0, cy - 0.02, cz), 0.024, scale=(1.6, 1, 1)), mat("emit", "FFE3B0"))
    b.build(parent=obj)
    return obj


def stamp(name, knob_hex, rubber_hex, iron=False):
    m = Model(name)
    base_mat = IRON if iron else WALNUT
    m.add(rrect_slab((0, 0.004, 0), 0.07, 0.034, 0.008, 0.004, plane="xz"), mat("rubber", rubber_hex))
    m.add(rrect_slab((0, 0.017, 0), 0.074, 0.038, 0.018, 0.006, plane="xz", bevel=0.003), base_mat)
    m.add(lathe([(0.012, 0.026), (0.008, 0.04), (0.007, 0.06), (0.01, 0.068)], (0, 0, 0), segments=20), base_mat)
    knob = mat("iron", knob_hex) if iron else mat("paint", knob_hex)
    m.add(sphere((0, 0.082, 0), 0.02, scale=(1, 0.82, 1), segments=24, rings=14), knob)
    m.add(torus((0, 0.068, 0), 0.0115, 0.0025, axis="y", segments=24), BRASS)
    if iron:
        m.add(torus((0, 0.1, 0), 0.008, 0.002, axis="z", segments=16), IRON)
    return m.build()


def stamp_rack():
    m = Model("StampRack")
    m.add(rrect_slab((0, 0.009, 0), 0.20, 0.07, 0.018, 0.01, plane="xz", bevel=0.004), WALNUT)
    for dx in (-0.055, 0.0, 0.055):
        m.add(rrect_slab((dx, 0.0185, 0), 0.08, 0.044, 0.002, 0.006, plane="xz"), WALNUT_D)
    m.add(cbox((0, 0.012, -0.036), (0.08, 0.012, 0.004), bevel=0.001), BRASS)
    obj = m.build()
    # ink pad tin in front
    p = Model("InkPad")
    p.add(rrect_slab((0.0, 0.006, -0.085), 0.11, 0.06, 0.012, 0.008, plane="xz", bevel=0.002), mat("iron", "3A3F44"))
    p.add(rrect_slab((0.0, 0.0125, -0.085), 0.096, 0.048, 0.002, 0.006, plane="xz"), mat("felt", "2B2A44"))
    p.build(parent=obj)
    return obj


def printer():
    m = Model("Printer")
    # cast base and enamel body with a rounded hood
    m.add(rrect_slab((0, 0.008, 0), 0.20, 0.17, 0.016, 0.02, plane="xz", bevel=0.004), IRON)
    m.add(box((-0.085, 0.016, -0.07), (0.085, 0.10, 0.075), bevel=0.014, segments=3), ENAMEL)
    m.add(cyl((0, 0.10, 0.01), 0.07, 0.17, axis="x", segments=32, bevel=0.01), ENAMEL)
    m.add(box((-0.087, 0.02, -0.072), (0.087, 0.03, 0.077), bevel=0.003), BRASS_D)
    # paper roll peeking from the top back
    m.add(cyl((0, 0.15, 0.04), 0.03, 0.12, axis="x", segments=28), PAPER)
    m.add(cyl((0, 0.15, 0.04), 0.008, 0.136, axis="x", segments=12), BRASS)
    # front face plate with the paper slot and brass nameplate
    m.add(box((-0.07, 0.035, -0.078), (0.07, 0.085, -0.072), bevel=0.003), CREAM)
    m.add(box((-0.04, 0.055, -0.0805), (0.04, 0.061, -0.077)), BLACK)
    m.add(box((-0.045, 0.067, -0.079), (0.045, 0.08, -0.0765), bevel=0.001), BRASS)
    m.add(text_mesh("NINEFOLD", (0, 0.0735, -0.0797), 0.0085, depth=0.0006, plane="xy", font=FONT_SIGN), BLACK)
    # little dials
    for dx in (-0.055, 0.055):
        m.add(cyl((dx, 0.046, -0.079), 0.008, 0.004, axis="z", segments=20), BRASS)
    m.add(cyl((0.088, 0.075, 0.0), 0.016, 0.01, axis="x", segments=24), BRASS)
    obj = m.build()
    lv = Model("PrinterLever")
    pivot = V(0.094, 0.075, 0.0)
    lv.add(cbox(pivot + V(0.004, 0.0, 0.0), (0.008, 0.012, 0.012), bevel=0.002), BRASS)
    lv.add(rod(pivot + V(0.006, 0, 0), pivot + V(0.006, 0.06, 0.03), 0.004), BRASS)
    lv.add(sphere(pivot + V(0.006, 0.064, 0.032), 0.011), mat("paint", "1A1A1A"))
    lv.build(origin=pivot, parent=obj)
    empty("PRINTER_SLOT", (0, 0.058, -0.081), parent=obj, up=(0, 1, 0), forward=(0, 0, -1))
    return obj


def spike():
    m = Model("Spike")
    m.add(lathe([(0.0, 0.0), (0.045, 0.0), (0.045, 0.008), (0.04, 0.014), (0.012, 0.02), (0.006, 0.024)], (0, 0, 0), segments=32), IRON)
    m.add(lathe([(0.003, 0.02), (0.0025, 0.1), (0.0002, 0.13)], (0, 0, 0), segments=12), STEEL)
    return m.build()


def bell():
    m = Model("Bell")
    m.add(lathe([(0.0, 0.0), (0.05, 0.0), (0.052, 0.006), (0.05, 0.012), (0.0, 0.012)], (0, 0, 0), segments=40), mat("paint", "1C1C1C"))
    m.add(lathe([(0.044, 0.012), (0.045, 0.02), (0.04, 0.036), (0.028, 0.05), (0.012, 0.056), (0.004, 0.058)], (0, 0, 0), segments=48), BRASS)
    obj = m.build()
    p = Model("BellPlunger")
    p.add(rod((0, 0.055, 0), (0, 0.072, 0), 0.0025), STEEL)
    p.add(sphere((0, 0.074, 0), 0.006, scale=(1, 0.6, 1)), BRASS_D)
    p.build(parent=obj)
    return obj


def calendar():
    m = Model("Calendar")
    m.add(rrect_slab((0, 0.01, 0), 0.13, 0.07, 0.02, 0.008, plane="xz", bevel=0.004), WALNUT)
    m.add(cbox((0, 0.021, -0.028), (0.09, 0.004, 0.006), bevel=0.001), BRASS)
    obj = m.build()
    # the page card leaning back on a brass stand
    tilt = -14.0
    pivot = V(0, 0.022, 0.0)
    pg = Model("CalendarCard")
    pg.add(box((-0.055, 0.022, -0.004), (0.055, 0.15, 0.0)), PAPER, transform=rotate_about(pivot, "X", tilt))
    pg.add(box((-0.057, 0.13, -0.006), (0.057, 0.152, 0.002)), mat("paint", "7A2222"), transform=rotate_about(pivot, "X", tilt))
    for dx in (-0.03, 0.03):
        pg.add(torus((dx, 0.152, -0.002), 0.006, 0.0012, axis="x", segments=16), BRASS, transform=rotate_about(pivot, "X", tilt))
    pg.add(box((-0.05, 0.022, 0.0), (0.05, 0.14, 0.004)), BRASS_D, transform=rotate_about(pivot, "X", tilt))
    pg.build(parent=obj)
    # anchor for the date text, on the page, tilted with it
    centre = (rotate_about(pivot, "X", tilt) @ V(0, 0.085, -0.0046).to_4d()).to_3d()
    up = (rotate_about((0, 0, 0), "X", tilt) @ V(0, 1, 0).to_4d()).to_3d()
    fwd = (rotate_about((0, 0, 0), "X", tilt) @ V(0, 0, 1).to_4d()).to_3d()
    empty("CAL_PAGE", centre, parent=obj, up=up, forward=fwd)
    return obj


def frame(name, w, h, border, frame_mat, oval=False):
    """A standing photo frame facing -z, leaning back on an easel."""
    tilt = -10.0
    pivot = V(0, 0, 0.0)
    m = Model(name)
    T = rotate_about(pivot, "X", tilt)
    d = 0.014
    if oval:
        m.add(cyl((0, h / 2 + 0.01, 0.002), 1.0, d, axis="z", segments=40), frame_mat,
              transform=T @ __import__("mathutils").Matrix.Translation((0, h / 2 + 0.01, 0.002)) @ __import__("mathutils").Matrix.Diagonal((w / 2 + border, h / 2 + border, 1, 1)) @ __import__("mathutils").Matrix.Translation((0, -(h / 2 + 0.01), -0.002)))
        m.add(torus((0, h / 2 + 0.01, -d / 2), 1.0, 0.004, axis="z", segments=48, ring_segments=8), BRASS,
              transform=T @ __import__("mathutils").Matrix.Translation((0, h / 2 + 0.01, -d / 2)) @ __import__("mathutils").Matrix.Diagonal((w / 2 + border * 0.5, h / 2 + border * 0.5, 1, 1)) @ __import__("mathutils").Matrix.Translation((0, -(h / 2 + 0.01), d / 2)))
    else:
        cy = h / 2 + border + 0.002
        m.add(box((-w / 2 - border, 0.002, -d / 2), (w / 2 + border, h + 2 * border + 0.002, d / 2), bevel=0.004, segments=3), frame_mat, transform=T)
        m.add(box((-w / 2 - border * 0.4, border * 0.6 + 0.002, -d / 2 - 0.002), (w / 2 + border * 0.4, h + border * 1.4 + 0.002, -d / 2 + 0.001), bevel=0.002), BRASS_D, transform=T)
    # easel leg at the back
    m.add(box((-0.008, 0.0, 0.01), (0.008, h * 0.8, 0.016)), WALNUT_D, transform=rotate_about((0, h * 0.8, 0.012), "X", -18) @ T)
    obj = m.build()
    cy = (h / 2 + 0.01) if oval else (h / 2 + border + 0.002)
    q = photo_quad(m, (0, cy, -d / 2 - 0.0035), w * (0.92 if oval else 1), h * (0.92 if oval else 1), tilt=tilt, pivot=pivot, oval=oval)
    q.build(parent=obj)
    return obj


def teacup():
    m = Model("TeaCup")
    china = mat("ceramic", "F2ECDD")
    band = mat("ceramic", "2F5A46")
    m.add(lathe([(0.0, 0.0), (0.075, 0.0), (0.078, 0.004), (0.07, 0.008), (0.03, 0.006), (0.0, 0.006)], (0, 0, 0), segments=40), china)
    m.add(torus((0, 0.007, 0), 0.068, 0.0015, axis="y", segments=40), band)
    m.add(lathe([(0.0, 0.008), (0.026, 0.008), (0.04, 0.03), (0.046, 0.06), (0.044, 0.062), (0.038, 0.032), (0.024, 0.014), (0.0, 0.014)], (0, 0, 0), segments=40), china)
    m.add(torus((0, 0.055, 0), 0.0455, 0.002, axis="y", segments=40), band)
    m.add(cyl((0, 0.05, 0), 0.04, 0.002, segments=32), mat("paint", "5A3418"))  # tea
    m.add(torus((0.052, 0.035, 0), 0.014, 0.0035, axis="z", segments=20, arc=0.62, start=-0.31), china)
    # spoon on the saucer
    m.add(sweep([(-0.02, 0.012, -0.05), (0.03, 0.009, -0.035), (0.06, 0.008, -0.02)], 0.002, segments=6, flat=0.4), STEEL)
    m.add(sphere((-0.026, 0.012, -0.052), 0.008, scale=(1.2, 0.35, 0.8)), STEEL)
    return m.build()


def inkwell():
    m = Model("Inkwell")
    m.add(box((-0.03, 0.0, -0.03), (0.03, 0.04, 0.03), bevel=0.008, segments=3), mat("glass", "8FB0C0"))
    m.add(cbox((0, 0.018, 0), (0.05, 0.03, 0.05), bevel=0.006), mat("paint", "141830"))
    m.add(cyl((0, 0.045, 0), 0.012, 0.012, segments=20, bevel=0.002), BRASS)
    obj = m.build()
    pen = Model("Pen")
    pen.add(rod((0.05, 0.006, -0.06), (0.18, 0.006, 0.0), 0.0055, segments=16), mat("plastic", "1A2C26"))
    pen.add(rod((0.035, 0.006, -0.067), (0.05, 0.006, -0.06), 0.004, radius2=0.0055, segments=16), BRASS)
    pen.add(rod((0.12, 0.012, -0.028), (0.16, 0.012, -0.01), 0.0012, segments=6), BRASS)
    pen.build(parent=obj)
    return obj


def ledger():
    m = Model("Ledger")
    cover = mat("cloth", "2C4A3C")
    m.add(box((-0.115, 0.0, -0.155), (0.115, 0.035, 0.155), bevel=0.004, segments=2), cover)
    m.add(box((-0.11, 0.003, -0.15), (0.112, 0.032, 0.152)), PAPER)
    m.add(box((-0.118, -0.001, -0.157), (-0.095, 0.036, 0.157), bevel=0.003), mat("leather", "5A2E1E"))
    for zc in (-0.14, 0.14):
        m.add(box((0.08, -0.001, zc - 0.018), (0.117, 0.036, zc + 0.018), bevel=0.003), mat("leather", "5A2E1E"))
    m.add(cbox((0.0, 0.0355, 0.0), (0.09, 0.001, 0.05)), mat("paper", "D8C8A0"))
    m.add(text_mesh("LOST PROPERTY", (0.0, 0.0362, 0.012), 0.012, depth=0.0004, plane="xz", font=FONT_SIGN), mat("gold", "C9A15A"))
    m.add(text_mesh("1921 –", (0.0, 0.0362, -0.014), 0.011, depth=0.0004, plane="xz", font=FONT_TYPE), mat("gold", "C9A15A"))
    return m.build()


def umbrella_stand():
    """A glazed bottle-green umbrella stand with brass bands, open at the top: the umbrellas live here,
    beside the shelves (they are longer than any board)."""
    m = Model("UmbrellaStand")
    glaze = mat("ceramic", "2F5A46")
    # one closed profile: out along the foot, up the outside, over the rolled lip, down the inside
    prof = [(0.0, 0.0), (0.104, 0.0), (0.112, 0.012), (0.106, 0.05), (0.104, 0.40), (0.112, 0.445),
            (0.118, 0.456), (0.115, 0.466), (0.104, 0.466), (0.096, 0.45), (0.094, 0.05), (0.09, 0.03), (0.0, 0.03)]
    m.add(lathe(prof, (0, 0, 0), segments=48), glaze)
    for y in (0.06, 0.40):
        m.add(torus((0, y, 0), 0.107, 0.004, axis="y", segments=48), BRASS)
    return m.build()


BUILDERS = {
    "Lamp": lamp,
    "StampRack": stamp_rack,
    "Stamp_Return": lambda: stamp("Stamp_Return", "2F6B3A", "1E4A28"),
    "Stamp_Refuse": lambda: stamp("Stamp_Refuse", "8A2420", "5A1A18"),
    "Stamp_Seal": lambda: stamp("Stamp_Seal", "2B2B30", "1A1A20", iron=True),
    "Printer": printer,
    "Spike": spike,
    "Bell": bell,
    "Calendar": calendar,
    "Frame_Tall": lambda: frame("Frame_Tall", 0.11, 0.15, 0.016, mat("wood", "3A2418")),
    "Frame_Wide": lambda: frame("Frame_Wide", 0.17, 0.12, 0.014, mat("silver", "B8B8BC")),
    "Frame_Small": lambda: frame("Frame_Small", 0.075, 0.10, 0.012, BRASS),
    "Frame_Oval": lambda: frame("Frame_Oval", 0.10, 0.13, 0.012, mat("wood", "5A3A24"), oval=True),
    "TeaCup": teacup,
    "Inkwell": inkwell,
    "Ledger": ledger,
    "UmbrellaStand": umbrella_stand,
}


def main():
    args = args_after_dashes()
    only = args[args.index("--only") + 1].split(",") if "--only" in args else None
    prev = args[args.index("--preview") + 1] if "--preview" in args else None
    for name, fn in BUILDERS.items():
        if only and name not in only:
            continue
        reset_scene()
        obj = fn()
        export_fbx(os.path.join(OUT, name + ".fbx"), [obj])
        if prev:
            import mathutils
            bpy.context.view_layer.update()
            pts = [o.matrix_world @ mathutils.Vector(c) for o in [obj] + list(obj.children_recursive) if o.type == "MESH" for c in o.bound_box]
            mn = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
            mx = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
            cb = (mn + mx) / 2
            cu = (-cb.x, cb.z, -cb.y)
            setup_preview(cu, max((mx - mn).length / 2, 0.02), yaw=25, pitch=22)
            render(os.path.join(prev, f"prop_{name}.png"))
        print("built", name)


if __name__ == "__main__":
    main()
