"""Signed-distance-field modelling for organic forms (heads, hands, hair, cloth).

Shapes are numpy functions of an (N, 3) array of Unity-space points returning signed distances
(negative inside). Combine them with smooth unions/subtractions, then `mesh()` polygonises the
field with marching cubes and returns a primitive for `laf.Model.add`, so a face, nose, lips and
ears come out as one continuous surface rather than intersecting balls.

Needs scikit-image from the project venv (`.venv/bin/pip install scikit-image==0.24.0`); the venv's
site-packages is put on Blender's path here.
"""
import glob
import math
import os
import sys

import numpy as np

_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
for _sp in glob.glob(os.path.join(_ROOT, ".venv", "lib", "python3*", "site-packages")):
    if _sp not in sys.path:
        sys.path.append(_sp)

import bmesh  # noqa: E402
import bpy  # noqa: E402
from skimage.measure import marching_cubes  # noqa: E402


def _v(p):
    return np.asarray(p, dtype=np.float64)


def rot_matrix(x=0.0, y=0.0, z=0.0):
    """Unity-space rotation (degrees, applied z then x then y like Unity's Euler)."""
    rx, ry, rz = (math.radians(a) for a in (x, y, z))
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    mx = np.array(((1, 0, 0), (0, cx, -sx), (0, sx, cx)))
    my = np.array(((cy, 0, sy), (0, 1, 0), (-sy, 0, cy)))
    mz = np.array(((cz, -sz, 0), (sz, cz, 0), (0, 0, 1)))
    return my @ mx @ mz


# ----------------------------------------------------------------------------- primitives

def sphere(c, r):
    c = _v(c)
    return lambda p: np.linalg.norm(p - c, axis=1) - r


def ellipsoid(c, radii, rot=None):
    c, r = _v(c), _v(radii)
    inv = None if rot is None else np.linalg.inv(rot_matrix(*rot))

    def f(p):
        q = p - c
        if inv is not None:
            q = q @ inv.T
        k0 = np.linalg.norm(q / r, axis=1)
        k1 = np.linalg.norm(q / (r * r), axis=1)
        return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)
    return f


def capsule(a, b, ra, rb=None):
    """Round cone from a (radius ra) to b (radius rb)."""
    a, b = _v(a), _v(b)
    rb = ra if rb is None else rb
    ba = b - a
    l2 = max(float(ba @ ba), 1e-12)

    def f(p):
        t = np.clip(((p - a) @ ba) / l2, 0.0, 1.0)
        return np.linalg.norm(p - a - t[:, None] * ba, axis=1) - (ra + (rb - ra) * t)
    return f


def chain(points, radii):
    """Smoothly joined capsules through a polyline (limbs, fingers, braids, straps)."""
    parts = [capsule(points[i], points[i + 1], radii[i], radii[i + 1]) for i in range(len(points) - 1)]
    return union(*parts)


def rbox(c, half, r=0.0, rot=None):
    c, h = _v(c), _v(half)
    inv = None if rot is None else np.linalg.inv(rot_matrix(*rot))

    def f(p):
        q = p - c
        if inv is not None:
            q = q @ inv.T
        q = np.abs(q) - (h - r)
        return np.linalg.norm(np.maximum(q, 0.0), axis=1) + np.minimum(q.max(axis=1), 0.0) - r
    return f


def halfspace(point, normal):
    """Negative on the side the normal points away from: keeps everything 'below' the plane."""
    p0, n = _v(point), _v(normal) / np.linalg.norm(normal)
    return lambda p: (p - p0) @ n


# ----------------------------------------------------------------------------- operators

def _smin(a, b, k):
    if k <= 0:
        return np.minimum(a, b)
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b + (a - b) * h - k * h * (1.0 - h)


def union(*fs, k=0.0):
    def f(p):
        d = fs[0](p)
        for g in fs[1:]:
            d = _smin(d, g(p), k)
        return d
    return f


def subtract(f, *gs, k=0.0):
    def h(p):
        d = f(p)
        for g in gs:
            d = -_smin(-d, g(p), k)
        return d
    return h


def intersect(*fs, k=0.0):
    def f(p):
        d = fs[0](p)
        for g in fs[1:]:
            d = -_smin(-d, -g(p), k)
        return d
    return f


def shell(f, thickness):
    return lambda p: np.abs(f(p)) - thickness


def offset(f, amount):
    return lambda p: f(p) - amount


def displace(f, fn):
    """Add fn(p) to the distance (bumps, grooves, wrinkles). Keep amplitudes small."""
    return lambda p: f(p) + fn(p)


def warp(f, fn):
    """Evaluate f at fn(p): bends and twists."""
    return lambda p: f(fn(p))


