"""Shared Blender modeling helpers for Lost & Found.

Everything is authored in *Unity* coordinates (x right, y up, z forward) in metres and converted to
Blender space only when an object is created, so numbers in the build scripts match what you see in
Unity. `export_fbx` maps Blender (x, y, z) -> Unity (-x, z, -y) and `UNITY_TO_BLENDER` is the inverse.

Materials are named `<surface>_<RRGGBB>` (for example `wood_6B4329`, `brass_C9A15A`). Unity's
MaterialLibrary turns each name into a shared URP Lit material with the matching tiling texture set
and the colour as a tint. Surfaces: wood, darkwood, leather, brass, iron, steel, silver, gold, paper,
card, cloth, felt, knit, velvet, glass, paint, ceramic, rubber, skin, hair, eye, emit, fur, plastic.

Every primitive gets box-projected UVs in Unity space (1 UV unit = UV_METRES), so textures keep a
consistent physical scale across parts and models.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

UNITY_TO_BLENDER = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
UV_METRES = 0.25

AXIS_ROT = {
    "x": Matrix.Rotation(math.pi / 2, 4, "Y"),   # local +z -> +x
    "y": Matrix.Rotation(-math.pi / 2, 4, "X"),  # local +z -> +y
    "z": Matrix.Identity(4),
}


def V(*a):
    if len(a) == 1:
        return Vector(a[0])
    return Vector(a)


def args_after_dashes():
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


# ----------------------------------------------------------------------------- materials

SURFACE_PBR = {
    # surface: (metallic, roughness, extra)
    "wood": (0.0, 0.55), "darkwood": (0.0, 0.5), "leather": (0.0, 0.6), "brass": (1.0, 0.32),
    "iron": (0.85, 0.55), "steel": (1.0, 0.3), "silver": (1.0, 0.22), "gold": (1.0, 0.2),
    "paper": (0.0, 0.85), "card": (0.0, 0.8), "cloth": (0.0, 0.9), "felt": (0.0, 0.95),
    "knit": (0.0, 0.95), "velvet": (0.0, 0.85), "glass": (0.0, 0.05), "paint": (0.0, 0.45),
    "ceramic": (0.0, 0.2), "rubber": (0.0, 0.8), "skin": (0.0, 0.6), "hair": (0.0, 0.7),
    "eye": (0.0, 0.1), "emit": (0.0, 0.5), "fur": (0.0, 1.0), "plastic": (0.0, 0.35),
    "frost": (0.0, 0.3),
}


def mat(surface, hexstr):
    return f"{surface}_{hexstr.lstrip('#').upper()}"


def shade(hexstr, factor):
    """Lighten (factor > 1) or darken (factor < 1) a hex colour."""
    h = hexstr.lstrip("#")
    rgb = [int(h[i:i + 2], 16) for i in (0, 2, 4)]
    if factor >= 1:
        rgb = [int(c + (255 - c) * (factor - 1)) for c in rgb]
    else:
        rgb = [int(c * factor) for c in rgb]
    return "".join(f"{max(0, min(255, c)):02X}" for c in rgb)


def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


ITEM_TEX = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                        "Assets", "Game", "Resources", "Textures", "Items")


def tex(name):
    """Material that shows a generated item texture (photo, ticket, label) via its UVs."""
    return "tex_" + name


def get_material(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    if name.startswith("tex_"):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        path = os.path.join(ITEM_TEX, name[4:] + ".png")
        if bsdf and os.path.exists(path):
            node = m.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = bpy.data.images.load(path, check_existing=True)
            m.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
            bsdf.inputs["Roughness"].default_value = 0.6
        return m
    m = bpy.data.materials.new(name)
    surface, hexstr = name.rsplit("_", 1)
    rgb = [_srgb_to_linear(int(hexstr[i:i + 2], 16) / 255) for i in (0, 2, 4)]
    metallic, rough = SURFACE_PBR.get(surface, (0.0, 0.5))
    m.diffuse_color = (*rgb, 1.0)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = rough
        if surface == "emit":
            bsdf.inputs["Emission Color"].default_value = (*rgb, 1.0)
            bsdf.inputs["Emission Strength"].default_value = 4.0
        if surface == "glass":
            bsdf.inputs["Transmission Weight"].default_value = 0.9
            m.blend_method = "BLEND" if hasattr(m, "blend_method") else None
        if surface in ("cloth", "felt", "knit", "velvet", "fur"):
            bsdf.inputs["Sheen Weight"].default_value = 0.5
    return m


# ----------------------------------------------------------------------------- UVs

def box_uv(bm, scale=1.0 / UV_METRES, offset=(0.0, 0.0)):
    """Box-project UVs from (Unity-space) vertex positions: each face uses the plane of its
    dominant normal axis."""
    uv = bm.loops.layers.uv.verify()
    ox, oy = offset
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in f.loops:
            p = loop.vert.co
            if ax == 0:
                u, v = p.z * (1 if n.x > 0 else -1), p.y
            elif ax == 1:
                u, v = p.x, p.z * (1 if n.y > 0 else -1)
            else:
                u, v = p.x * (-1 if n.z > 0 else 1), p.y
            loop[uv].uv = (u * scale + ox, v * scale + oy)


def planar_uv(bm, origin, u_axis, v_axis, size_u, size_v):
    """Map faces onto [0,1] over a rectangle (for labels, photos, faces of cards)."""
    uv = bm.loops.layers.uv.verify()
    o, ua, va = V(origin), V(u_axis).normalized(), V(v_axis).normalized()
    for f in bm.faces:
        for loop in f.loops:
            d = loop.vert.co - o
            loop[uv].uv = (d.dot(ua) / size_u + 0.5, d.dot(va) / size_v + 0.5)


# ----------------------------------------------------------------------------- primitives
# Each returns (bmesh in Unity space, smooth flag). Model.add() merges it.

def _mark_caps_flat(bm, axis_vec):
    for f in bm.faces:
        f.smooth = abs(f.normal.normalized().dot(axis_vec)) < 0.9


def box(mn, mx, bevel=0.0, segments=2):
    mn, mx = V(mn), V(mx)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    size = mx - mn
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    bmesh.ops.translate(bm, vec=(mn + mx) / 2, verts=bm.verts)
    if bevel > 0:
        b = min(bevel, min(size) * 0.49)
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=b, offset_type="OFFSET", segments=segments,
                        profile=0.5, affect="EDGES", clamp_overlap=True)
    bm.normal_update()
    return bm, ("auto" if bevel > 0 and segments >= 2 else False)


def cbox(center, size, bevel=0.0, segments=2):
    c, s = V(center), V(size) / 2
    return box(c - s, c + s, bevel, segments)


def cyl(center, radius, length, axis="y", radius2=None, segments=24, bevel=0.0, bevel_segments=2):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments,
                          radius1=radius, radius2=radius if radius2 is None else radius2, depth=length)
    if bevel > 0:
        bm.normal_update()
        rim = [e for e in bm.edges if len(e.link_faces) == 2 and
               (abs(e.link_faces[0].normal.z) > 0.9) != (abs(e.link_faces[1].normal.z) > 0.9)]
        bmesh.ops.bevel(bm, geom=rim, offset=bevel, offset_type="OFFSET", segments=bevel_segments,
                        profile=0.5, affect="EDGES", clamp_overlap=True)
    bm.transform(Matrix.Translation(V(center)) @ AXIS_ROT[axis])
    bm.normal_update()
    _mark_caps_flat(bm, {"x": V(1, 0, 0), "y": V(0, 1, 0), "z": V(0, 0, 1)}[axis])
    return bm, None


def rod(p0, p1, radius, segments=12, radius2=None):
    p0, p1 = V(p0), V(p1)
    d = p1 - p0
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=radius,
                          radius2=radius if radius2 is None else radius2, depth=d.length)
    rot = V(0, 0, 1).rotation_difference(d.normalized()).to_matrix().to_4x4()
    bm.transform(Matrix.Translation((p0 + p1) / 2) @ rot)
    bm.normal_update()
    _mark_caps_flat(bm, d.normalized())
    return bm, None


def sphere(center, radius, scale=(1, 1, 1), segments=24, rings=14):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=radius)
    bm.transform(Matrix.Translation(V(center)) @ Matrix.Diagonal(V(*scale, 1.0)))
    bm.normal_update()
    return bm, True


def ellipsoid(center, radii, segments=24, rings=14):
    return sphere(center, 1.0, radii, segments, rings)


def torus(center, major, minor, axis="y", segments=32, ring_segments=12, arc=1.0, scale=(1, 1, 1), start=0.0):
    """Torus in the plane perpendicular to `axis`. `arc` < 1 gives an open arc starting at `start` (turns)."""
    bm = bmesh.new()
    rings = []
    count = segments if arc >= 1.0 else segments + 1
    for i in range(count):
        a = 2 * math.pi * (start + arc * i / segments)
        ring = []
        for j in range(ring_segments):
            b = 2 * math.pi * j / ring_segments
            r = major + minor * math.cos(b)
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), minor * math.sin(b))))
        rings.append(ring)
    for i in range(len(rings) - (0 if arc >= 1.0 else 1)):
        r0, r1 = rings[i], rings[(i + 1) % len(rings)]
        for j in range(ring_segments):
            bm.faces.new((r0[j], r1[j], r1[(j + 1) % ring_segments], r0[(j + 1) % ring_segments]))
    if arc < 1.0:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bm.transform(Matrix.Translation(V(center)) @ AXIS_ROT[axis] @ Matrix.Diagonal(V(*scale, 1.0)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm, True


def lathe(profile, center, axis="y", segments=28, smooth=True, cap_bottom=True, cap_top=True):
    """Revolve [(radius, height), ...] (bottom to top) around `axis`."""
    bm = bmesh.new()
    rings = []
    for r, h in profile:
        ring = []
        for i in range(segments):
            a = 2 * math.pi * i / segments
            ring.append(bm.verts.new((max(r, 1e-4) * math.cos(a), max(r, 1e-4) * math.sin(a), h)))
        rings.append(ring)
    for k in range(len(rings) - 1):
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
    if cap_bottom:
        bm.faces.new(list(reversed(rings[0])))
    if cap_top:
        bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bm.transform(Matrix.Translation(V(center)) @ AXIS_ROT[axis])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm, ("auto" if smooth else False)


def sweep(points, radius, closed=False, segments=12, radius_end=None, caps=True, flat=1.0):
    """Tube along a polyline. `flat` < 1 squashes the cross-section (ribbons, straps)."""
    pts = [V(p) for p in points]
    n = len(pts)
    tangents = []
    for i in range(n):
        if closed:
            t = (pts[(i + 1) % n] - pts[i - 1])
        elif i == 0:
            t = pts[1] - pts[0]
        elif i == n - 1:
            t = pts[-1] - pts[-2]
        else:
            t = (pts[i + 1] - pts[i]).normalized() + (pts[i] - pts[i - 1]).normalized()
        tangents.append(t.normalized())
    ref = V(0, 1, 0) if abs(tangents[0].y) < 0.9 else V(1, 0, 0)
    normal = (ref - tangents[0] * ref.dot(tangents[0])).normalized()
    bm = bmesh.new()
    rings = []
    for i in range(n):
        if i > 0:
            rot = tangents[i - 1].rotation_difference(tangents[i])
            normal = (rot @ normal).normalized()
        binormal = tangents[i].cross(normal)
        r = radius if radius_end is None else radius + (radius_end - radius) * i / max(1, n - 1)
        ring = []
        for k in range(segments):
            a = 2 * math.pi * k / segments
            ring.append(bm.verts.new(pts[i] + (normal * math.cos(a) * flat + binormal * math.sin(a)) * r))
        rings.append(ring)
    count = n if closed else n - 1
    for i in range(count):
        r0, r1 = rings[i], rings[(i + 1) % n]
        for k in range(segments):
            bm.faces.new((r0[k], r0[(k + 1) % segments], r1[(k + 1) % segments], r1[k]))
    if not closed and caps:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm, True


def arc_points(center, radius, start_deg, end_deg, plane="xy", steps=12):
    c = V(center)
    out = []
    for i in range(steps + 1):
        a = math.radians(start_deg + (end_deg - start_deg) * i / steps)
        u, v = math.cos(a) * radius, math.sin(a) * radius
        d = {"xy": V(u, v, 0), "xz": V(u, 0, v), "zy": V(0, v, u)}[plane]
        out.append(c + d)
    return out


def rrect_outline(w, h, r, steps=6):
    """Rounded rectangle outline (2D, centred) as [(x, y), ...] counter-clockwise."""
    r = max(1e-4, min(r, w * 0.499, h * 0.499))
    pts = []
    for cx, cy, a0 in ((w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90),
                       (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)):
        for i in range(steps + 1):
            a = math.radians(a0 + 90 * i / steps)
            pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def slab(outline, z0, z1, plane="xy", bevel=0.0, bevel_segments=2):
    """Extrude a (possibly concave) 2D outline between z0 and z1. plane 'xy' extrudes along z,
    'xz' along y (outline is x,z), 'zy' along x (outline is z,y)."""
    bm = bmesh.new()

    def P(a, b, d):
        if plane == "xy":
            return (a, b, d)
        if plane == "xz":
            return (a, d, b)
        return (d, b, a)

    bottom = [bm.verts.new(P(a, b, z0)) for a, b in outline]
    top = [bm.verts.new(P(a, b, z1)) for a, b in outline]
    n = len(outline)
    bm.faces.new(list(reversed(bottom)))
    bm.faces.new(top)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # triangulate big n-gons so concave outlines shade correctly
    big = [f for f in bm.faces if len(f.verts) > 4]
    if big:
        bmesh.ops.triangulate(bm, faces=big, quad_method="BEAUTY", ngon_method="EAR_CLIP")
    if bevel > 0:
        cap_edges = [e for e in bm.edges if len(e.link_faces) == 2 and
                     abs(e.link_faces[0].normal.dot(e.link_faces[1].normal)) < 0.5]
        bmesh.ops.bevel(bm, geom=cap_edges, offset=bevel, offset_type="OFFSET", segments=bevel_segments,
                        profile=0.5, affect="EDGES", clamp_overlap=True)
    bm.normal_update()
    return bm, "auto"


def rrect_slab(center, w, h, depth, r, plane="xy", bevel=0.0, steps=6):
    """Rounded-rectangle plate. For plane 'xz' the plate lies flat (w along x, h along z, depth along y)."""
    c = V(center)
    out = rrect_outline(w, h, r, steps)
    if plane == "xy":
        bm, s = slab([(x + c.x, y + c.y) for x, y in out], c.z - depth / 2, c.z + depth / 2, "xy", bevel)
    elif plane == "xz":
        bm, s = slab([(x + c.x, y + c.z) for x, y in out], c.y - depth / 2, c.y + depth / 2, "xz", bevel)
    else:
        bm, s = slab([(x + c.z, y + c.y) for x, y in out], c.x - depth / 2, c.x + depth / 2, "zy", bevel)
    return bm, s


def loft(sections, cap=True):
    """Skin equal-length closed outlines (lists of 3D points) into a solid."""
    bm = bmesh.new()
    rings = [[bm.verts.new(V(p)) for p in sec] for sec in sections]
    n = len(rings[0])
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(n):
            bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    if cap:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm, True


def hull(points, bevel=0.0, segments=2):
    bm = bmesh.new()
    for p in points:
        bm.verts.new(V(p))
    res = bmesh.ops.convex_hull(bm, input=bm.verts)
    leftovers = list({g for g in res["geom_interior"] + res["geom_unused"] if isinstance(g, bmesh.types.BMVert)})
    if leftovers:
        bmesh.ops.delete(bm, geom=leftovers, context="VERTS")
    bmesh.ops.dissolve_limit(bm, angle_limit=0.01, verts=bm.verts, edges=bm.edges)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type="OFFSET", segments=segments,
                        profile=0.5, affect="EDGES", clamp_overlap=True)
    bm.normal_update()
    return bm, ("auto" if bevel > 0 else False)


def grid_quad(center, w, h, plane="xz", flip=False, nx=1, ny=1):
    """Single (subdivided) quad. plane 'xz' faces +y, 'xy' faces -z (towards the player), 'zy' faces +x."""
    c = V(center)
    bm = bmesh.new()
    verts = []
    for j in range(ny + 1):
        row = []
        for i in range(nx + 1):
            a = (i / nx - 0.5) * w
            b = (j / ny - 0.5) * h
            if plane == "xz":
                p = c + V(a, 0, b)
            elif plane == "xy":
                p = c + V(-a, b, 0)
            else:
                p = c + V(0, b, a)
            row.append(bm.verts.new(p))
        verts.append(row)
    for j in range(ny):
        for i in range(nx):
            q = (verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i])
            bm.faces.new(tuple(reversed(q)) if flip else q)
    bm.normal_update()
    return bm, False


def text_mesh(text, center, size, depth=0.0006, plane="xz", font=None, align="CENTER", rot=0.0):
    """Real geometry text (engravings, stamped letters). plane 'xz' lies flat facing +y,
    'xy' faces -z (towards the player/camera), 'zy' faces +x."""
    curve = bpy.data.curves.new("_txt", "FONT")
    curve.body = text
    curve.size = size
    curve.extrude = depth / 2
    curve.align_x = align
    curve.align_y = "CENTER"
    if font:
        curve.font = bpy.data.fonts.load(font, check_existing=True)
    obj = bpy.data.objects.new("_txt", curve)
    bpy.context.scene.collection.objects.link(obj)
    deps = bpy.context.evaluated_depsgraph_get()
    me = obj.evaluated_get(deps).to_mesh()
    bm = bmesh.new()
    bm.from_mesh(me)
    obj.evaluated_get(deps).to_mesh_clear()
    bpy.data.objects.remove(obj)
    bpy.data.curves.remove(curve)
    # Blender text lies in its XY plane facing +Z (reading along +X). Map it onto a Unity plane:
    #   xz: flat, facing +y, read from above with +z "up the page"   (x, y, z) -> (x, z, y)
    #   xy: upright, facing -z (towards the player)                   (x, y, z) -> (x, y, -z)
    #   zy: upright, facing +x (Unity right of the reader is +z)      (x, y, z) -> (z, y, x)
    if plane == "xz":
        bm.transform(Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1))))
    elif plane == "xy":
        bm.transform(Matrix(((1, 0, 0, 0), (0, 1, 0, 0), (0, 0, -1, 0), (0, 0, 0, 1))))
    else:
        bm.transform(Matrix(((0, 0, 1, 0), (0, 1, 0, 0), (1, 0, 0, 0), (0, 0, 0, 1))))
    if rot:
        axis = {"xz": "Y", "xy": "Z", "zy": "X"}[plane]
        bm.transform(Matrix.Rotation(math.radians(rot), 4, axis))
    bmesh.ops.translate(bm, vec=V(center), verts=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm, False


def transformed(prim, matrix):
    bm, smooth = prim
    bm.transform(matrix)
    bm.normal_update()
    return bm, smooth


def rotate_about(point, axis, degrees):
    """Rotation matrix about a Unity-space point. axis is 'X', 'Y' or 'Z' (Unity axes)."""
    p = V(point)
    return Matrix.Translation(p) @ Matrix.Rotation(math.radians(degrees), 4, axis) @ Matrix.Translation(-p)


def subdivide(prim, cuts=1, smooth=True):
    bm, s = prim
    bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=cuts, use_grid_fill=True, smooth=0.0)
    bm.normal_update()
    return bm, (True if smooth else s)


def displace(prim, fn):
    """Move every vertex by fn(Vector) -> Vector (Unity space)."""
    bm, s = prim
    for v in bm.verts:
        v.co = fn(v.co.copy())
    bm.normal_update()
    return bm, s


# ----------------------------------------------------------------------------- model

AUTO_SMOOTH_DEGREES = 42.0


def _sharpen(bm, degrees):
    limit = math.radians(degrees)
    bm.normal_update()
    for e in bm.edges:
        if len(e.link_faces) != 2:
            e.smooth = False
            continue
        e.smooth = e.calc_face_angle(math.pi) < limit


class Model:
    """Accumulates primitives (each with one material) into a single mesh object."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.materials = []

    def add(self, prim, material, smooth=None, transform=None, uv=True, uv_scale=1.0 / UV_METRES):
        part, default_smooth = prim
        if transform is not None:
            part.transform(transform)
            part.normal_update()
        if uv is True:
            box_uv(part, uv_scale)
        if material not in self.materials:
            self.materials.append(material)
        index = self.materials.index(material)
        flag = default_smooth if smooth is None else smooth
        for f in part.faces:
            f.material_index = index
            if flag is not None and flag is not False:
                f.smooth = True
            elif flag is False:
                f.smooth = False
        if flag == "auto":
            _sharpen(part, AUTO_SMOOTH_DEGREES)
        else:
            for e in part.edges:
                e.smooth = True
        mesh = bpy.data.meshes.new("_part")
        part.to_mesh(mesh)
        part.free()
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)
        return self

    def empty(self):
        return len(self.bm.verts) == 0

    def build(self, origin=(0, 0, 0), parent=None, parent_origin=(0, 0, 0)):
        """Create the Blender object with vertices relative to `origin` (Unity space)."""
        bm = self.bm
        bmesh.ops.translate(bm, vec=-V(origin), verts=bm.verts)
        bm.transform(UNITY_TO_BLENDER)
        bmesh.ops.reverse_faces(bm, faces=bm.faces)  # the axis swap mirrors, so flip winding
        bm.normal_update()
        mesh = bpy.data.meshes.new(self.name)
        bm.to_mesh(mesh)
        bm.free()
        for m in self.materials:
            mesh.materials.append(get_material(m))
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.location = (UNITY_TO_BLENDER @ (V(origin) - V(parent_origin)).to_4d()).to_3d()
        if parent is not None:
            obj.parent = parent
        return obj


