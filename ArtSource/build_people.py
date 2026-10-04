"""The commuters: a modular character generator. Each character is a spec (build, skin, hair, hat,
moustache, coat, accessories...) and exported with the pieces the game animates:
Body (torso, arms) > Head (pivot at the neck) > EyeL/EyeR, BrowL/BrowR, Mouth, Hat; HandL/HandR.

Heads, hair, hands and bodies are sculpted as signed distance fields (lib/sdf.py) so skin, lips,
lids and ears are one continuous surface. The game blinks by squashing EyeL/EyeR vertically (the
eyeballs sit in sockets, so a squashed eye shows the lids) and talks by stretching Mouth, a dark
mouth cavity sitting in the parting of the lips.

    blender -b -P ArtSource/build_people.py [-- --only walter,odile] [--preview /tmp/p]

Unity space, metres; feet at the origin, facing -z (towards the booth).
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib"))
import bpy  # noqa: E402
import mathutils  # noqa: E402
import numpy as np  # noqa: E402
import sdf  # noqa: E402
from laf import (V, Model, cbox, cyl, empty, export_fbx, lathe, mat, rod, sphere, torus, ellipsoid,  # noqa: E402
                 reset_scene, setup_preview, render, sweep, hull, rotate_about, args_after_dashes, shade)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Game", "Resources", "Models", "People")

SCLERA = mat("eye", "D2C8BA")
PUPIL = mat("eye", "0A0806")
MOUTH = mat("paint", "3A1614")
BRASS = mat("brass", "C9A15A")
BLACKISH = mat("paint", "1A1A1A")

HEAD_REF = (0.105, 0.125, 0.11)   # the spec's "head" proportions are relative to this
HEAD_GAIN = 1.15                  # a touch larger than life so faces read through the booth glass
NECK_Y = 1.45                     # Head pivot
STEP_HEAD = 0.0015
STEP_BODY = 0.004
RIM_TILT = 0.15
# soft hats are carved around the skull; their rims sit this far above (+) or below the default rim
FITTED_HATS = {"flatcap": 0.0, "beret": 0.012, "beanie": -0.012, "cloche": -0.02}


def iris_for(spec):
    if "iris" in spec:
        return spec["iris"]
    h = spec.get("hair_col", "3A2A20")
    lum = sum(int(h[i:i + 2], 16) for i in (0, 2, 4)) / 3
    return "4E6A7C" if lum > 150 or h.startswith(("B", "C")) else "4A3322"


def is_fem(spec):
    return bool(spec.get("skirt")) or spec.get("fem", False)


# ----------------------------------------------------------------------------- head geometry

class HeadGeo:
    """Head frame: C is the eye-level centre, S the per-axis scale. P() and R() map the reference
    head (an adult about 23 cm chin to crown) into this character's head."""

    def __init__(self, spec):
        ref = spec.get("head", HEAD_REF)
        rel = np.array([ref[i] / HEAD_REF[i] for i in range(3)])
        self.S = HEAD_GAIN * (1.0 + 0.6 * (rel - 1.0))
        self.sm = float(self.S.mean())
        self.C = np.array((0.0, 1.468 + 0.112 * self.S[1], 0.0))
        self.fem = is_fem(spec)
        self.child = spec.get("build") == "child"
        self.eye_r = 0.0118 * self.sm
        self.E = [self.P(sx * 0.0315, 0.0, -0.077) for sx in (-1, 1)]

    def P(self, x, y, z):
        return self.C + np.array((x, y, z)) * self.S

    def R(self, x, y, z):
        return tuple(np.array((x, y, z)) * self.S)

    def r(self, v):
        return v * self.sm

    def skull(self, pad=0.0):
        """Cranium and forehead grown by `pad`: the surface hats are fitted to and hair is pressed onto."""
        P, R = self.P, self.R
        return sdf.offset(sdf.union(sdf.ellipsoid(P(0, 0.03, 0.012), R(0.077, 0.094, 0.097)),
                                    sdf.ellipsoid(P(0, 0.034, -0.042), R(0.068, 0.062, 0.05)), k=self.r(0.02)), pad)

    @property
    def crown(self):
        return self.C[1] + 0.124 * self.S[1]

    @property
    def rim_y(self):
        return self.C[1] + 0.06 * self.S[1]

    def above_rim(self, lift=0.0):
        """Everything above the hat rim, which sits on the forehead and drops towards the nape."""
        return sdf.halfspace((0, self.rim_y + lift, -0.09 * self.S[2]), (0, -1, -RIM_TILT))

    def below_rim(self, lift=0.0):
        return sdf.halfspace((0, self.rim_y + lift, -0.09 * self.S[2]), (0, 1, RIM_TILT))

    # the legacy frame the hat builders use
    @property
    def hat_frame(self):
        hc = V(*self.C)
        return hc, (0.088 * self.S[0], 0.097 * self.S[1], 0.101 * self.S[2])


NOSES = {
    # bridge top (y, z, r), tip (y, z, r), nostril half-width, alae size
    "round": ((0.012, -0.083, 0.0072), (-0.034, -0.107, 0.0105), 0.0125, 1.0),
    "button": ((0.006, -0.083, 0.0062), (-0.03, -0.1015, 0.0088), 0.0108, 0.85),
    "long": ((0.016, -0.084, 0.0068), (-0.043, -0.112, 0.0092), 0.0118, 0.9),
    "hook": ((0.016, -0.084, 0.0072), (-0.044, -0.109, 0.0095), 0.0122, 0.95),
    "big": ((0.012, -0.084, 0.0085), (-0.037, -0.111, 0.0145), 0.0145, 1.2),
}


def nose_field(g, spec):
    (by, bz, br), (ty, tz, tr), nw, al = NOSES.get(spec.get("nose", "round"), NOSES["round"])
    if g.fem:
        tr *= 0.92
    parts = [
        sdf.capsule(g.P(0, by, bz), g.P(0, ty + tr * 0.6, tz + tr * 0.55), g.r(br), g.r(tr * 0.8)),
        sdf.sphere(g.P(0, ty, tz), g.r(tr)),
        sdf.ellipsoid(g.P(0, ty - tr * 0.55, tz + tr * 0.9), g.R(0.006, 0.0055, 0.009)),  # columella
    ]
    for sx in (-1, 1):
        parts.append(sdf.ellipsoid(g.P(sx * nw, ty - 0.003, tz + 0.009), g.R(0.0085 * al, 0.0068 * al, 0.0095 * al)))
    if spec.get("nose") == "hook":
        parts.append(sdf.ellipsoid(g.P(0, -0.012, -0.099), g.R(0.0075, 0.012, 0.007)))
    f = sdf.union(*parts, k=g.r(0.006))
    nostrils = [sdf.ellipsoid(g.P(sx * nw * 0.55, ty - tr * 0.85, tz + 0.006), g.R(0.0034, 0.0022, 0.0045), rot=(25, 0, 0))
                for sx in (-1, 1)]
    return f, nostrils


def mouth_geo(g, spec):
    mw = spec.get("mouth_w", 1.0)
    y = -0.069 + spec.get("mouth_y", 0.0) / g.S[1]
    smile = 9.0 if spec.get("smile") else (-4.0 if spec.get("frown") else 0.0)
    return mw, y, smile


def smile_warp(g, smile):
    cx = g.C[0]

    def w(p):
        q = p.copy()
        q[:, 1] -= smile * (p[:, 0] - cx) ** 2
        return q
    return w


def lips_field(g, spec):
    mw, y, smile = mouth_geo(g, spec)
    full = 1.25 if g.fem else 1.0
    upper = sdf.ellipsoid(g.P(0, y + 0.0048, -0.0985), g.R(0.021 * mw, 0.0048 * full, 0.0075))
    lower = sdf.ellipsoid(g.P(0, y - 0.0058, -0.0965), g.R(0.019 * mw, 0.006 * full, 0.0078))
    return sdf.warp(sdf.union(upper, lower, k=g.r(0.003)), smile_warp(g, smile))


