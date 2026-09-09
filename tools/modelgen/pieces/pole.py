"""
A ski pole, 120 cm, aluminium.

Stood on its tip at the origin so the game can put it in a rack or in a
rider's hand without a magic offset: the tip is at z = 0 and the grip is at
the top.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import math

import kit
import shapes
import surfaces as S

NAME = "SkiPole"
LENGTH = 1.20

RED = (0.52, 0.09, 0.05, 1.0)


def materials():
    return [
        S.anodised("PoleShaft", tint=(0.44, 0.47, 0.52, 1.0), roughness=0.26),
        S.grip_foam("PoleGrip"),
        S.plastic("PoleBasket", (0.055, 0.058, 0.065, 1.0), roughness=0.42, scale=70.0),
        S.webbing("PoleStrap", (0.16, 0.17, 0.19, 1.0)),
        S.steel("PoleTip", tint=(0.48, 0.48, 0.50, 1.0), roughness=0.22, scale=140.0),
        S.plastic("PoleCollar", RED, roughness=0.34, scale=70.0),
    ]


def build(mats=None):
    shaft_mat, grip_mat, basket_mat, strap_mat, tip_mat, collar_mat = mats or materials()

    parts = []

    # ---- shaft: tapered, because a pole is drawn tube and the swing
    # weight is kept at the top end.
    shaft = kit.loft("PoleShaft", [
        shapes.circle(0.0062, 0.030, count=16),
        shapes.circle(0.0068, 0.220, count=16),
        shapes.circle(0.0080, 0.700, count=16),
        shapes.circle(0.0092, LENGTH - 0.170, count=16),
        shapes.circle(0.0094, LENGTH - 0.140, count=16),
    ], mat=shaft_mat)
    parts.append(kit.smooth(shaft, angle=40))

    # ---- grip: moulded, thicker where the hand closes, ribbed underneath
    rings = []
    for i in range(25):
        t = i / 24.0
        z = LENGTH - 0.150 + 0.150 * t
        swell = 0.0155 + 0.0055 * math.sin(math.pi * min(1.0, t * 1.15)) \
            + 0.0010 * math.sin(t * 26.0)
        rings.append(shapes.circle(swell, z, count=18))
    rings.append(shapes.circle(0.0125, LENGTH + 0.004, count=18))

    grip = kit.loft("PoleGrip", rings, mat=grip_mat)
    parts.append(kit.smooth(grip, angle=45))

    collar = kit.cylinder("PoleCollar", 0.0125, 0.020, (0, 0, LENGTH - 0.156),
                          verts=18, mat=collar_mat)
    parts.append(kit.smooth(collar, angle=40))

    # ---- strap: a loop of webbing through the top of the grip
    loop = shapes.arc((0.0, 0.0, LENGTH - 0.040), 0.055, -80, 260, steps=22,
                      plane='YZ', squash=1.35)
    strap = kit.loft("PoleStrap",
                     shapes.sweep([(-0.011, -0.0012), (0.011, -0.0012),
                                   (0.011, 0.0012), (-0.011, 0.0012)], loop,
                                  up=(1.0, 0.0, 0.0)),
                     closed_ends=False, mat=strap_mat)
    parts.append(kit.smooth(strap, angle=35))

    # ---- basket: a shallow dished disc, well up the shaft
    basket = kit.loft("PoleBasket", [
        shapes.circle(0.0085, 0.100, count=20),
        shapes.circle(0.0340, 0.086, count=20),
        shapes.circle(0.0345, 0.082, count=20),
        shapes.circle(0.0090, 0.094, count=20),
    ], mat=basket_mat)
    parts.append(kit.smooth(basket, angle=38))

    # ---- tip: a hardened point, the one part that is not aluminium
    tip = kit.loft("PoleTip", [
        shapes.circle(0.0007, 0.000, count=12),
        shapes.circle(0.0040, 0.018, count=12),
        shapes.circle(0.0062, 0.048, count=12),
    ], mat=tip_mat)
    parts.append(kit.smooth(tip, angle=40))

    return parts


def make():
    kit.reset()
    return kit.join(build(), NAME)