def unity_basis(up=(0, 1, 0), forward=None):
    """Unity-space rotation (3x3) whose local +Y is `up` and local +Z is as close to `forward` as possible."""
    u = V(up).normalized()
    f = V(forward) if forward is not None else (V(0, 0, 1) if abs(u.z) < 0.9 else V(0, 1, 0) * -1)
    f = (f - u * f.dot(u)).normalized()
    r = u.cross(f)  # numerically x = y cross z keeps det(+1) (a proper rotation)
    return Matrix((r, u, f)).transposed()


def empty(name, origin=(0, 0, 0), parent=None, parent_origin=(0, 0, 0), up=None, forward=None):
    """An empty (anchor) in Unity space. `up`/`forward` orient its local +Y/+Z (Unity axes), e.g.
    hotspots point +Y out of the surface so the game can tell when they face the camera."""
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.empty_display_size = 0.02
    loc = (UNITY_TO_BLENDER @ (V(origin) - V(parent_origin)).to_4d()).to_3d()
    # Always write a full matrix: location-only empties parented to meshes come out of the FBX
    # exporter rotated 90 degrees with y/z swapped, while explicit matrices survive intact.
    ru = unity_basis(up or (0, 1, 0), forward) if (up is not None or forward is not None) else Matrix.Identity(3)
    c = UNITY_TO_BLENDER.to_3x3()
    rb = c @ ru @ c.inverted()
    obj.matrix_basis = Matrix.Translation(loc) @ rb.to_4x4()
    obj.parent = parent
    return obj


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.objects, bpy.data.curves):
        for block in list(coll):
            coll.remove(block)