def head_field(g, spec):
    P, R, r = g.P, g.R, g.r
    jaw = spec.get("jaw", 1.0) * (0.9 if g.fem else 1.0)
    ridge = 0.65 if (g.fem or g.child) else 1.0
    cheeks = 1.12 if spec.get("rosy") else 1.0
    masses = [
        sdf.ellipsoid(P(0, 0.03, 0.012), R(0.077, 0.094, 0.097)),                      # cranium
        sdf.ellipsoid(P(0, 0.034, -0.042), R(0.068, 0.062, 0.05)),                     # forehead
        sdf.ellipsoid(P(0, -0.032, -0.036), R(0.067 * jaw, 0.078, 0.058)),             # face
        sdf.ellipsoid(P(0, -0.104, -0.066), R(0.022, 0.017, 0.02)),                    # chin
        sdf.ellipsoid(P(0, -0.064, -0.073), R(0.031, 0.029, 0.029)),                   # muzzle
        sdf.capsule(P(-0.04, 0.019, -0.081), P(0.04, 0.019, -0.081), r(0.0105 * ridge)),  # brow ridge
    ]
    for sx in (-1, 1):
        masses.append(sdf.capsule(P(sx * 0.057 * jaw, -0.035, 0.018), P(sx * 0.026 * jaw, -0.099, -0.056), r(0.017)))  # jaw
        masses.append(sdf.ellipsoid(P(sx * 0.043, -0.021, -0.064), R(0.025 * cheeks, 0.019 * cheeks, 0.023)))       # cheekbone
    head = sdf.union(*masses, k=r(0.02))
    nose, nostrils = nose_field(g, spec)
    head = sdf.union(head, nose, k=r(0.007))
    lips = lips_field(g, spec)
    head = sdf.union(head, lips, k=r(0.004))
    # ears
    ear = spec.get("ears", 1.0)
    ears = []
    for sx in (-1, 1):
        shell_ = sdf.ellipsoid(P(sx * 0.079, -0.002, 0.016), R(0.012 * ear, 0.032 * ear, 0.021 * ear), rot=(0, sx * 28, sx * -8))
        lobe = sdf.ellipsoid(P(sx * 0.077, -0.027 * ear, 0.008), R(0.008 * ear, 0.01 * ear, 0.009 * ear))
        bowl = sdf.ellipsoid(P(sx * 0.089, -0.002, 0.012), R(0.0045 * ear, 0.016 * ear, 0.01 * ear), rot=(0, sx * 28, 0))
        ears.append(sdf.subtract(sdf.union(shell_, lobe, k=r(0.004)), bowl, k=r(0.004)))
    head = sdf.union(head, *ears, k=r(0.006))
    # neck down into the collar
    nk = {"broad": 1.15, "huge": 1.3, "round": 1.1, "thin": 0.88, "child": 0.8}.get(spec.get("build"), 1.0) * (0.88 if g.fem else 1.0)
    neck = sdf.capsule(P(0, -0.07, 0.024), (0, NECK_Y - 0.06, 0.012), r(0.052 * nk), 0.058 * nk)
    head = sdf.union(head, neck, k=r(0.025))
    # eyes: an orbital hollow, a seat for the eyeball, and lids around the opening
    es = spec.get("eye_size", 1.0)
    lids = spec.get("lids", 0.0)
    er = g.eye_r
    carve = []
    for E in g.E:
        carve.append(sdf.ellipsoid(E + np.array((0, 0.0005, -er * 1.05)), (er * 1.18, er * 0.92, er * 0.85)))
        carve.append(sdf.sphere(E, er * 1.02))
    head = sdf.subtract(head, *carve, nostrils[0], nostrils[1], k=r(0.0025))
    # mouth parting
    mw, my, smile = mouth_geo(g, spec)
    slit = sdf.warp(sdf.ellipsoid(P(0, my, -0.104), R(0.021 * mw, 0.0009, 0.009)), smile_warp(g, smile))
    head = sdf.subtract(head, slit, k=r(0.0012))
    lid_parts = []
    for i, E in enumerate(g.E):
        sx = -1 if i == 0 else 1
        up = er * (0.36 * es - 0.6 * lids)
        tilt = (sx * 0.12, -1.0, 0.0)
        upper = sdf.intersect(sdf.sphere(E, er * 1.13), sdf.halfspace(E + np.array((0, up, 0)), tilt),
                              sdf.halfspace(E, (0, 0, 1)))
        lower = sdf.intersect(sdf.sphere(E, er * 1.08), sdf.halfspace(E + np.array((0, -er * 0.5 * es, 0)), (sx * -0.1, 1.0, 0.0)),
                              sdf.halfspace(E, (0, 0, 1)))
        lid_parts += [upper, lower]
    head = sdf.union(head, *lid_parts, k=r(0.0022))
    return head, lips


def head_bounds(g):
    lo = g.C + np.array((-0.105, -0.135, -0.135)) * g.S
    hi = g.C + np.array((0.105, 0.135, 0.12)) * g.S
    lo[1] = NECK_Y - 0.07
    return lo, hi


def surface_z(f, x, y, z0=-0.3, z1=0.2, step=0.0005):
    """March from the front (-z) towards +z and return where f first goes inside."""
    zs = np.arange(z0, z1, step)
    pts = np.stack([np.full_like(zs, x), np.full_like(zs, y), zs], axis=1)
    d = f(pts)
    idx = np.argmax(d < 0)
    return float(zs[idx]) if d[idx] < 0 else None


# ----------------------------------------------------------------------------- hair

def strands(axis_point, freq, amp, seed=1, mode="radial"):
    """Groove displacement that runs along combed hair."""
    nz = sdf.noise(seed, 5, 90.0)
    ap = np.asarray(axis_point)

    def fn(p):
        q = p - ap
        if mode == "radial":       # combed down from the crown
            t = np.arctan2(q[:, 0], q[:, 2]) * freq
        elif mode == "back":       # slicked straight back
            t = q[:, 0] * freq * 30.0
        else:                      # curls and waves
            t = (q[:, 1] * 1.3 + q[:, 0] * 0.4) * freq * 30.0
        return amp * (np.sin(t + nz(p) * 1.6) * 0.75 + nz(p * 1.7) * 0.25)
    return fn


def hair_field(g, spec):
    P, R, r = g.P, g.R, g.r
    style = spec.get("hair", "short")
    if style in (None, "none"):
        return None
    cran = sdf.ellipsoid(P(0, 0.03, 0.012), R(0.077, 0.094, 0.097))
    crown = P(0, 0.12, 0.02)

    def cap(t, front_y=0.056, nape_y=-0.075, sides=0.0):
        keep = sdf.halfspace(P(0, front_y, -0.095), (0, -1.0, (nape_y - front_y) / 0.2 - sides))
        return sdf.intersect(sdf.offset(cran, r(t)), keep, k=r(0.006))

    if style in ("short", "ginger"):
        t = 0.0075 if style == "short" else 0.011
        f = sdf.displace(cap(t), strands(crown, 46, r(0.0011), 3))
    elif style == "slick":
        f = sdf.displace(cap(0.0055, front_y=0.062), strands(crown, 9, r(0.0007), 4, "back"))
    elif style == "bald":
        band = sdf.intersect(sdf.offset(cran, r(0.006)), sdf.halfspace(P(0, 0.035, 0), (0, 1, 0)),
                             sdf.halfspace(P(0, 0, 0.012), (0, 0, -1)), sdf.halfspace(P(0, -0.065, 0), (0, -1, 0)), k=r(0.012))
        f = sdf.displace(band, strands(crown, 50, r(0.0009), 5))
    elif style == "bob":
        body = sdf.union(cap(0.012, front_y=0.03, nape_y=-0.13),
                         sdf.ellipsoid(P(0, -0.035, 0.025), R(0.094, 0.07, 0.092)), k=r(0.02))
        body = sdf.subtract(body, sdf.ellipsoid(P(0, -0.05, -0.08), R(0.07, 0.085, 0.07)), k=r(0.008))   # open the face
        fringe = sdf.intersect(sdf.offset(cran, r(0.013)), sdf.halfspace(P(0, 0.035, 0), (0, -1, 0)),
                               sdf.halfspace(P(0, 0, -0.03), (0, 0, 1)), k=r(0.006))
        f = sdf.displace(sdf.union(body, fringe, k=r(0.008)), strands(crown, 60, r(0.0012), 6))
    elif style == "bun":
        f = sdf.union(cap(0.006, front_y=0.06),
                      sdf.displace(sdf.ellipsoid(P(0, 0.07, 0.1), R(0.042, 0.038, 0.036)), strands(P(0, 0.07, 0.1), 3, r(0.0018), 7, "wave")),
                      k=r(0.012))
        f = sdf.displace(f, strands(crown, 40, r(0.0008), 8))
    elif style == "curls":
        nz = sdf.noise(11, 7, 160.0)
        parts = [cap(0.012)]
        rng = np.random.default_rng(2)
        for _ in range(26):
            a, b = rng.random() * math.tau, rng.random() * 0.9
            q = np.array((math.cos(a) * math.cos(b) * 0.084, 0.03 + math.sin(b) * 0.098, math.sin(a) * math.cos(b) * 0.1))
            if q[1] < 0.035 and q[2] < -0.02:
                continue
            parts.append(sdf.sphere(P(*q), r(0.022)))
        f = sdf.displace(sdf.union(*parts, k=r(0.01)), lambda p: r(0.0035) * nz(p))
    elif style == "braids":
        f = sdf.displace(cap(0.006), strands(crown, 40, r(0.0008), 9))
        for sx in (-1, 1):
            pts = [P(sx * 0.075, -0.0, 0.03), P(sx * 0.085, -0.06, 0.02), P(sx * 0.088, -0.12, 0.0), P(sx * 0.085, -0.18, -0.01)]
            braid = sdf.chain(pts, [r(0.019), r(0.017), r(0.015), r(0.012)])
            braid = sdf.displace(braid, lambda p, s=sx: r(0.003) * np.sin(p[:, 1] * 260 + s))
            f = sdf.union(f, braid, k=r(0.008))
    elif style == "platinum":
        body = sdf.union(cap(0.011, front_y=0.045),
                         sdf.ellipsoid(P(0, -0.025, 0.03), R(0.09, 0.052, 0.084)), k=r(0.02))
        body = sdf.subtract(body, sdf.ellipsoid(P(0, -0.05, -0.08), R(0.072, 0.075, 0.07)), k=r(0.008))
        f = sdf.displace(body, lambda p: r(0.0022) * np.sin((p[:, 1] - g.C[1]) * 260 + np.arctan2(p[:, 0], p[:, 2]) * 3)
                         + r(0.0006) * np.sin(np.arctan2(p[:, 0], p[:, 2]) * 70))
    elif style == "wild":
        nz = sdf.noise(12, 7, 70.0)
        f = sdf.displace(cap(0.02, front_y=0.05), lambda p: r(0.007) * nz(p))
    else:
        f = cap(0.008)
    return f


