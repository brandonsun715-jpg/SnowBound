"""
The small things a resort is made of.

None of it is a landmark and none of it is looked at directly, which is
exactly why it matters: a base area with no bins, no benches, no lamps and
no signs reads as a model of a resort rather than a resort. Every piece
here is something that would have been carried up the hill by somebody and
put down for a reason.

One file, one texture, a hundred triangles or so each. Drawn at real size
with their feet at z = 0 so they can be stood on the snow.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import kit
import shapes
import surfaces as S

NAME = "ResortProps"


def materials():
    return {
        'timber': S.timber("PropTimber"),
        'steel': S.galvanised("PropSteel"),
        'paint': S.powder_coat("PropPaint", (0.055, 0.075, 0.105), roughness=0.36, wear=0.5),
        'orange': S.powder_coat("PropOrange", (0.62, 0.22, 0.03), roughness=0.40, wear=0.7),
        'snow': S.settled_snow("PropSnow"),
        'sign': S.plastic("PropSign", (0.86, 0.86, 0.88), roughness=0.30),
        'lamp': S.plastic("PropLamp", (0.90, 0.86, 0.72), roughness=0.18),
    }


def bench(m):
    """A slatted bench outside the lodge, with snow along the back of it."""
    parts = []

    for i in range(4):
        slat = kit.box("Slat", (1.80, 0.095, 0.042), (0, -0.06 + i * 0.115, 0.44),
                       mat=m['timber'])
        kit.bevel(slat, 0.008, segments=2)
        parts.append(slat)

    for i in range(3):
        back = kit.box("Back", (1.80, 0.040, 0.10), (0, 0.26, 0.60 + i * 0.13),
                       rot=(-12, 0, 0), mat=m['timber'])
        kit.bevel(back, 0.008, segments=2)
        parts.append(back)

    for side in (-1, 1):
        leg = kit.box("Leg", (0.055, 0.42, 0.44), (side * 0.78, 0.06, 0.22), mat=m['steel'])
        kit.bevel(leg, 0.008, segments=2)
        parts.append(leg)

        stay = kit.rod("Stay", (side * 0.78, 0.24, 0.42), (side * 0.78, 0.30, 0.92),
                       0.026, verts=8, mat=m['steel'])
        parts.append(kit.smooth(stay))

    cap = kit.box("BenchSnow", (1.76, 0.075, 0.035), (0, 0.245, 0.985), rot=(-12, 0, 0),
                  mat=m['snow'])
    kit.bevel(cap, 0.010, segments=2)
    parts.append(cap)

    return kit.join(parts, "Bench")


def bin_(m):
    """A bin. Every lift queue in the world has one and none of them are new."""
    parts = []

    body = kit.loft("BinBody", [
        [(x, y, 0.02) for x, y, _ in shapes.circle(0.24, 0.0, count=14)],
        [(x, y, 0.30) for x, y, _ in shapes.circle(0.27, 0.0, count=14)],
        [(x, y, 0.86) for x, y, _ in shapes.circle(0.29, 0.0, count=14)],
    ], closed_ends=True, mat=m['paint'])
    parts.append(kit.smooth(body, angle=40))

    lid = kit.loft("BinLid", [
        [(x, y, 0.88) for x, y, _ in shapes.circle(0.31, 0.0, count=14)],
        [(x, y, 0.96) for x, y, _ in shapes.circle(0.26, 0.0, count=14)],
        [(x, y, 0.99) for x, y, _ in shapes.circle(0.10, 0.0, count=14)],
    ], closed_ends=True, mat=m['steel'])
    parts.append(kit.smooth(lid, angle=40))

    band = kit.loft("BinBand", [
        [(x, y, 0.52) for x, y, _ in shapes.circle(0.292, 0.0, count=14)],
        [(x, y, 0.62) for x, y, _ in shapes.circle(0.292, 0.0, count=14)],
    ], closed_ends=False, mat=m['orange'])
    parts.append(kit.smooth(band, angle=40))

    return kit.join(parts, "Bin")


def lamp(m):
    """A path lamp: a tapered post, a shaded head, and snow on the shade."""
    parts = []

    base = kit.loft("LampBase", [
        [(x, y, 0.0) for x, y, _ in shapes.circle(0.16, 0.0, count=12)],
        [(x, y, 0.14) for x, y, _ in shapes.circle(0.12, 0.0, count=12)],
    ], closed_ends=True, mat=m['steel'])
    parts.append(kit.smooth(base, angle=40))

    post = kit.loft("LampPost", [
        [(x, y, 0.10) for x, y, _ in shapes.circle(0.062, 0.0, count=10)],
        [(x, y, 3.30) for x, y, _ in shapes.circle(0.044, 0.0, count=10)],
    ], closed_ends=True, mat=m['paint'])
    parts.append(kit.smooth(post, angle=40))

    shade = kit.loft("LampShade", [
        [(x, y, 3.30) for x, y, _ in shapes.circle(0.075, 0.0, count=12)],
        [(x, y, 3.44) for x, y, _ in shapes.circle(0.30, 0.0, count=12)],
        [(x, y, 3.50) for x, y, _ in shapes.circle(0.30, 0.0, count=12)],
    ], closed_ends=True, mat=m['paint'])
    parts.append(kit.smooth(shade, angle=42))

    glass = kit.loft("LampGlass", [
        [(x, y, 3.28) for x, y, _ in shapes.circle(0.20, 0.0, count=12)],
        [(x, y, 3.36) for x, y, _ in shapes.circle(0.26, 0.0, count=12)],
    ], closed_ends=True, mat=m['lamp'])
    parts.append(kit.smooth(glass, angle=42))

    cap = kit.loft("LampSnow", [
        [(x, y, 3.50) for x, y, _ in shapes.circle(0.29, 0.0, count=12)],
        [(x, y, 3.57) for x, y, _ in shapes.circle(0.16, 0.0, count=12)],
    ], closed_ends=True, mat=m['snow'])
    parts.append(kit.smooth(cap, angle=42))

    return kit.join(parts, "Lamp")


def sign(m):
    """
    A trail sign at the top of a run: a board on two posts, with a snow
    ledge along the top of it.
    """
    parts = []

    for side in (-1, 1):
        post = kit.box("SignPost", (0.10, 0.10, 2.30), (side * 0.62, 0, 1.15),
                       mat=m['timber'])
        kit.bevel(post, 0.010, segments=2)
        parts.append(post)

    board = kit.box("SignBoard", (1.56, 0.055, 0.72), (0, 0, 1.86), mat=m['sign'])
    kit.bevel(board, 0.012, segments=3)
    parts.append(board)

    stripe = kit.box("SignStripe", (1.50, 0.020, 0.14), (0, -0.040, 1.62), mat=m['orange'])
    kit.bevel(stripe, 0.006, segments=2)
    parts.append(stripe)

    ledge = kit.box("SignSnow", (1.58, 0.075, 0.04), (0, 0, 2.24), mat=m['snow'])
    kit.bevel(ledge, 0.010, segments=2)
    parts.append(ledge)

    return kit.join(parts, "Sign")


def fence(m):
    """
    A panel of safety fencing: two rails and a run of slats, in the orange
    everybody's eye reads as "not past here".
    """
    parts = []
    span = 3.0

    for side in (-1, 1):
        post = kit.box("FencePost", (0.075, 0.075, 1.50), (side * span * 0.5, 0, 0.75),
                       mat=m['steel'])
        kit.bevel(post, 0.008, segments=2)
        parts.append(post)

    for z in (0.42, 1.18):
        rail = kit.box("FenceRail", (span, 0.045, 0.055), (0, 0, z), mat=m['orange'])
        kit.bevel(rail, 0.006, segments=2)
        parts.append(rail)

    for i in range(9):
        x = -span * 0.44 + i * (span * 0.88 / 8)
        slat = kit.box("FenceSlat", (0.055, 0.022, 0.86), (x, 0, 0.80), mat=m['orange'])
        kit.bevel(slat, 0.004, segments=2)
        parts.append(slat)

    return kit.join(parts, "Fence")


def pile(m):
    """
    A heap of pushed snow: what a groomer leaves at the edge of everything
    it has been over.
    """
    rings = []

    for i in range(5):
        t = i / 4.0
        radius = 2.4 * math.sqrt(max(0.0, 1.0 - t * t)) + 0.05
        wobble = 1.0 + 0.10 * math.sin(t * 7.0)
        rings.append([(x * wobble, y * (1.0 - 0.25 * t), t * 1.15)
                      for x, y, _ in shapes.circle(radius, 0.0, count=12)])

    ob = kit.loft("Pile", rings, closed_ends=True, mat=m['snow'])
    return kit.smooth(ob, angle=44)


def build(mats=None):
    m = mats or materials()

    parts = [bench(m), bin_(m), lamp(m), sign(m), fence(m), pile(m)]

    for i, part in enumerate(parts):
        kit.lay(part, (i * 5.0, 0, 0))

    return parts


def make():
    kit.reset()
    return build()
