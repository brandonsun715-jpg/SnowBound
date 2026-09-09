"""
Build the hero models.

    python3 tools/modelgen/build.py            every model
    python3 tools/modelgen/build.py ski board  just those

Each one is modelled, unwrapped, baked to a set of PBR maps and written to
Assets/Resources/Models/<Name>/ as an FBX beside its textures, which is the
layout HeroAssets loads. A preview render goes to tools/modelgen/preview/
so the thing can be looked at rather than guessed at.
"""

import argparse
import importlib
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))

sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "pieces"))

import kit

MODELS = {
    "trees": ("trees", dict(angle=32, elevation=10, distance=3.0)),
    "park": ("park", dict(angle=36, elevation=14, distance=2.8)),
    "rocks": ("rocks", dict(angle=34, elevation=16, distance=2.8)),
    "skier": ("skier", dict(angle=30, elevation=8, distance=3.2)),
    "boarder": ("boarder", dict(angle=34, elevation=8, distance=3.2)),
    "ski": ("ski", dict(angle=28, elevation=26)),
    "pole": ("pole", dict(angle=30, elevation=18)),
    "board": ("board", dict(angle=30, elevation=28)),
    "chair": ("chair", dict(angle=38, elevation=12)),
    "tower": ("tower", dict(angle=42, elevation=14)),
    "station": ("station", dict(angle=48, elevation=16)),
}


def one(key, size, preview_only=False):
    module_name, shot = MODELS[key]
    module = importlib.import_module(module_name)

    started = time.time()
    ob = module.make()

    folder = os.path.join(ROOT, "Assets", "Resources", "Models", module.NAME)
    faces = sum(len(part.data.polygons) for part in kit.each(ob))

    if not preview_only:
        kit.unwrap(ob, margin=0.0025)
        kit.bake(ob, folder, module.NAME, size=size, mask_size=max(512, size // 2))
        kit.export(ob, folder, module.NAME)

    # A model with a rig can be stood in a pose for its picture. It is
    # posed after export, so what is written is still the neutral rig.
    if hasattr(module, "pose"):
        module.pose(kit.each(ob))

    shots = os.path.join(HERE, "preview")
    os.makedirs(shots, exist_ok=True)
    kit.preview(ob, os.path.join(shots, module.NAME + ".png"), size=900, samples=64, **shot)

    print("[%s] %d faces, %d part(s), %.0f s" %
          (module.NAME, faces, len(kit.each(ob)), time.time() - started))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("models", nargs="*", choices=list(MODELS), default=None)
    parser.add_argument("--size", type=int, default=2048)
    parser.add_argument("--preview-only", action="store_true")
    args = parser.parse_args()

    for key in (args.models or list(MODELS)):
        one(key, args.size, args.preview_only)


if __name__ == "__main__":
    main()