def facial_hair_field(g, spec, head):
    P, R, r = g.P, g.R, g.r
    parts = []
    mw, my, smile = mouth_geo(g, spec)
    mz = -0.104
    fh = spec.get("moustache")
    if fh == "walrus":
        parts.append(sdf.ellipsoid(P(0, my + 0.011, mz), R(0.03, 0.011, 0.012)))
        for sx in (-1, 1):
            parts.append(sdf.ellipsoid(P(sx * 0.022, my + 0.002, mz + 0.004), R(0.016, 0.014, 0.01)))
    elif fh == "pencil":
        parts.append(sdf.capsule(P(-0.02, my + 0.0115, mz - 0.001), P(0.02, my + 0.0115, mz - 0.001), r(0.0024)))
    elif fh == "handlebar":
        parts.append(sdf.ellipsoid(P(0, my + 0.012, mz), R(0.022, 0.007, 0.009)))
        for sx in (-1, 1):
            parts.append(sdf.chain([P(sx * 0.018, my + 0.011, mz), P(sx * 0.038, my + 0.012, mz + 0.006), P(sx * 0.052, my + 0.024, mz + 0.016)],
                                   [r(0.0055), r(0.0035), r(0.0015)]))
    elif fh == "huge":
        parts.append(sdf.ellipsoid(P(0, my + 0.011, mz), R(0.042, 0.013, 0.014)))
        for sx in (-1, 1):
            parts.append(sdf.chain([P(sx * 0.035, my + 0.008, mz + 0.002), P(sx * 0.062, my + 0.004, mz + 0.016), P(sx * 0.08, my + 0.02, mz + 0.03)],
                                   [r(0.011), r(0.007), r(0.0025)]))
    beard = spec.get("beard")
    if beard == "goatee":
        parts.append(sdf.ellipsoid(P(0, -0.096, -0.082), R(0.016, 0.022, 0.014)))
    elif beard == "muttonchops":
        for sx in (-1, 1):
            parts.append(sdf.intersect(sdf.offset(head, r(0.006)), sdf.ellipsoid(P(sx * 0.062, -0.04, -0.015), R(0.026, 0.05, 0.04)), k=r(0.004)))
    if not parts:
        return None
    f = sdf.union(*parts, k=r(0.004))
    nzf = sdf.noise(21, 5, 120.0)
    return sdf.displace(f, lambda p: r(0.0012) * np.sin(p[:, 0] * 900 + nzf(p) * 2) + r(0.0008) * nzf(p * 2))


# ----------------------------------------------------------------------------- head assembly

def build_head(spec):
    g = HeadGeo(spec)
    skin_hex = spec["skin"]
    skin = mat("skin", skin_hex)
    lip_hex = spec.get("lips", shade(mix_hex(skin_hex, "B0505A", 0.32 if g.fem else 0.2), 0.92))
    head_f, lips_f = head_field(g, spec)
    lo, hi = head_bounds(g)
    skin_prim, lips_prim = sdf.mesh(head_f, lo, hi, STEP_HEAD, smooth=1, tris=14000, regions=[sdf.offset(lips_f, g.r(0.0008))])
    head = Model("Head")
    head.add(skin_prim, skin)
    head.add(lips_prim, mat("skin", lip_hex))
    hair_col = spec.get("hair_col", "3A2A20")
    hf = hair_field(g, spec)
    hat = spec.get("hat")
    if hf is not None and hat and hat != "pillbox":
        if hat in FITTED_HATS:
            uncovered = g.below_rim(FITTED_HATS[hat])
        elif hat == "headscarf":
            uncovered = sdf.halfspace((0, g.C[1] - 0.06, 0), (0, 1, 0))
        else:
            hc, (_, hh, _) = g.hat_frame
            uncovered = sdf.halfspace((0, hc.y + hh * 0.72, 0), (0, 1, 0))
        hf = sdf.intersect(hf, sdf.union(uncovered, g.skull(0.003)))
    if hf is not None:
        head.add(sdf.mesh(hf, lo + np.array((-0.01, 0, -0.01)), hi + np.array((0.01, 0.03, 0.03)), STEP_HEAD, smooth=1, tris=6000),
                 mat("hair", hair_col))
    ff = facial_hair_field(g, spec, head_f)
    if ff is not None:
        head.add(sdf.mesh(ff, lo, hi, 0.0012, smooth=1, tris=2500), mat("hair", spec.get("moustache_col", hair_col)))
    add_glasses(head, g, spec, head_f)
    return head, g, head_f


def mix_hex(a, b, t):
    ca = [int(a[i:i + 2], 16) for i in (0, 2, 4)]
    cb = [int(b[i:i + 2], 16) for i in (0, 2, 4)]
    return "".join(f"{int(x + (y - x) * t):02X}" for x, y in zip(ca, cb))


def add_glasses(head, g, spec, head_f):
    kind = spec.get("glasses")
    if not kind:
        return
    er = g.eye_r
    z = min(surface_z(head_f, float(E[0]), float(E[1]) + er * 1.6) or E[2] for E in g.E) - 0.004
    if kind in ("round", "owl"):
        rr = er * (1.45 if kind == "round" else 1.75)
        brass = spec.get("glasses_brass", True)
        gm = mat("brass", "B8964E") if brass else mat("paint", "1A1A1A")
        for E in g.E:
            c = V(float(E[0]), float(E[1]), z)
            head.add(torus(c, rr, 0.0012 if brass else 0.0022, axis="z", segments=36, ring_segments=8), gm)
            head.add(cyl(c, rr, 0.0006, axis="z", segments=36), mat("glass", "D8E6EA"))
            side = 1 if E[0] > 0 else -1
            temple = [c + V(side * rr, 0, 0), V(side * 0.079 * g.S[0], float(E[1]) + 0.004, -0.04), V(side * 0.08 * g.S[0], float(E[1]) - 0.004, 0.012 * g.S[2])]
            head.add(sweep(temple, 0.0009 if brass else 0.0015, segments=6), gm)
        xl, xr = float(g.E[0][0]) + rr, float(g.E[1][0]) - rr
        head.add(sweep([V(xl, float(g.E[0][1]) + 0.002, z), V(0, float(g.E[0][1]) + 0.005, z - 0.002), V(xr, float(g.E[0][1]) + 0.002, z)],
                       0.0011, segments=6), gm)
    elif kind == "monocle":
        E = g.E[0]
        c = V(float(E[0]), float(E[1]), z)
        head.add(torus(c, er * 1.55, 0.0016, axis="z", segments=36, ring_segments=8), BRASS)
        head.add(cyl(c, er * 1.55, 0.0006, axis="z", segments=36), mat("glass", "D8E6EA"))
        head.add(sweep([c + V(-er * 1.5, -0.004, 0), V(-0.06, float(E[1]) - 0.08, -0.07), V(-0.07, 1.36, -0.1)], 0.0006, segments=5), BRASS)
    elif kind == "shades":
        for E in g.E:
            head.add(ellipsoid(V(float(E[0]), float(E[1]) - 0.001, z), (er * 1.75, er * 1.25, 0.0025)), mat("glass", "101418"))
        head.add(rod((float(g.E[0][0]) + er * 1.6, float(g.E[0][1]) + 0.004, z), (float(g.E[1][0]) - er * 1.6, float(g.E[1][1]) + 0.004, z), 0.0016), BLACKISH)
        for E in g.E:
            side = 1 if E[0] > 0 else -1
            head.add(rod((float(E[0]) + side * er * 1.7, float(E[1]) + 0.004, z), (side * 0.08 * g.S[0], float(E[1]) + 0.003, 0.01), 0.0015), BLACKISH)


