"""
A chairlift line tower: footing, tapered mast, crossarm and two sheave
trains, with a ladder up the back and a platform at the top.

Nine metres to the cable, which is what the game's towerHeight is set to,
and a 4.4 m crossarm, which is its trackSpacing. The model is scaled to
whatever the lift in the scene actually uses, so those are proportions
rather than promises.

The line runs along Y and the crossarm across X. Uphill is -Y, which is
+Z in Unity.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import kit
import shapes
import surfaces as S

NAME = "ChairliftTower"

HEIGHT = 9.0
SPACING = 4.4
YELLOW = (0.72, 0.52, 0.05, 1.0)


def materials():
    return {
        'zinc': S.galvanised("TowerZinc"),
        'steel': S.steel("TowerSteel", roughness=0.30, scale=40.0),
        'paint': S.powder_coat("TowerPaint", (0.16, 0.17, 0.19, 1.0), roughness=0.40, wear=0.7),
        'rubber': S.rubber("SheaveLiner"),
        'concrete': S.concrete("TowerFooting"),
        'sign': S.powder_coat("TowerSign", YELLOW, roughness=0.34, wear=0.5, dirt=0.5),
        'snow': S.settled_snow("TowerSnow"),
    }


def build(mats=None):
    m = mats or materials()
    parts = []

    parts += footing(m)
    parts += mast(m)
    parts += crossarm(m)
    parts += ladder(m)

    # One train, built at the origin, then stood out at each end of the
    # crossarm. The second is the first turned round rather than a mirror,
    # so the two hang the same way up.
    parts += weather(m)

    train = sheaves(m, "A")
    kit.place(train, (SPACING * 0.5, 0, 0))
    parts.append(train)
    parts.append(kit.clone(train, rot=(0, 0, 180)))

    return parts


def weather(m):
    """
    What settles on it between storms.

    Snow lies on anything facing up and wide enough to hold it: the top of
    the crossarm, the platform, the footing. A lift tower with no snow on it
    in the middle of a snowfield is the sort of thing you do not notice
    until it is there.
    """
    parts = []

    ridge = kit.loft("ArmSnow", [
        [(x, y, z) for x, y, z in shapes.circle(0.118, -SPACING * 0.5 - 0.36, count=10)],
        [(x, y, z) for x, y, z in shapes.circle(0.126, SPACING * 0.5 + 0.36, count=10)],
    ], closed_ends=True, mat=m['snow'])
    kit.place(ridge, rot=(0, 90, 0))
    ridge.location = (0, 0, HEIGHT + 0.11)
    kit.apply_transform(ridge)
    parts.append(kit.smooth(ridge, angle=40))

    deck = kit.box("PlatformSnow", (1.24, 0.80, 0.05), (0, 0.50, HEIGHT - 0.66), mat=m['snow'])
    kit.bevel(deck, 0.010, segments=2)
    parts.append(deck)

    pad = kit.box("FootingSnow", (1.86, 1.86, 0.06), (0, 0, 0.49), mat=m['snow'])
    kit.bevel(pad, 0.020, segments=2)
    parts.append(pad)

    return parts


def footing(m):
    """Concrete, cast in the summer, with the mast bolted down to it."""
    pad = kit.box("Footing", (1.9, 1.9, 0.62), (0, 0, 0.16), mat=m['concrete'])
    kit.bevel(pad, 0.035, segments=2)

    plate = kit.box("BasePlate", (0.98, 0.98, 0.045), (0, 0, 0.492), mat=m['zinc'])
    kit.bevel(plate, 0.006, segments=2)

    parts = [pad, plate]

    for i in range(8):
        angle = i * 45.0
        at = shapes.circle(0.40, 0.53, count=8)[i]
        bolt = kit.cylinder("Anchor", 0.028, 0.075, (at[0], at[1], 0.53), verts=8, mat=m['steel'])
        parts.append(bolt)

    # Gussets: the mast is a tube and the plate is flat, and the load has
    # to get from one to the other.
    for i in range(4):
        gusset = kit.box("Gusset", (0.026, 0.44, 0.52), (0, 0, 0.76), rot=(0, 0, i * 45.0),
                         mat=m['zinc'])
        kit.bevel(gusset, 0.006, segments=2)
        parts.append(gusset)

    return parts


def mast(m):
    """A drawn tube, wider at the bottom where the bending moment is."""
    profile = [(0.51, 0.300), (1.60, 0.286), (4.00, 0.262), (6.60, 0.238),
               (HEIGHT - 0.55, 0.222), (HEIGHT - 0.30, 0.216)]

    tube = kit.loft("Mast", [shapes.circle(r, z, count=24) for z, r in profile],
                    closed_ends=True, mat=m['zinc'])
    kit.smooth(tube, angle=36)

    collar = kit.cylinder("MastCollar", 0.245, 0.10, (0, 0, HEIGHT - 0.34), verts=24,
                          mat=m['zinc'])
    kit.smooth(collar, angle=36)

    sign = kit.box("TowerSign", (0.44, 0.030, 0.34), (0, -0.28, 2.60), mat=m['sign'])
    kit.bevel(sign, 0.008, segments=2)

    return [tube, collar, sign]


def crossarm(m):
    """
    The beam the sheave trains hang from, and the head casting it sits in.

    It is a tube rather than a box because the real one is: a lift tower is
    steel pipe from the ground to the cable.
    """
    head = kit.box("Head", (0.62, 0.58, 0.42), (0, 0, HEIGHT - 0.18), mat=m['zinc'])
    kit.bevel(head, 0.014, segments=3)

    beam = kit.loft("Crossarm", [
        shapes.circle(0.115, -SPACING * 0.5 - 0.62, count=18),
        shapes.circle(0.150, -SPACING * 0.5 - 0.40, count=18),
        shapes.circle(0.150, SPACING * 0.5 + 0.40, count=18),
        shapes.circle(0.115, SPACING * 0.5 + 0.62, count=18),
    ], closed_ends=True, mat=m['zinc'])
    kit.place(beam, rot=(0, 90, 0))
    kit.smooth(beam, angle=36)
    beam.location = (0, 0, HEIGHT + 0.02)
    kit.apply_transform(beam)

    parts = [head, beam]

    for side in (-1, 1):
        stay = kit.rod("Stay", (side * 0.22, 0, HEIGHT - 0.42),
                       (side * (SPACING * 0.5 - 0.35), 0, HEIGHT - 0.02), 0.045,
                       verts=10, mat=m['zinc'])
        parts.append(kit.smooth(stay, angle=36))

    return parts


def sheaves(m, tag, count=4):
    """
    A four-wheel sheave train, hung under one end of the crossarm.

    The load has to be shared between the wheels however the cable is
    pulling, so a real train is a beam on a pivot carrying two smaller
    beams, each carrying two wheels — which is exactly what this is, and
    it is the detail that makes a tower read as machinery.
    """
    parts = []

    top = HEIGHT + 0.02
    beam_z = top - 0.62
    rocker_z = beam_z - 0.30
    axle_z = rocker_z - 0.26

    hanger = kit.box("Hanger" + tag, (0.16, 0.34, 0.52), (0, 0, top - 0.34), mat=m['zinc'])
    kit.bevel(hanger, 0.010, segments=2)
    parts.append(hanger)

    beam = kit.box("Beam" + tag, (0.10, 1.72, 0.20), (0, 0, beam_z), mat=m['zinc'])
    kit.bevel(beam, 0.012, segments=2)
    parts.append(beam)

    pivot = kit.cylinder("BeamPivot" + tag, 0.055, 0.24, (0, 0, beam_z), rot=(0, 90, 0),
                         verts=14, mat=m['steel'])
    parts.append(kit.smooth(pivot))

    for side in (-1, 1):
        rocker = kit.box("Rocker" + tag, (0.085, 0.86, 0.17),
                         (0, side * 0.44, rocker_z), mat=m['zinc'])
        kit.bevel(rocker, 0.010, segments=2)
        parts.append(rocker)

        link = kit.rod("Link" + tag, (0, side * 0.44, beam_z), (0, side * 0.44, rocker_z),
                       0.042, verts=10, mat=m['steel'])
        parts.append(kit.smooth(link))

    # The wheels themselves: a steel rim with a moulded liner in it, which
    # is the part that actually touches the rope and the reason a lift
    # hums rather than clanks.
    for i in range(count):
        y = (i - (count - 1) * 0.5) * 0.56

        rim = kit.loft("Sheave" + tag, [
            shapes.circle(0.070, -0.055, count=20),
            shapes.circle(0.185, -0.052, count=20),
            shapes.circle(0.186, -0.030, count=20),
            shapes.circle(0.186, 0.030, count=20),
            shapes.circle(0.185, 0.052, count=20),
            shapes.circle(0.070, 0.055, count=20),
        ], closed_ends=True, mat=m['steel'])
        kit.place(rim, (0, y, axle_z), (0, 90, 0))
        kit.smooth(rim, angle=34)
        parts.append(rim)

        liner = kit.loft("Liner" + tag, [
            shapes.circle(0.186, -0.048, count=20),
            shapes.circle(0.205, -0.030, count=20),
            shapes.circle(0.196, 0.000, count=20),
            shapes.circle(0.205, 0.030, count=20),
            shapes.circle(0.186, 0.048, count=20),
        ], closed_ends=False, mat=m['rubber'])
        kit.place(liner, (0, y, axle_z), (0, 90, 0))
        kit.smooth(liner, angle=34)
        parts.append(liner)

        axle = kit.cylinder("Axle" + tag, 0.032, 0.16, (0, y, axle_z), rot=(0, 90, 0),
                            verts=12, mat=m['steel'])
        parts.append(kit.smooth(axle))

        arm = kit.rod("SheaveArm" + tag, (0, y, rocker_z), (0, y, axle_z), 0.036,
                      verts=10, mat=m['zinc'])
        parts.append(kit.smooth(arm))

    # Guards above and below the rope: without them a derailed cable is
    # on the ground, so every tower has them and their absence is loud.
    for z, radius in ((axle_z + 0.30, 0.026), (axle_z - 0.26, 0.026)):
        guard = kit.rod("Guard" + tag, (0, -1.05, z), (0, 1.05, z), radius, verts=10,
                        mat=m['zinc'])
        parts.append(kit.smooth(guard))

    return kit.join(parts, "Sheaves" + tag)


def ladder(m):
    """A ladder up the downhill side, because a lift is maintained by hand."""
    parts = []

    for side in (-1, 1):
        rail = kit.rod("LadderRail", (side * 0.22, 0.36, 0.90),
                       (side * 0.22, 0.30, HEIGHT - 0.70), 0.026, verts=10, mat=m['zinc'])
        parts.append(kit.smooth(rail))

    rung = kit.rod("Rung", (-0.22, 0.33, 1.10), (0.22, 0.33, 1.10), 0.017, verts=8,
                   mat=m['zinc'])
    kit.smooth(rung)
    kit.array(rung, count=25, offset=(0, -0.0025, 0.30))
    parts.append(rung)

    deck = kit.box("Platform", (1.30, 0.86, 0.05), (0, 0.50, HEIGHT - 0.72), mat=m['zinc'])
    kit.bevel(deck, 0.008, segments=2)
    parts.append(deck)

    for x, y0, y1 in ((-0.62, 0.10, 0.92), (0.62, 0.10, 0.92)):
        parts.append(kit.smooth(kit.rod("RailPost", (x, y0, HEIGHT - 0.70),
                                        (x, y0, HEIGHT - 0.72 + 1.05), 0.020, verts=8,
                                        mat=m['zinc'])))
        parts.append(kit.smooth(kit.rod("RailPost", (x, y1, HEIGHT - 0.70),
                                        (x, y1, HEIGHT - 0.72 + 1.05), 0.020, verts=8,
                                        mat=m['zinc'])))
        parts.append(kit.smooth(kit.rod("HandRail", (x, y0, HEIGHT + 0.31),
                                        (x, y1, HEIGHT + 0.31), 0.020, verts=8,
                                        mat=m['zinc'])))

    parts.append(kit.smooth(kit.rod("HandRailBack", (-0.62, 0.92, HEIGHT + 0.31),
                                    (0.62, 0.92, HEIGHT + 0.31), 0.020, verts=8,
                                    mat=m['zinc'])))

    return parts


def make():
    kit.reset()
    return kit.join(build(), NAME)
