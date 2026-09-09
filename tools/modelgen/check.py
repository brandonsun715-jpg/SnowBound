"""
Read the exported models back and check them.

An FBX that Blender wrote and Blender can read is not proof that Unity
will place it correctly — but a model that comes back the wrong size,
upside down, or standing a metre under the floor is broken for everybody,
and that can be caught here rather than by opening the editor.

    python3 tools/modelgen/check.py

What it checks is what the game assumes: the size the model was authored
at, and where its origin sits, because the game positions every one of
these by their origin and scales them by one measured number.
"""

import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
MODELS = os.path.join(ROOT, "Assets", "Resources", "Models")

# name: (size x, y, z), where the origin sits.
#
#   floor  the model stands on z = 0
#   sunk   it stands on z = 0 with its footing buried a little, so a tower
#          on a slope never shows daylight under one side of it
#   hook   z = 0 is the top, because a chair hangs from its grip
EXPECTED = {
    "Ski": ((0.14, 1.72, 0.16), "floor"),
    "SkiPole": ((0.07, 0.13, 1.25), "floor"),
    "Snowboard": ((0.31, 1.55, 0.26), "floor"),
    "ChairliftChair": ((2.00, 0.95, 2.17), "hook"),
    "ChairliftTower": ((5.64, 2.10, 9.48), "sunk"),
    "ChairliftStation": ((11.02, 13.60, 7.93), "sunk"),
}

TOLERANCE = 0.35


def check(name):
    path = os.path.join(MODELS, name, name + ".fbx")
    if not os.path.exists(path):
        return "%-18s MISSING" % name, False

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)

    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        return "%-18s NO MESH" % name, False

    ob = meshes[0]
    size = ob.dimensions
    low = min((ob.matrix_world @ v.co).z for v in ob.data.vertices)
    high = max((ob.matrix_world @ v.co).z for v in ob.data.vertices)

    faces = len(ob.data.polygons)
    uvs = len(ob.data.uv_layers) > 0

    want, anchor = EXPECTED[name]
    fits = all(abs(size[i] - want[i]) < TOLERANCE + want[i] * 0.1 for i in range(3))

    if anchor == "hook":
        stands = abs(high) < 0.06
    elif anchor == "sunk":
        stands = -0.30 < low <= 0.02
    else:
        stands = abs(low) < 0.06

    ok = fits and stands and uvs

    return ("%-18s %5.2f x %5.2f x %5.2f m  %5d faces  %s  %s  %s" %
            (name, size.x, size.y, size.z, faces,
             "uv" if uvs else "NO UV",
             "size" if fits else "SIZE(want %.2f x %.2f x %.2f)" % want,
             anchor if stands else "OFF %s (%.2f..%.2f)" % (anchor, low, high))), ok


def main():
    failed = 0

    for name in sys.argv[1:] or EXPECTED:
        line, ok = check(name)
        print(("  " if ok else "! ") + line)
        failed += 0 if ok else 1

    print("%d model(s) checked, %d problem(s)" % (len(sys.argv[1:] or EXPECTED), failed))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
