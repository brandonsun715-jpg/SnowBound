"""
Three boulders.

A rock is the easiest thing in the world to get wrong: a sphere with a
noise texture on it reads as a beach ball however good the texture is.
What makes granite look like granite is flat faces meeting at hard edges,
so these are built by pushing an icosphere's vertices out along fracture
planes and leaving the faces flat.

Four hundred of them go on the mountain, batched, so they are about two
hundred triangles each. Drawn two metres across and scaled from there.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import bpy

import kit
import surfaces as S

NAME = "Rocks"
SIZE = 2.0

SHAPES = [
    # tag, subdivisions, fracture planes, roughness of the break, squash, bedding
    ("A", 2, 8, 0.30, (1.00, 0.86, 0.72), 0.0),
    ("B", 2, 6, 0.42, (1.15, 0.62, 0.95), 0.0),
    ("C", 2, 9, 0.26, (0.86, 0.94, 1.10), 0.0),

    # The two that are not boulders. A slab is what a cliff band is made of
    # — broad, flat-topped, bedded — and a spire is the tooth left standing
    # when the rock either side of it went. Both are stood on steep ground at
    # several times this size and buried to the waist, which is how an
    # outcrop reads: rock coming out of the mountain rather than sitting on
    # top of it.
    ("Slab", 2, 7, 0.12, (1.60, 1.25, 1.00), 0.21),
    ("Spire", 2, 8, 0.22, (0.74, 0.82, 1.95), 0.0),
]


def materials():
    return {
        'granite': S.granite("RockGranite"),
        'darker': S.granite("RockSchist", colour=(0.165, 0.168, 0.180), scale=13.0),
    }


def boulder(tag, subdivisions, planes, break_up, squash, bedding, mat, rnd):
    """
    An icosphere cut back to a set of random planes.

    Every vertex is pulled in to whichever plane it is outside of, which
    is what a rock that has split off a cliff actually is: a lump bounded
    by the surfaces it broke along.

    `bedding`, when set, adds two level cuts that distance above and below
    the middle. Squashing a sphere flat gives a lens; cutting it flat gives
    a slab with a top you can stand on, which is what a bedded rock is.
    """
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=SIZE * 0.5)
    ob = bpy.context.object
    ob.name = "Rock" + tag

    cuts = []
    if bedding:
        cuts.append(((0.0, 0.0, 1.0), bedding))
        cuts.append(((0.0, 0.0, -1.0), bedding))

    for _ in range(planes):
        direction = (rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))
        length = math.sqrt(sum(c * c for c in direction)) or 1.0
        cuts.append((tuple(c / length for c in direction),
                     SIZE * 0.5 * rnd.uniform(0.30, 0.46)))

    for vert in ob.data.vertices:
        point = [vert.co.x, vert.co.y, vert.co.z]

        for normal, offset in cuts:
            reach = sum(p * n for p, n in zip(point, normal))
            if reach > offset:
                pull = (reach - offset) * (1.0 - break_up * rnd.uniform(0.0, 1.0))
                point = [p - n * pull for p, n in zip(point, normal)]

        # A whisker of noise on top of the flat faces, so no two are twins.
        wobble = 1.0 + rnd.uniform(-0.05, 0.05)
        vert.co = (point[0] * squash[0] * wobble,
                   point[1] * squash[1] * wobble,
                   point[2] * squash[2] * wobble)

    kit.apply_transform(ob)
    kit.paint(ob, mat)

    # Flat faces, hard edges: that is the whole look.
    for polygon in ob.data.polygons:
        polygon.use_smooth = False

    return ob


def build(mats=None):
    mats = mats or materials()
    rnd = random.Random(4242)
    parts = []

    for i, (tag, subdivisions, planes, break_up, squash, bedding) in enumerate(SHAPES):
        mat = mats['darker'] if i in (1, 4) else mats['granite']
        rock = boulder(tag, subdivisions, planes, break_up, squash, bedding, mat, rnd)

        # Sat on the ground and spread out, for the unwrap and the picture.
        low = min(v.co.z for v in rock.data.vertices)
        kit.lay(rock, (i * 3.0, 0, -low))
        parts.append(rock)

    return parts


def make():
    kit.reset()
    return build()
