"""The photographs on the desk, and the ending photographs, staged with the real cast.

    blender -b -P ArtSource/render_photos.py [-- --only staff1921,polaroid]
    .venv/bin/python Tools/textures/finish_photos.py      (ages them: sepia, grain, borders)

Each desk photo has a 'before' and an 'after' (the world once the ring has gone home). Scenes are
built with everyone in them, rendered as 'after', then the people who only exist afterwards are
hidden and the scene is rendered again as 'before'. Raw renders go to ArtSource/_renders/photos/.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "lib"))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import build_people as bp  # noqa: E402
from laf import (V, Model, box, cyl, lathe, mat, text_mesh, reset_scene, args_after_dashes, UNITY_TO_BLENDER, sphere, sweep)  # noqa: E402

OUT = os.path.join(HERE, "_renders", "photos")
FONT_SIGN = os.path.join(HERE, "fonts", "Limelight-Regular.ttf")

CAST = dict(bp.CAST)
young_agnes = dict(CAST["agnes_old"])
young_agnes.update(hair="bob", hair_col="5A3A24", coat="3A4A5A", lids=0.1)
young_agnes.pop("glasses", None)
CAST["agnes_young"] = young_agnes
you = dict(build="slim", skin="EEC4A4", hair="short", hair_col="4A3020", nose="button", coat="4A5A4A")
CAST["you"] = you
CAST["you_child"] = dict(build="child", skin="F0C8A8", hair="short", hair_col="6A4A2A", nose="button", coat="C8483A")
mum = dict(CAST["harriet"])
mum.update(coat="E8D8A8", hair_col="4A3020")
CAST["mum"] = mum
agnes_1951 = dict(CAST["agnes_old"])
agnes_1951.update(hair_col="9A8A7A")
CAST["agnes_1951"] = agnes_1951


def to_b(p):
    return (UNITY_TO_BLENDER @ V(p).to_4d()).to_3d()


def person(cid, x, z, yaw=0.0, scale=1.0, lift=0.0):
    """The cast are modelled from the waist up (they stand behind a counter): every scene keeps
    something in front of them below about 1 m."""
    root = bp.build_character(cid + "_" + str(len(bpy.data.objects)), CAST[cid])
    root.location = to_b((x, lift, z))
    root.rotation_euler = (0, 0, math.radians(yaw))
    root.scale = (scale, scale, scale)
    mouth = [o for o in root.children_recursive if o.name.startswith("Mouth")]
    for mo in mouth:
        mo.scale.z = 0.25
    return root


def hide(objs):
    for o in objs:
        for c in [o] + list(o.children_recursive):
            c.hide_render = True


def solid(name, mn, mx, hexcol, surface="paint"):
    m = Model(name)
    m.add(box(mn, mx), mat(surface, hexcol))
    return m.build()


def sign(text, center, size, hexcol="E8D8A8", depth=0.01, board="1E3A30"):
    m = Model("Sign")
    c = V(center)
    if board:
        w = size * 0.62 * len(text) + size
        m.add(box((c.x - w / 2, c.y - size * 0.75, c.z + 0.005), (c.x + w / 2, c.y + size * 0.75, c.z + 0.03)), mat("paint", board))
        c = c - V(0, 0, 0.012)
    m.add(text_mesh(text, c, size, depth=depth, plane="xy", font=FONT_SIGN), mat("paint", hexcol))
    return m.build()


def camera(target, eye, fov=38.0, w=800, h=1000):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x, scene.render.resolution_y = w, h
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = -0.6
    cd = bpy.data.cameras.new("cam")
    cd.lens_unit = "FOV"
    cd.angle = math.radians(fov)
    cam = bpy.data.objects.new("cam", cd)
    scene.collection.objects.link(cam)
    cam.location = to_b(eye)
    cam.rotation_euler = (to_b(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    world = scene.world or bpy.data.worlds.new("w")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.35, 0.33, 0.3, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.5
    return cam


def light(kind, energy, pos, target, size=2.0, color=(1, 0.95, 0.88)):
    ld = bpy.data.lights.new("l", kind)
    ld.energy = energy
    ld.color = color
    if hasattr(ld, "size"):
        ld.size = size
    lo = bpy.data.objects.new("l", ld)
    bpy.context.scene.collection.objects.link(lo)
    lo.location = to_b(pos)
    lo.rotation_euler = (to_b(target) - lo.location).to_track_quat("-Z", "Y").to_euler()


def studio(target):
    light("AREA", 600, (-2.0, 3.0, -3.0), target, 3.0)
    light("AREA", 220, (2.5, 1.8, -2.5), target, 4.0, (0.85, 0.9, 1.0))
    light("AREA", 380, (0.5, 3.0, 2.5), target, 3.0)


def render(name):
    bpy.context.scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("rendered", name)


def station_back(z=1.2, tiles="D8CCB0", trim="3A5A4A"):
    solid("Floor", (-6, -0.02, -4), (6, 0.0, 6), "8A8070")
    solid("Wall", (-6, 0, z), (6, 4, z + 0.1), tiles)
    solid("Dado", (-6, 0, z - 0.01), (6, 1.0, z), trim)


# ----------------------------------------------------------------------------- scenes

def staff1921():
    """Lost Property, 1921: Agnes on her first day behind this desk (after: Thomas beside her)."""
    station_back(0.9, "C8B898", "4A3A2A")
    solid("Counter", (-1.0, 0.0, -0.25), (1.0, 1.0, 0.25), "5A3A24", "wood")
    solid("CounterTop", (-1.05, 1.0, -0.3), (1.05, 1.04, 0.3), "3A2418", "wood")
    sign("LOST PROPERTY", (0, 2.05, 0.85), 0.2)
    a = person("agnes_young", -0.25 if True else 0, 0.45)
    t = person("thomas", 0.32, 0.5, yaw=-8)
    tgt = (0.0, 1.45, 0.4)
    camera(tgt, (0.0, 1.5, -3.2), fov=30, w=660, h=900)
    studio(tgt)
    render("staff1921_after")
    hide([t])
    a.location = to_b((0.0, 0.0, 0.45))
    render("staff1921_before")


def retirement():
    """Agnes's retirement party (after: with Thomas, a golden anniversary)."""
    station_back(1.6, "D8C8A0", "6A2A2A")
    solid("Table", (-0.9, 0.0, -0.4), (0.9, 1.0, 0.2), "E8E0D0", "cloth")
    m = Model("Cake")
    m.add(cyl((0, 1.08, -0.12), 0.18, 0.16, segments=40), mat("paint", "F4EEE0"))
    m.add(cyl((0, 1.17, -0.12), 0.12, 0.02, segments=40), mat("paint", "E8B8C0"))
    for k in range(6):
        a = k * math.pi / 3
        m.add(cyl((math.cos(a) * 0.08, 1.22, -0.12 + math.sin(a) * 0.08), 0.006, 0.08, segments=8), mat("paint", "E8E0A0"))
    m.add(text_mesh("41", (0, 1.08, -0.305), 0.09, depth=0.01, plane="xy", font=FONT_SIGN), mat("paint", "C83A3A"))
    m.build()
    sign("41 YEARS", (0, 1.95, 1.55), 0.2, "E8C060", board="6A2A2A")
    a = person("agnes_old", -0.28, 0.45)
    t = person("thomas_old", 0.32, 0.5, yaw=-10)
    tgt = (0.0, 1.3, 0.2)
    camera(tgt, (0.0, 1.55, -3.0), fov=36, w=1000, h=720)
    studio(tgt)
    render("retirement_after")
    hide([t])
    a.location = to_b((0.0, 0.0, 0.45))
    render("retirement_before")


