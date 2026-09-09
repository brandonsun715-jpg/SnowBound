"""
Blender-side toolkit for building SnowBound's hero models.

Everything here is deliberately small and explicit: primitives at real
world sizes, bevels on every edge because a perfectly sharp edge is the
single loudest tell that something was made in a computer, one joined mesh
per asset, one UV atlas, and one set of baked PBR maps.

Run through `bpy`, Blender as a Python module. No Blender install, no GUI,
no hand-placed vertices, so a model is a script that anybody can re-run and
change rather than a binary nobody can edit.

Units are metres, Blender Z-up. The FBX exporter turns that into Unity's
Y-up on the way out, so a model built "forward is -Y" here arrives in Unity
facing +Z, which is the direction of travel for the lift and the tip for a
ski or a board.
"""

import math
import os

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

R = math.radians


# ---------------------------------------------------------------- scene


def reset():
    """An empty metric scene with Cycles on the CPU."""
    bpy.ops.wm.read_factory_settings(use_empty=True)

    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.use_denoising = True
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0


def _active(ob):
    for other in bpy.context.selected_objects:
        other.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    return ob


def apply_transform(ob):
    """Bake position, rotation and scale into the mesh.

    Bevel width is in metres, so a box that is still wearing a scale of
    (3, 0.1, 1) gets three different bevel widths on its three axes.
    """
    _active(ob)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return ob


# ------------------------------------------------------------ primitives


def box(name, size, at=(0, 0, 0), rot=(0, 0, 0), mat=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=at, rotation=(R(rot[0]), R(rot[1]), R(rot[2])))
    ob = bpy.context.object
    ob.name = name
    ob.scale = size
    apply_transform(ob)
    return paint(ob, mat)


def cylinder(name, radius, depth, at=(0, 0, 0), rot=(0, 0, 0), verts=32, mat=None, caps=True):
    bpy.ops.mesh.primitive_cylinder_add(
        radius=radius, depth=depth, vertices=verts, location=at,
        rotation=(R(rot[0]), R(rot[1]), R(rot[2])),
        end_fill_type='NGON' if caps else 'NOTHING')
    ob = bpy.context.object
    ob.name = name
    apply_transform(ob)
    return paint(ob, mat)


def rod(name, a, b, radius, verts=16, mat=None, stretch=0.0):
    """
    A round bar from one point to another.

    Struts, braces, brake arms, ladder rungs and handrails are all this,
    and working out a centre and two Euler angles by hand every time is
    how a model ends up with a strut that misses its bracket.
    """
    a, b = Vector(a), Vector(b)
    direction = b - a
    length = direction.length + stretch
    if length < 1e-6:
        return None

    ob = cylinder(name, radius, length, verts=verts, mat=mat)
    ob.location = (a + b) * 0.5
    ob.rotation_euler = direction.to_track_quat('Z', 'Y').to_euler()
    return apply_transform(ob)


def loft(name, rings, closed_ends=True, mat=None, mats=None, groups=None, caps_group=0):
    """
    Skin a stack of rings.

    `rings` is a list of equal-length lists of (x, y, z). This is how the
    skis, the poles and the board are built: a cross-section swept along a
    curve, which is how the real things are pressed.

    `groups` gives a material slot per segment of the cross-section, which
    is what lets one swept shape be a steel edge, a plastic sidewall, a
    printed topsheet and a sintered base without cutting it into pieces.
    """
    bm = bmesh.new()
    made = [[bm.verts.new(p) for p in ring] for ring in rings]
    segments = []

    for a, b in zip(made, made[1:]):
        n = len(a)
        for i in range(n):
            j = (i + 1) % n
            try:
                face = bm.faces.new((a[i], a[j], b[j], b[i]))
                segments.append((face, i))
            except ValueError:
                pass

    if closed_ends:
        for ring, flip in ((made[0], True), (made[-1], False)):
            try:
                face = bm.faces.new(list(reversed(ring)) if flip else ring)
                face.smooth = False
                segments.append((face, None))
            except ValueError:
                pass

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    if groups is not None:
        for face, index in segments:
            face.material_index = caps_group if index is None else groups[index]

    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()

    ob = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(ob)

    if mats:
        for material in mats:
            ob.data.materials.append(material)
        return ob

    return paint(ob, mat)


