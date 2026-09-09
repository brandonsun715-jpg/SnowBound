"""
The player on a snowboard: same body, softer boots, hood up, feet planted
fore and aft where the bindings are.

The torso is modelled square to the board rather than turned across it.
The game turns the shoulders itself — that is what SnowboardMode's body
yaw is — and it has to turn them about the waist while the feet stay
strapped in, which a pre-twisted model could not do.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import rider

NAME = "RiderBoard"


def make():
    return rider.make('board')


def pose(parts):
    """
    Turn the shoulders for the preview render only, so the picture shows
    the stance the player actually rides in. The exported model is
    untouched: this runs after it is written.
    """
    for part in parts:
        if part.name == "Torso":
            part.rotation_euler = (0.0, 0.0, math.radians(65.0))
