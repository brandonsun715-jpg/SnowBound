"""The player on skis: baggy park kit, poles held in front."""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import rider

NAME = "RiderSki"


def make():
    return rider.make('ski')