def place(ob, at=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    ob.location = at
    ob.rotation_euler = (R(rot[0]), R(rot[1]), R(rot[2]))
    ob.scale = scale
    return apply_transform(ob)


def clone(ob, at=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    dup = ob.copy()
    dup.data = ob.data.copy()
    bpy.context.collection.objects.link(dup)
    return place(dup, at, rot, scale)


# ------------------------------------------------------------- modifiers


def only(obs):
    """Select a list of objects and make the first one active."""
    for other in bpy.context.selected_objects:
        other.select_set(False)

    for ob in obs:
        ob.select_set(True)

    bpy.context.view_layer.objects.active = obs[0]
    return obs


def each(ob):
    return list(ob) if isinstance(ob, (list, tuple)) else [ob]


def pivot(ob, at):
    """
    Move an object's origin to a joint without moving the object.

    A knee has to rotate about the knee. Everything here is modelled in
    place and then given its origin afterwards, which is much easier to
    read than modelling every limb at the origin and working out where it
    has to be put back.
    """
    at = Vector(at)
    ob.data.transform(Matrix.Translation(-at))
    ob.location = at
    return ob


def attach(child, parent, offset):
    """
    Hang one part off another, `offset` being the distance between their
    two joints. Rotating the parent now carries the child with it, which is
    the whole of the rig: no bones, no skinning, just parts that turn about
    the right points.
    """
    child.parent = parent
    child.matrix_parent_inverse = Matrix.Identity(4)
    child.location = Vector(offset)
    return child


def paint(ob, mat):
    if mat is not None:
        ob.data.materials.clear()
        ob.data.materials.append(mat)
    return ob


def bevel(ob, width=0.004, segments=2, angle=32, clamp=True):
    """
    Round every hard edge.

    Nothing manufactured has a truly sharp edge — it is cast, pressed,
    extruded or machined, and every one of those leaves a radius that
    catches a line of light. Putting one on is the cheapest realism in
    the whole pipeline.
    """
    mod = ob.modifiers.new("Bevel", 'BEVEL')
    mod.width = width
    mod.segments = segments
    mod.limit_method = 'ANGLE'
    mod.angle_limit = R(angle)
    mod.miter_outer = 'MITER_ARC'
    mod.use_clamp_overlap = clamp
    mod.harden_normals = True
    return ob


def smooth(ob, angle=35):
    _active(ob)
    bpy.ops.object.shade_smooth()
    ob.data.set_sharp_from_angle(angle=R(angle))
    return ob


def array(ob, count, offset):
    mod = ob.modifiers.new("Array", 'ARRAY')
    mod.count = count
    mod.use_relative_offset = False
    mod.use_constant_offset = True
    mod.constant_offset_displace = offset
    return ob


def apply_all(ob):
    _active(ob)
    for mod in list(ob.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=mod.name)
        except RuntimeError:
            ob.modifiers.remove(mod)
    return ob


def join(objects, name):
    """One mesh, one renderer, one draw call."""
    objects = [o for o in objects if o is not None]
    for ob in objects:
        apply_all(ob)

    _active(objects[0])
    for ob in objects[1:]:
        ob.select_set(True)

    bpy.ops.object.join()
    ob = bpy.context.object
    ob.name = name
    ob.data.name = name
    return apply_transform(ob)


def weld(ob, distance=0.0004):
    _active(ob)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=distance)
    bpy.ops.object.mode_set(mode='OBJECT')
    return ob


# ------------------------------------------------------------------- uv


def unwrap(ob, margin=0.003, angle=66):
    """
    Cut the model up and lay it flat.

    Smart projection does the cutting, and then the islands are packed
    again properly. Straight out of the projector they use a fifth of the
    texture and the rest is empty, which is the same as baking at a fifth
    of the resolution and shipping the file size anyway.
    """
    only(each(ob))
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=R(angle), island_margin=margin,
                             correct_aspect=True, scale_to_bounds=False)

    try:
        bpy.ops.uv.pack_islands(rotate=True, margin=margin, shape_method='CONCAVE')
    except TypeError:
        bpy.ops.uv.pack_islands(rotate=True, margin=margin)

    bpy.ops.object.mode_set(mode='OBJECT')
    return ob


# ----------------------------------------------------------------- bake


def _image(name, size, alpha=False, float_buffer=True):
    img = bpy.data.images.new(name, size, size, alpha=alpha, float_buffer=float_buffer)
    img.colorspace_settings.name = 'Non-Color'
    return img


def _target(ob, img):
    for slot in ob.data.materials:
        if slot is None:
            continue
        node = slot.node_tree.nodes.get("BakeTarget")
        if node is None:
            node = slot.node_tree.nodes.new("ShaderNodeTexImage")
            node.name = "BakeTarget"
            node.location = (-900, 600)
        node.image = img
        slot.node_tree.nodes.active = node
        node.select = True


