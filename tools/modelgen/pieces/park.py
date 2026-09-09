"""
The terrain park: a kicker, a jib box, a rail and the leg they stand on.

Four parts in one file and one texture, because the park has only a
handful of features and each of them wants a different material — packed
snow, galvanised steel, a scuffed plastic deck.

The kicker is drawn two metres tall with a 42 degree lip, and the whole
takeoff is a circular arc tangent to the ground: that is what a shaped
jump is, and a power curve with a vertical face at the end — which is what
the game built before — is not. The game scales it to whatever height that
jump is and levels the ground under it first, the way a park crew grooms a
pad before building on it.

Everything is drawn with its length along Y, which is Unity's Z, because
that is the axis the park lays its features out along.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import kit
import shapes
import surfaces as S

NAME = "ParkFeatures"

KICKER_HEIGHT = 2.0
KICKER_LENGTH = 5.2
KICKER_WIDTH = 6.0
LIP = 42.0

BOX_LENGTH = 8.0
BOX_WIDTH = 0.95
BOX_THICK = 0.22
RAIL_RADIUS = 0.048


def materials():
    return {
        'piste': S.settled_snow("ParkPiste", tint=(0.90, 0.93, 0.98), scale=9.0),
        'snow': S.settled_snow("ParkSnow", tint=(0.84, 0.88, 0.96), scale=16.0),
        'steel': S.galvanised("ParkSteel"),
        'deck': S.plastic("ParkDeck", (0.055, 0.058, 0.070), roughness=0.24, scale=40.0),
        'rail': S.steel("ParkRail", tint=(0.62, 0.63, 0.65), roughness=0.16, scale=60.0),
        'edge': S.powder_coat("ParkEdge", (0.52, 0.20, 0.04), roughness=0.36, wear=0.8),
    }


def kicker(mats):
    """
    The takeoff: an arc from the snow to the lip, with the sides falling
    away at the angle snow actually sits at and flaring into a skirt.

    The top edges are rolled rather than sharp. A shaped jump is built
    with a shovel and there is no such thing as a sharp edge in snow.
    """
    radius = KICKER_LENGTH / math.sin(math.radians(LIP))
    stations = 18
    rings = []

    half = KICKER_WIDTH * 0.5
    top, side = 0, 1

    for i in range(stations + 1):
        t = i / stations
        angle = math.radians(LIP) * t

        y = -KICKER_LENGTH * 0.5 + radius * math.sin(angle)
        z = radius * (1.0 - math.cos(angle))

        # Snow piled at its angle of repose, plus a skirt where it meets
        # the flat.
        flare = 0.34 * z + 0.22
        crown = z + 0.02 * (1.0 - t)

        rings.append([
            (-half - flare, y, -0.30),
            (-half + 0.02, y, z * 0.55),
            (-half + 0.18, y, crown),
            (half - 0.18, y, crown),
            (half - 0.02, y, z * 0.55),
            (half + flare, y, -0.30),
        ])

    # The lip: a short kicked section at the takeoff angle, then the face.
    tip = rings[-1]
    kick = math.radians(LIP)
    rings.append([(x, y + 0.34 * math.cos(kick), z + 0.34 * math.sin(kick))
                  for x, y, z in tip])

    groups = [side, side, top, top, side, side]

    ramp = kit.loft("Kicker", rings, closed_ends=True,
                    mats=[mats['piste'], mats['snow']], groups=groups, caps_group=side)
    kit.smooth(ramp, angle=38)

    return ramp


def box(mats):
    """
    A jib box: a plastic deck on a steel frame, with the nose and tail
    tapered so an edge cannot catch on them.
    """
    parts = []
    half = BOX_LENGTH * 0.5
    wide = BOX_WIDTH * 0.5

    deck = kit.loft("BoxDeck", [
        [(-wide + 0.10, -half - 0.16, -BOX_THICK * 0.25),
         (wide - 0.10, -half - 0.16, -BOX_THICK * 0.25),
         (wide - 0.10, -half - 0.16, BOX_THICK * 0.30),
         (-wide + 0.10, -half - 0.16, BOX_THICK * 0.30)],
        [(-wide, -half, -BOX_THICK * 0.5), (wide, -half, -BOX_THICK * 0.5),
         (wide, -half, BOX_THICK * 0.5), (-wide, -half, BOX_THICK * 0.5)],
        [(-wide, half, -BOX_THICK * 0.5), (wide, half, -BOX_THICK * 0.5),
         (wide, half, BOX_THICK * 0.5), (-wide, half, BOX_THICK * 0.5)],
        [(-wide + 0.10, half + 0.16, -BOX_THICK * 0.25),
         (wide - 0.10, half + 0.16, -BOX_THICK * 0.25),
         (wide - 0.10, half + 0.16, BOX_THICK * 0.30),
         (-wide + 0.10, half + 0.16, BOX_THICK * 0.30)],
    ], closed_ends=True, mat=mats['deck'])
    parts.append(deck)

    # The frame under it, and the rolled steel edge along each side.
    for side in (-1, 1):
        rail = kit.box("BoxEdge", (0.055, BOX_LENGTH - 0.10, 0.055),
                       (side * (wide - 0.02), 0, BOX_THICK * 0.42), mat=mats['steel'])
        kit.bevel(rail, 0.010, segments=3)
        parts.append(rail)

        beam = kit.box("BoxBeam", (0.05, BOX_LENGTH - 0.30, 0.10),
                       (side * (wide - 0.08), 0, -BOX_THICK * 0.55), mat=mats['steel'])
        kit.bevel(beam, 0.008, segments=2)
        parts.append(beam)

    for i in range(3):
        cross = kit.box("BoxCross", (BOX_WIDTH - 0.10, 0.06, 0.08),
                        (0, -2.4 + i * 2.4, -BOX_THICK * 0.55), mat=mats['steel'])
        kit.bevel(cross, 0.008, segments=2)
        parts.append(cross)

    return kit.join(parts, "Box")


def rail(mats):
    """
    A round tube on a spine, which is what a rail is: the tube is the only
    part that matters and everything else exists to hold it at a height.
    """
    parts = []
    half = BOX_LENGTH * 0.5

    tube = kit.loft("RailTube", [
        [(x, -half - 0.20, z * 0.45) for x, _, z in shapes.circle(RAIL_RADIUS, 0.0, count=14)],
        [(x, -half, z) for x, _, z in shapes.circle(RAIL_RADIUS, 0.0, count=14)],
        [(x, half, z) for x, _, z in shapes.circle(RAIL_RADIUS, 0.0, count=14)],
        [(x, half + 0.20, z * 0.45) for x, _, z in shapes.circle(RAIL_RADIUS, 0.0, count=14)],
    ], closed_ends=True, mat=mats['rail'])
    parts.append(kit.smooth(tube, angle=40))

    spine = kit.box("RailSpine", (0.05, BOX_LENGTH - 0.20, 0.14),
                    (0, 0, -RAIL_RADIUS - 0.08), mat=mats['steel'])
    kit.bevel(spine, 0.008, segments=2)
    parts.append(spine)

    for i in range(3):
        stem = kit.box("RailStem", (0.045, 0.09, 0.10),
                       (0, -2.4 + i * 2.4, -RAIL_RADIUS - 0.02), mat=mats['steel'])
        kit.bevel(stem, 0.006, segments=2)
        parts.append(stem)

    return kit.join(parts, "Rail")


def leg(mats):
    """
    One metre of leg, hanging from z = 0 so the game can stretch it down
    to whatever the snow is doing under that end.
    """
    parts = []

    post = kit.box("LegPost", (0.14, 0.10, 1.0), (0, 0, -0.5), mat=mats['steel'])
    kit.bevel(post, 0.010, segments=2)
    parts.append(post)

    foot = kit.box("LegFoot", (0.46, 0.34, 0.05), (0, 0, -0.985), mat=mats['steel'])
    kit.bevel(foot, 0.008, segments=2)
    parts.append(foot)

    for side in (-1, 1):
        brace = kit.rod("LegBrace", (0, 0, -0.30), (side * 0.20, 0, -0.94), 0.022,
                        verts=8, mat=mats['steel'])
        parts.append(kit.smooth(brace))

    collar = kit.box("LegCollar", (0.20, 0.16, 0.06), (0, 0, -0.03), mat=mats['edge'])
    kit.bevel(collar, 0.008, segments=2)
    parts.append(collar)

    return kit.join(parts, "Leg")


def build(mats=None):
    mats = mats or materials()

    parts = [kicker(mats), box(mats), rail(mats), leg(mats)]

    # Spread out for the unwrap and the bake, without moving their meshes.
    for i, part in enumerate(parts):
        kit.lay(part, (0, 0, 0) if i == 0 else (i * 4.0, 0, 0))

    return parts


def make():
    kit.reset()
    return build()
