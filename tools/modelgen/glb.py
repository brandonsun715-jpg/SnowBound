"""
Bring a generated GLB into the pipeline.

Everything else under this folder is authored: a Python script says where
each face goes, so it comes out already metric, already anchored, already
named. A mesh that arrives from a generator has none of that. It is Y-up,
it is scaled to whatever the generator felt like, its origin is wherever
the generator left it, its object is called something like "mesh_0", and
its textures are packed inside the file in glTF's channel layout rather
than the four PNGs the game loads.

So this is the customs desk. It takes the GLB, turns it to face the way
every other model in this folder faces, scales it by one measured number,
stands it on the floor, gives it the name the game looks for, and writes
the textures out split into the maps the material wants. What comes out
the far side is an ordinary member of the set that check.py can judge on
the same terms as the rest.

    python3 tools/modelgen/glb.py <in.glb> --name Groomer --length 5.4 --yaw 90

--length is the one measured number: the metres the model's longest
horizontal axis should end up. Look up the real machine, type the number.
--yaw turns it until it faces -Y, which is what the exporter turns into
Unity's +Z. Render the preview, look at it, set the number.
"""

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np
from PIL import Image

import kit

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
MODELS = os.path.join(ROOT, "Assets", "Resources", "Models")
PREVIEW = os.path.join(HERE, "preview")


def load(path):
    """The GLB into an empty metric scene, as one mesh."""
    kit.reset()
    bpy.ops.import_scene.gltf(filepath=path)

    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        raise SystemExit("no mesh in " + path)

    # Generators split a model into parts on their own logic, not the
    # game's. The game finds parts by name, and these have none worth
    # keeping, so they become one object and get named properly below.
    for ob in meshes:
        kit.apply_transform(ob)

    return kit.join(meshes, "Imported") if len(meshes) > 1 else meshes[0]


def measure(ob):
    """Bounding box in world space."""
    corners = [ob.matrix_world @ v.co for v in ob.data.vertices]

    low = [min(c[i] for c in corners) for i in range(3)]
    high = [max(c[i] for c in corners) for i in range(3)]

    return low, [high[i] - low[i] for i in range(3)]


def place(ob, name, length, yaw, sink):
    """
    Turned, scaled, named and stood on the floor.

    The order matters: turning after scaling would measure the length
    across the wrong axis, and dropping to the floor before scaling would
    leave it hanging.
    """
    ob.name = name

    ob.rotation_euler = (0.0, 0.0, kit.R(yaw))
    kit.apply_transform(ob)

    _, size = measure(ob)
    longest = max(size[0], size[1])

    if longest <= 0.0:
        raise SystemExit("model has no size")

    ob.scale = (length / longest,) * 3
    kit.apply_transform(ob)

    # On the floor, the same anchor every standing model in the set uses,
    # optionally buried a little the way a tower is so nothing shows
    # daylight under one side of it on a slope.
    low, size = measure(ob)
    ob.location = (-low[0] - size[0] * 0.5, -low[1] - size[1] * 0.5, -low[2] - sink)
    kit.apply_transform(ob)

    return ob


def channel(image, index):
    """One channel of an image, as greyscale."""
    return Image.fromarray(np.asarray(image.convert("RGB"))[:, :, index], mode="L")


def textures(folder, name):
    """
    The GLB's own images, written out as the four maps the game loads.

    glTF packs metal and rough into one image — green is roughness, blue
    is metallic — because that is one texture fetch instead of two. The
    material here wants them apart, so they are split rather than
    re-authored: it is the same data either way.
    """
    written = []

    for image in bpy.data.images:
        if image.size[0] == 0 or not image.has_data:
            continue

        path = os.path.join(folder, "_tmp_" + image.name + ".png")
        image.filepath_raw = path
        image.file_format = 'PNG'
        image.save()

        picture = Image.open(path)
        label = image.name.lower()

        if "normal" in label:
            picture.convert("RGB").save(os.path.join(folder, name + "_Normal.png"))
            written.append("Normal")
        elif "metal" in label or "rough" in label or "orm" in label:
            channel(picture, 1).save(os.path.join(folder, name + "_Roughness.png"))
            channel(picture, 2).save(os.path.join(folder, name + "_Metallic.png"))
            written += ["Roughness", "Metallic"]
        else:
            picture.convert("RGB").save(os.path.join(folder, name + "_Albedo.png"))
            written.append("Albedo")

        picture.close()
        os.remove(path)

    return written


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source")
    parser.add_argument("--name", required=True)
    parser.add_argument("--length", type=float, required=True,
                        help="metres across the model's longest horizontal axis")
    parser.add_argument("--yaw", type=float, default=0.0,
                        help="degrees to turn it until it faces -Y")
    parser.add_argument("--sink", type=float, default=0.0,
                        help="metres to bury the footing, as a tower is buried")
    parser.add_argument("--preview-only", action="store_true")
    args = parser.parse_args()

    ob = load(args.source)
    place(ob, args.name, args.length, args.yaw, args.sink)

    os.makedirs(PREVIEW, exist_ok=True)
    kit.preview(ob, os.path.join(PREVIEW, args.name + ".png"))

    _, size = measure(ob)
    print("[%s] %.2f x %.2f x %.2f m, %d faces"
          % (args.name, size[0], size[1], size[2], len(ob.data.polygons)))

    if args.preview_only:
        return

    folder = os.path.join(MODELS, args.name)
    os.makedirs(folder, exist_ok=True)

    kit.export(ob, folder, args.name)
    print("[%s] wrote %s" % (args.name, ", ".join(textures(folder, args.name)) or "no textures"))


if __name__ == "__main__":
    main()