def build_face(root, spec, g, head_f):
    er = g.eye_r
    iris = mat("eye", iris_for(spec))
    for name, E in (("EyeL", g.E[0]), ("EyeR", g.E[1])):
        c = V(*E)
        e = Model(name)
        e.add(sphere(c, er, segments=28, rings=18), SCLERA)
        e.add(ellipsoid(c + V(0, 0, -er * 0.86), (er * 0.47, er * 0.47, er * 0.2), segments=24, rings=10), iris)
        e.add(ellipsoid(c + V(0, 0, -er * 0.99), (er * 0.2, er * 0.2, er * 0.06), segments=16, rings=8), PUPIL)
        e.add(sphere(c + V(er * 0.28, er * 0.3, -er * 1.0), er * 0.07, segments=8, rings=6), mat("emit", "FFF6E8"))
        e.build(origin=c, parent=root)
    # brows follow the brow ridge surface
    bt = spec.get("brow", 1.0)
    brow_col = mat("hair", spec.get("brow_col", spec.get("hair_col", "3A2A20")))
    for name, E, sx in (("BrowL", g.E[0], -1), ("BrowR", g.E[1], 1)):
        ang = math.radians(spec.get("brow_angle", 8))
        pts, radii = [], []
        for t, dy, rad in ((-1.0, 0.0, 0.0033), (-0.3, 0.0042, 0.003), (0.35, 0.0045, 0.0024), (1.0, 0.0005, 0.0012)):
            x = float(E[0]) + sx * t * er * 1.45
            y = float(E[1]) + er * 1.75 + dy * g.sm + t * math.sin(ang) * 0.006
            z = surface_z(head_f, x, y) or float(E[2]) - er
            pts.append((x, y, z + 0.0006))
            radii.append(rad * bt * g.sm)
        cen = np.mean(np.array(pts), axis=0)
        f = sdf.chain(pts, radii)
        f = sdf.intersect(f, sdf.offset(head_f, 0.0035), k=0.0008)
        nz = sdf.noise(31 + sx, 4, 300.0)
        f = sdf.displace(f, lambda p: 0.0004 * nz(p))
        b = Model(name)
        b.add(sdf.mesh(f, cen - 0.035, cen + 0.035, 0.0009, smooth=1, tris=300), brow_col)
        b.build(origin=tuple(cen), parent=root)
    # the mouth cavity, stretched open by the game when talking
    mw, my, smile = mouth_geo(g, spec)
    mc = g.P(0, my, 0)
    z = surface_z(head_f, float(mc[0]), float(mc[1]) + 0.004) or -0.1
    c = V(float(mc[0]), float(mc[1]), z + 0.0032)
    mo = Model("Mouth")
    mo.add(ellipsoid(c, (0.017 * mw * g.S[0], 0.0046 * g.S[1], 0.0035), segments=24, rings=12), MOUTH)
    mo.add(ellipsoid(c + V(0, 0.003 * g.S[1], 0.0012), (0.011 * mw * g.S[0], 0.0014 * g.S[1], 0.0028), segments=16, rings=8), mat("ceramic", "D8D0C0"))
    mo.build(origin=c, parent=root)


# ----------------------------------------------------------------------------- hats (built on the head frame)

