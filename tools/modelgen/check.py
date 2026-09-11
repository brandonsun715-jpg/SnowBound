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
import struct
import sys
import zlib

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
    "RiderSki": ((0.68, 0.73, 1.77), "floor"),
    "RiderBoard": ((0.75, 0.90, 1.77), "floor"),

    # Sets of parts laid out side by side in one file, welded or spawned one
    # at a time by the game. Their union has no meaning, so only what does
    # is checked: the units, the unwrap, and that every part came through.
    "Trees": ((0, 0, 0), "kit"),
    "Rocks": ((0, 0, 0), "kit"),
    "ParkFeatures": ((0, 0, 0), "kit"),
    "Flora": ((0, 0, 0), "kit"),
    "ResortProps": ((0, 0, 0), "kit"),
}

PARTS = {
    "Trees": ["TreeA", "TreeB", "TreeC", "SnowA", "SnowB", "SnowC"],
    "Rocks": ["RockA", "RockB", "RockC", "RockSlab", "RockSpire"],
    "ParkFeatures": ["Kicker", "Box", "Rail", "Leg"],
    "Flora": ["ShrubA", "SnowShrubA", "ShrubB", "Fallen", "SnowFallen", "Stump"],
    "ResortProps": ["Bench", "Bin", "Lamp", "Sign", "Fence", "Pile"],
}

# A rider is a rig: the game finds these by name and turns them, so a
# renamed or unparented part is a rider who cannot sit on the chairlift.
RIGS = {
    "RiderSki": {
        "Hips": None, "Torso": "Hips", "Head": "Torso",
        "ThighLeft": "Hips", "ShinLeft": "ThighLeft", "BootLeft": "ShinLeft",
        "ThighRight": "Hips", "ShinRight": "ThighRight", "BootRight": "ShinRight",
        "ArmLeft": "Torso", "ForearmLeft": "ArmLeft", "HandLeft": "ForearmLeft",
        "ArmRight": "Torso", "ForearmRight": "ArmRight", "HandRight": "ForearmRight",
    },
}
RIGS["RiderBoard"] = RIGS["RiderSki"]

TOLERANCE = 0.35


def metres(path):
    """
    What the file says a unit means, read out of the FBX itself.

    Blender writes this and Blender reads it back the same way, so a
    round trip through Blender cannot catch it being wrong. Unity can, and
    does, by importing everything a hundred times too small. So it is read
    raw here: 100 means the numbers in the file are metres, which is what
    Unity wants.
    """
    data = open(path, "rb").read(300000)
    at = data.find(b"UnitScaleFactor")
    if at < 0:
        return None

    at = data.index(b"D", data.index(b"Number", at) + 6)
    return struct.unpack("<d", data[at + 1:at + 9])[0]


def stored(path):
    """
    The extents of each mesh as the bytes actually hold them.

    Blender writes what it reads, so importing a file back into Blender
    cannot tell you which way up it is any more than it could tell you what
    a unit meant — the converter runs in both directions and cancels out.
    Unity does not: it reads the vertex data, and the game reads it again
    off the mesh to batch it, with no node transform attached. A file whose
    geometry is still Z-up hands the batcher trees lying on their backs.

    So the numbers are read raw, out of the file.
    """
    data = open(path, "rb").read()
    sizes = []
    at = 0

    while True:
        found = data.find(b"Vertices", at)
        if found < 0:
            break

        at = found + 8
        if data[at:at + 1] != b"d":
            continue

        length, encoding, packed = struct.unpack("<III", data[at + 1:at + 13])
        body = data[at + 13:at + 13 + (packed if encoding else length * 8)]

        try:
            raw = zlib.decompress(body) if encoding else body
        except zlib.error:
            continue

        count = len(raw) // 8
        if count < 9:
            continue

        numbers = struct.unpack("<%dd" % count, raw[:count * 8])
        axes = [numbers[i::3] for i in range(3)]
        sizes.append(tuple(max(a) - min(a) for a in axes))

    return sizes


def upright(path, meshes):
    """
    Is the geometry stored the way up Unity reads it?

    Blender is Z-up and Unity is Y-up, so a correctly written file holds
    the height on Y. Compared against the same mesh as Blender hands it
    back, a good file has its raw Y where Blender has Z.
    """
    sizes = stored(path)
    if not sizes:
        return True, "no vertices"

    raw = max(sizes, key=sum)

    local = []
    for ob in meshes:
        points = [v.co for v in ob.data.vertices]
        if len(points) < 3:
            continue
        local.append(tuple(max(p[i] for p in points) - min(p[i] for p in points)
                           for i in range(3)))

    if not local:
        return True, "no vertices"

    here = max(local, key=sum)

    swapped = abs(raw[1] - here[2]) + abs(raw[2] - here[1])
    same = abs(raw[1] - here[1]) + abs(raw[2] - here[2])

    if swapped <= same:
        return True, "Y up"

    return False, "Z UP (height %.2f on Z, should be on Y)" % raw[2]


def check(name):
    path = os.path.join(MODELS, name, name + ".fbx")
    if not os.path.exists(path):
        return "%-18s MISSING" % name, False

    unit = metres(path)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)

    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        return "%-18s NO MESH" % name, False

    corners = [(o.matrix_world @ v.co) for o in meshes for v in o.data.vertices]
    low = min(c.z for c in corners)
    high = max(c.z for c in corners)
    size = [max(c[i] for c in corners) - min(c[i] for c in corners) for i in range(3)]

    faces = sum(len(o.data.polygons) for o in meshes)
    uvs = all(len(o.data.uv_layers) > 0 for o in meshes)

    rig = "-"
    if name in PARTS:
        missing = [part for part in PARTS[name] if part not in [o.name for o in meshes]]
        rig = "parts" if not missing else "MISSING(" + ",".join(missing) + ")"

    if name in RIGS:
        found = {o.name: (o.parent.name if o.parent else None) for o in meshes}
        wrong = [part for part, parent in RIGS[name].items()
                 if part not in found or found[part] != parent]
        rig = "rig" if not wrong else "RIG(" + ",".join(sorted(wrong)[:3]) + ")"
        if wrong:
            uvs = uvs and False

    want, anchor = EXPECTED[name]
    fits = all(abs(size[i] - want[i]) < TOLERANCE + want[i] * 0.1 for i in range(3))
    stands = True

    if anchor == "kit":
        fits = stands = True
    elif anchor == "hook":
        stands = abs(high) < 0.06
    elif anchor == "sunk":
        stands = -0.30 < low <= 0.02
    else:
        stands = abs(low) < 0.06

    metric = unit is not None and abs(unit - 100.0) < 0.5
    standing, axis = upright(path, meshes)
    ok = (fits and stands and uvs and metric and standing and
          not rig.startswith("RIG") and not rig.startswith("MISSING"))

    return ("%-18s %5.2f x %5.2f x %5.2f m  %5d faces  %2d part(s)  %s  %s  %s  %s  %s  %s" %
            (name, size[0], size[1], size[2], faces, len(meshes),
             "uv" if uvs else "NO UV",
             "size" if fits else "SIZE(want %.2f x %.2f x %.2f)" % want,
             anchor if stands else "OFF %s (%.2f..%.2f)" % (anchor, low, high),
             rig,
             "metres" if metric else "UNITS(%s)" % unit,
             axis)), ok


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