def mum():
    """Mum and me at the seaside, 1951 (after: with Grandma Agnes and Grandpa Tom)."""
    solid("Sand", (-6, -0.02, -4), (6, 0.0, 8), "E0D0A8")
    solid("Sea", (-8, 0.0, 3.0), (8, 0.02, 12), "6A8AA0", "glass")
    solid("Sky", (-10, 0, 12), (10, 8, 12.1), "B8C8D0")
    for k in range(12):
        solid("Windbreak", (-1.5 + k * 0.25, 0.0, -0.32), (-1.25 + k * 0.25, 0.98, -0.3), "C83A3A" if k % 2 else "F0E8D8", "cloth")
    for x in (-1.5, -0.5, 0.5, 1.5):
        solid("Pole", (x - 0.015, 0.0, -0.34), (x + 0.015, 1.05, -0.29), "8A6A4A", "wood")
    m = person("mum", -0.18, 0.35)
    c = person("you_child", 0.22, 0.12, yaw=-6, scale=0.78, lift=0.12)
    g1 = person("agnes_1951", -0.6, 0.75, yaw=10)
    g2 = person("thomas_old", 0.64, 0.8, yaw=-12)
    tgt = (0.0, 1.3, 0.3)
    camera(tgt, (0.0, 1.4, -3.6), fov=33, w=660, h=880)
    light("SUN", 4.0, (-2, 5, -3), tgt)
    light("AREA", 300, (2.5, 1.8, -2.5), tgt, 4.0, (0.85, 0.9, 1.0))
    render("mum_after")
    hide([g1, g2])
    render("mum_before")


