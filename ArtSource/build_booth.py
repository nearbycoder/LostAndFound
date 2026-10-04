"""Builds the Lost Property booth: desk, counter window, drawer cabinet, shelf, Iron Drawer, walls,
plus the concourse seen through the glass.

    blender -b -P ArtSource/build_booth.py                       # export FBX
    blender -b -P ArtSource/build_booth.py -- --preview /tmp/p    # also render player-eye previews

Layout (Unity space, metres). The player sits at the origin, eye at (0, 1.22, 0), facing +z.
  desk top y=0.76, z 0.28..1.0 | counter ledge y=0.92 at z 0.92..1.25 | glass at z=1.04
  cabinet (left) front face x=-0.88 | shelf (right) front x=+0.90 | commuter stands at z~1.5
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib"))
import bpy  # noqa: E402
from laf import (V, Model, box, cbox, cyl, empty, export_fbx, lathe, mat, rod, rrect_slab, sphere,  # noqa: E402
                 torus, reset_scene, shade, setup_preview, render, UNITY_TO_BLENDER, text_mesh, grid_quad,
                 rotate_about, transformed, sweep, args_after_dashes)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Models")

WALNUT = mat("wood", "6B4329")
WALNUT_D = mat("wood", "4A2E1E")
DARK = mat("darkwood", "2E1C12")
LEATHER = mat("leather", "1F3B33")
BRASS = mat("brass", "C9A15A")
BRASS_D = mat("brass", "8E6D35")
IRON = mat("iron", "2B2B2E")
IRON_L = mat("iron", "45454A")
FELT = mat("felt", "2A4A3A")
PAPER = mat("paper", "EFE4CC")
WALL = mat("paint", "C9C3A2")
WALL_G = mat("paint", "3F5A4C")
GLASS = mat("glass", "A8C8D0")
FLOOR = mat("wood", "3B2618")
BLACK = mat("paint", "141414")

CAB_FRONT = -0.95                        # cabinet front plane (faces +x), z -0.62..0.28
SHELF_FRONT = 0.95                       # shelf front plane (faces -x), z -0.62..0.28
CAB_Z = (-0.62, 0.28)
DRAWER_COLS = [-0.47, -0.17, 0.13]       # z centres; facing the cabinet (-x), +z is to the right
DRAWER_ROWS = [0.955, 0.685]             # y centres, top row (A B C) then bottom (D E F)
SHELF_BOARDS = [0.40, 0.88, 1.36, 1.84]
CONCOURSE_Y = -0.20                      # the booth is raised: commuters stand lower, at eye level
LEDGE_Y = 0.80
GLASS_Z = 1.02
GLASS_Y = (0.835, 1.95)


def build_desk(root):
    m = Model("Desk")
    # top with a softly bevelled edge, leather inlay and a brass nosing along the front
    m.add(box((-0.86, 0.72, 0.30), (0.86, 0.76, 0.97), bevel=0.012, segments=3), WALNUT)
    m.add(box((-0.77, 0.758, 0.37), (0.77, 0.7625, 0.92), bevel=0.002, segments=1), LEATHER)
    m.add(box((-0.862, 0.735, 0.292), (0.862, 0.748, 0.302), bevel=0.003), BRASS)
    # pedestals with drawer fronts and brass pulls (decorative, below the eye line)
    for sx in (-1, 1):
        x0, x1 = (0.46, 0.85) if sx > 0 else (-0.85, -0.46)
        m.add(box((x0, 0.0, 0.33), (x1, 0.72, 0.96), bevel=0.008), WALNUT_D)
        for k, (y0, y1) in enumerate(((0.50, 0.70), (0.27, 0.48), (0.04, 0.25))):
            m.add(box((x0 + 0.02, y0, 0.315), (x1 - 0.02, y1, 0.33), bevel=0.006), WALNUT)
            cx = (x0 + x1) / 2
            m.add(cbox((cx, (y0 + y1) / 2 + 0.03, 0.311), (0.07, 0.016, 0.008), bevel=0.003), BRASS)
    m.add(box((-0.46, 0.25, 0.93), (0.46, 0.72, 0.96)), WALNUT_D)
    return m.build(parent=root)


def build_counter(root):
    objs = []
    m = Model("Counter")
    # low ledge under the glass: our side, then the customer side, both at LEDGE_Y
    m.add(box((-1.05, 0.72, 0.96), (1.05, LEDGE_Y, 1.06), bevel=0.008, segments=3), WALNUT)
    m.add(box((-1.05, LEDGE_Y - 0.05, 1.06), (1.05, LEDGE_Y, 1.30), bevel=0.012, segments=3), WALNUT)
    m.add(box((-1.05, CONCOURSE_Y, 1.12), (1.05, LEDGE_Y - 0.05, 1.18)), WALNUT_D)
    for x in (-0.9, -0.3, 0.3, 0.9):  # panelled customer-side front
        m.add(cbox((x, (CONCOURSE_Y + LEDGE_Y) / 2 - 0.03, 1.185), (0.5, 0.7, 0.012), bevel=0.004), WALNUT)
    m.add(box((-1.07, LEDGE_Y - 0.02, 1.27), (1.07, LEDGE_Y + 0.006, 1.32), bevel=0.008, segments=3), BRASS_D)
    m.add(box((-1.05, 0.79, 0.955), (1.05, 0.802, 0.965), bevel=0.002), BRASS)  # brass edge, our side
    # tray dish sunk into the ledge, passing under the glass
    m.add(rrect_slab((0, LEDGE_Y - 0.004, GLASS_Z), 0.38, 0.26, 0.010, 0.04, plane="xz", bevel=0.003), BRASS)
    m.add(rrect_slab((0, LEDGE_Y + 0.0005, GLASS_Z), 0.34, 0.22, 0.003, 0.035, plane="xz"), BRASS_D)
    objs.append(m.build(parent=root))
    empty("TRAY", (0, LEDGE_Y + 0.004, GLASS_Z - 0.03), parent=root)

    w = Model("WindowFrame")
    for sx in (-1, 1):
        xin = sx * 0.74
        xo = sx * 0.82
        w.add(box((min(xin, xo), LEDGE_Y, GLASS_Z - 0.04), (max(xin, xo), 2.02, GLASS_Z + 0.06), bevel=0.012, segments=3), WALNUT)
        w.add(box((xin - 0.008, GLASS_Y[0], GLASS_Z - 0.006), (xin + 0.008, GLASS_Y[1], GLASS_Z + 0.006)), BRASS_D)
    w.add(box((-0.84, 1.95, GLASS_Z - 0.04), (0.84, 2.08, GLASS_Z + 0.06), bevel=0.012, segments=3), WALNUT)
    w.add(box((-0.74, GLASS_Y[1] - 0.012, GLASS_Z - 0.008), (0.74, GLASS_Y[1], GLASS_Z + 0.008)), BRASS_D)
    w.add(box((-0.74, GLASS_Y[0], GLASS_Z - 0.008), (0.74, GLASS_Y[0] + 0.01, GLASS_Z + 0.008)), BRASS_D)
    # speaking grille: brass ring with perforations, set into the glass at mouth height
    gc = V(-0.42, 1.30, GLASS_Z)
    w.add(torus(gc, 0.065, 0.008, axis="z", segments=40), BRASS)
    w.add(cyl(gc, 0.06, 0.004, axis="z", segments=40), BRASS_D)
    for ring, count in ((0.0, 1), (0.02, 6), (0.04, 12)):
        for i in range(count):
            a = 2 * math.pi * i / count
            p = gc + V(math.cos(a) * ring, math.sin(a) * ring, -0.0025)
            w.add(cyl(p, 0.0042, 0.002, axis="z", segments=10), BLACK)
    objs.append(w.build(parent=root))

    g = Model("Glass")
    g.add(box((-0.735, GLASS_Y[0] + 0.01, GLASS_Z - 0.002), (0.735, GLASS_Y[1] - 0.012, GLASS_Z + 0.002)), GLASS)
    objs.append(g.build(parent=root))

    h = Model("Header")
    h.add(box((-1.3, 2.08, 0.98), (1.3, 2.5, 1.10), bevel=0.01), WALNUT_D)
    h.add(box((-0.9, 2.14, 1.10), (0.9, 2.42, 1.115), bevel=0.006), mat("paint", "1F3B33"))
    h.add(box((-0.92, 2.12, 1.105), (0.92, 2.44, 1.11)), BRASS)
    # text_mesh's "xy" faces the booth (-z); spin it 180 degrees so it reads from the concourse
    h.add(transformed(text_mesh("LOST PROPERTY", (0, 2.28, 1.119), 0.13, depth=0.004, plane="xy",
                                font=os.path.join(ROOT, "ArtSource", "fonts", "Limelight-Regular.ttf")),
                      rotate_about((0, 2.28, 1.119), "Y", 180)), mat("gold", "E2C27A"))
    objs.append(h.build(parent=root))
    return objs


def build_walls(root):
    m = Model("Walls")
    # front wall around the window (booth side)
    m.add(box((-1.40, 0.0, 0.98), (-0.82, 2.5, 1.04)), WALL_G)
    m.add(box((0.82, 0.0, 0.98), (1.45, 2.5, 1.04)), WALL_G)
    m.add(box((-1.40, 0.0, 0.975), (-0.82, 1.1, 0.985)), WALNUT_D)
    m.add(box((0.82, 0.0, 0.975), (1.45, 1.1, 0.985)), WALNUT_D)
    # side walls: wood wainscot to 1.15 m with a rail, painted above, panel mouldings
    for side in (-1, 1):
        inner = -1.36 if side < 0 else 1.40
        outer = inner - 0.06 if side < 0 else inner + 0.06
        x0, x1 = min(inner, outer), max(inner, outer)
        m.add(box((x0, 0.0, -1.0), (x1, 1.15, 1.0)), WALNUT_D)
        m.add(box((x0, 1.15, -1.0), (x1, 2.5, 1.0)), WALL)
        rail = (inner, inner + 0.022) if side < 0 else (inner - 0.022, inner)
        m.add(box((rail[0], 1.13, -1.0), (rail[1], 1.17, 1.0)), WALNUT)
        m.add(box((rail[0], 2.05, -1.0), (rail[1], 2.08, 1.0)), WALNUT)
        for zc in (-0.75, -0.25, 0.25, 0.75):
            m.add(cbox((inner - side * 0.006, 0.6, zc), (0.012, 0.8, 0.38), bevel=0.004), WALNUT)
    m.add(box((-1.45, 0.0, -1.05), (1.45, 2.5, -0.99)), WALL)
    m.add(box((-1.45, 2.5, -1.05), (1.45, 2.56, 1.1)), WALL)  # ceiling
    m.add(box((-1.45, -0.02, -1.05), (1.45, 0.0, 1.0)), FLOOR)
    return m.build(parent=root)


def drawer_slot(col, row):
    return V(CAB_FRONT, DRAWER_ROWS[row], DRAWER_COLS[col])


def build_cabinet(root):
    objs = []
    c = Model("Cabinet")
    x0, x1 = -1.34, CAB_FRONT
    z0, z1 = CAB_Z
    y0, y1 = 0.0, 1.11
    t = 0.025
    c.add(box((x0, y1 - t, z0 - 0.015), (x1 + 0.02, y1 + 0.014, z1 + 0.015), bevel=0.008, segments=3), WALNUT)  # top
    c.add(box((x0, 0.0, z0), (x1, 0.08, z1)), WALNUT_D)  # plinth
    c.add(box((x0, 0.08, z0), (x1, 0.54, z0 + t)), WALNUT)
    c.add(box((x0, 0.08, z1 - t), (x1, 0.54, z1)), WALNUT)
    # cupboard doors below the drawers
    for zc in ((z0 + z1) / 2 - 0.22, (z0 + z1) / 2 + 0.22):
        c.add(cbox((x1 + 0.005, 0.31, zc), (0.02, 0.44, 0.43), bevel=0.006, segments=3), WALNUT)
        c.add(cbox((x1 + 0.016, 0.31, zc), (0.006, 0.36, 0.35), bevel=0.003), WALNUT_D)
        c.add(sphere((x1 + 0.03, 0.42, zc + (0.17 if zc < (z0 + z1) / 2 else -0.17)), 0.012, segments=12, rings=8), BRASS)
    c.add(box((x0, 0.54, z0), (x1 + 0.01, 0.56, z1)), WALNUT)
    c.add(box((x0, 0.54, z0), (x1, y1, z0 + t)), WALNUT)
    c.add(box((x0, 0.54, z1 - t), (x1, y1, z1)), WALNUT)
    c.add(box((x0, 0.0, z0), (x0 + t, y1, z1)), WALNUT_D)  # back
    for zc in ((DRAWER_COLS[0] + DRAWER_COLS[1]) / 2, (DRAWER_COLS[1] + DRAWER_COLS[2]) / 2):
        c.add(box((x0, 0.56, zc - 0.008), (x1, y1 - t, zc + 0.008)), WALNUT)
    c.add(box((x0, 0.812, z0), (x1, 0.828, z1)), WALNUT)
    objs.append(c.build(parent=root))

    for i, letter in enumerate("ABCDEF"):
        col, row = i % 3, i // 3
        p = drawer_slot(col, row)
        d = Model(f"Drawer_{letter}")
        w, h, depth = 0.284, 0.245, 0.36
        # front panel (proud of the carcass, facing +x) with a raised field
        d.add(box((p.x - 0.005, p.y - h / 2, p.z - w / 2), (p.x + 0.014, p.y + h / 2, p.z + w / 2), bevel=0.006, segments=3), WALNUT)
        d.add(box((p.x + 0.012, p.y - h / 2 + 0.028, p.z - w / 2 + 0.028), (p.x + 0.018, p.y + h / 2 - 0.028, p.z + w / 2 - 0.028), bevel=0.004), WALNUT_D)
        by = p.y - h / 2 + 0.018
        d.add(box((p.x - depth, by, p.z - w / 2 + 0.01), (p.x - 0.005, by + 0.012, p.z + w / 2 - 0.01)), WALNUT_D)
        d.add(box((p.x - depth + 0.012, by + 0.012, p.z - w / 2 + 0.022), (p.x - 0.01, by + 0.016, p.z + w / 2 - 0.022)), FELT)
        for sz in (-1, 1):
            zz = p.z + sz * (w / 2 - 0.016)
            d.add(box((p.x - depth, by, zz - 0.006), (p.x - 0.005, p.y + h / 2 - 0.05, zz + 0.006)), WALNUT)
        d.add(box((p.x - depth, by, p.z - w / 2 + 0.01), (p.x - depth + 0.012, p.y + h / 2 - 0.05, p.z + w / 2 - 0.01)), WALNUT)
        # brass cup pull and label holder on the front
        hp = V(p.x + 0.02, p.y - 0.04, p.z)
        d.add(cbox(hp + V(0.002, 0.012, 0), (0.008, 0.014, 0.10), bevel=0.003), BRASS)
        d.add(torus(hp + V(0.01, -0.002, 0), 0.036, 0.0055, axis="x", segments=24, arc=0.5, start=0.5), BRASS)
        lp = V(p.x + 0.019, p.y + 0.058, p.z)
        d.add(cbox(lp, (0.004, 0.048, 0.10), bevel=0.0015), BRASS)
        d.add(cbox(lp + V(0.0025, 0, 0), (0.0015, 0.038, 0.088)), PAPER)
        obj = d.build(origin=p, parent=root)
        # anchors live under the root (see laf: children of moved parents export mangled);
        # the game re-parents them onto the drawer
        empty(f"Drawer_{letter}_LABEL", lp + V(0.0045, 0, 0), parent=root)
        empty(f"Drawer_{letter}_FLOOR", V(p.x - depth / 2, by + 0.017, p.z), parent=root)
        objs.append(obj)
    return objs


def build_shelf(root):
    objs = []
    s = Model("Shelf")
    x0, x1 = SHELF_FRONT, 1.38
    z0, z1 = CAB_Z
    for y in SHELF_BOARDS:
        s.add(box((x0 + 0.01, y - 0.025, z0), (x1, y, z1), bevel=0.005), WALNUT)
        s.add(box((x0, y - 0.035, z0), (x0 + 0.016, y + 0.004, z1), bevel=0.003), WALNUT_D)
    for z in (z0, z1 - 0.03):
        s.add(box((x0, 0.0, z), (x1, SHELF_BOARDS[-1] + 0.03, z + 0.03), bevel=0.006), WALNUT_D)
    s.add(box((x1 - 0.02, 0.0, z0), (x1, SHELF_BOARDS[-1], z1)), DARK)
    s.add(box((x0, 0.0, z0), (x1, 0.08, z1)), WALNUT_D)
    objs.append(s.build(parent=root))

    # the Iron Drawer: a riveted strongbox on the middle shelf, right end (behind the player's shoulder)
    b = Model("IronBox")
    bz0, bz1 = -0.60, -0.16
    by0, by1 = SHELF_BOARDS[1], SHELF_BOARDS[1] + 0.30
    bx0, bx1 = SHELF_FRONT + 0.02, 1.36
    b.add(box((bx0, by0, bz0), (bx1, by1, bz1), bevel=0.012, segments=3), IRON)
    b.add(box((bx0 - 0.004, by0 + 0.02, bz0 + 0.02), (bx0 + 0.01, by1 - 0.02, bz1 - 0.02)), mat("iron", "1A1A1C"))
    for yy in (by0 + 0.012, by1 - 0.012):
        for k in range(9):
            zz = bz0 + 0.02 + k * (bz1 - bz0 - 0.04) / 8
            b.add(sphere((bx0 - 0.002, yy, zz), 0.006, segments=10, rings=6), IRON_L)
    # bands wrapping the box
    for zz in (bz0 + 0.06, bz1 - 0.06):
        b.add(box((bx0 - 0.003, by0, zz - 0.015), (bx1, by1 + 0.003, zz + 0.015), bevel=0.002), IRON_L)
    objs.append(b.build(parent=root))

    d = Model("IronDrawer")
    p = V(bx0 - 0.004, (by0 + by1) / 2, (bz0 + bz1) / 2)
    dw, dh = bz1 - bz0 - 0.05, by1 - by0 - 0.05
    d.add(box((p.x - 0.02, p.y - dh / 2, p.z - dw / 2), (p.x, p.y + dh / 2, p.z + dw / 2), bevel=0.006, segments=2), IRON_L)
    for zz in (p.z - dw / 2 + 0.015, p.z + dw / 2 - 0.015):
        for yy in (p.y - dh / 2 + 0.015, p.y + dh / 2 - 0.015):
            d.add(sphere((p.x - 0.0205, yy, zz), 0.007, segments=10, rings=6), IRON)
    d.add(torus(p + V(-0.032, -0.03, 0), 0.03, 0.0065, axis="x", segments=28), IRON)
    d.add(cbox(p + V(-0.024, 0.005, 0), (0.008, 0.016, 0.03), bevel=0.003), IRON)
    d.add(cyl(p + V(-0.023, 0.065, 0), 0.022, 0.006, axis="x", segments=28, bevel=0.002), BRASS)
    d.add(cyl(p + V(-0.0265, 0.069, 0), 0.005, 0.002, axis="x", segments=12), BLACK)
    d.add(cbox(p + V(-0.0265, 0.06, 0), (0.002, 0.012, 0.004)), BLACK)
    depth = 0.36
    d.add(box((p.x, p.y - dh / 2 + 0.01, p.z - dw / 2 + 0.01), (p.x + depth, p.y - dh / 2 + 0.02, p.z + dw / 2 - 0.01)), IRON)
    d.add(box((p.x + 0.01, p.y - dh / 2 + 0.02, p.z - dw / 2 + 0.02), (p.x + depth - 0.01, p.y - dh / 2 + 0.024, p.z + dw / 2 - 0.02)), mat("velvet", "3A1010"))
    for sz in (-1, 1):
        zz = p.z + sz * (dw / 2 - 0.012)
        d.add(box((p.x, p.y - dh / 2 + 0.01, zz - 0.005), (p.x + depth, p.y + dh / 2 - 0.04, zz + 0.005)), IRON)
    d.add(box((p.x + depth - 0.01, p.y - dh / 2 + 0.01, p.z - dw / 2 + 0.01), (p.x + depth, p.y + dh / 2 - 0.04, p.z + dw / 2 - 0.01)), IRON)
    obj = d.build(origin=p, parent=root)
    empty("IronDrawer_FLOOR", V(p.x + depth / 2, p.y - dh / 2 + 0.026, p.z), parent=root)
    empty("IronDrawer_KEYHOLE", p + V(-0.028, 0.065, 0), parent=root)
    objs.append(obj)

    # anchors for large items, in board order (top board first). Facing the shelf (+x), +z is to the left.
    slots = {
        "Shelf_1a": (1.16, SHELF_BOARDS[2], 0.10), "Shelf_1b": (1.16, SHELF_BOARDS[2], -0.18), "Shelf_1c": (1.16, SHELF_BOARDS[2], -0.46),
        "Shelf_2a": (1.16, SHELF_BOARDS[1], 0.12), "Shelf_2b": (1.16, SHELF_BOARDS[1], -0.06),
        "Shelf_3a": (1.16, SHELF_BOARDS[0], 0.10), "Shelf_3b": (1.16, SHELF_BOARDS[0], -0.18), "Shelf_3c": (1.16, SHELF_BOARDS[0], -0.46),
    }
    for name, pos in slots.items():
        empty(name, pos, parent=root)
    return objs


def build_concourse(root):
    """The station beyond the glass. Low detail: it is always seen through the window, out of focus."""
    objs = []
    floor = Model("ConcourseFloor")
    # big stone tiles in two tones
    for i in range(-8, 9):
        for k in range(0, 14):
            x0, z0 = i * 0.9, 1.30 + k * 0.9
            tone = "8A8070" if (i + k) % 2 == 0 else "6F675A"
            floor.add(box((x0 + 0.004, CONCOURSE_Y - 0.04, z0 + 0.004), (x0 + 0.896, CONCOURSE_Y, z0 + 0.896)), mat("ceramic", tone))
    objs.append(floor.build(parent=root))

    hall = Model("Hall")
    # back wall with three tall arched windows glowing with dusk
    hall.add(box((-9, 0, 13.6), (9, 9, 14.0)), mat("paint", "4B5A5C"))
    for xc in (-5.0, 0.0, 5.0):
        hall.add(box((xc - 1.6, 1.2, 13.55), (xc + 1.6, 5.6, 13.6)), mat("emit", "3E6A86"))
        hall.add(cyl((xc, 5.6, 13.575), 1.6, 0.05, axis="z", segments=32), mat("emit", "5B86A0"))
        for k in range(1, 4):
            hall.add(box((xc - 1.6 + k * 0.8 - 0.03, 1.2, 13.5), (xc - 1.6 + k * 0.8 + 0.03, 6.8, 13.55)), mat("iron", "1E2428"))
        for yy in (2.4, 3.6, 4.8):
            hall.add(box((xc - 1.6, yy - 0.03, 13.5), (xc + 1.6, yy + 0.03, 13.55)), mat("iron", "1E2428"))
    # iron columns and roof trusses
    for zc in (4.5, 9.0):
        for xc in (-6.0, -2.4, 2.4, 6.0):
            hall.add(cyl((xc, 3.5, zc), 0.16, 7.0, segments=16), mat("iron", "2A3236"))
            hall.add(cyl((xc, 0.25, zc), 0.26, 0.5, segments=16), mat("iron", "2A3236"))
    for zc in (4.5, 9.0, 13.0):
        hall.add(box((-9, 6.9, zc - 0.1), (9, 7.1, zc + 0.1)), mat("iron", "232A2E"))
    # side walls
    for x in (-9.0, 9.0):
        hall.add(box((x - 0.2, 0, 1.2), (x + 0.2, 9, 14)), mat("paint", "55605D"))
    # benches
    for xc, zc in ((-3.8, 6.2), (3.8, 6.2), (-3.8, 10.8), (3.8, 10.8)):
        hall.add(box((xc - 0.9, 0.42, zc - 0.22), (xc + 0.9, 0.47, zc + 0.22), bevel=0.01), mat("wood", "5A3A22"))
        hall.add(box((xc - 0.9, 0.5, zc + 0.18), (xc + 0.9, 0.85, zc + 0.24), bevel=0.01), mat("wood", "5A3A22"))
        for sx in (-0.75, 0.75):
            hall.add(box((xc + sx - 0.03, 0, zc - 0.2), (xc + sx + 0.03, 0.45, zc + 0.2)), mat("iron", "1E1E1E"))
    objs.append(hall.build(parent=root))

    # the great clock hanging from the truss, and the departures board
    clock = Model("StationClock")
    cc = V(-1.6, 4.2, 6.5)
    clock.add(cyl(cc, 0.75, 0.22, axis="z", segments=48, bevel=0.03), mat("brass", "B08A48"))
    clock.add(cyl(cc + V(0, 0, -0.115), 0.66, 0.02, axis="z", segments=48), mat("emit", "F3E6C4"))
    for i in range(12):
        a = 2 * math.pi * i / 12
        clock.add(cbox(cc + V(math.sin(a) * 0.56, math.cos(a) * 0.56, -0.13), (0.03, 0.09, 0.01)), mat("paint", "1A1A1A"))
    clock.add(rod(cc + V(0, 0.75, 0), cc + V(0, 2.8, 0), 0.025), mat("iron", "202020"))
    objs.append(clock.build(parent=root))
    empty("ClockHands", cc + V(0, 0, -0.14), parent=root)

    board = Model("DepartureBoard")
    bc = V(2.6, 3.4, 8.0)
    board.add(cbox(bc, (3.4, 1.5, 0.2), bevel=0.03), mat("paint", "15191B"))
    board.add(cbox(bc + V(0, 0.95, 0), (3.6, 0.36, 0.22), bevel=0.02), mat("paint", "1F3B33"))
    for r in range(5):
        for k in range(16):
            board.add(cbox(bc + V(-1.5 + k * 0.2, 0.5 - r * 0.25, -0.105), (0.16, 0.18, 0.01)), mat("emit", "C9B27A" if (r + k) % 5 else "E8D9A8"))
    objs.append(board.build(parent=root))

    lamps = Model("ConcourseLamps")
    for xc, zc in ((-4.5, 3.0), (4.5, 3.0), (-4.5, 7.5), (4.5, 7.5), (0.0, 11.5)):
        lamps.add(rod((xc, 0, zc), (xc, 3.2, zc), 0.05, segments=10), mat("iron", "1E1E1E"))
        lamps.add(sphere((xc, 3.35, zc), 0.22, segments=18, rings=10), mat("emit", "FFD9A0"))
    # pendant globes high up
    for xc in (-4.0, 0.0, 4.0):
        for zc in (5.5, 10.0):
            lamps.add(rod((xc, 5.4, zc), (xc, 7.0, zc), 0.015, segments=6), mat("iron", "1E1E1E"))
            lamps.add(sphere((xc, 5.3, zc), 0.3, segments=18, rings=10), mat("emit", "FFE2B0"))
    objs.append(lamps.build(parent=root))

    sign = Model("PlatformSign")
    sc = V(-4.6, 2.6, 12.6)
    sign.add(cbox(sc, (1.2, 0.8, 0.08), bevel=0.02), mat("paint", "6E5AA8"))
    sign.add(text_mesh("9", sc + V(0, 0, -0.045), 0.6, depth=0.02, plane="xy",
                       font=os.path.join(ROOT, "ArtSource", "fonts", "Limelight-Regular.ttf")), mat("emit", "EFE4CC"))
    objs.append(sign.build(parent=root))
    return objs


def build():
    reset_scene()
    root = empty("Booth")
    objs = [root]
    objs.append(build_desk(root))
    objs += build_counter(root)
    objs.append(build_walls(root))
    objs += build_cabinet(root)
    objs += build_shelf(root)
    export_fbx(os.path.join(OUT, "Booth.fbx"), [root])

    croot = empty("Concourse")
    build_concourse(croot)
    export_fbx(os.path.join(OUT, "Concourse.fbx"), [croot])
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "ArtSource", "blend", "booth.blend"))
    return root, croot


def preview(outdir):
    """Player-eye renders for composition checks: counter, cabinet and shelf views."""
    scene = bpy.context.scene
    setup_preview((0, 1.0, 0.6), 0.6)  # creates lights + camera; we re-aim the camera below
    for o in list(scene.objects):
        if o.type == "LIGHT":
            o.data.energy *= 6
    # a warm lamp light over the desk and a cool fill from the concourse
    to_b = lambda p: (UNITY_TO_BLENDER @ V(p).to_4d()).to_3d()
    ld = bpy.data.lights.new("desk", "POINT")
    ld.energy = 60
    ld.color = (1.0, 0.75, 0.45)
    lo = bpy.data.objects.new("desk", ld)
    scene.collection.objects.link(lo)
    lo.location = to_b((-0.5, 1.15, 0.7))
    ld2 = bpy.data.lights.new("hall", "SUN")
    ld2.energy = 1.5
    ld2.color = (0.6, 0.75, 1.0)
    lo2 = bpy.data.objects.new("hall", ld2)
    scene.collection.objects.link(lo2)
    lo2.rotation_euler = (math.radians(60), 0, math.radians(180))
    cam = scene.camera
    cam.data.angle = math.radians(92)
    eye = V(0, 1.26, -0.08)
    views = {"counter": (0, -16), "cabinet": (-100, -24), "shelf": (100, -16)}
    for name, (yaw, pitch) in views.items():
        yr, pr = math.radians(yaw), math.radians(pitch)
        fwd = V(math.sin(yr) * math.cos(pr), math.sin(pr), math.cos(yr) * math.cos(pr))
        cam.location = to_b(eye)
        cam.rotation_euler = (to_b(eye + fwd) - to_b(eye)).to_track_quat("-Z", "Y").to_euler()
        scene.render.resolution_x, scene.render.resolution_y = 960, 540
        render(os.path.join(outdir, f"booth_{name}.png"))


if __name__ == "__main__":
    args = args_after_dashes()
    os.makedirs(os.path.join(ROOT, "ArtSource", "blend"), exist_ok=True)
    build()
    if "--preview" in args:
        preview(args[args.index("--preview") + 1])