def build_hat(head_obj, spec, g):
    hat = spec.get("hat")
    if not hat:
        return
    hc, (hw, hh, hd) = g.hat_frame
    col = spec.get("hat_col", "3A3530")
    hm = mat(spec.get("hat_mat", "felt"), col)
    band = mat("cloth", spec.get("hat_band", shade(col, 0.6)))
    top = hc.y + hh * 0.72
    crown, rim = g.crown, g.rim_y
    inner = g.skull(0.006)
    ch = crown + 0.012 - top                       # crown height that clears the skull for straight-sided hats
    cover = sdf.intersect(g.skull(0.010), sdf.halfspace((0, top, 0), (0, -1, 0)))
    fit_lo, fit_hi = (-hw * 1.4, rim - 0.06, -hd * 1.8), (hw * 1.4, crown + 0.04, hd * 1.4)
    h = Model("Hat")
    if hat == "flatcap":
        body = sdf.union(sdf.intersect(g.skull(0.013), g.above_rim()),
                         sdf.ellipsoid(g.P(0, 0.112, -0.018), (hw * 1.12, 0.024 * g.S[1], hd * 1.14)), k=0.014)
        visor = sdf.ellipsoid((0, rim - 0.002, -hd * 0.8), (hw * 0.9, 0.009, 0.068))
        f = sdf.subtract(sdf.union(body, visor, k=0.01), inner, k=0.003)
        h.add(sdf.mesh(f, fit_lo, fit_hi, 0.002, tris=3500), hm)
        h.add(sphere(V(0, crown + 0.016, -0.006), 0.006), hm)
    elif hat == "trilby":
        brim = sdf.warp(sdf.rbox((0, top, 0), (hw * 1.55, 0.004, hd * 1.6), 0.003),
                        lambda p: p - np.stack([0 * p[:, 0], 9.0 * (p[:, 0] ** 2) * (p[:, 2] > -0.02) - 3.0 * p[:, 2] ** 2, 0 * p[:, 0]], 1))
        brim = sdf.intersect(brim, sdf.ellipsoid((0, top, 0), (hw * 1.55, 0.05, hd * 1.6)))
        cf = sdf.subtract(sdf.ellipsoid((0, top + 0.035, 0), (hw * 0.97, 0.072, hd * 1.0)), sdf.halfspace((0, top, 0), (0, 1, 0)))
        cf = sdf.union(cf, cover, k=0.01)
        cf = sdf.subtract(cf, sdf.ellipsoid((0, top + 0.11, 0.0), (0.012, 0.03, hd * 0.75)), k=0.01)   # centre dent
        for sx in (-1, 1):
            cf = sdf.subtract(cf, sdf.sphere((sx * 0.02, top + 0.085, -hd * 0.95), 0.015), k=0.008)    # pinch
        f = sdf.union(brim, cf, k=0.006)
        h.add(sdf.mesh(f, (-hw * 1.7, top - 0.03, -hd * 1.75), (hw * 1.7, top + 0.12, hd * 1.75), 0.002, tris=3000), hm)
        h.add(sdf.mesh(sdf.intersect(sdf.offset(cf, 0.0015), sdf.halfspace((0, top + 0.026, 0), (0, 1, 0)), sdf.halfspace((0, top + 0.002, 0), (0, -1, 0))),
                       (-hw * 1.2, top - 0.01, -hd * 1.25), (hw * 1.2, top + 0.04, hd * 1.25), 0.0015, tris=800), band)
    elif hat == "bowler":
        brim = sdf.warp(sdf.rbox((0, top - 0.004, 0), (hw * 1.28, 0.003, hd * 1.32), 0.003),
                        lambda p: p - np.stack([0 * p[:, 0], 14.0 * (p[:, 0] ** 2), 0 * p[:, 0]], 1))
        brim = sdf.intersect(brim, sdf.ellipsoid((0, top, 0), (hw * 1.28, 0.06, hd * 1.32)))
        cf = sdf.subtract(sdf.ellipsoid((0, top + 0.02, 0), (hw * 1.0, 0.105, hd * 1.03)), sdf.halfspace((0, top - 0.006, 0), (0, 1, 0)))
        cf = sdf.union(cf, cover, k=0.01)
        f = sdf.union(brim, cf, k=0.006)
        h.add(sdf.mesh(f, (-hw * 1.4, top - 0.03, -hd * 1.45), (hw * 1.4, top + 0.14, hd * 1.45), 0.002, tris=3000), hm)
        h.add(sdf.mesh(sdf.intersect(sdf.offset(cf, 0.0012), sdf.halfspace((0, top + 0.016, 0), (0, 1, 0))),
                       (-hw * 1.2, top - 0.01, -hd * 1.25), (hw * 1.2, top + 0.03, hd * 1.25), 0.0015, tris=800), band)
    elif hat == "beret":
        lift = FITTED_HATS["beret"]
        body = sdf.union(sdf.intersect(g.skull(0.011), g.above_rim(lift)),
                         sdf.ellipsoid((0.018, crown - 0.004, 0.0), (hw * 1.2, 0.024, hd * 1.2)), k=0.02)
        nz = sdf.noise(43, 5, 60.0)
        f = sdf.subtract(sdf.displace(body, lambda p: 0.0015 * nz(p)), inner, k=0.003)
        h.add(sdf.mesh(f, fit_lo, (hw * 1.5, fit_hi[1], fit_hi[2]), 0.002, tris=3000), hm)
        h.add(sphere(V(0.02, crown + 0.021, 0), 0.005), hm)
    elif hat == "porter":
        h.add(cyl(V(0, top + ch / 2 - 0.004, 0), hw * 1.05, ch + 0.008, segments=40, bevel=0.006), hm)
        h.add(ellipsoid(V(0, top - 0.006, -hd * 0.95), (hw * 0.85, 0.008, 0.05), segments=32), mat("plastic", "141414"))
        h.add(cyl(V(0, top + ch * 0.5, -hw * 1.05 - 0.002), 0.016, 0.004, axis="z", segments=20), BRASS)
        h.add(torus(V(0, top + 0.0, 0), hw * 1.05, 0.006, axis="y", segments=40), band)
    elif hat == "custodian":
        h.add(lathe([(hw * 1.1, top - 0.02), (hw * 1.08, top + 0.04), (hw * 0.95, top + 0.11), (hw * 0.62, top + 0.165), (hw * 0.2, top + 0.185), (0.001, top + 0.187)], (0, 0, 0), segments=44), hm)
        h.add(sphere(V(0, top + 0.19, 0), 0.011), mat("silver", "C0C0C8"))
        h.add(cyl(V(0, top + 0.08, -hw * 1.01 - 0.003), 0.026, 0.004, axis="z", segments=24), mat("silver", "C0C0C8"))
        h.add(torus(V(0, top - 0.015, 0), hw * 1.08, 0.008, axis="y", segments=40), mat("leather", "1A1A1A"))
    elif hat == "feathered":
        brim = sdf.warp(sdf.rbox((0, top - 0.008, 0), (hw * 2.0, 0.004, hd * 2.0), 0.003),
                        lambda p: p - np.stack([0 * p[:, 0], 3.0 * (p[:, 0] ** 2 + p[:, 2] ** 2) - 0.6 * p[:, 0], 0 * p[:, 0]], 1))
        brim = sdf.intersect(brim, sdf.ellipsoid((0, top, 0), (hw * 2.0, 0.08, hd * 2.0)))
        cf = sdf.subtract(sdf.ellipsoid((0, top + 0.018, 0), (hw * 1.0, 0.07, hd * 1.02)), sdf.halfspace((0, top - 0.006, 0), (0, 1, 0)))
        cf = sdf.union(cf, cover, k=0.01)
        h.add(sdf.mesh(sdf.union(brim, cf, k=0.006), (-hw * 2.2, top - 0.05, -hd * 2.2), (hw * 2.2, top + 0.1, hd * 2.2), 0.0025, tris=3500), hm)
        h.add(torus(V(0, top + 0.016, 0), hw * 1.0, 0.012, axis="y", segments=40), band)
        for k, (dx, ang) in enumerate(((0.06, 30), (0.08, 55), (0.04, 10))):
            h.add(sweep([(dx, top + 0.05, 0.04), (dx + 0.06, top + 0.14 + k * 0.02, 0.07), (dx + 0.12 + k * 0.03, top + 0.2 + k * 0.03, 0.02)], 0.02, radius_end=0.003, segments=10, flat=0.25), mat("fur", spec.get("feather", "E8E0F0")))
        h.add(sphere(V(0.07, top + 0.04, -0.05), 0.018), mat("cloth", spec.get("hat_flower", "C05070")))
    elif hat == "pillbox":
        piv = (0.03, top + 0.05, 0.0)
        h.add(cyl(V(*piv), hw * 0.6, 0.042, segments=40, bevel=0.005), hm, transform=rotate_about(piv, "Z", -14))
        h.add(torus(V(piv[0], piv[1] - 0.017, piv[2]), hw * 0.6, 0.0035, axis="y", segments=40), mat("cloth", spec.get("hat_band", "2A2A2A")),
              transform=rotate_about(piv, "Z", -14))
    elif hat == "peaked":
        h.add(cyl(V(0, top + ch / 2 - 0.0025, 0), hw * 1.06, ch + 0.005, segments=40, bevel=0.006), hm)
        h.add(cyl(V(0, top + ch + 0.003, 0.01), hw * 1.14, 0.012, segments=40, bevel=0.004), hm)
        h.add(ellipsoid(V(0, top - 0.004, -hd * 0.95), (hw * 0.85, 0.008, 0.05), segments=32), mat("leather", "2A1A10"))
        h.add(cyl(V(0, top + ch * 0.55, -hw * 1.06 - 0.002), 0.014, 0.004, axis="z", segments=20), BRASS)
    elif hat == "tophat":
        h.add(cyl(V(0, top - 0.006, 0), hw * 1.4, 0.006, segments=48, bevel=0.002), hm)
        h.add(lathe([(hw * 0.95, top - 0.004), (hw * 0.9, top + 0.1), (hw * 0.97, top + 0.185), (0.001, top + 0.186)], (0, 0, 0), segments=44), hm)
        h.add(torus(V(0, top + 0.016, 0), hw * 0.95, 0.012, axis="y", segments=40, scale=(1, 1, 1.4)), band)
    elif hat == "porkpie":
        h.add(cyl(V(0, top - 0.004, 0), hw * 1.35, 0.006, segments=48, bevel=0.002), hm)
        h.add(cyl(V(0, top + ch / 2 - 0.001, 0), hw * 0.98, ch + 0.002, segments=44, bevel=0.008), hm)
        h.add(torus(V(0, top + 0.012, 0), hw * 0.99, 0.011, axis="y", segments=40), band)
    elif hat == "beanie":
        lift = FITTED_HATS["beanie"]
        f = sdf.union(sdf.intersect(g.skull(0.010), g.above_rim(lift)),
                      sdf.ellipsoid((0, crown - 0.01, 0.01), (hw * 0.7, 0.03, hd * 0.7)), k=0.02)
        f = sdf.subtract(sdf.displace(f, lambda p: 0.0012 * np.sin(np.arctan2(p[:, 0], p[:, 2]) * 60)), inner, k=0.002)
        h.add(sdf.mesh(f, fit_lo, fit_hi, 0.002, tris=3000), mat("knit", col))
        cuff = sdf.subtract(sdf.intersect(g.skull(0.017), g.above_rim(lift - 0.004), g.below_rim(lift + 0.022), k=0.004), inner)
        h.add(sdf.mesh(cuff, fit_lo, fit_hi, 0.002, tris=1500), mat("knit", shade(col, 0.8)))
    elif hat == "cloche":
        lift = FITTED_HATS["cloche"]
        by = rim + lift - RIM_TILT * 0.09 * g.S[2]
        brim = sdf.warp(sdf.rbox((0, by, 0.008), (hw * 1.2, 0.003, hd * 1.22), 0.003),
                        lambda p: p - np.stack([0 * p[:, 0], -6.0 * (p[:, 0] ** 2 + p[:, 2] ** 2) - RIM_TILT * p[:, 2], 0 * p[:, 0]], 1))
        brim = sdf.intersect(brim, sdf.ellipsoid((0, by + 0.025, 0.008), (hw * 1.2, 0.11, hd * 1.22)))
        f = sdf.subtract(sdf.union(sdf.intersect(g.skull(0.010), g.above_rim(lift)), brim, k=0.006), inner, k=0.002)
        h.add(sdf.mesh(f, fit_lo, fit_hi, 0.002, tris=3000), hm)
        bf = sdf.subtract(sdf.intersect(g.skull(0.0125), g.above_rim(lift + 0.008), g.below_rim(lift + 0.02)), inner)
        h.add(sdf.mesh(bf, fit_lo, fit_hi, 0.0015, tris=800), band)
    elif hat == "headscarf":
        f = sdf.union(sdf.ellipsoid((0, top - 0.03, 0.012), (hw * 1.14, hh * 0.86, hd * 1.16)), g.skull(0.011), k=0.01)
        f = sdf.subtract(f, sdf.ellipsoid((0, hc.y - 0.06, -hd * 0.9), (hw * 0.9, hh * 1.2, hd * 0.6)), k=0.01)
        f = sdf.subtract(f, sdf.halfspace((0, hc.y - 0.06, 0), (0, 1, 0)))
        nz = sdf.noise(44, 6, 70.0)
        f = sdf.displace(f, lambda p: 0.0018 * nz(p))
        h.add(sdf.mesh(f, (-hw * 1.4, hc.y - 0.15, -hd * 1.4), (hw * 1.4, top + 0.08, hd * 1.4), 0.002, tris=3000), mat("cloth", col))
        h.add(sphere(V(0, hc.y - 0.12, -hd * 0.45), 0.014), mat("cloth", col))
    if not h.empty():
        h.build(parent=head_obj)


# ----------------------------------------------------------------------------- body

BUILD_W = {"slim": 0.88, "average": 1.0, "round": 1.18, "broad": 1.16, "tiny": 0.9, "thin": 0.82, "huge": 1.4, "child": 0.8}


def body_geo(spec):
    b = spec.get("build", "average")
    w = BUILD_W.get(b, 1.0) * (0.92 if is_fem(spec) else 1.0)
    belly = {"round": 1.22, "huge": 1.12}.get(b, 1.0)
    sw = 0.165 * w if not is_fem(spec) else 0.15 * w
    return w, belly, sw


def arm_points(spec):
    w, belly, sw = body_geo(spec)
    out = []
    for sx in (-1, 1):
        sh = np.array((sx * sw, 1.355, 0.012))
        el = np.array((sx * (sw + 0.03 * belly), 1.105, 0.03))
        wr = np.array((sx * (sw + 0.022 * belly), 0.875, -0.022))
        out.append((sh, el, wr))
    return out