def platform9():
    """The opening of Platform 9, 1921 (after: Thomas waving at the front)."""
    station_back(2.4, "C0B090", "3A4A3A")
    solid("Barrier", (-3, 0.0, -0.05), (3, 1.0, 0.0), "5A3A24", "wood")
    solid("Rail", (-3, 1.0, -0.07), (3, 1.04, 0.02), "C8A040", "brass")
    solid("Banner", (-1.7, 2.2, 2.25), (1.7, 2.85, 2.28), "8A2A2A", "cloth")
    sign("PLATFORM 9  ·  OPENED 1921", (0, 2.52, 2.24), 0.17, "F0E0B0", board=None)
    crowd = []
    rows = (("gus", -1.25, 1.0, 0.1), ("walter", -0.62, 1.15, 0.1), ("odile", 0.55, 1.05, 0.1), ("dora", 1.2, 1.0, 0.1),
            ("bramble", -0.95, 1.75, 0.34), ("crane", 0.15, 1.85, 0.36), ("clementine", 0.9, 1.75, 0.34))
    for i, (cid, x, z, lift) in enumerate(rows):
        crowd.append(person(cid, x, z, yaw=-x * 9, lift=lift))
    t = person("thomas", -0.05, 0.45)
    tgt = (0.0, 1.75, 1.2)
    camera(tgt, (0.0, 1.7, -3.2), fov=40, w=700, h=900)
    studio(tgt)
    render("platform9_after")
    hide([t])
    render("platform9_before")


def desk_front():
    solid("Booth", (-1.2, 0.0, 0.35), (1.2, 2.6, 0.45), "3A2A1E", "wood")
    solid("Counter", (-1.0, 0.0, -0.3), (1.0, 0.95, 0.2), "4A3020", "wood")
    solid("Glass", (-0.9, 0.95, -0.32), (0.9, 0.98, -0.3), "D8E0E0", "glass")
    sign("LOST PROPERTY", (0, 2.15, 0.33), 0.16)


def polaroid():
    """You, at this desk (after: old Agnes and Thomas standing behind you)."""
    desk_front()
    y = person("you", 0.0, 0.0)
    a = person("agnes_old", -0.35, 0.28, yaw=8)
    t = person("thomas_old", 0.38, 0.3, yaw=-8)
    tgt = (0.0, 1.35, 0.1)
    camera(tgt, (0.0, 1.45, -2.6), fov=34, w=800, h=800)
    studio(tgt)
    render("polaroid_after")
    hide([a, t])
    render("polaroid_before")


