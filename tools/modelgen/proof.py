"""
Render an exported model the way the game will see it: the FBX that was
written, wearing the four maps that were baked, and nothing else.

The preview renders in build.py show the shaders the model was baked
*from*. This shows what came out the other end — so a broken unwrap, a
bake that missed, or a normal map with its green channel the wrong way up
is visible here rather than in the editor.

    python3 tools/modelgen/proof.py chair ski
"""

import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))

sys.path.insert(0, HERE)

import kit

NAMES = {
    "ski": ("Ski", dict(angle=28, elevation=26)),
    "pole": ("SkiPole", dict(angle=30, elevation=18)),
    "board": ("Snowboard", dict(angle=30, elevation=28)),
    "chair": ("ChairliftChair", dict(angle=38, elevation=12)),
    "tower": ("ChairliftTower", dict(angle=42, elevation=14)),
    "station": ("ChairliftStation", dict(angle=48, elevation=16)),
}


def dressed(name, folder):
    """A material built from the baked maps, the way URP will build it."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]

    def image(suffix, data=True):
        path = os.path.join(folder, name + "_" + suffix + ".png")
        if not os.path.exists(path):
            return None

        node = nt.nodes.new("ShaderNodeTexImage")
        node.image = bpy.data.images.load(path)
        node.image.colorspace_settings.name = 'Non-Color' if data else 'sRGB'
        return node

    base = image("Albedo", data=False)
    if base:
        nt.links.new(base.outputs['Color'], bsdf.inputs['Base Color'])

    rough = image("Roughness")
    if rough:
        nt.links.new(rough.outputs['Color'], bsdf.inputs['Roughness'])

    metal = image("Metallic")
    if metal:
        nt.links.new(metal.outputs['Color'], bsdf.inputs['Metallic'])

    normal = image("Normal")
    if normal:
        unpack = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(normal.outputs['Color'], unpack.inputs['Color'])
        nt.links.new(unpack.outputs['Normal'], bsdf.inputs['Normal'])

    return mat


def proof(key):
    name, shot = NAMES[key]
    folder = os.path.join(ROOT, "Assets", "Resources", "Models", name)

    kit.reset()
    bpy.ops.import_scene.fbx(filepath=os.path.join(folder, name + ".fbx"))

    ob = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
    ob.data.materials.clear()
    ob.data.materials.append(dressed(name, folder))

    for polygon in ob.data.polygons:
        polygon.material_index = 0

    shots = os.path.join(HERE, "preview")
    os.makedirs(shots, exist_ok=True)

    path = os.path.join(shots, name + "_Baked.png")
    kit.preview(ob, path, size=900, samples=64, **shot)
    print(name, "->", path)


for key in sys.argv[1:] or list(NAMES):
    proof(key)