def torso_field(spec):
    w, belly, sw = body_geo(spec)
    fem = is_fem(spec)
    parts = [
        sdf.ellipsoid((0, 1.27, 0.005), (0.15 * w, 0.17, 0.1 * w * (1.08 if fem else 1.0))),   # chest
        sdf.ellipsoid((0, 1.06, -0.012 * belly), (0.142 * w * belly, 0.2, 0.108 * w * belly)),  # belly
        sdf.ellipsoid((0, 0.88, 0), (0.15 * w * (1.08 if fem else 1.0), 0.12, 0.1 * w)),        # hips
        sdf.ellipsoid((0, 1.395, 0.012), (0.085 * w, 0.055, 0.06)),                            # trapezius
    ]
    for sx in (-1, 1):                                                                         # sloping shoulders
        parts.append(sdf.capsule((sx * 0.045, 1.405, 0.012), (sx * sw, 1.36, 0.012), 0.045 * w, 0.052 * w))
    torso = sdf.union(*parts, k=0.05)
    arms = [sdf.chain([sh, el, wr], [0.05 * w, 0.044 * w, 0.039 * w]) for sh, el, wr in arm_points(spec)]
    return torso, arms


def wrinkles(seed, amp):
    nz = sdf.noise(seed, 7, 45.0)
    fine = sdf.noise(seed + 1, 5, 140.0)
    return lambda p: amp * nz(p) + amp * 0.35 * fine(p)


def elbow_folds(spec, amp):
    pts = [el for _, el, _ in arm_points(spec)]

    def fn(p):
        acc = np.zeros(len(p))
        for el in pts:
            d2 = np.sum((p - el) ** 2, axis=1)
            acc += np.exp(-d2 / 0.004) * np.sin((p[:, 1] - el[1]) * 220 + p[:, 0] * 40)
        return acc * amp
    return fn


def prism_xy(points):
    """Convex polygon (x, y) extruded through z: intersection of edge half-spaces."""
    pts = [np.array((x, y, 0.0)) for x, y in points]
    cx, cy = np.mean([p[0] for p in pts]), np.mean([p[1] for p in pts])
    hs = []
    for i in range(len(pts)):
        a, b = pts[i], pts[(i + 1) % len(pts)]
        e = b - a
        n = np.array((e[1], -e[0], 0.0))
        if np.dot(np.array((cx, cy, 0.0)) - a, n) > 0:
            n = -n
        hs.append(sdf.halfspace(a, n))
    return sdf.intersect(*hs)


def build_body(spec):
    coat_hex = spec["coat"]
    coat_m = mat(spec.get("coat_mat", "cloth"), coat_hex)
    w, belly, sw = body_geo(spec)
    torso, arms = torso_field(spec)
    hem = 0.42 if spec.get("long_coat") else 0.76
    coat = sdf.union(torso, *arms, k=0.022)
    if spec.get("long_coat"):
        coat = sdf.union(coat, sdf.capsule((0, 0.86, 0), (0, hem + 0.02, 0.005), 0.15 * w * belly, 0.165 * w * belly), k=0.04)
    coat = sdf.intersect(coat, sdf.halfspace((0, hem, 0), (0, -1, 0)), k=0.004)
    # open the front: a V down to the top button, showing the shirt
    vy = 1.25
    front = -0.08 * w
    v = prism_xy([(-0.07 * w, 1.47), (0.07 * w, 1.47), (0.0, vy)])
    v_cut = sdf.intersect(v, sdf.halfspace((0, 0, front * 0.3), (0, 0, 1)))
    neck_hole = sdf.capsule((0, 1.38, 0.01), (0, 1.55, 0.005), 0.05)
    coat_shell = sdf.subtract(coat, v_cut, neck_hole, k=0.004)
    # lapels: a raised layer folded back along each side of the V
    lapels = []
    if not spec.get("no_lapels"):
        for sx in (-1, 1):
            tri = prism_xy([(sx * 0.0, vy), (sx * 0.075 * w, 1.465), (sx * 0.125 * w, 1.37), (sx * 0.035 * w, 1.24)] if sx > 0 else
                           [(sx * 0.0, vy), (sx * 0.035 * w, 1.24), (sx * 0.125 * w, 1.37), (sx * 0.075 * w, 1.465)])
            lapels.append(sdf.intersect(sdf.offset(torso, 0.006), tri, sdf.halfspace((0, 0, -0.02), (0, 0, 1)), k=0.002))
        coat_shell = sdf.union(coat_shell, *lapels, k=0.0015)
    coat_shell = sdf.displace(coat_shell, wrinkles(5, 0.0016))
    coat_shell = sdf.displace(coat_shell, elbow_folds(spec, 0.0022))
    body = Model("Body")
    lo, hi = (-0.32 * w * belly - 0.06, hem - 0.01, -0.22 * w * belly), (0.32 * w * belly + 0.06, 1.5, 0.18 * w)
    if spec.get("cuffs"):
        cuffs = sdf.union(*[sdf.sphere(wr + np.array((0, 0.035, 0.008)), 0.05) for _, _, wr in arm_points(spec)])
        coat_prim, cuff_prim = sdf.mesh(coat_shell, lo, hi, STEP_BODY, smooth=1, tris=12000, regions=[cuffs])
        body.add(cuff_prim, mat("cloth", spec["cuffs"]))
    else:
        coat_prim = sdf.mesh(coat_shell, lo, hi, STEP_BODY, smooth=1, tris=12000)
    body.add(coat_prim, coat_m)
    # shirt in the V, with a collar
    shirt_hex = spec.get("shirt", "EDE6D6")
    shirt_m = mat("cloth", shirt_hex)
    shirt = sdf.intersect(sdf.offset(torso, -0.002), sdf.offset(v, 0.012), sdf.halfspace((0, 1.44, 0), (0, 1, 0)))
    shirt = sdf.union(shirt, sdf.subtract(sdf.capsule((0, 1.4, 0.012), (0, 1.47, 0.008), 0.058, 0.054),
                                         sdf.capsule((0, 1.38, 0.012), (0, 1.5, 0.008), 0.05, 0.048)), k=0.004)
    body.add(sdf.mesh(shirt, (-0.12, 1.15, -0.16), (0.12, 1.5, 0.1), 0.0025, smooth=1, tris=1500), shirt_m)
    for sx in (-1, 1):
        body.add(hull([(sx * 0.004, 1.455, -0.052), (sx * 0.05, 1.45, -0.044), (sx * 0.03, 1.405, -0.07), (sx * 0.006, 1.425, -0.06)], bevel=0.002), shirt_m)
    if spec.get("tie"):
        wide = 1.5 if spec.get("tie_wide") else 1.0
        z0 = -0.07
        tf = sdf.union(sdf.rbox((0, 1.425, z0 - 0.006), (0.011, 0.012, 0.007), 0.004),
                       sdf.intersect(sdf.offset(torso, 0.003), prism_xy([(-0.009, 1.42), (0.009, 1.42), (0.021 * wide, 1.22), (0.0, 1.195), (-0.021 * wide, 1.22)])), k=0.003)
        body.add(sdf.mesh(tf, (-0.06, 1.17, -0.17), (0.06, 1.45, -0.03), 0.0018, tris=600), mat("cloth", spec["tie"]))
    if spec.get("bowtie"):
        bt = mat("cloth", spec["bowtie"])
        bf = sdf.union(sdf.ellipsoid((0, 1.428, -0.074), (0.007, 0.008, 0.006)),
                       *[sdf.capsule((0, 1.428, -0.072), (sx * 0.032, 1.428, -0.064), 0.006, 0.012) for sx in (-1, 1)], k=0.004)
        body.add(sdf.mesh(bf, (-0.06, 1.39, -0.1), (0.06, 1.47, -0.04), 0.0012, tris=500), bt)
    if spec.get("scarf"):
        sc = mat("knit", spec["scarf"])
        tor = lambda p: np.sqrt((np.sqrt(p[:, 0] ** 2 + (p[:, 2] / 1.05) ** 2) - 0.072) ** 2 + ((p[:, 1] - 1.435) / 1.25) ** 2) - 0.028
        tail = sdf.chain([np.array((0.04, 1.42, -0.085)), np.array((0.055, 1.3, -0.118)), np.array((0.05, 1.17, -0.122))], [0.022, 0.02, 0.019])
        tail = sdf.intersect(tail, sdf.rbox((0.05, 1.3, -0.12), (0.03, 0.2, 0.009), 0.004))
        f = sdf.union(tor, tail, k=0.01)
        f = sdf.displace(f, lambda p: 0.0016 * np.sin(np.arctan2(p[:, 0], p[:, 2]) * 70) + 0.0012 * np.sin(p[:, 1] * 500))
        body.add(sdf.mesh(f, (-0.13, 1.1, -0.16), (0.13, 1.5, 0.12), 0.0025, tris=2500), sc)
    for k in range(spec.get("buttons", 3)):
        y = vy - 0.015 - k * 0.085
        if y < hem + 0.04:
            break
        x = 0.035 * w if spec.get("double") else 0.0
        z = surface_z(coat_shell, x, y, z0=-0.3, z1=0.1) or -0.11
        bm = mat("brass", "C9A15A") if spec.get("brass_buttons") else mat("plastic", "2A2420")
        body.add(cyl(V(x, y, z - 0.001), 0.0095, 0.004, axis="z", segments=20, bevel=0.0015), bm)
    if spec.get("stole"):
        st = mat("fur", spec["stole"])
        nzs = sdf.noise(51, 7, 160.0)
        tor = lambda p: np.sqrt((np.sqrt(p[:, 0] ** 2 + (p[:, 2] / 0.8) ** 2) - 0.122 * w) ** 2 + ((p[:, 1] - 1.415) / 0.8) ** 2) - 0.03
        f = sdf.intersect(sdf.displace(tor, lambda p: 0.0025 * nzs(p)), sdf.offset(torso, 0.05))
        body.add(sdf.mesh(f, (-0.25, 1.3, -0.2), (0.25, 1.52, 0.2), 0.003, tris=3000), st)
    if spec.get("flower"):
        z = surface_z(coat_shell, -0.095 * w, 1.33) or -0.1
        petals = sdf.union(*[sdf.ellipsoid((-0.095 * w + 0.008 * math.cos(a), 1.33 + 0.008 * math.sin(a), z - 0.006), (0.008, 0.008, 0.004))
                             for a in np.linspace(0, math.tau, 6, endpoint=False)], k=0.002)
        body.add(sdf.mesh(petals, (-0.14, 1.29, z - 0.03), (-0.05, 1.37, z + 0.02), 0.001, tris=300), mat("cloth", spec["flower"]))
        body.add(sphere(V(-0.095 * w, 1.33, z - 0.01), 0.004), mat("cloth", "F2E2A0"))
    if spec.get("badge"):
        body.add(cyl((0.1, 1.3, -0.11), 0.022, 0.006, axis="z", segments=16), BRASS)
    if spec.get("satchel"):
        body.add(sweep([(-0.16 * w, 1.42, -0.04), (0.0, 1.12, -0.13), (0.17 * w, 0.95, -0.09)], 0.012, segments=8, flat=0.3), mat("leather", spec["satchel"]))
        body.add(cbox((0.18 * w, 0.92, -0.07), (0.05, 0.15, 0.19), bevel=0.012), mat("leather", spec["satchel"]))
    if spec.get("flour"):
        nzf = sdf.noise(61, 5, 200.0)
        dust = sdf.intersect(sdf.offset(coat_shell, 0.0006), sdf.union(*[sdf.sphere(c, 0.03) for c in ((-0.15, 1.33, -0.05), (0.16, 1.1, -0.05))]),
                             lambda p: 0.0025 - nzf(p) * 0.004)
        try:
            body.add(sdf.mesh(dust, (-0.2, 1.08, -0.2), (0.2, 1.45, 0.05), 0.0025, tris=800), mat("cloth", "EEE8DC"))
        except ValueError:
            pass
    if spec.get("apron"):
        ap = sdf.intersect(sdf.offset(torso, 0.012), prism_xy([(-0.13 * w, 1.27), (0.13 * w, 1.27), (0.16 * w * belly, 0.72), (-0.16 * w * belly, 0.72)]),
                           sdf.halfspace((0, 0, -0.02), (0, 0, 1)), k=0.003)
        ap = sdf.displace(ap, wrinkles(7, 0.0012))
        body.add(sdf.mesh(ap, (-0.3, 0.7, -0.25), (0.3, 1.3, 0.05), 0.003, tris=1500), mat("cloth", spec["apron"]))
    # legs, skirt and shoes
    if spec.get("skirt"):
        sk = sdf.capsule((0, 0.86, 0), (0, 0.4, 0), 0.15 * w * belly, 0.2 * w * belly)
        sk = sdf.intersect(sk, sdf.halfspace((0, 0.38, 0), (0, -1, 0)), sdf.halfspace((0, 0.95, 0), (0, 1, 0)))
        sk = sdf.displace(sk, lambda p: 0.004 * np.sin(np.arctan2(p[:, 0], p[:, 2]) * 9) * np.clip((0.86 - p[:, 1]) * 3, 0, 1))
        body.add(sdf.mesh(sk, (-0.3, 0.36, -0.3), (0.3, 0.97, 0.3), STEP_BODY, tris=2000), mat("cloth", spec["skirt"]))
        legs = sdf.union(*[sdf.capsule((sx * 0.06, 0.45, 0), (sx * 0.058, 0.07, 0.005), 0.042, 0.03) for sx in (-1, 1)])
        body.add(sdf.mesh(legs, (-0.15, 0.03, -0.08), (0.15, 0.5, 0.08), STEP_BODY, tris=1200), mat("cloth", spec.get("stockings", "3A2E2A")))
    else:
        tr = sdf.union(*[sdf.capsule((sx * 0.078 * w, 0.86, 0), (sx * 0.08 * w, 0.065, 0.005), 0.072 * w, 0.056) for sx in (-1, 1)], k=0.02)
        tr = sdf.displace(tr, wrinkles(9, 0.0018))
        body.add(sdf.mesh(tr, (-0.25, 0.04, -0.14), (0.25, 0.9, 0.14), STEP_BODY, tris=2500),
                 mat("cloth", spec.get("trousers", shade(coat_hex, 0.7))))
    for sx in (-1, 1):
        x = sx * (0.06 if spec.get("skirt") else 0.08 * w)
        shoe = sdf.union(sdf.ellipsoid((x, 0.04, -0.035), (0.045, 0.038, 0.115)), sdf.rbox((x, 0.012, 0.0), (0.042, 0.012, 0.1), 0.008), k=0.01)
        shoe = sdf.intersect(shoe, sdf.halfspace((0, 0.0, 0), (0, -1, 0)))
        body.add(sdf.mesh(shoe, (x - 0.07, -0.01, -0.17), (x + 0.07, 0.1, 0.13), 0.003, tris=600), mat("leather", spec.get("shoes", "2A1A12")))
    return body


