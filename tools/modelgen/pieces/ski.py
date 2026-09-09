"""
One all-mountain ski, 172 cm, with an alpine binding on it.

Built from the numbers a real ski is specified by — 128 at the shovel, 84
at the waist, 114 at the tail, camber between the contact points and an
early-rise tip — because those numbers are the shape. Guessing at the
silhouette instead gives a plank, and everybody who has ever seen a ski
knows immediately that it is a plank.

The tip points down -Y, which is +Z once Unity has it: the direction the
rider is travelling.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import kit
import shapes
import surfaces as S

NAME = "Ski"
LENGTH = 1.72
HALF_WIDTH = 0.064

BLUE = (0.030, 0.13, 0.30, 1.0)
ORANGE = (0.72, 0.24, 0.045, 1.0)
PALE = (0.80, 0.82, 0.84, 1.0)
DARK = (0.020, 0.021, 0.024, 1.0)


def profiles():
    """Half-width, thickness and base height, all as curves down the ski."""
    width = shapes.sidecut(tail=0.057, waist=0.042, nose=0.064)

    thickness = shapes.keyed([
        (0.00, 0.0045), (0.05, 0.0090), (0.15, 0.0122), (0.30, 0.0155),
        (0.45, 0.0190), (0.55, 0.0188), (0.70, 0.0148), (0.85, 0.0100),
        (0.94, 0.0070), (1.00, 0.0040)])

    # Camber between the contact points, a little tail kick and a long
    # early-rise shovel. A ski that is flat along its length reads as a
    # length of skirting board.
    base = shapes.keyed([
        (0.000, 0.0165), (0.030, 0.0055), (0.070, 0.0005), (0.180, 0.0035),
        (0.350, 0.0060), (0.500, 0.0068), (0.650, 0.0058), (0.780, 0.0010),
        (0.840, 0.0035), (0.900, 0.0145), (0.950, 0.0360), (0.980, 0.0610),
        (1.000, 0.0810)])

    return width, thickness, base


def materials():
    return [
        S.sintered_base("SkiBase"),
        S.steel("SkiEdge", tint=(0.70, 0.71, 0.72, 1.0), roughness=0.14, scale=90.0),
        S.plastic("SkiSidewall", DARK, roughness=0.42, scale=90.0),
        S.topsheet("SkiTop", BLUE, ORANGE, PALE, length=LENGTH, half_width=HALF_WIDTH),
        S.plastic("BindingShell", (0.045, 0.048, 0.055, 1.0), roughness=0.36, scale=80.0),
        S.plastic("BindingAccent", ORANGE, roughness=0.32, scale=80.0),
        S.anodised("BindingMetal", tint=(0.50, 0.52, 0.55, 1.0), roughness=0.28),
        S.steel("BindingSteel", roughness=0.20, scale=120.0),
    ]


def build(mats=None):
    width, thickness, base = profiles()
    mats = mats or materials()

    rings, groups = shapes.plank(LENGTH, width, thickness, base, stations=96)

    ski = kit.loft(NAME + "Body", rings, closed_ends=True, mats=mats,
                   groups=groups, caps_group=shapes.SIDEWALL)
    kit.smooth(ski, angle=32)
    kit.weld(ski)

    return [ski] + binding(base, thickness, mats)


def binding(base, thickness, mats):
    """
    A step-in alpine binding: toe piece, heel piece and brake arms.

    Mounted where a binding is actually mounted — the boot's midsole mark,
    a little behind the middle of the ski — because a binding sitting dead
    centre is the sort of thing that looks wrong without anyone being able
    to say why.
    """
    shell, accent, metal, steel = mats[4], mats[5], mats[6], mats[7]

    def top(y):
        s = 0.5 - y / LENGTH
        return base(s) + thickness(s)

    boot = 0.02
    toe_y = boot - 0.150
    heel_y = boot + 0.155
    parts = []

    def solid(name, size, at, mat, rot=(0, 0, 0), width=0.004, segments=3):
        ob = kit.box(name, size, at, rot=rot, mat=mat)
        kit.bevel(ob, width, segments=segments)
        parts.append(ob)
        return ob

    # ---- toe piece
    solid("ToePlate", (0.086, 0.135, 0.010), (0, toe_y, top(toe_y) + 0.005), metal, width=0.0025)
    solid("ToeBody", (0.062, 0.080, 0.046), (0, toe_y + 0.012, top(toe_y) + 0.033), shell, width=0.005)
    solid("ToeHood", (0.050, 0.052, 0.020), (0, toe_y - 0.008, top(toe_y) + 0.062), accent, rot=(-14, 0, 0))
    solid("ToePad", (0.062, 0.040, 0.004), (0, toe_y - 0.052, top(toe_y) + 0.012), steel, width=0.0015)

    for side in (-1, 1):
        solid("ToeWing", (0.020, 0.058, 0.030),
              (side * 0.036, toe_y - 0.030, top(toe_y) + 0.026), shell, rot=(0, 0, side * 9))

    parts.append(kit.smooth(kit.cylinder(
        "ToeScrew", 0.006, 0.010, (0, toe_y - 0.004, top(toe_y) + 0.058),
        rot=(90, 0, 0), verts=16, mat=steel)))

    # ---- heel piece
    solid("HeelTrack", (0.052, 0.190, 0.010), (0, heel_y + 0.010, top(heel_y) + 0.005), metal, width=0.0025)
    solid("HeelBody", (0.064, 0.104, 0.062), (0, heel_y + 0.030, top(heel_y) + 0.042), shell,
          rot=(-6, 0, 0), width=0.005)
    solid("HeelCup", (0.058, 0.030, 0.036), (0, heel_y - 0.028, top(heel_y) + 0.030), shell, rot=(12, 0, 0))
    solid("HeelLever", (0.050, 0.070, 0.011), (0, heel_y + 0.086, top(heel_y) + 0.070), accent, rot=(24, 0, 0))

    parts.append(kit.smooth(kit.cylinder(
        "HeelSpring", 0.014, 0.052, (0, heel_y + 0.036, top(heel_y) + 0.078),
        rot=(84, 0, 0), verts=20, mat=metal)))

    # ---- brakes: two sprung arms that drop either side of the ski
    for side in (-1, 1):
        pivot = (side * 0.028, heel_y - 0.026, top(heel_y) + 0.018)
        knee = (side * 0.058, heel_y - 0.044, -0.004)
        foot = (side * 0.062, heel_y - 0.070, -0.032)

        parts.append(kit.smooth(kit.rod("BrakeArm", pivot, knee, 0.0035, verts=10, mat=steel)))
        parts.append(kit.smooth(kit.rod("BrakeShin", knee, foot, 0.0035, verts=10, mat=steel)))

        solid("BrakePad", (0.013, 0.038, 0.014),
              (side * 0.063, heel_y - 0.074, -0.036), accent, rot=(0, side * 10, 0), width=0.003)

    return parts


def make():
    kit.reset()
    return kit.join(build(), NAME)