def noise(seed=0, octaves=7, scale=60.0):
    """Cheap smooth pseudo-noise in about [-1, 1]: a sum of randomly oriented sine waves."""
    rng = np.random.default_rng(seed)
    dirs = rng.normal(size=(octaves, 3))
    dirs /= np.linalg.norm(dirs, axis=1)[:, None]
    freqs = scale * (1.0 + rng.random(octaves) * 1.5)
    phases = rng.random(octaves) * 6.283

    def n(p):
        acc = np.zeros(len(p))
        for d, fr, ph in zip(dirs, freqs, phases):
            acc += np.sin((p @ d) * fr + ph)
        return acc / math.sqrt(octaves)
    return n


# ----------------------------------------------------------------------------- meshing

def mesh(f, bmin, bmax, step=0.002, smooth=1, ratio=None, tris=None, regions=None):
    """Polygonise f inside the Unity-space box [bmin, bmax]. Returns (bmesh, True) for Model.add.
    `tris` decimates (collapse) to about that many triangles; `ratio` < 1 decimates by a fraction.
    With `regions` (a list of fields), faces are tagged at full resolution before decimating (so
    material borders stay clean) and a list of primitives is returned: [outside all, region 0, ...]."""
    bmin, bmax = _v(bmin) - step * 2, _v(bmax) + step * 2
    n = np.maximum(np.ceil((bmax - bmin) / step).astype(int) + 1, 3)
    xs, ys, zs = (bmin[i] + np.arange(n[i]) * step for i in range(3))
    vol = np.empty(tuple(n), dtype=np.float32)
    yy, zz = np.meshgrid(ys, zs, indexing="ij")
    plane = np.stack([np.zeros(yy.size), yy.ravel(), zz.ravel()], axis=1)
    for i, x in enumerate(xs):
        plane[:, 0] = x
        vol[i] = f(plane).reshape(n[1], n[2])
    if vol.min() >= 0 or vol.max() <= 0:
        raise ValueError("sdf.mesh: surface does not cross the sampling box")
    verts, faces, _, _ = marching_cubes(vol, level=0.0, spacing=(step, step, step))
    verts += bmin
    if tris is not None:
        ratio = min(1.0, tris / max(1, len(faces)))
    me = bpy.data.meshes.new("_sdf")
    me.from_pydata(verts.tolist(), [], faces.tolist())
    me.update()
    if regions:
        cents = verts[faces].mean(axis=1)
        idx = np.zeros(len(faces), dtype=np.int32)
        for i, reg in reversed(list(enumerate(regions))):
            idx[reg(cents) < 0] = i + 1
        for _ in range(len(regions) + 1):
            me.materials.append(None)
        me.polygons.foreach_set("material_index", idx)
        me.update()
        # pin the vertices on material borders so the decimator keeps those edges crisp
        tri_idx = idx[:, None].repeat(3, 1).ravel()
        vmin = np.full(len(verts), 1 << 30)
        vmax = np.full(len(verts), -1)
        np.minimum.at(vmin, faces.ravel(), tri_idx)
        np.maximum.at(vmax, faces.ravel(), tri_idx)
        border = np.nonzero(vmin != vmax)[0]
    obj = bpy.data.objects.new("_sdf", me)
    bpy.context.scene.collection.objects.link(obj)
    if smooth:
        m = obj.modifiers.new("smooth", "SMOOTH")
        m.factor, m.iterations = 0.5, smooth
    if ratio is not None and ratio < 1.0:
        m = obj.modifiers.new("dec", "DECIMATE")
        m.ratio = ratio
        if regions and len(border):
            vg = obj.vertex_groups.new(name="free")
            free = np.ones(len(verts), bool)
            free[border] = False
            vg.add(np.nonzero(free)[0].tolist(), 1.0, "REPLACE")
            m.vertex_group = "free"
            m.vertex_group_factor = 1000.0
    deps = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(deps)
    bm = bmesh.new()
    bm.from_mesh(ev.to_mesh())
    ev.to_mesh_clear()
    bpy.data.objects.remove(obj)
    bpy.data.meshes.remove(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=step * 0.05)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    if not regions:
        return bm, True
    out = []
    for i in range(len(regions) + 1):
        part = bm.copy()
        bmesh.ops.delete(part, geom=[fc for fc in part.faces if fc.material_index != i], context="FACES")
        for fc in part.faces:
            fc.material_index = 0
        out.append((part, True))
    bm.free()
    return out


def split(prim, region, inside_margin=0.0):
    """Split a meshed primitive into (faces whose centroid is inside `region`, the rest), so one
    continuous surface can carry two materials (lips on a face, a cuff on a sleeve)."""
    bm, s = prim
    other = bm.copy()
    cents = np.array([tuple(f.calc_center_median()) for f in bm.faces]) if bm.faces else np.zeros((0, 3))
    inside = region(cents) < inside_margin if len(cents) else np.zeros(0, bool)
    bm.faces.ensure_lookup_table()
    other.faces.ensure_lookup_table()
    drop_in = [other.faces[i] for i in range(len(inside)) if inside[i]]
    drop_out = [bm.faces[i] for i in range(len(inside)) if not inside[i]]
    bmesh.ops.delete(bm, geom=drop_out, context="FACES")
    bmesh.ops.delete(other, geom=drop_in, context="FACES")
    return (bm, s), (other, s)
