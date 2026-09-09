"""
A chairlift terminal: bullwheel, drive, portal frame, canopy and an
operator's hut.

One model serves both ends. A real bottom station and a real top station
differ mostly in what is inside the machine housing, and the game already
turns the model round so the loading side faces the right way, so building
it fore-and-aft symmetric is both honest and half the work.

The bullwheel sits at the origin at cable height, which is where the
game's terminal puts its own, so the model drops onto the placeholder
without anything having to be measured twice.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import math

import kit
import shapes
import surfaces as S

NAME = "ChairliftStation"

CABLE = 3.40
SPACING = 4.40
HALF = SPACING * 0.5
LENGTH = 5.60
EAVE = CABLE + 2.40
RIDGE = CABLE + 4.20
ROOF_HALF = 4.80

# How far outboard of the roof's quarter points the legs stand.
RAKE = 1.06
SHELL = (0.075, 0.090, 0.115, 1.0)
WARN = (0.66, 0.46, 0.04, 1.0)


def materials():
    return {
        'zinc': S.galvanised("StationZinc"),
        'steel': S.steel("StationSteel", roughness=0.28, scale=30.0),
        'paint': S.powder_coat("StationPaint", SHELL, roughness=0.34, wear=0.45, dirt=0.4),
        'roof': S.powder_coat("StationRoof", (0.105, 0.115, 0.130, 1.0), roughness=0.30,
                              wear=0.35, dirt=0.5, scale=12.0),
        'rubber': S.rubber("StationRubber"),
        'concrete': S.concrete("StationPad"),
        'timber': S.timber("StationTimber"),
        'glass': S.glass("StationGlass"),
        'warn': S.powder_coat("StationWarn", WARN, roughness=0.34, wear=0.6, dirt=0.35),
        'snow': S.settled_snow("StationSnow"),
    }


def build(mats=None):
    m = mats or materials()

    return (pad(m) + legs(m) + bullwheel(m) + drive(m) + canopy(m) +
            rails(m) + fences(m) + hut(m) + access(m) + weather(m))


def weather(m):
    """
    The snow on the roof, which is most of what a terminal looks like from
    anywhere on the mountain.

    It lies on the panels themselves, thicker toward the eaves where it
    slides to and stops, and on the ridge, the walkways and the hut. It
    stops short of the eaves because that is where it breaks off.
    """
    parts = []

    pitch = math.degrees(math.atan2(RIDGE - EAVE, ROOF_HALF))
    slope = math.hypot(ROOF_HALF, RIDGE - EAVE)

    for side in (-1, 1):
        lying = kit.box("RoofSnow", (slope - 0.30, 2 * LENGTH + 0.6, 0.11),
                        (side * ROOF_HALF * 0.5, 0, (EAVE + RIDGE) * 0.5 + 0.11),
                        rot=(0, side * pitch, 0), mat=m['snow'])
        kit.bevel(lying, 0.030, segments=2)
        parts.append(lying)

        walk = kit.box("DeckSnow", (1.20, 3.70, 0.05), (side * 2.35, 0, CABLE + 1.12),
                       mat=m['snow'])
        kit.bevel(walk, 0.010, segments=2)
        parts.append(walk)

    cap = kit.box("RidgeSnow", (0.72, 2 * LENGTH + 0.7, 0.10), (0, 0, RIDGE + 0.16),
                  mat=m['snow'])
    kit.bevel(cap, 0.024, segments=2)
    parts.append(cap)

    hut_top = kit.box("HutSnow", (2.40, 2.10, 0.09), (-4.55, 3.30, 2.72), rot=(4, 0, 0),
                      mat=m['snow'])
    kit.bevel(hut_top, 0.020, segments=2)
    parts.append(hut_top)

    return parts


def pad(m):
    """The concrete raft the whole terminal is anchored to."""
    slab = kit.box("Pad", (10.4, 12.6, 0.44), (0, 0, 0.02), mat=m['concrete'])
    kit.bevel(slab, 0.04, segments=2)

    parts = [slab]

    # Under the legs, which are raked out a little, not under where a
    # straight leg would have been.
    for x in (-3.9 * RAKE, 3.9 * RAKE):
        for y in (-4.4, 4.4):
            plinth = kit.box("Plinth", (1.05, 1.05, 0.36), (x, y, 0.34), mat=m['concrete'])
            kit.bevel(plinth, 0.03, segments=2)
            parts.append(plinth)

    return parts


def legs(m):
    """
    Two portal frames, one each side of the wheel.

    A terminal is not a table: the load is a rope pulling sideways at the
    top of four legs, so the legs are raked and braced, and that bracing is
    most of what a lift station looks like from underneath.
    """
    parts = []
    top = EAVE - 0.35

    for x in (-3.9, 3.9):
        for y in (-4.4, 4.4):
            leg = kit.loft("Leg", [
                shapes.circle(0.190, 0.50, count=16),
                shapes.circle(0.170, top * 0.45, count=16),
                shapes.circle(0.150, top, count=16),
            ], closed_ends=True, mat=m['zinc'])
            kit.place(leg, (x * RAKE, y, 0))
            parts.append(kit.smooth(leg, angle=36))

            foot = kit.box("Foot", (0.46, 0.46, 0.05), (x * RAKE, y, 0.53), mat=m['zinc'])
            kit.bevel(foot, 0.006, segments=2)
            parts.append(foot)

    for y in (-4.4, 4.4):
        beam = kit.box("PortalBeam", (8.9, 0.34, 0.42), (0, y, top + 0.12), mat=m['zinc'])
        kit.bevel(beam, 0.012, segments=2)
        parts.append(beam)

        for side in (-1, 1):
            brace = kit.rod("PortalBrace", (side * 3.9 * RAKE, y, top - 0.10),
                            (side * 2.10, y, top + 0.30), 0.075, verts=10, mat=m['zinc'])
            parts.append(kit.smooth(brace))

    for x in (-3.9, 3.9):
        beam = kit.box("SideBeam", (0.30, 9.2, 0.38), (x * RAKE, 0, top + 0.10), mat=m['zinc'])
        kit.bevel(beam, 0.012, segments=2)
        parts.append(beam)

        for y in (-4.4, 4.4):
            brace = kit.rod("SideBrace", (x * RAKE, y, top - 0.90),
                            (x * RAKE, y * 0.42, top + 0.02), 0.070, verts=10, mat=m['zinc'])
            parts.append(kit.smooth(brace))

    return parts


def bullwheel(m):
    """
    The one moving part of a lift that anybody on the ground can see.

    A grooved rim on eight spokes, with the rope sitting in the groove and
    a liner under it. It is deliberately left in the open under the
    housing, because covering it up would be modelling the machinery and
    then hiding it.
    """
    parts = []

    rim = kit.loft("Bullwheel", [
        shapes.circle(0.42, -0.14, count=48),
        shapes.circle(1.94, -0.14, count=48),
        shapes.circle(2.16, -0.12, count=48),
        shapes.circle(2.20, -0.05, count=48),
        shapes.circle(2.12, 0.00, count=48),
        shapes.circle(2.20, 0.05, count=48),
        shapes.circle(2.16, 0.12, count=48),
        shapes.circle(1.94, 0.14, count=48),
        shapes.circle(0.42, 0.14, count=48),
    ], closed_ends=False, mat=m['steel'])
    kit.place(rim, (0, 0, CABLE))
    parts.append(kit.smooth(rim, angle=30))

    liner = kit.loft("WheelLiner", [
        shapes.circle(2.145, -0.055, count=48),
        shapes.circle(2.125, -0.020, count=48),
        shapes.circle(2.125, 0.020, count=48),
        shapes.circle(2.145, 0.055, count=48),
    ], closed_ends=False, mat=m['rubber'])
    kit.place(liner, (0, 0, CABLE))
    parts.append(kit.smooth(liner, angle=30))

    hub = kit.cylinder("Hub", 0.44, 0.52, (0, 0, CABLE), verts=28, mat=m['steel'])
    parts.append(kit.smooth(hub))

    for i in range(8):
        angle = i * 45.0
        radius = 1.20
        spoke = kit.box("Spoke", (0.16, 1.62, 0.10),
                        (radius * math.cos(math.radians(angle + 90)),
                         radius * math.sin(math.radians(angle + 90)), CABLE),
                        rot=(0, 0, angle), mat=m['steel'])
        kit.bevel(spoke, 0.010, segments=2)
        parts.append(spoke)

    shaft = kit.cylinder("Shaft", 0.16, 1.30, (0, 0, CABLE + 0.70), verts=20, mat=m['steel'])
    parts.append(kit.smooth(shaft))

    return parts


def drive(m):
    """The gearbox, the motor and the housing they live in, over the wheel."""
    parts = []

    # Walkways either side rather than a floor over everything: the
    # bullwheel has to stay in sight, and a service deck is a walkway
    # round the machine anyway.
    for x in (-2.35, 2.35):
        walk = kit.box("MachineDeck", (1.30, 3.80, 0.09), (x, 0, CABLE + 1.05),
                       mat=m['zinc'])
        kit.bevel(walk, 0.010, segments=2)
        parts.append(walk)

    bridge = kit.box("DeckBridge", (5.60, 1.10, 0.09), (0, 1.35, CABLE + 1.05), mat=m['zinc'])
    kit.bevel(bridge, 0.010, segments=2)
    parts.append(bridge)

    gearbox = kit.box("Gearbox", (1.70, 1.50, 1.05), (0, -0.30, CABLE + 1.62), mat=m['paint'])
    kit.bevel(gearbox, 0.030, segments=3)
    parts.append(gearbox)

    motor = kit.cylinder("Motor", 0.42, 1.60, (1.85, -0.30, CABLE + 1.60), rot=(0, 90, 0),
                         verts=24, mat=m['paint'])
    parts.append(kit.smooth(motor, angle=36))

    for i in range(7):
        fin = kit.box("MotorFin", (0.10, 0.95, 0.95), (1.20 + i * 0.22, -0.30, CABLE + 1.60),
                      mat=m['paint'])
        kit.bevel(fin, 0.006, segments=2)
        parts.append(fin)

    brake = kit.cylinder("Brake", 0.52, 0.24, (-1.30, -0.30, CABLE + 1.30), verts=24,
                         mat=m['steel'])
    parts.append(kit.smooth(brake))

    cabinet = kit.box("Cabinet", (0.90, 0.60, 1.30), (-2.30, 1.20, CABLE + 1.75),
                      mat=m['warn'])
    kit.bevel(cabinet, 0.020, segments=3)
    parts.append(cabinet)

    for side in (-1, 1):
        rail = kit.rod("DeckRail", (-2.80, side * 1.90, CABLE + 2.10),
                       (2.80, side * 1.90, CABLE + 2.10), 0.030, verts=10, mat=m['zinc'])
        parts.append(kit.smooth(rail))

        for x in (-2.6, 0.0, 2.6):
            post = kit.rod("DeckPost", (x, side * 1.90, CABLE + 1.10),
                           (x, side * 1.90, CABLE + 2.12), 0.028, verts=8, mat=m['zinc'])
            parts.append(kit.smooth(post))

    return parts


def canopy(m):
    """
    A pitched steel roof over the machinery, because snow has to come off
    it and because the drive is the one part of a lift that must not get
    wet.
    """
    parts = []

    pitch = math.degrees(math.atan2(RIDGE - EAVE, ROOF_HALF))
    slope = math.hypot(ROOF_HALF, RIDGE - EAVE)

    for side in (-1, 1):
        panel = kit.box("RoofPanel", (slope + 0.22, 2 * LENGTH + 0.9, 0.13),
                        (side * ROOF_HALF * 0.5, 0, (EAVE + RIDGE) * 0.5),
                        rot=(0, side * pitch, 0), mat=m['roof'])
        kit.bevel(panel, 0.020, segments=2)
        parts.append(panel)

        fascia = kit.box("Fascia", (0.16, 2 * LENGTH + 0.9, 0.34),
                         (side * (ROOF_HALF + 0.10), 0, EAVE - 0.16), mat=m['roof'])
        kit.bevel(fascia, 0.012, segments=2)
        parts.append(fascia)

    ridge = kit.box("Ridge", (0.62, 2 * LENGTH + 1.0, 0.14), (0, 0, RIDGE + 0.06),
                    mat=m['roof'])
    kit.bevel(ridge, 0.016, segments=2)
    parts.append(ridge)

    # The gable ends, closing the roof off so it is a building rather
    # than four sheets of steel on legs.
    for y in (-LENGTH - 0.45, LENGTH + 0.45):
        inward = 0.14 if y < 0 else -0.14
        face = [(-ROOF_HALF - 0.06, EAVE - 0.50), (ROOF_HALF + 0.06, EAVE - 0.50),
                (ROOF_HALF + 0.06, EAVE - 0.02), (0.0, RIDGE - 0.02),
                (-ROOF_HALF - 0.06, EAVE - 0.02)]

        gable = kit.loft("Gable",
                         [[(x, y, z) for x, z in face],
                          [(x, y + inward, z) for x, z in face]],
                         closed_ends=True, mat=m['roof'])
        parts.append(gable)

    sign = kit.box("StationSign", (3.40, 0.10, 0.72), (0, -LENGTH - 0.52, EAVE + 0.30),
                   mat=m['warn'])
    kit.bevel(sign, 0.012, segments=2)
    parts.append(sign)

    return parts


def rails(m):
    """
    The track the grips run on through the terminal, and the guards under
    it. This is the part a rider watches while they wait.
    """
    parts = []

    for side in (-1, 1):
        rail = kit.rod("GuideRail", (side * HALF, -LENGTH - 1.2, CABLE + 0.30),
                       (side * HALF, LENGTH + 1.2, CABLE + 0.30), 0.075, verts=14,
                       mat=m['zinc'])
        parts.append(kit.smooth(rail))

        catch = kit.rod("CatchRail", (side * (HALF + 0.32), -LENGTH - 0.8, CABLE - 0.22),
                        (side * (HALF + 0.32), LENGTH + 0.8, CABLE - 0.22), 0.038,
                        verts=10, mat=m['zinc'])
        parts.append(kit.smooth(catch))

        for i in range(7):
            y = -LENGTH + i * (2 * LENGTH / 6.0)
            hanger = kit.rod("RailHanger", (side * HALF, y, CABLE + 0.34),
                             (side * HALF, y, CABLE + 1.02), 0.034, verts=8, mat=m['zinc'])
            parts.append(kit.smooth(hanger))

    return parts


def fences(m):
    """The lane. A queue with no shape is a crowd."""
    parts = []

    for side in (-1, 1):
        for end in (-1, 1):
            for i in range(4):
                y = end * (2.2 + i * 1.45)
                post = kit.rod("LanePost", (side * 3.05, y, 0.20), (side * 3.05, y, 1.10),
                               0.035, verts=8, mat=m['warn'])
                parts.append(kit.smooth(post))

            for z in (0.62, 1.04):
                rail = kit.rod("LaneRail", (side * 3.05, end * 2.2, z),
                               (side * 3.05, end * 6.55, z), 0.028, verts=8, mat=m['warn'])
                parts.append(kit.smooth(rail))

    return parts


def hut(m):
    """The operator's hut: somebody has to be able to stop the lift."""
    parts = []

    at = (-4.55, 3.30)

    walls = kit.box("Hut", (2.20, 1.90, 2.35), (at[0], at[1], 1.35), mat=m['timber'])
    kit.bevel(walls, 0.020, segments=2)
    parts.append(walls)

    roof = kit.box("HutRoof", (2.55, 2.25, 0.14), (at[0], at[1], 2.60), rot=(4, 0, 0),
                   mat=m['roof'])
    kit.bevel(roof, 0.012, segments=2)
    parts.append(roof)

    window = kit.box("HutWindow", (0.06, 1.30, 0.80), (at[0] + 1.10, at[1], 1.75),
                     mat=m['glass'])
    kit.bevel(window, 0.006, segments=2)
    parts.append(window)

    door = kit.box("HutDoor", (0.90, 0.06, 1.90), (at[0], at[1] + 0.96, 1.20),
                   mat=m['paint'])
    kit.bevel(door, 0.008, segments=2)
    parts.append(door)

    stop = kit.box("StopButton", (0.30, 0.06, 0.30), (at[0] + 0.80, at[1] - 0.98, 1.40),
                   mat=m['warn'])
    kit.bevel(stop, 0.008, segments=2)
    parts.append(stop)

    return parts


def access(m):
    """A ladder up to the machine deck, because it has to be serviced."""
    parts = []

    x = 3.30

    for side in (-1, 1):
        rail = kit.rod("AccessRail", (x + side * 0.24, 4.05, 0.30),
                       (x + side * 0.24, 4.05, CABLE + 1.10), 0.028, verts=8, mat=m['zinc'])
        parts.append(kit.smooth(rail))

    rung = kit.rod("AccessRung", (x - 0.24, 4.05, 0.60), (x + 0.24, 4.05, 0.60), 0.018,
                   verts=8, mat=m['zinc'])
    kit.smooth(rung)
    kit.array(rung, count=13, offset=(0, 0, 0.32))
    parts.append(rung)

    cage = kit.rod("CageHoop", (x - 0.34, 3.70, 2.40), (x + 0.34, 3.70, 2.40), 0.020,
                   verts=8, mat=m['zinc'])
    kit.smooth(cage)
    kit.array(cage, count=5, offset=(0, 0, 0.55))
    parts.append(cage)

    return parts


def make():
    kit.reset()
    return kit.join(build(), NAME)
