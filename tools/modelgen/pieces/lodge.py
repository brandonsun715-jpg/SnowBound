"""
The day lodge: stone base, timber walls, a long alpine roof and a deck.

This replaces a model that came from outside this folder, and it is built
to the same numbers the placeholder uses — twenty-two metres by thirteen,
seven and a half to the eave, five more to the ridge — so the thing that
arrives is the size of the thing it stands in rather than something fitted
down to squeeze inside it.

What makes a building read as a ski lodge rather than a shed is mostly the
roof: a long ridge, a deep overhang, and enough pitch to shed a metre of
snow. So the roof is the biggest thing here, the eaves reach well past the
walls, and snow lies on the slopes and stops short of the edge — which is
where it breaks off and drops, and why nobody sensible stands under an
alpine eave in March.

The front is -Y, the way every model in this folder faces.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import math

import kit
import surfaces as S

NAME = "Lodge"

# The placeholder's own numbers, so the model does not have to be resized
# to fit the box it replaces.
WIDTH = 22.0
DEPTH = 13.0
WALL = 7.5
ROOF = 5.0
OVERHANG = 1.6
PLINTH = 1.4

DECK = 6.0
DECK_TOP = PLINTH - 0.4

EAVE = PLINTH + WALL
RIDGE = EAVE + ROOF

# The run from ridge to eave, and the angle that makes.
RUN = DEPTH * 0.5 + OVERHANG
PITCH = math.degrees(math.atan2(ROOF, RUN))
SLOPE = math.hypot(ROOF, RUN)

# The entrance wing: a cross gable over the door, which is what stops a
# twenty-two metre frontage reading as a wall with a hole in it.
WING = 6.4
WING_OUT = 2.4
WING_EAVE = PLINTH + WALL - 0.9
WING_RIDGE = WING_EAVE + 2.9


def materials():
    return {
        'stone': S.granite("LodgeStone", colour=(0.208, 0.196, 0.184), scale=6.0),
        'timber': S.timber("LodgeTimber"),
        'dark': S.timber("LodgeDarkTimber", tint=(0.121, 0.084, 0.058, 1.0)),
        'roof': S.powder_coat("LodgeRoof", (0.098, 0.104, 0.118, 1.0), roughness=0.42,
                              wear=0.40, dirt=0.55, scale=10.0),
        'glass': S.glass("LodgeGlass"),
        'metal': S.galvanised("LodgeMetal"),
        'snow': S.settled_snow("LodgeSnow"),
    }


def base(m):
    """Stone, and wider than the building, so the walls have something to sit on."""
    parts = []

    plinth = kit.box("Plinth", (WIDTH + 0.9, DEPTH + 0.9, PLINTH),
                     (0, 0, PLINTH * 0.5), mat=m['stone'])
    kit.bevel(plinth, 0.05, segments=2)
    parts.append(plinth)

    # A course of larger blocks at the top of the plinth, because a stone
    # base that is one smooth extrusion reads as poured concrete.
    for side in (-1, 1):
        band = kit.box("PlinthCourse", (WIDTH + 1.05, 0.34, 0.40),
                       (0, side * (DEPTH * 0.5 + 0.45), PLINTH - 0.22), mat=m['stone'])
        kit.bevel(band, 0.03, segments=2)
        parts.append(band)

    return parts


def walls(m):
    parts = []

    shell = kit.box("Walls", (WIDTH, DEPTH, WALL), (0, 0, PLINTH + WALL * 0.5),
                    mat=m['timber'])
    kit.bevel(shell, 0.05, segments=2)
    parts.append(shell)

    # Corner posts. Every timber building has them and they are what gives
    # the corners a line of light instead of a seam.
    for x in (-1, 1):
        for y in (-1, 1):
            post = kit.box("Corner", (0.42, 0.42, WALL),
                           (x * (WIDTH * 0.5 - 0.05), y * (DEPTH * 0.5 - 0.05),
                            PLINTH + WALL * 0.5), mat=m['dark'])
            kit.bevel(post, 0.02, segments=2)
            parts.append(post)

    # The band under the eave, which is where the wall stops and the roof
    # structure starts.
    belt = kit.box("Beltcourse", (WIDTH + 0.30, DEPTH + 0.30, 0.44),
                   (0, 0, EAVE - 0.22), mat=m['dark'])
    kit.bevel(belt, 0.025, segments=2)
    parts.append(belt)

    return parts


def glazing(m):
    """
    The front is glass, because the view is the reason the building is here.

    Panels are set back into the wall and divided by posts rather than
    being one sheet: a mullion every couple of metres is what makes a
    window read as a window at this size.
    """
    parts = []

    top = EAVE - 0.70
    low = PLINTH + 1.05
    height = top - low
    face = -(DEPTH * 0.5)

    bays = 5
    span = (WIDTH - 3.0) / bays

    for i in range(bays):
        x = -(WIDTH - 3.0) * 0.5 + span * (i + 0.5)

        pane = kit.box("Pane", (span - 0.28, 0.10, height),
                       (x, face + 0.10, low + height * 0.5), mat=m['glass'])
        parts.append(pane)

        post = kit.box("Mullion", (0.18, 0.26, height + 0.30),
                       (x + span * 0.5, face + 0.04, low + height * 0.5), mat=m['dark'])
        kit.bevel(post, 0.015, segments=2)
        parts.append(post)

    for edge in (-1, 1):
        post = kit.box("Mullion", (0.30, 0.26, height + 0.30),
                       (edge * (WIDTH - 3.0) * 0.5, face + 0.04, low + height * 0.5),
                       mat=m['dark'])
        kit.bevel(post, 0.015, segments=2)
        parts.append(post)

    head = kit.box("WindowHead", (WIDTH - 2.2, 0.34, 0.34), (0, face + 0.06, top + 0.22),
                   mat=m['dark'])
    kit.bevel(head, 0.02, segments=2)
    parts.append(head)

    sill = kit.box("WindowSill", (WIDTH - 2.2, 0.44, 0.22), (0, face + 0.02, low - 0.14),
                   mat=m['dark'])
    kit.bevel(sill, 0.02, segments=2)
    parts.append(sill)

    # Smaller windows down each end, so the sides are not blank.
    for x in (-1, 1):
        for i in range(2):
            y = (i - 0.5) * 5.0
            pane = kit.box("SidePane", (0.10, 2.10, 2.30),
                           (x * (WIDTH * 0.5 - 0.10), y, PLINTH + 3.6), mat=m['glass'])
            parts.append(pane)

            frame = kit.box("SideFrame", (0.16, 2.40, 2.60),
                            (x * (WIDTH * 0.5 - 0.04), y, PLINTH + 3.6), mat=m['dark'])
            kit.bevel(frame, 0.015, segments=2)
            parts.append(frame)

    return parts


def roof(m):
    """
    A long ridge with deep eaves, and a cross gable over the door.

    The slabs are boxes turned to the pitch rather than lofted wedges: at
    this size the thickness of the roof is a line you can see from below,
    and a wedge has no underside to catch the light.
    """
    parts = []

    for side in (-1, 1):
        slab = kit.box("RoofSlab", (WIDTH + OVERHANG * 2, SLOPE, 0.34),
                       (0, side * RUN * 0.5, EAVE + ROOF * 0.5),
                       rot=(side * -PITCH, 0, 0), mat=m['roof'])
        kit.bevel(slab, 0.02, segments=2)
        parts.append(slab)

        # The fascia at the bottom edge of each slope.
        fascia = kit.box("Fascia", (WIDTH + OVERHANG * 2 + 0.1, 0.20, 0.42),
                         (0, side * RUN, EAVE - 0.10), mat=m['dark'])
        kit.bevel(fascia, 0.02, segments=2)
        parts.append(fascia)

    cap = kit.box("RidgeCap", (WIDTH + OVERHANG * 2 + 0.2, 0.52, 0.26),
                  (0, 0, RIDGE + 0.02), mat=m['roof'])
    kit.bevel(cap, 0.03, segments=2)
    parts.append(cap)

    # The gable walls at each end, filling the triangle under the ridge.
    for x in (-1, 1):
        gable = kit.loft("Gable", [
            [(0, -DEPTH * 0.5, EAVE), (0, DEPTH * 0.5, EAVE), (0, 0, RIDGE)],
            [(0.34, -DEPTH * 0.5, EAVE), (0.34, DEPTH * 0.5, EAVE), (0.34, 0, RIDGE)],
        ], mat=m['timber'])
        kit.place(gable, (x * (WIDTH * 0.5 - 0.34) if x > 0 else -WIDTH * 0.5, 0, 0))
        parts.append(gable)

    # Purlins showing under the overhang, which is what a deep eave is for.
    for side in (-1, 1):
        for i in range(9):
            x = -WIDTH * 0.5 + (WIDTH / 8.0) * i
            rafter = kit.box("Rafter", (0.16, 1.75, 0.22),
                             (x, side * (DEPTH * 0.5 + 0.70), EAVE - 0.42),
                             rot=(side * -PITCH, 0, 0), mat=m['dark'])
            kit.bevel(rafter, 0.012, segments=2)
            parts.append(rafter)

    parts += wing(m)
    parts.append(chimney(m))

    return parts


def wing(m):
    """The cross gable over the entrance."""
    parts = []

    front = -(DEPTH * 0.5)
    out = front - WING_OUT
    run = WING * 0.5 + 0.7
    pitch = math.degrees(math.atan2(WING_RIDGE - WING_EAVE, run))
    slope = math.hypot(WING_RIDGE - WING_EAVE, run)

    # Its walls, which is what makes it a wing rather than a canopy.
    shell = kit.box("WingWall", (WING, WING_OUT + 0.4, WING_EAVE - PLINTH),
                    (0, front - WING_OUT * 0.5 + 0.2,
                     PLINTH + (WING_EAVE - PLINTH) * 0.5), mat=m['timber'])
    kit.bevel(shell, 0.04, segments=2)
    parts.append(shell)

    for x in (-1, 1):
        slab = kit.box("WingRoof", (slope, WING_OUT + 1.5, 0.30),
                       (x * run * 0.5, out + (WING_OUT + 1.5) * 0.5 - 0.75,
                        WING_EAVE + (WING_RIDGE - WING_EAVE) * 0.5),
                       rot=(0, x * pitch, 0), mat=m['roof'])
        kit.bevel(slab, 0.02, segments=2)
        parts.append(slab)

    cap = kit.box("WingRidge", (0.42, WING_OUT + 1.7, 0.22),
                  (0, out + (WING_OUT + 1.7) * 0.5 - 0.85, WING_RIDGE + 0.02),
                  mat=m['roof'])
    kit.bevel(cap, 0.025, segments=2)
    parts.append(cap)

    # Glazed gable end above the door.
    tri = kit.loft("WingGable", [
        [(-run, 0, WING_EAVE), (run, 0, WING_EAVE), (0, 0, WING_RIDGE)],
        [(-run, 0.22, WING_EAVE), (run, 0.22, WING_EAVE), (0, 0.22, WING_RIDGE)],
    ], mat=m['glass'])
    kit.place(tri, (0, out - 0.1, 0))
    parts.append(tri)

    # The door, recessed under the gable.
    door = kit.box("Door", (2.6, 0.18, 2.7), (0, out + 0.16, PLINTH + 1.35), mat=m['dark'])
    kit.bevel(door, 0.02, segments=2)
    parts.append(door)

    jamb = kit.box("DoorJamb", (3.1, 0.26, 3.1), (0, out + 0.06, PLINTH + 1.5),
                   mat=m['timber'])
    kit.bevel(jamb, 0.02, segments=2)
    parts.append(jamb)

    return parts


def chimney(m):
    stack = kit.box("Chimney", (1.5, 1.5, RIDGE + 1.6 - PLINTH),
                    (WIDTH * 0.5 - 4.2, 2.2, PLINTH + (RIDGE + 1.6 - PLINTH) * 0.5),
                    mat=m['stone'])
    kit.bevel(stack, 0.04, segments=2)
    return stack


def deck(m):
    """
    The deck, its rail, and the steps down off the front of it.

    A lodge without one has nowhere for anybody to stand and take their
    boots off, which is half of what the front of a lodge is for.
    """
    parts = []

    front = -(DEPTH * 0.5)
    mid = front - DECK * 0.5

    boards = kit.box("Deck", (WIDTH - 1.0, DECK, 0.26), (0, mid, DECK_TOP - 0.13),
                     mat=m['timber'])
    kit.bevel(boards, 0.02, segments=2)
    parts.append(boards)

    skirt = kit.box("DeckSkirt", (WIDTH - 0.9, DECK + 0.1, DECK_TOP - 0.26),
                    (0, mid, (DECK_TOP - 0.26) * 0.5), mat=m['stone'])
    kit.bevel(skirt, 0.03, segments=2)
    parts.append(skirt)

    # Rail around three sides, with a gap in the middle of the front for
    # the steps.
    rail_top = DECK_TOP + 1.05

    for x in (-1, 1):
        for i in range(5):
            y = front - 0.5 - i * (DECK - 1.0) / 4.0
            post = kit.box("RailPost", (0.16, 0.16, 1.05),
                           (x * (WIDTH * 0.5 - 0.7), y, DECK_TOP + 0.52), mat=m['dark'])
            kit.bevel(post, 0.012, segments=2)
            parts.append(post)

        rail = kit.box("Rail", (0.22, DECK - 0.8, 0.14),
                       (x * (WIDTH * 0.5 - 0.7), mid, rail_top), mat=m['dark'])
        kit.bevel(rail, 0.015, segments=2)
        parts.append(rail)

    for side in (-1, 1):
        run = (WIDTH - 1.4) * 0.5 - 1.9

        for i in range(4):
            x = side * (2.4 + i * run / 3.0)
            post = kit.box("RailPost", (0.16, 0.16, 1.05),
                           (x, front - DECK + 0.3, DECK_TOP + 0.52), mat=m['dark'])
            kit.bevel(post, 0.012, segments=2)
            parts.append(post)

        rail = kit.box("Rail", (run + 0.4, 0.22, 0.14),
                       (side * (2.4 + run * 0.5), front - DECK + 0.3, rail_top),
                       mat=m['dark'])
        kit.bevel(rail, 0.015, segments=2)
        parts.append(rail)

    # Steps down to the snow.
    for i in range(4):
        tread = kit.box("Step", (6.2, 0.62, 0.24),
                        (0, front - DECK - 0.1 - i * 0.60, DECK_TOP - 0.12 - i * 0.26),
                        mat=m['stone'])
        kit.bevel(tread, 0.02, segments=2)
        parts.append(tread)

    return parts


def weather(m):
    """
    Snow where snow sits.

    On the roof it stops short of the eave, because that is the line it
    breaks off at. It also sits on the rails and along the back of the
    deck, where nobody walks.
    """
    parts = []

    for side in (-1, 1):
        lying = kit.box("RoofSnow", (WIDTH + OVERHANG * 2 - 0.5, SLOPE - 1.3, 0.16),
                        (0, side * (RUN * 0.5 - 0.30), EAVE + ROOF * 0.5 + 0.42),
                        rot=(side * -PITCH, 0, 0), mat=m['snow'])
        kit.bevel(lying, 0.03, segments=2)
        parts.append(lying)

    cap = kit.box("RidgeSnow", (WIDTH + OVERHANG * 2 - 0.3, 0.80, 0.16),
                  (0, 0, RIDGE + 0.18), mat=m['snow'])
    kit.bevel(cap, 0.04, segments=2)
    parts.append(cap)

    # On the wing.
    front = -(DEPTH * 0.5)
    out = front - WING_OUT
    run = WING * 0.5 + 0.7
    pitch = math.degrees(math.atan2(WING_RIDGE - WING_EAVE, run))
    slope = math.hypot(WING_RIDGE - WING_EAVE, run)

    for x in (-1, 1):
        lying = kit.box("WingSnow", (slope - 0.9, WING_OUT + 1.0, 0.14),
                        (x * (run * 0.5 - 0.15), out + (WING_OUT + 1.0) * 0.5 - 0.5,
                         WING_EAVE + (WING_RIDGE - WING_EAVE) * 0.5 + 0.36),
                        rot=(0, x * pitch, 0), mat=m['snow'])
        kit.bevel(lying, 0.025, segments=2)
        parts.append(lying)

    # A drift along the back of the deck and a cap on each rail.
    drift = kit.box("DeckSnow", (WIDTH - 2.0, 0.9, 0.12),
                    (0, front - 0.55, DECK_TOP + 0.04), mat=m['snow'])
    kit.bevel(drift, 0.02, segments=2)
    parts.append(drift)

    for x in (-1, 1):
        cap = kit.box("RailSnow", (0.26, DECK - 0.8, 0.07),
                      (x * (WIDTH * 0.5 - 0.7), -(DEPTH * 0.5) - DECK * 0.5,
                       DECK_TOP + 1.05 + 0.10), mat=m['snow'])
        kit.bevel(cap, 0.012, segments=2)
        parts.append(cap)

    # And on the chimney, which is the one flat top up there.
    top = kit.box("ChimneySnow", (1.35, 1.35, 0.12),
                  (WIDTH * 0.5 - 4.2, 2.2, RIDGE + 1.6 + 0.05), mat=m['snow'])
    kit.bevel(top, 0.02, segments=2)
    parts.append(top)

    return parts


def build(mats=None):
    m = mats or materials()

    parts = []
    parts += base(m)
    parts += walls(m)
    parts += glazing(m)
    parts += roof(m)
    parts += deck(m)
    parts += weather(m)

    return [kit.join(parts, NAME)]


def make():
    kit.reset()
    return build()