def _read(img):
    """Blender's buffer, bottom-up float RGBA, as a top-down numpy array."""
    buf = np.empty(len(img.pixels), dtype=np.float32)
    img.pixels.foreach_get(buf)
    pixels = buf.reshape(img.size[1], img.size[0], 4)
    return np.flipud(pixels)


def _bake(ob, img, kind, samples=1, **passes):
    scene = bpy.context.scene
    scene.cycles.samples = samples
    scene.render.bake.margin = 24
    scene.render.bake.margin_type = 'ADJACENT_FACES'
    scene.render.bake.use_clear = True
    scene.render.bake.use_selected_to_active = False

    for key, value in passes.items():
        setattr(scene.render.bake, key, value)

    _target(ob, img)
    _active(ob)
    bpy.ops.object.bake(type=kind)
    return _read(img)


def _probe(ob, socket):
    """
    Rewire every material to emit one of its own inputs, so it can be baked.

    Cycles bakes colour, roughness and normals but has no pass for metallic
    or for anything else feeding the shader. Emitting the value and baking
    that is the standard way round it, and it works for any input.
    """
    saved = []

    for slot in ob.data.materials:
        nt = slot.node_tree
        out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
        bsdf = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
        link = out.inputs['Surface'].links[0] if out.inputs['Surface'].is_linked else None
        saved.append((nt, out, link.from_socket if link else None))

        emit = nt.nodes.new("ShaderNodeEmission")
        emit.name = "Probe"

        if bsdf is not None and socket in bsdf.inputs:
            src = bsdf.inputs[socket]
            if src.is_linked:
                nt.links.new(src.links[0].from_socket, emit.inputs['Color'])
            elif isinstance(src.default_value, float):
                v = src.default_value
                emit.inputs['Color'].default_value = (v, v, v, 1.0)
            else:
                v = src.default_value
                emit.inputs['Color'].default_value = (v[0], v[1], v[2], 1.0)

        nt.links.new(emit.outputs['Emission'], out.inputs['Surface'])

    return saved


def _restore(saved):
    for nt, out, source in saved:
        probe = nt.nodes.get("Probe")
        if probe is not None:
            nt.nodes.remove(probe)
        if source is not None:
            nt.links.new(source, out.inputs['Surface'])


def _srgb(linear):
    linear = np.clip(linear, 0.0, 1.0)
    return np.where(linear <= 0.0031308, linear * 12.92,
                    1.055 * np.power(linear, 1.0 / 2.4) - 0.055)


def _save(path, array, size=None, grey=False):
    from PIL import Image

    data = np.clip(array * 255.0 + 0.5, 0, 255).astype(np.uint8)
    img = Image.fromarray(data[:, :, 0] if grey else data[:, :, :3], 'L' if grey else 'RGB')

    if size is not None and size != img.size[0]:
        img = img.resize((size, size), Image.LANCZOS)

    img.save(path, optimize=True)
    return path


def merged(objs, name="Baking"):
    """
    One throwaway mesh of everything, for baking.

    A rider is fifteen parts so it can bend at the knees, but a bake wants
    one object and one texture. The parts are already unwrapped into a
    shared atlas by this point, so copying and welding them changes
    nothing about where anything lands.
    """
    copies = []

    for ob in objs:
        dup = ob.copy()
        dup.data = ob.data.copy()
        dup.parent = None
        bpy.context.collection.objects.link(dup)
        copies.append(dup)

    return join(copies, name)


def bake(ob, folder, name, size=2048, mask_size=1024, normal_size=1024,
         occlusion=0.55, ao_samples=48):
    """
    Every map the game wants, out of the same geometry and the same shaders.

    Ambient occlusion is multiplied into the base map rather than shipped
    separately, because URP has nowhere to put a fourth texture and the
    contact shading it adds — under the seat, inside the lattice, around
    every bolt — is most of what stops a model reading as plastic.
    """
    os.makedirs(folder, exist_ok=True)

    parts = each(ob)
    ob = parts[0] if len(parts) == 1 else merged(parts)

    albedo = _image(name + "_A", size)
    rough = _image(name + "_R", size)
    metal = _image(name + "_M", size)
    normal = _image(name + "_N", size)
    ao = _image(name + "_O", size)

    # Base colour is read straight off the shader rather than through the
    # diffuse pass. A metal has no diffuse colour at all, so a diffuse bake
    # of a galvanised tower comes back black — which is exactly what the
    # first version of this shipped.
    saved = _probe(ob, 'Base Color')
    base = _bake(ob, albedo, 'EMIT', samples=1)
    _restore(saved)

    roughness = _bake(ob, rough, 'ROUGHNESS', samples=1)
    normals = _bake(ob, normal, 'NORMAL', samples=1)
    shade = _bake(ob, ao, 'AO', samples=ao_samples)

    saved = _probe(ob, 'Metallic')
    metallic = _bake(ob, metal, 'EMIT', samples=1)
    _restore(saved)

    shaded = base[:, :, :3] * (1.0 - (1.0 - shade[:, :, :1]) * occlusion)

    # The normal map is baked at full size and written at half. Its fine
    # grain is below a pixel at any distance the player sees these from,
    # and a two thousand pixel map of noise is four megabytes of repository
    # that nobody can see.
    written = [
        _save(os.path.join(folder, name + "_Albedo.png"), _srgb(shaded), size),
        _save(os.path.join(folder, name + "_Normal.png"), normals, normal_size),
        _save(os.path.join(folder, name + "_Roughness.png"), roughness, mask_size, grey=True),
        _save(os.path.join(folder, name + "_Metallic.png"), metallic, mask_size, grey=True),
    ]

    for img in (albedo, rough, metal, normal, ao):
        bpy.data.images.remove(img)

    if len(parts) > 1:
        bpy.data.objects.remove(ob, do_unlink=True)

    return written


