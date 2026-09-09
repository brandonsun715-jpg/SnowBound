"""
What grows on a mountain that is not a full-sized conifer.

A forest that is only trees has one silhouette in it and reads as a
plantation. What makes an alpine forest look alive is the stuff between
the trees: krummholz scrub at the tree line, bare winter bushes in the
gaps, blown-down trunks going grey, and the stumps of the ones that went
before.

All of it is batched with the forest, so everything here is fifty to a
hundred and fifty triangles and shares one texture.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import kit
import shapes
import surfaces as S

NAME = "Flora"

SHRUB_HEIGHT = 1.6
FALLEN_LENGTH = 7.0


def materials():
    return {
        'bark': S.bark("FloraBark"),
        'dead': S.bark("FloraDead", colour=(0.145, 0.125, 0.100), scale=18.0),
        'needle': S.foliage("FloraNeedle", (0.036, 0.058, 0.042)),
        'twig': S.foliage("FloraTwig", (0.115, 0.092, 0.068), scale=40.0),
        'snow': S.settled_snow("FloraSnow"),
    }


def star(radius, z, count=8, notch=0.55, phase=0.0):
    return [(radius * (1.0 if i % 2 == 0 else notch) * math.cos(phase + 2 * math.pi * i / count),
             radius * (1.0 if i % 2 == 0 else notch) * math.sin(phase + 2 * math.pi * i / count),
             z) for i in range(count)]


def krummholz(mats, rnd):
    """
    Scrub at the tree line: a conifer that has been beaten flat by wind
    and never got above the snow it shelters under.
    """
    parts = []
    caps = []

    for i in range(3):
        lean = rnd.uniform(0.0, math.pi * 2.0)
        off = rnd.uniform(0.10, 0.34)
        at = (math.cos(lean) * off, math.sin(lean) * off)
        high = SHRUB_HEIGHT * rnd.uniform(0.55, 1.0)

        clump = kit.loft("Scrub", [
            [(x + at[0], y + at[1], z) for x, y, z in star(0.30, 0.02, phase=lean)],
            [(x + at[0], y + at[1], z) for x, y, z in star(0.52, high * 0.35, phase=lean)],
            [(x + at[0], y + at[1], z) for x, y, z in star(0.34, high * 0.75, phase=lean + 0.4)],
            [(x + at[0], y + at[1], z) for x, y, z in star(0.05, high, phase=lean)],
        ], closed_ends=True, mat=mats['needle'])
        parts.append(kit.smooth(clump, angle=46))

        cap = kit.loft("ScrubSnow", [
            [(x + at[0], y + at[1], z) for x, y, z in star(0.40, high * 0.42, phase=lean)],
            [(x + at[0], y + at[1], z) for x, y, z in star(0.24, high * 0.66, phase=lean)],
            [(x + at[0], y + at[1], z) for x, y, z in star(0.04, high * 0.86, phase=lean)],
        ], closed_ends=True, mat=mats['snow'])
        caps.append(kit.smooth(cap, angle=44))

    return kit.join(parts, "ShrubA"), kit.join(caps, "SnowShrubA")


def bush(mats, rnd):
    """A bare winter bush: twigs and nothing else, with snow caught in it."""
    parts = []

    for i in range(9):
        angle = rnd.uniform(0.0, math.pi * 2.0)
        reach = rnd.uniform(0.25, 0.55)
        high = rnd.uniform(0.5, 1.1)

        twig = kit.loft("Twig", shapes.sweep(None, [
            (0.0, 0.0, 0.0),
            (math.cos(angle) * reach * 0.4, math.sin(angle) * reach * 0.4, high * 0.55),
            (math.cos(angle) * reach, math.sin(angle) * reach, high)],
            up=(0, 0, 1),
            sections=[shapes.oval(0.030, 0.030, 5), shapes.oval(0.018, 0.018, 5),
                      shapes.oval(0.006, 0.006, 5)]),
            closed_ends=True, mat=mats['twig'])
        parts.append(kit.smooth(twig, angle=50))

    return kit.join(parts, "ShrubB")


def fallen(mats, rnd):
    """
    A blown-down trunk with its root plate torn up, going grey, with snow
    lying along the top of it.
    """
    parts = []
    caps = []
    half = FALLEN_LENGTH * 0.5

    trunk = kit.loft("Fallen", [
        [(x, -half, z + 0.30) for x, _, z in shapes.circle(0.26, 0.0, count=9)],
        [(x, -half * 0.4, z + 0.32) for x, _, z in shapes.circle(0.23, 0.0, count=9)],
        [(x, half * 0.4, z + 0.28) for x, _, z in shapes.circle(0.19, 0.0, count=9)],
        [(x, half, z + 0.24) for x, _, z in shapes.circle(0.13, 0.0, count=9)],
    ], closed_ends=True, mat=mats['dead'])
    parts.append(kit.smooth(trunk, angle=48))

    plate = kit.loft("RootPlate", [
        [(x * 1.15, -half - 0.12, z * 1.35 + 0.42) for x, _, z in shapes.circle(0.70, 0.0, count=10)],
        [(x * 1.10, -half - 0.42, z * 1.30 + 0.40) for x, _, z in shapes.circle(0.62, 0.0, count=10)],
    ], closed_ends=True, mat=mats['dead'])
    parts.append(kit.smooth(plate, angle=40))

    for i in range(3):
        y = -half * 0.5 + i * half * 0.55
        angle = rnd.uniform(0.6, 2.4) * (1 if i % 2 else -1)

        stub = kit.rod("Stub", (0, y, 0.30),
                       (math.cos(angle) * 0.75, y + math.sin(angle) * 0.35, 0.30 + rnd.uniform(0.1, 0.5)),
                       0.055, verts=6, mat=mats['dead'])
        parts.append(kit.smooth(stub))

    cap = kit.loft("FallenSnow", [
        [(x * 1.02, -half + 0.10, z * 0.38 + 0.46) for x, _, z in shapes.circle(0.24, 0.0, count=9)],
        [(x * 1.02, half - 0.30, z * 0.38 + 0.40) for x, _, z in shapes.circle(0.17, 0.0, count=9)],
    ], closed_ends=True, mat=mats['snow'])
    caps.append(kit.smooth(cap, angle=44))

    return kit.join(parts, "Fallen"), kit.join(caps, "SnowFallen")


def stump(mats, rnd):
    """What is left when one came down years ago: a jagged broken column."""
    top = []
    for i in range(9):
        angle = 2 * math.pi * i / 9
        radius = 0.30 * rnd.uniform(0.86, 1.06)
        top.append((radius * math.cos(angle), radius * math.sin(angle),
                    0.55 + rnd.uniform(-0.16, 0.16)))

    ob = kit.loft("Stump", [
        shapes.circle(0.44, 0.0, count=9),
        shapes.circle(0.36, 0.22, count=9),
        top,
    ], closed_ends=True, mat=mats['dead'])

    return kit.smooth(ob, angle=48)


def build(mats=None):
    mats = mats or materials()
    rnd = random.Random(90909)

    scrub, scrub_snow = krummholz(mats, rnd)
    twiggy = bush(mats, rnd)
    log, log_snow = fallen(mats, rnd)
    post = stump(mats, rnd)

    parts = [scrub, scrub_snow, twiggy, log, log_snow, post]

    for i, part in enumerate(parts):
        kit.lay(part, (i * 3.0, 0, 0))

    return parts


def make():
    kit.reset()
    return build()