# ----------------------------------------------------------------------------- hands

def hand_field(c, sx, spec):
    """A relaxed hand hanging at the side: palm facing the thigh (+-x), fingers down and curled in."""
    s = 0.9 if spec.get("build") == "child" else (0.93 if is_fem(spec) else 1.0)
    s *= {"huge": 1.12, "broad": 1.06}.get(spec.get("build"), 1.0)
    c = np.asarray(c)

    def P(x, y, z):
        return c + np.array((sx * x, y, z)) * s

    palm = sdf.rbox(P(0, 0.005, 0), np.array((0.012, 0.042, 0.04)) * s, 0.011 * s)
    wrist = sdf.capsule(P(0, 0.04, 0.0), P(0, 0.075, 0.003), 0.024 * s, 0.026 * s)
    parts = [palm, wrist]
    for i, (z, ln) in enumerate(((-0.027, 0.045), (-0.009, 0.05), (0.009, 0.047), (0.026, 0.038))):
        r0 = 0.0085 * s - i * 0.0004
        k0 = P(0.0, -0.034, z)
        k1 = P(-0.008, -0.034 - ln * 0.45, z * 1.02)
        k2 = P(-0.02, -0.034 - ln * 0.8, z * 1.04)
        k3 = P(-0.032, -0.034 - ln * 0.95, z * 1.05)
        parts.append(sdf.chain([k0, k1, k2, k3], [r0, r0 * 0.92, r0 * 0.82, r0 * 0.72]))
    th = [P(-0.006, 0.02, -0.03), P(-0.016, -0.005, -0.046), P(-0.024, -0.03, -0.05), P(-0.03, -0.045, -0.046)]
    parts.append(sdf.chain(th, [0.011 * s, 0.0095 * s, 0.0085 * s, 0.0075 * s]))
    return sdf.union(*parts, k=0.006 * s)


def build_hands(root, spec):
    skin = mat("leather" if spec.get("gloves") else "skin", spec.get("gloves", spec["skin"]))
    for name, (sh, el, wr), sx in zip(("HandL", "HandR"), arm_points(spec), (-1, 1)):
        c = wr + np.array((0, -0.06, -0.006))
        f = hand_field(c, sx, spec)
        hnd = Model(name)
        hnd.add(sdf.mesh(f, c - 0.1, c + 0.1, 0.0016, smooth=1, tris=2500), skin)
        hnd.build(origin=tuple(c), parent=root)


def build_character(cid, spec):
    """Every part is a direct child of the root (FBX hierarchies under moved parents come out
    mangled); the game re-parents Head under Body and the face parts under Head on load."""
    root = empty(cid)
    body = build_body(spec)
    body.build(origin=(0, 0.95, 0), parent=root)
    head, g, head_f = build_head(spec)
    head.build(origin=(0, NECK_Y, 0), parent=root)
    build_face(root, spec, g, head_f)
    build_hat(root, spec, g)
    build_hands(root, spec)
    return root


# ----------------------------------------------------------------------------- the cast

