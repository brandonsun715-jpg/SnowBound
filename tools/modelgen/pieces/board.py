"""
A 155 cm all-mountain snowboard with two bindings on it.

Same press as the ski — a swept section with a base, steel edges, a
sidewall and a printed top — with a board's numbers instead: a twin-ish
shape, much wider, much thinner, and rocker at both ends because a board
is ridden in both directions.

The nose points down -Y, which is +Z in Unity. The bindings are set at a
real stance: 55 cm apart, front foot turned out 15 degrees and back foot
6 degrees the other way.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import kit
import shapes
import surfaces as S

NAME = "Snowboard"
LENGTH = 1.55
HALF_WIDTH = 0.148

AMBER = (0.62, 0.26, 0.03, 1.0)
INK = (0.045, 0.050, 0.065, 1.0)
PALE = (0.80, 0.81, 0.83, 1.0)

STANCE = 0.55
SET_BACK = 0.02


def materials():
    return [
        S.sintered_base("BoardBase"),
        S.steel("BoardEdge", tint=(0.70, 0.71, 0.72, 1.0), roughness=0.14, scale=90.0),
        S.plastic("BoardSidewall", (0.030, 0.032, 0.036, 1.0), roughness=0.44, scale=90.0),
        S.topsheet("BoardTop", INK, AMBER, PALE, length=LENGTH, half_width=HALF_WIDTH),
        S.plastic("BindingBase", (0.050, 0.053, 0.060, 1.0), roughness=0.38, scale=70.0),
        S.plastic("BindingTrim", AMBER, roughness=0.34, scale=70.0),
        S.webbing("BindingStrap", (0.10, 0.105, 0.115, 1.0)),
        S.anodised("BindingDisc", tint=(0.46, 0.48, 0.52, 1.0), roughness=0.30),
        S.rubber("BoardPad"),
    ]


def profiles():
    width = shapes.sidecut(tail=0.144, waist=0.125, nose=0.148,
                           waist_at=0.50, nose_at=0.86, tail_at=0.14, ends=0.028)

    thickness = shapes.keyed([
        (0.00, 0.0035), (0.06, 0.0056), (0.20, 0.0072), (0.40, 0.0090),
        (0.50, 0.0092), (0.60, 0.0090), (0.80, 0.0070), (0.94, 0.0052),
        (1.00, 0.0034)])

    # A twin: rocker at both ends, a whisker of camber between the feet.
    base = shapes.keyed([
        (0.000, 0.0530), (0.030, 0.0330), (0.070, 0.0150), (0.110, 0.0055),
        (0.160, 0.0010), (0.300, 0.0042), (0.500, 0.0055), (0.700, 0.0042),
        (0.840, 0.0010), (0.890, 0.0055), (0.930, 0.0150), (0.970, 0.0330),
        (1.000, 0.0530)])

    return width, thickness, base


def build(mats=None):
    mats = mats or materials()
    width, thickness, base = profiles()

    rings, groups = shapes.plank(LENGTH, width, thickness, base, stations=110,
                                 edge=0.004, sidewall=0.002, chamfer=0.012)

    board = kit.loft(NAME + "Body", rings, closed_ends=True, mats=mats,
                     groups=groups, caps_group=shapes.SIDEWALL)
    kit.smooth(board, angle=30)
    kit.weld(board)

    def top(y):
        s = 0.5 - y / LENGTH
        return base(s) + thickness(s)

    parts = [board]

    pad = kit.box("StompPad", (0.075, 0.045, 0.006),
                  (0, SET_BACK + STANCE * 0.5 + 0.16, top(SET_BACK + STANCE * 0.5 + 0.16) + 0.003),
                  mat=mats[8])
    kit.bevel(pad, 0.004, segments=2)
    parts.append(pad)

    front = binding(mats, "Front")
    kit.place(front, (0.0, SET_BACK - STANCE * 0.5, top(SET_BACK - STANCE * 0.5)), (0, 0, -15))

    back = kit.clone(front, (0.0, SET_BACK + STANCE * 0.5, top(SET_BACK + STANCE * 0.5)), (0, 0, 186))

    return parts + [front, back]


def shell(half_width, thickness, bow, steps=8):
    """
    A curved cross-section: a shell rather than a plank.

    A highback and a heel cup are both pressed sheets that wrap around a
    leg. Sweeping a flat rectangle instead gives a garden fence, which is
    what the first version of this looked like.
    """
    front = []
    for i in range(steps + 1):
        u = -1.0 + 2.0 * i / steps
        front.append((u * half_width, bow * (1.0 - u * u)))

    return front + [(x, v - thickness) for x, v in reversed(front)]


def binding(mats, tag):
    """
    One strap binding: baseplate, disc, heel cup, highback and two straps.

    Everything is built lying flat around the origin so it can be dropped
    onto the board twice at two different angles, which is what a stance
    is.
    """
    base, trim, strap, disc = mats[4], mats[5], mats[6], mats[7]
    parts = []

    plate = kit.loft("Base" + tag,
                     [shapes.rounded_rect(0.090, 0.128, 0.045, 0.002),
                      shapes.rounded_rect(0.094, 0.132, 0.048, 0.007),
                      shapes.rounded_rect(0.088, 0.126, 0.046, 0.012)],
                     mat=base)
    parts.append(kit.smooth(plate, angle=34))

    parts.append(kit.smooth(kit.cylinder("Disc" + tag, 0.050, 0.007, (0, 0, 0.0145),
                                         verts=28, mat=disc)))
    parts.append(kit.smooth(kit.cylinder("DiscBoss" + tag, 0.018, 0.006, (0, 0, 0.019),
                                         verts=20, mat=base)))

    # The heel cup: a wall following the back of the plate, which is what
    # the highback is bolted to and what holds the boot back.
    cup = kit.loft("Cup" + tag,
                   shapes.sweep(shell(0.030, 0.008, 0.004, steps=4),
                                shapes.arc((0, 0.004, 0.012), 0.106, 196, 344, steps=16,
                                           plane='XY'),
                                up=(0, 0, 1)),
                   closed_ends=False, mat=base)
    parts.append(kit.smooth(cup, angle=40))

    # The highback leans forward, which is the entire point of it: it is
    # what a rider drives a heelside turn against.
    spine = [(0.0, 0.104, 0.024), (0.0, 0.110, 0.080), (0.0, 0.104, 0.140),
             (0.0, 0.090, 0.196), (0.0, 0.072, 0.232)]

    highback = kit.loft("Highback" + tag,
                        shapes.sweep(shell(0.066, 0.007, 0.020), spine, up=(0, 1, 0),
                                     scale=shapes.keyed([(0.0, 1.0), (0.45, 0.96),
                                                         (0.8, 0.86), (1.0, 0.74)])),
                        closed_ends=True, mat=base)
    parts.append(kit.smooth(highback, angle=44))

    strut = kit.box("Strut" + tag, (0.108, 0.012, 0.009), (0, 0.080, 0.230), rot=(26, 0, 0),
                    mat=trim)
    kit.bevel(strut, 0.003, segments=2)
    parts.append(strut)

    # Straps: an arch of webbing over the instep and another over the toe,
    # each with a padded top and a ratchet on the outboard side.
    for name, centre, radius, squash, half, thick, pad in (
            ("Ankle", (0, 0.030, 0.014), 0.094, 0.88, 0.038, 0.008, 0.030),
            ("Toe", (0, -0.072, 0.010), 0.082, 0.82, 0.032, 0.007, 0.024)):

        path = shapes.arc(centre, radius, 10, 170, steps=18, plane='XZ', squash=squash)

        band = kit.loft(name + tag,
                        shapes.sweep(shell(half, thick, 0.006, steps=3), path, up=(0, 1, 0)),
                        closed_ends=True, mat=strap)
        parts.append(kit.smooth(band, angle=40))

        cushion = kit.loft(name + "Pad" + tag,
                           shapes.sweep(shell(pad, 0.006, 0.005, steps=3),
                                        path[5:14], up=(0, 1, 0)),
                           closed_ends=True, mat=trim)
        parts.append(kit.smooth(cushion, angle=40))

    for at, size in (((0.084, 0.030, 0.062), (0.026, 0.048, 0.024)),
                     ((0.078, -0.072, 0.048), (0.024, 0.042, 0.022))):
        ratchet = kit.box("Ratchet" + tag, size, at, rot=(0, 16, 0), mat=trim)
        kit.bevel(ratchet, 0.004, segments=3)
        parts.append(ratchet)

    return kit.join(parts, "Binding" + tag)


def make():
    kit.reset()
    return kit.join(build(), NAME)
