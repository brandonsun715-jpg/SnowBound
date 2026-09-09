"""
Three conifers, and the snow sitting on them.

There are eighteen hundred trees on this mountain and they are welded into
a handful of meshes, so this is not the place for a beautiful tree. It is
the place for a cheap one with a good outline: about three hundred
triangles, built as a trunk and four or five tiers of drooping branches
rather than stacked cones, because the ragged edge of a conifer against
snow is the whole of what reads at fifty metres.

Three species in one file and one texture: a narrow spruce, a broad fir
and a scrappy pine. MountainProps picks one per tree, so a forest is a
forest instead of one tree stamped nine hundred times.

Each is drawn ten metres tall and scaled to whatever the mountain asks
for. Snow is a separate object per tree, so it can be batched against its
own material.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import kit
import shapes
import surfaces as S

NAME = "Trees"
HEIGHT = 10.0

SPECIES = [
    # tag, tiers, top, spread, droop, needle colour, trunk radius
    ("A", 6, 0.97, 0.190, 0.42, (0.031, 0.062, 0.048), 0.115),
    ("B", 5, 0.93, 0.245, 0.30, (0.052, 0.078, 0.040), 0.135),
    ("C", 5, 0.99, 0.150, 0.52, (0.043, 0.070, 0.058), 0.100),
]


def materials():
    return {
        'bark': S.bark("TreeBark"),
        'snow': S.settled_snow("TreeSnow"),
        'A': S.foliage("NeedleSpruce", SPECIES[0][5]),
        'B': S.foliage("NeedleFir", SPECIES[1][5]),
        'C': S.foliage("NeedlePine", SPECIES[2][5]),
    }


def star(radius, z, count=10, notch=0.58, phase=0.0):
    """
    A ring with alternate points pulled in.

    A conifer tier lofted from plain circles is a cone, and a cone reads
    as a Christmas decoration. Pulling every other point in gives the
    ragged edge that makes it read as branches, for no extra triangles.
    """
    points = []

    for i in range(count):
        angle = phase + 2 * math.pi * i / count
        reach = radius * (1.0 if i % 2 == 0 else notch)
        points.append((reach * math.cos(angle), reach * math.sin(angle), z))

    return points


def tier(name, radius, bottom, height, mat, phase, notch=0.58, count=10):
    """
    One skirt of branches: widest just above its bottom edge, drooping
    below it, tapering to nothing at the top. Tiers overlap, because a
    spruce is a continuous mass of needles and not a stack of hats.
    """
    ob = kit.loft(name, [
        star(radius * 0.62, bottom - height * 0.10, count, notch * 0.9, phase),
        star(radius, bottom + height * 0.14, count, notch, phase),
        star(radius * 0.58, bottom + height * 0.52, count, notch, phase + 0.18),
        star(radius * 0.10, bottom + height, count, 0.9, phase + 0.3),
    ], closed_ends=True, mat=mat)

    return kit.smooth(ob, angle=46)


def cap(name, radius, bottom, height, mat, phase, count=10):
    """Snow on the upper side of a tier: smaller than the branches it sits
    on, so the needles still show underneath and around it."""
    # Sat on the tier's upper slope and a little proud of it, so it shows
    # from above and from the side rather than hiding inside the branches.
    ob = kit.loft(name, [
        star(radius * 0.76, bottom + height * 0.40, count, 0.62, phase),
        star(radius * 0.54, bottom + height * 0.60, count, 0.66, phase),
        star(radius * 0.10, bottom + height * 0.86, count, 0.90, phase),
    ], closed_ends=True, mat=mat)

    return kit.smooth(ob, angle=44)


def tree(tag, tiers, top, spread, droop, trunk_radius, mats, rnd):
    """A trunk and its overlapping tiers. Returns the tree and its snow."""
    needle = mats[tag]
    limbs = []
    caps = []

    trunk = kit.loft("Trunk" + tag, [
        shapes.circle(trunk_radius * 1.6, 0.00, count=7),
        shapes.circle(trunk_radius, 0.70, count=7),
        shapes.circle(trunk_radius * 0.55, HEIGHT * 0.45, count=7),
        shapes.circle(trunk_radius * 0.12, HEIGHT * top + 0.30, count=7),
    ], closed_ends=True, mat=mats['bark'])
    kit.smooth(trunk, angle=50)
    limbs.append(trunk)

    base = HEIGHT * 0.16
    span = HEIGHT * top - base
    step = span / tiers

    for i in range(tiers):
        share = i / max(1, tiers - 1.0)
        bottom = base + step * i

        # Wide at the bottom, tight at the top, never quite regular.
        radius = HEIGHT * spread * (1.0 - 0.74 * share) * rnd.uniform(0.90, 1.10)
        height = step * rnd.uniform(2.0, 2.5) * (1.0 + droop * 0.4)
        phase = rnd.uniform(0.0, math.pi * 2.0)

        limbs.append(tier("Tier", radius, bottom, height, needle, phase,
                          notch=0.50 + droop * 0.12))

        # Snow only where it would settle and stay: the upper tiers, which
        # nothing above them is sheltering.
        if share > 0.18:
            caps.append(cap("Cap", radius, bottom, height, mats['snow'], phase))

    return kit.join(limbs, "Tree" + tag), kit.join(caps, "Snow" + tag)


def build(mats=None):
    mats = mats or materials()
    rnd = random.Random(20260909)
    parts = []

    for tag, tiers, top, spread, droop, _, trunk_radius in SPECIES:
        wood, snow = tree(tag, tiers, top, spread, droop, trunk_radius, mats, rnd)
        parts += [wood, snow]

    # Stood side by side so they can be looked at together, and so the
    # unwrap has room. The game reads each part by name, never by where it
    # sits in the file.
    for i, part in enumerate(parts):
        kit.lay(part, (0, (i // 2) * 9.0, 0))

    return parts


def make():
    kit.reset()
    return build()
