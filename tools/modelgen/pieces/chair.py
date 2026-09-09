"""
A four-seat fixed-grip chair: grip, hanger, frame, seat and a stowed
safety bar.

The origin is the cable grip, so the model hangs from (0, 0, 0) exactly
the way the game's carrier does — the chair is put on the line by moving
that one point, and everything else follows it.

Seat height below the grip is 2.1 m, the game's hangerLength. Travel is
-Y, which is +Z in Unity, so the riders face the way they are going and
the backrest is behind them.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import kit
import shapes
import surfaces as S

NAME = "ChairliftChair"

DROP = 2.10
HALF = 1.00
SEAT = -DROP

SLATE = (0.055, 0.085, 0.145, 1.0)
WARM = (0.62, 0.30, 0.04, 1.0)


def materials():
    return {
        'paint': S.powder_coat("ChairPaint", SLATE, roughness=0.36, wear=0.55, dirt=0.30),
        'zinc': S.galvanised("ChairZinc"),
        'steel': S.steel("ChairSteel", roughness=0.26, scale=60.0),
        'seat': S.seat_pad("ChairSeat", (0.075, 0.095, 0.130, 1.0)),
        'grip': S.powder_coat("ChairBar", WARM, roughness=0.32, wear=0.75, dirt=0.25),
        'rubber': S.rubber("ChairRubber"),
    }


def build(mats=None):
    m = mats or materials()
    return grip(m) + hanger(m) + frame(m) + seat(m) + bar(m)


def slab(name, section, x0, x1, mat, stations=2, taper=None, base=SEAT):
    """
    A shape lofted sideways: a seat pan, a backrest, a footrest.

    The section is given relative to the seat, because a seat's profile is
    a thing in its own right and writing it out two metres below the
    origin makes it unreadable.
    """
    rings = []

    for i in range(stations):
        t = i / (stations - 1.0)
        x = x0 + (x1 - x0) * t
        pull = taper(t) if taper else 0.0
        rings.append([(x, y, base + z + pull) for y, z in section])

    return kit.loft(name, rings, closed_ends=True, mat=mat)


def grip(m):
    """
    The clamp on the cable, and the spring stack that holds it shut.

    On a fixed-grip lift this never opens: the chair is bolted to the rope
    and goes round the bullwheel still attached, which is why the whole
    lift stops when somebody falls over at the bottom.
    """
    parts = []

    jaw = kit.box("GripJaw", (0.130, 0.360, 0.115), (0, 0, -0.045), mat=m['steel'])
    kit.bevel(jaw, 0.008, segments=3)
    parts.append(jaw)

    body = kit.box("GripBody", (0.150, 0.300, 0.150), (0, 0, -0.165), mat=m['zinc'])
    kit.bevel(body, 0.010, segments=3)
    parts.append(body)

    spring = kit.cylinder("GripSpring", 0.062, 0.230, (0, 0, -0.150), rot=(90, 0, 0),
                          verts=20, mat=m['steel'])
    parts.append(kit.smooth(spring))

    for side in (-1, 1):
        cap = kit.cylinder("GripCap", 0.072, 0.026, (0, side * 0.128, -0.150), rot=(90, 0, 0),
                           verts=20, mat=m['zinc'])
        parts.append(kit.smooth(cap))

        bolt = kit.cylinder("GripBolt", 0.016, 0.170, (side * 0.052, 0.0, -0.075),
                            rot=(90, 0, 0), verts=10, mat=m['steel'])
        parts.append(kit.smooth(bolt))

    throat = kit.box("GripThroat", (0.110, 0.180, 0.120), (0, 0, -0.290), mat=m['zinc'])
    kit.bevel(throat, 0.010, segments=3)
    parts.append(throat)

    return parts


def hanger(m):
    """
    The arm, an oval section so it is stiff along the line and slim across
    it, sweeping backwards as it drops.

    It has to come down behind the riders and pick the chair up at the
    back, and it has to end far enough back that the seat, the riders and
    the bar between them hang plumb under the cable. An arm that drops
    straight through the middle of the seat is the first thing anybody
    notices, because there is nowhere to sit.
    """
    profile = [(-0.30, 0.052, 0.078, 0.000), (-0.60, 0.051, 0.077, 0.035),
               (-0.90, 0.049, 0.075, 0.115), (-1.15, 0.048, 0.073, 0.225),
               (-1.35, 0.046, 0.070, 0.355), (-1.55, 0.045, 0.068, 0.460),
               (-1.72, 0.047, 0.071, 0.500)]

    rings = []
    for z, rx, ry, y in profile:
        rings.append([(px, py * (ry / rx) + y, z)
                      for px, py, _ in shapes.circle(rx, 0.0, count=18)])

    arm = kit.loft("Hanger", rings, closed_ends=True, mat=m['paint'])
    kit.smooth(arm, angle=36)

    plate = kit.box("HangerPlate", (0.120, 0.170, 0.150), (0, 0.500, SEAT + 0.375),
                    mat=m['zinc'])
    kit.bevel(plate, 0.008, segments=2)

    bail = kit.cylinder("Bail", 0.036, 0.620, (0, 0.500, SEAT + 0.350), rot=(0, 90, 0),
                        verts=16, mat=m['steel'])
    kit.smooth(bail)

    parts = [arm, plate, bail]

    # The chair hangs off the bail: one link up to the top of the back
    # frame and one down to the rail under the seat, which is what stops
    # it swinging like a garden bench.
    for side in (-1, 1):
        parts.append(kit.smooth(kit.rod(
            "BailTop", (side * 0.260, 0.500, SEAT + 0.350),
            (side * 0.260, 0.305, SEAT + 0.620), 0.024, verts=10, mat=m['paint'])))
        parts.append(kit.smooth(kit.rod(
            "BailFoot", (side * 0.260, 0.500, SEAT + 0.350),
            (side * 0.260, 0.240, SEAT + 0.030), 0.024, verts=10, mat=m['paint'])))

    tag = kit.box("ChairNumber", (0.130, 0.014, 0.100), (0, 0.245, -1.02), mat=m['grip'])
    kit.bevel(tag, 0.004, segments=2)
    parts.append(tag)

    return parts


def frame(m):
    """
    The tubular frame the seat sits in: two side loops and the rails
    between them. It is what a chair actually is — the seat and back are
    panels hung on this.
    """
    parts = []

    for side in (-1, 1):
        x = side * (HALF - 0.03)

        # Up the back, along under the seat, and down to the front rail.
        spine = [(x, 0.300, SEAT + 0.640), (x, 0.290, SEAT + 0.380),
                 (x, 0.250, SEAT + 0.120), (x, 0.180, SEAT + 0.010),
                 (x, -0.060, SEAT - 0.020), (x, -0.300, SEAT - 0.010),
                 (x, -0.345, SEAT + 0.050)]

        loop = kit.loft("SideFrame", shapes.sweep(
            [(p[0], p[1]) for p in shapes.circle(0.030, 0.0, count=12)],
            spine, up=(1, 0, 0)), closed_ends=True, mat=m['paint'])
        parts.append(kit.smooth(loop, angle=40))

        strut = kit.rod("Strut", (side * 0.22, -0.055, SEAT + 0.030),
                        (x, 0.150, SEAT + 0.055), 0.026, verts=10, mat=m['paint'])
        parts.append(kit.smooth(strut))

    for y, z, radius in ((-0.300, SEAT - 0.010, 0.028), (0.230, SEAT + 0.030, 0.028),
                         (0.300, SEAT + 0.620, 0.030), (0.060, SEAT - 0.030, 0.024)):
        rail = kit.rod("Rail", (-HALF + 0.03, y, z), (HALF - 0.03, y, z), radius,
                       verts=12, mat=m['paint'])
        parts.append(kit.smooth(rail))

    return parts


def seat(m):
    """
    The pan and the back: pressed panels with a rolled front edge, dished
    a little so a rider does not slide off, and split into four by ribs.
    """
    parts = []

    pan = [(-0.300, 0.010), (-0.250, 0.028), (-0.120, 0.020), (0.060, 0.000),
           (0.200, 0.010), (0.245, 0.040), (0.245, -0.010), (0.200, -0.040),
           (0.060, -0.050), (-0.120, -0.030), (-0.250, -0.022), (-0.300, -0.040)]

    seat_pan = slab("SeatPan", pan, -HALF, HALF, m['seat'], stations=9,
                    taper=lambda t: -0.012 * (1.0 - (2.0 * t - 1.0) ** 2))
    parts.append(kit.smooth(seat_pan, angle=34))

    for i in range(3):
        x = -HALF * 0.5 + i * HALF * 0.5
        rib = kit.box("SeatRib", (0.030, 0.480, 0.028), (x, -0.030, SEAT + 0.030),
                      mat=m['seat'])
        kit.bevel(rib, 0.010, segments=3)
        parts.append(rib)

    # Leaning back, because a backrest that leans forward tips its riders
    # out and looks, from any angle, like a mistake.
    back = [(0.244, 0.070), (0.264, 0.360), (0.286, 0.578), (0.298, 0.616),
            (0.338, 0.606), (0.330, 0.560), (0.312, 0.350), (0.294, 0.066)]

    backrest = slab("Backrest", back, -HALF + 0.01, HALF - 0.01, m['seat'], stations=9,
                    taper=lambda t: 0.0)
    parts.append(kit.smooth(backrest, angle=34))

    # A rubber pad on the pan's front edge: the bit every rider's boots
    # scrape past on the way off.
    lip = kit.box("SeatLip", (2.0 * HALF - 0.10, 0.055, 0.028),
                  (0, -0.300, SEAT + 0.006), mat=m['rubber'])
    kit.bevel(lip, 0.008, segments=2)
    parts.append(lip)

    return parts


def bar(m):
    """
    The safety bar, swung up where an empty chair carries it, with the
    footrest folded under it.

    Where "up" is has to clear two things. The hanger comes down the back,
    so the bar cannot stow through it; and a rider is sitting in the chair,
    so it cannot stow through their head either. Up and just behind the
    headline is the one place that misses both, which is also where a real
    one ends up.
    """
    parts = []
    tops = []

    for side in (-1, 1):
        x = side * (HALF - 0.06)
        pivot = (x, 0.140, SEAT + 0.200)
        top = (x, 0.300, SEAT + 1.350)
        tops.append(top)

        spine = [pivot,
                 (x, 0.170, SEAT + 0.560),
                 (x, 0.215, SEAT + 0.900),
                 (x, 0.265, SEAT + 1.150),
                 top]

        arm = kit.loft("BarArm", shapes.sweep(
            [(p[0], p[1]) for p in shapes.circle(0.022, 0.0, count=10)],
            spine, up=(1, 0, 0)), closed_ends=True, mat=m['grip'])
        parts.append(kit.smooth(arm, angle=40))

        hinge = kit.cylinder("BarHinge", 0.040, 0.070, pivot, rot=(0, 90, 0), verts=14,
                             mat=m['steel'])
        parts.append(kit.smooth(hinge))

    cross = kit.rod("BarTop", tops[0], tops[1], 0.022, verts=12, mat=m['grip'])
    parts.append(kit.smooth(cross))

    rest = kit.box("Footrest", (2.0 * HALF - 0.26, 0.150, 0.028),
                   (0, 0.222, SEAT + 1.010), rot=(74, 0, 0), mat=m['grip'])
    kit.bevel(rest, 0.008, segments=2)
    parts.append(rest)

    for side in (-1, 1):
        pad = kit.cylinder("BarGrip", 0.032, 0.240, (side * 0.34, 0.300, SEAT + 1.350),
                           rot=(0, 90, 0), verts=14, mat=m['rubber'])
        parts.append(kit.smooth(pad))

    return parts


def make():
    kit.reset()
    return kit.join(build(), NAME)