def ending_nine40():
    station_back(3.0, "C0B090", "3A4A3A")
    sign("PLATFORM 9", (0, 2.9, 2.9), 0.32)
    solid("Barrier", (-4, 0.0, 0.1), (4, 1.0, 0.15), "5A3A24", "wood")
    solid("Rail", (-4, 1.0, 0.08), (4, 1.04, 0.17), "C8A040", "brass")
    back = (("walter", -1.6, 2.0), ("odile", -1.0, 2.1), ("bramble", -0.4, 2.2), ("pip", 0.15, 1.9), ("lou", 0.7, 2.1), ("edie", 1.25, 2.0),
            ("sid", 1.8, 2.2), ("clementine", -2.1, 1.9), ("penhallow", 2.3, 2.0), ("harriet", -1.3, 1.4), ("corporal", -0.8, 1.5),
            ("cecily", 1.0, 1.4), ("gus", 1.6, 1.3), ("lark", 2.1, 1.4), ("dora", -1.9, 1.3))
    for i, (cid, x, z) in enumerate(back):
        person(cid, x, z, yaw=(-x) * 6, lift=0.42 if z > 1.7 else 0.24)
    person("you", 0.0, 0.95, lift=0.12)
    person("agnes_old", -0.35, 0.5, yaw=6)
    person("thomas_old", 0.35, 0.55, yaw=-6)
    tgt = (0.0, 1.62, 1.3)
    camera(tgt, (0.0, 1.7, -3.4), fov=38, w=840, h=890)
    studio(tgt)
    render("ending_nine40")


def ending_longwait():
    station_back(2.2, "C0B090", "3A4A3A")
    solid("Edge", (-6, 0.0, -1.2), (6, 0.03, -0.9), "E0D8C0")
    sign("PLATFORM 9", (0.75, 2.05, 2.1), 0.2)
    m = Model("Bench")
    m.add(box((-0.8, 0.42, 0.0), (0.8, 0.47, 0.4)), mat("wood", "5A3A24"))
    for k in range(4):
        m.add(box((-0.8, 0.55 + k * 0.13, 0.36), (0.8, 0.64 + k * 0.13, 0.4)), mat("wood", "6A4A2A"))
    m.add(box((-0.82, 0.0, 0.3), (-0.74, 1.04, 0.42)), mat("iron", "2A2A2A"))
    m.add(box((0.74, 0.0, 0.3), (0.82, 1.04, 0.42)), mat("iron", "2A2A2A"))
    m.build()
    person("agnes_old", -0.2, 0.6, yaw=12)
    tgt = (-0.1, 1.3, 0.6)
    camera(tgt, (0.5, 1.45, -2.6), fov=34, w=840, h=890)
    studio(tgt)
    render("ending_longwait")


def ending_grey():
    desk_front()
    person("vell", 0.0, 0.05)
    tgt = (0.0, 1.45, 0.1)
    camera(tgt, (0.0, 1.5, -2.6), fov=34, w=840, h=890)
    studio(tgt)
    render("ending_grey")