# --------------------------------------------------------------- export


def export(ob, folder, name):
    """
    Out as FBX, the one interchange format Unity reads without an importer.

    Blender is Z-up and right handed, Unity is Y-up and left handed. The
    exporter's default axes do that conversion, so a model built facing -Y
    here arrives facing +Z there, which is the convention every model in
    this folder is authored to.
    """
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name + ".fbx")

    only(each(ob))
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_NONE',
        use_space_transform=True,
        bake_space_transform=False,
        mesh_smooth_type='EDGE',
        use_mesh_modifiers=True,
        use_tspace=False,
        add_leaf_bones=False,
        path_mode='STRIP',
        axis_forward='-Z',
        axis_up='Y')

    return path


# -------------------------------------------------------------- preview


def preview(ob, path, size=720, samples=48, angle=35, elevation=22, distance=2.9,
            up=0.45, lens=50, background=(0.30, 0.36, 0.45)):
    """
    A render of what was just built, so the model can be looked at.

    Nobody can review a mesh by reading its vertex count.
    """
    scene = bpy.context.scene

    parts = each(ob)
    corners = [p.matrix_world @ Vector(c) for p in parts for c in p.bound_box]
    low = Vector((min(c.x for c in corners), min(c.y for c in corners),
                  min(c.z for c in corners)))
    high = Vector((max(c.x for c in corners), max(c.y for c in corners),
                   max(c.z for c in corners)))
    centre = (low + high) * 0.5

    radius = (high - low).length * 0.5

    # Frame the bounding sphere against the narrower of the two fields of
    # view, or anything tall — a pole, a tower — comes out with its top cut
    # off, which is exactly the part worth looking at.
    aspect = 0.72
    half_fov = math.atan((36.0 * aspect * 0.5) / lens)
    reach = radius / math.sin(half_fov) * (distance / 2.9)

    world = bpy.data.worlds.new("Preview")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (background[0], background[1], background[2], 1.0)
    bg.inputs[1].default_value = 1.0
    scene.world = world

    sun_data = bpy.data.lights.new("Sun", 'SUN')
    sun_data.energy = 2.6
    sun_data.angle = R(2.0)
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (R(52), 0, R(38))
    bpy.context.collection.objects.link(sun)

    theta, phi = R(angle), R(elevation)
    eye = centre + Vector((math.sin(theta) * math.cos(phi),
                           -math.cos(theta) * math.cos(phi),
                           math.sin(phi))) * reach

    cam_data = bpy.data.cameras.new("Camera")
    cam_data.lens = lens
    cam = bpy.data.objects.new("Camera", cam_data)
    cam.location = eye
    direction = (centre - eye).normalized()
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    bpy.context.collection.objects.link(cam)
    scene.camera = cam

    floor = box("PreviewFloor", (reach * 6, reach * 6, 0.02), (0, 0, low.z - 0.011))
    mat = bpy.data.materials.new("PreviewFloor")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs['Base Color'].default_value = (0.32, 0.34, 0.38, 1)
    mat.node_tree.nodes["Principled BSDF"].inputs['Roughness'].default_value = 0.75
    floor.data.materials.append(mat)

    scene.render.resolution_x = size
    scene.render.resolution_y = int(size * 0.72)
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = path
    scene.cycles.samples = samples
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'

    bpy.ops.render.render(write_still=True)

    bpy.data.objects.remove(floor, do_unlink=True)
    bpy.data.objects.remove(sun, do_unlink=True)
    bpy.data.objects.remove(cam, do_unlink=True)

    return path