CAST = {
    "gus": dict(build="slim", skin="E6B08A", hair="ginger", hair_col="B5562A", ears=1.3, nose="button", rosy="E58C70",
                coat="22304A", coat_mat="cloth", trousers="1E2638", hat="porter", hat_col="8A2420", hat_band="1A1A1A",
                brass_buttons=True, buttons=4, cuffs="8A2420", tie="1A1A1A"),
    "walter": dict(lids=0.18, build="round", skin="E8B48E", hair="bald", hair_col="9A928A", moustache="walrus", moustache_col="C8C0B4",
                   nose="big", rosy="E89080", coat="6A5038", hat="flatcap", hat_col="7A6448", head=(0.115, 0.125, 0.115),
                   flour=True, apron="EDE6D6", shirt="E8E2D4", brow=1.3, brow_col="B8B0A4", buttons=0),
    "clementine": dict(build="slim", skin="F2CCAE", hair="bob", hair_col="1E1614", nose="button", glasses="round",
                       coat="C8A040", coat_mat="knit", shirt="F4EEE2", hat="beret", hat_col="7A2232", skirt="3A3A4A",
                       eye_size=1.1, mouth_w=0.85, brow_angle=-6, buttons=4),
    "reggie": dict(lids=0.38, build="average", skin="DDAA84", hair="slick", hair_col="141010", moustache="pencil", nose="long",
                   coat="4A5468", tie="E07A20", tie_wide=True, hat="trilby", hat_col="6A5038", hat_band="1A1A1A",
                   smile=True, mouth_w=1.1, brow_angle=14, flower="C02020", buttons=2),
    "odile": dict(build="tiny", skin="EEC8AC", hair="bun", hair_col="E8E4DE", nose="button", rosy="E8A0A0", coat="6A3A5A",
                  hat="feathered", hat_col="4A2A5A", feather="E8D8F0", stole="D8CCB8", skirt="4A2A3A", eye_size=0.9,
                  smile=True, buttons=0, height=0.86, lids=0.2),
    "bramble": dict(build="broad", skin="E2AE8A", hair="short", hair_col="4A3A2A", moustache="huge", moustache_col="5A4030",
                    nose="big", coat="1C2440", hat="custodian", hat_col="1C2440", brass_buttons=True, buttons=5, rosy="E09080",
                    brow=1.4, jaw=1.1),
    "rosalind": dict(build="slim", skin="F2D0B8", hair="bob", hair_col="B07040", nose="button", coat="8AC8B0", hat="cloche",
                     hat_col="5AA890", skirt="5A7A70", ribbon="C03030", smile=True, eye_size=1.08, buttons=3, scarf="C03030", iris="5A7A5A"),
    "cecily": dict(build="slim", skin="F2D0B8", hair="bob", hair_col="B07040", nose="button", coat="8AC8B0", hat="cloche",
                   hat_col="5AA890", skirt="5A7A70", smile=True, eye_size=1.08, buttons=3, scarf="3050B0", iris="5A7A5A"),
    "haversham": dict(build="round", skin="F0C0A0", hair="bald", hair_col="6A5A4A", glasses="round", glasses_brass=False,
                      nose="round", coat="3A3A3E", bowtie="8A2A5A", shirt="F4F0E8", rosy="F0A090", brow_angle=-12, mouth_w=0.8),
    "vell": dict(lids=0.5, build="thin", skin="D0CCC8", hair="slick", hair_col="5A5A5E", nose="long", coat="5A5C60", long_coat=True,
                 hat="bowler", hat_col="4A4C50", hat_band="2A2C30", gloves="6A6C70", smile=True, mouth_w=1.3,
                 eye_size=0.85, head=(0.095, 0.135, 0.1), brow_angle=18, shirt="DCDCDC", tie="3A3C40", buttons=2, height=1.1,
                 iris="7A8088", jaw=0.92),
    "pip": dict(build="child", skin="7A4A30", hair="braids", hair_col="141010", ribbon="E0C030", nose="button", coat="2A3A6A",
                skirt="3A3A3A", satchel="7A4A28", smile=True, eye_size=1.15, head=(0.1, 0.115, 0.1), height=0.74, tie="C0302A",
                buttons=2, iris="2A1A10"),
    "dora": dict(lids=0.12, build="broad", skin="E0A884", hair="curls", hair_col="8A3A22", nose="round", coat="8A6A3A", hat="flatcap",
                 hat_col="5A6A3A", rosy="E09070", smile=True, mouth_w=1.15, scarf="E0C040", buttons=3, fem=True),
    "harriet": dict(build="slim", skin="F0C8A4", hair="short", hair_col="6A4028", nose="button", coat="A8B8C8", coat_mat="knit",
                    hat="headscarf", hat_col="C86A5A", skirt="4A5A6A", eye_size=1.08, buttons=4),
    "crane": dict(lids=0.42, build="average", skin="ECC0A0", hair="slick", hair_col="2A2420", nose="hook", glasses="monocle",
                  coat="5A2238", coat_mat="velvet", scarf="D8C8A0", brow_angle=16, mouth_w=0.9, buttons=2),
    "thomas": dict(build="slim", skin="E8BC98", hair="short", hair_col="6A4A2A", nose="round", coat="7A6A4A", hat="flatcap",
                   hat_col="5A4A34", flower="E04060", tie="4A3A2A", eye_size=1.03, smile=True, buttons=3),
    "penhallow": dict(lids=0.3, build="average", skin="D8C8BC", hair="short", hair_col="4A3A2A", moustache="pencil", nose="long",
                      coat="6A6A48", hat="peaked", hat_col="5A5A3C", brass_buttons=True, buttons=4, tie="5A5A3C"),
    "sid": dict(build="huge", skin="D89870", hair="short", hair_col="2A1E14", nose="big", coat="2A2A2A", hat="beanie",
                hat_col="3A4A6A", rosy="D08060", smile=True, mouth_w=1.15, head=(0.12, 0.125, 0.115), buttons=4, jaw=1.12),
    "lou": dict(lids=0.0, build="average", skin="6A3E28", hair="short", hair_col="141010", beard="goatee", nose="round", glasses="shades",
                coat="2A2E4A", hat="porkpie", hat_col="1A1A1A", hat_band="C0A040", tie="C0A040", smile=True, buttons=2),
    "spratt": dict(lids=0.3, build="thin", skin="E4D0BC", hair="wild", hair_col="3A3428", nose="long", coat="1E1E22", bowtie="1E1E22",
                   shirt="E0D8C8", brow_angle=-10, mouth_w=0.85, eye_size=0.9),
    "edie": dict(build="slim", skin="F6D8C4", hair="platinum", hair_col="F0E4B8", nose="button", coat="B83A5A",
                 hat="pillbox", hat_col="1A1A1A", stole="F0E8E0", skirt="7A2A3A", gloves="F0F0F0", smile=True,
                 eye_size=1.08, brow_angle=12, buttons=0, lips="B02A3A", iris="4A6A8A"),
    "plum": dict(lids=0.55, build="round", skin="D4CCC4", hair="bald", hair_col="D8D4CC", beard="muttonchops", moustache_col="D8D4CC",
                 nose="big", coat="2A2A30", long_coat=True, hat="tophat", hat_col="1E1E22", brass_buttons=False, buttons=4),
    "lark": dict(build="average", skin="ECC4A4", hair="wild", hair_col="C8C4BC", glasses="owl", nose="hook", coat="7A6A4A",
                 hat="flatcap", hat_col="6A5A40", brow=1.5, brow_col="E0DCD4", bowtie="3A6A4A", eye_size=1.1, buttons=3),
    "agnes_old": dict(build="slim", skin="EEC8B0", hair="bun", hair_col="D8D4D0", nose="button", glasses="round", coat="5A6A7A",
                      coat_mat="knit", skirt="3A4A5A", smile=True, rosy="E8A8A0", eye_size=1.0, buttons=5, lids=0.2, iris="5A6A78"),
    "thomas_old": dict(build="slim", skin="E8C0A0", hair="bald", hair_col="D8D4CC", nose="round", coat="6A5A44", hat="flatcap",
                       hat_col="5A4A34", flower="E04060", moustache="walrus", moustache_col="D8D4CC", smile=True, buttons=3, lids=0.25),
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
    face_only = "--faces" in args
    for cid, spec in CAST.items():
        if only and cid not in only:
            continue
        reset_scene()
        root = build_character(cid, spec)
        if not face_only:
            export_fbx(os.path.join(OUT, cid + ".fbx"), [root])
        if prev:
            g = HeadGeo(spec)
            mouth = bpy.data.objects.get("Mouth")
            if mouth:
                mouth.scale.z = 0.25   # the game's resting mouth (Unity y is Blender z)
            setup_preview((0, float(g.C[1]) - 0.01, 0), 0.15, yaw=24, pitch=6, resolution=720)
            render(os.path.join(prev, f"face_{cid}.png"))
            if not face_only:
                for o in [o for o in bpy.data.objects if o.type == "CAMERA" or o.type == "LIGHT"]:
                    bpy.data.objects.remove(o)
                setup_preview((0, 1.36, 0), 0.34, yaw=12, pitch=4)
                render(os.path.join(prev, f"person_{cid}.png"))
                if "--full" in args:
                    for o in [o for o in bpy.data.objects if o.type == "CAMERA" or o.type == "LIGHT"]:
                        bpy.data.objects.remove(o)
                    setup_preview((0, 0.85, 0), 0.85, yaw=30, pitch=8)
                    render(os.path.join(prev, f"full_{cid}.png"))
        print("built", cid)


if __name__ == "__main__":
    main()