def dachshund(x=0.0, z=0.0, yaw=0.0):
    """Biscuit: a smooth red dachshund, sculpted as one signed-distance surface."""
    import numpy as np
    import sdf
    P = lambda *a: np.array(a, dtype=float)  # noqa: E731
    body = sdf.capsule(P(-0.17, 0.2, 0), P(0.13, 0.21, 0), 0.082, 0.088)
    chest = sdf.ellipsoid(P(0.15, 0.17, 0), (0.1, 0.1, 0.08))
    belly = sdf.ellipsoid(P(-0.01, 0.15, 0), (0.17, 0.06, 0.068))
    haunch = sdf.ellipsoid(P(-0.16, 0.17, 0), (0.08, 0.08, 0.075))
    neck = sdf.capsule(P(0.18, 0.23, 0), P(0.25, 0.32, 0), 0.06, 0.048)
    skull = sdf.ellipsoid(P(0.285, 0.355, 0), (0.068, 0.058, 0.054))
    stop = sdf.ellipsoid(P(0.33, 0.36, 0), (0.04, 0.035, 0.04))
    muzzle = sdf.capsule(P(0.33, 0.345, 0), P(0.415, 0.33, 0), 0.034, 0.022)
    jaw = sdf.capsule(P(0.31, 0.315, 0), P(0.39, 0.31, 0), 0.028, 0.016)
    legs = [sdf.capsule(P(lx, 0.15, lz), P(lx + 0.012, 0.03, lz), 0.03, 0.024) for lx in (0.14, -0.15) for lz in (-0.048, 0.048)]
    paws = [sdf.ellipsoid(P(lx + 0.028, 0.019, lz), (0.038, 0.019, 0.026)) for lx in (0.14, -0.15) for lz in (-0.048, 0.048)]
    tail = sdf.chain([P(-0.24, 0.22, 0), P(-0.31, 0.25, 0), P(-0.37, 0.3, 0)], [0.022, 0.015, 0.007])
    ears = [sdf.ellipsoid(P(0.262, 0.305, sz * 0.058), (0.036, 0.068, 0.011), rot=(sz * 8.0, 0.0, -12.0)) for sz in (-1, 1)]
    dog = sdf.union(body, chest, belly, haunch, neck, skull, stop, muzzle, jaw, *legs, *paws, tail, k=0.028)
    dog = sdf.union(dog, *ears, k=0.006)
    ear_region = sdf.union(*[sdf.ellipsoid(P(0.262, 0.3, sz * 0.06), (0.04, 0.075, 0.02), rot=(sz * 8.0, 0.0, -12.0)) for sz in (-1, 1)])
    m = Model("Biscuit")
    coat, ear = sdf.mesh(dog, (-0.42, 0.0, -0.12), (0.46, 0.43, 0.12), 0.0035, smooth=2, tris=30000, regions=[ear_region])
    m.add(coat, mat("fur", "7A3A18"))
    m.add(ear, mat("fur", "5A2810"))
    m.add(sphere((0.434, 0.333, 0), 0.016, scale=(0.8, 0.85, 1.1)), mat("eye", "141010"))
    for sz in (-1, 1):
        m.add(sphere((0.343, 0.372, sz * 0.036), 0.011), mat("eye", "100A08"))
        m.add(sphere((0.351, 0.376, sz * 0.04), 0.0028), mat("emit", "FFFFFF"))
    # a red collar round the neck, with a brass tag
    c = V(0.205, 0.265, 0.0)
    pts = [(c.x + math.sin(a) * 0.012, c.y + math.cos(a) * 0.058, math.sin(a) * 0.058 * 1.05) for a in [i * math.pi / 12 for i in range(25)]]
    m.add(sweep(pts, 0.008, segments=8, flat=0.5), mat("leather", "9A2020"))
    m.add(cyl((0.215, 0.2, 0.0), 0.012, 0.003, axis="x", segments=16), mat("brass", "C9A15A"))
    obj = m.build()
    obj.location = to_b((x, 0.0, z))
    obj.rotation_euler = (0, 0, math.radians(yaw))
    return obj


def dog():
    """Walter's photo of Biscuit, on the back step of the bakery."""
    solid("Ground", (-4, -0.02, -4), (4, 0.0, 6), "6A6A4A", "cloth")
    solid("Step", (-1.2, 0.0, 0.25), (1.2, 0.12, 1.2), "8A8070")
    solid("Wall", (-3, 0, 0.9), (3, 3, 1.0), "B8A890")
    solid("Door", (0.35, 0.12, 0.88), (1.15, 2.2, 0.9), "4A3A2A", "wood")
    for k in range(6):
        solid("Brick", (-3, 0.35 + k * 0.32, 0.885), (3, 0.37 + k * 0.32, 0.89), "9A8A74")
    dachshund(-0.02, 0.55, yaw=-40)
    for o in bpy.context.scene.objects:
        if o.name.startswith("Biscuit"):
            o.location.z += 0.12 * 1.0   # sat on the step (Blender z is Unity y)
    tgt = (0.05, 0.38, 0.55)
    camera(tgt, (0.15, 0.62, -0.9), fov=34, w=800, h=600)
    light("SUN", 3.5, (-2, 4, -2), tgt)
    light("AREA", 120, (1.5, 1.2, -1.0), tgt, 2.0, (0.85, 0.9, 1.0))
    render("dog")


SCENES = {f.__name__: f for f in (staff1921, retirement, mum, platform9, polaroid, ending_nine40, ending_longwait, ending_grey, dog)}


def main():
    args = args_after_dashes()
    only = args[args.index("--only") + 1].split(",") if "--only" in args else None
    os.makedirs(OUT, exist_ok=True)
    for name, fn in SCENES.items():
        if only and name not in only:
            continue
        reset_scene()
        fn()


if __name__ == "__main__":
    main()