def export_fbx(path, objects):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
        for c in o.children_recursive:
            c.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH", "EMPTY"},
        mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
        path_mode="STRIP", use_custom_props=False)


# ----------------------------------------------------------------------------- preview renders

def setup_preview(target_center, radius, yaw=35.0, pitch=25.0, resolution=640, bg=(0.11, 0.1, 0.1)):
    """Eevee studio render: key/fill/rim lights and a camera framing a sphere around the target
    (Unity-space centre and radius). yaw is measured from the -z (player) side."""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE"
    scene.render.resolution_x = resolution
    scene.render.resolution_y = resolution
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX" if "AgX" in [i.identifier for i in scene.view_settings.bl_rna.properties["view_transform"].enum_items] else "Filmic"
    world = bpy.data.worlds.new("w") if not scene.world else scene.world
    scene.world = world
    world.use_nodes = True
    bgn = world.node_tree.nodes.get("Background")
    bgn.inputs[0].default_value = (*bg, 1)
    bgn.inputs[1].default_value = 0.6
    c = V(target_center)
    yr, pr = math.radians(yaw), math.radians(pitch)
    dist = radius / math.tan(math.radians(18)) * 1.08
    eye_u = c + V(math.sin(yr) * math.cos(pr), math.sin(pr), -math.cos(yr) * math.cos(pr)) * dist
    to_b = lambda p: (UNITY_TO_BLENDER @ V(p).to_4d()).to_3d()
    cam_data = bpy.data.cameras.new("cam")
    cam_data.lens_unit = "FOV"
    cam_data.angle = math.radians(36)
    cam_data.clip_start = 0.005
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = to_b(eye_u)
    direction = to_b(c) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam

    def light(name, kind, energy, pos, color=(1, 1, 1), size=1.0):
        ld = bpy.data.lights.new(name, kind)
        ld.energy = energy
        ld.color = color
        if hasattr(ld, "size"):
            ld.size = size
        lo = bpy.data.objects.new(name, ld)
        scene.collection.objects.link(lo)
        lo.location = to_b(pos)
        d = to_b(c) - lo.location
        lo.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        return lo

    s = max(radius, 0.05)
    light("key", "AREA", 220 * s * s, c + V(-1.4, 1.6, -1.2) * s * 4, (1.0, 0.86, 0.7), size=s * 2)
    light("fill", "AREA", 70 * s * s, c + V(1.6, 0.6, -1.0) * s * 4, (0.7, 0.8, 1.0), size=s * 3)
    light("rim", "AREA", 160 * s * s, c + V(0.4, 1.2, 1.8) * s * 4, (1.0, 0.95, 0.9), size=s * 2)
    return cam


def render(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
