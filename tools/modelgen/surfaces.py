"""
The materials the hero models are baked from.

Nothing here is a flat colour with a roughness slider. Every surface is
built the way the real one got that way: paint sprayed over steel wears
through on the edges people grab and knocks off where things hit it, zinc
freezes into spangles as it cools, a ski base is ground with a stone that
leaves structure lines along it, and a moulded grip has the mould's own
texture in it.

Coordinates are object space in metres, so a scale of 40 means detail
every two and a half centimetres whatever the part is, and the same
material used on a tower and on a ski pole matches.
"""

import bpy

BLACK = (0.012, 0.013, 0.015, 1.0)


# ------------------------------------------------------------- plumbing


def _rgba(colour):
    """Colours are written as three numbers here; Blender wants four."""
    return tuple(colour) if len(colour) == 4 else (colour[0], colour[1], colour[2], 1.0)


def _mat(name, roughness=0.4, metallic=0.0, base=(0.5, 0.5, 0.5, 1.0)):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True

    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs['Base Color'].default_value = _rgba(base)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic

    return mat, nt, bsdf


def _coords(nt, scale=1.0):
    coord = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs['Scale'].default_value = (scale, scale, scale)
    nt.links.new(coord.outputs['Object'], mapping.inputs['Vector'])
    return mapping.outputs['Vector']


def _noise(nt, vector, scale, detail=8.0, roughness=0.55, distortion=0.0):
    node = nt.nodes.new("ShaderNodeTexNoise")
    node.inputs['Scale'].default_value = scale
    node.inputs['Detail'].default_value = detail
    node.inputs['Roughness'].default_value = roughness
    node.inputs['Distortion'].default_value = distortion
    if vector is not None:
        nt.links.new(vector, node.inputs['Vector'])
    return node


def _voronoi(nt, vector, scale, feature='F1', randomness=1.0, smoothness=1.0):
    node = nt.nodes.new("ShaderNodeTexVoronoi")
    node.feature = feature
    node.inputs['Scale'].default_value = scale
    node.inputs['Randomness'].default_value = randomness
    if 'Smoothness' in node.inputs:
        node.inputs['Smoothness'].default_value = smoothness
    if vector is not None:
        nt.links.new(vector, node.inputs['Vector'])
    return node


def _wave(nt, vector, scale, distortion=0.0, detail=2.0, bands='X', profile='SIN'):
    node = nt.nodes.new("ShaderNodeTexWave")
    node.wave_type = 'BANDS'
    node.bands_direction = bands
    node.wave_profile = profile
    node.inputs['Scale'].default_value = scale
    node.inputs['Distortion'].default_value = distortion
    node.inputs['Detail'].default_value = detail
    if vector is not None:
        nt.links.new(vector, node.inputs['Vector'])
    return node


def _ramp(nt, source, stops):
    node = nt.nodes.new("ShaderNodeValToRGB")
    elements = node.color_ramp.elements

    while len(elements) > 1:
        elements.remove(elements[-1])

    def colour(value):
        return value if len(value) == 4 else (value[0], value[1], value[2], 1.0)

    elements[0].position = stops[0][0]
    elements[0].color = colour(stops[0][1])

    for position, value in stops[1:]:
        elements.new(position).color = colour(value)

    nt.links.new(source, node.inputs['Factor'])
    return node


def _mix(nt, factor, a, b, blend='MIX'):
    node = nt.nodes.new("ShaderNodeMixRGB")
    node.blend_type = blend

    if hasattr(factor, 'node'):
        nt.links.new(factor, node.inputs['Factor'])
    else:
        node.inputs['Factor'].default_value = factor

    for socket, value in (('Color1', a), ('Color2', b)):
        if hasattr(value, 'node'):
            nt.links.new(value, node.inputs[socket])
        else:
            node.inputs[socket].default_value = value if len(value) == 4 else (*value, 1.0)

    return node


def _maths(nt, operation, a, b=None, clamp=False):
    node = nt.nodes.new("ShaderNodeMath")
    node.operation = operation
    node.use_clamp = clamp

    for index, value in ((0, a), (1, b)):
        if value is None:
            continue
        if hasattr(value, 'node'):
            nt.links.new(value, node.inputs[index])
        else:
            node.inputs[index].default_value = value

    return node


def _bump(nt, bsdf, height, strength=0.25, distance=0.004, normal=None):
    node = nt.nodes.new("ShaderNodeBump")
    node.inputs['Strength'].default_value = strength
    node.inputs['Distance'].default_value = distance
    nt.links.new(height, node.inputs['Height'])
    if normal is not None:
        nt.links.new(normal, node.inputs['Normal'])
    nt.links.new(node.outputs['Normal'], bsdf.inputs['Normal'])
    return node


def _edges(nt, low=0.50, high=0.56):
    """
    How exposed a point is: 1 on a convex edge, 0 on a flat face.

    Paint wears off edges, zinc polishes on them, dirt never sits on them.
    Driving all three off one number is why the wear on these models lands
    where wear actually lands instead of being noise sprayed over
    everything.
    """
    geometry = nt.nodes.new("ShaderNodeNewGeometry")
    return _ramp(nt, geometry.outputs['Pointiness'],
                 [(low, (0, 0, 0)), (high, (1, 1, 1))]).outputs['Color']


# ------------------------------------------------------------- surfaces


def powder_coat(name, colour, roughness=0.38, wear=0.6, dirt=0.35, scale=30.0):
    """
    Painted steelwork: the lift's towers, its chairs, its terminal shells.

    Powder coat is sprayed and cured, so it has orange peel in it rather
    than being glass flat, and it fails at the edges first — which is where
    a chair is grabbed, kicked and knocked against a station all day.
    """
    mat, nt, bsdf = _mat(name, roughness=roughness)

    vector = _coords(nt, 1.0)
    steel = (0.30, 0.30, 0.32, 1.0)

    peel = _noise(nt, vector, scale * 8.0, detail=4.0, roughness=0.7)
    grime = _noise(nt, vector, scale * 0.35, detail=6.0, roughness=0.6)
    flecks = _noise(nt, vector, scale * 14.0, detail=6.0, roughness=0.7)

    # Chips are sparse and they are on the edges. A wear mask spread evenly
    # over a whole panel is just a grey paint job with extra steps.
    exposure = _edges(nt)
    chipped = _maths(nt, 'MULTIPLY', exposure, wear)
    specks = _ramp(nt, flecks.outputs['Fac'],
                   [(0.62, (0, 0, 0)), (0.70, (1, 1, 1))])
    scratched = _maths(nt, 'MULTIPLY', specks.outputs['Color'], wear * 0.45)
    worn = _maths(nt, 'ADD', chipped.outputs[0], scratched.outputs[0], clamp=True)

    shaded = _ramp(nt, grime.outputs['Fac'],
                   [(0.32, (0.68, 0.68, 0.70)), (0.72, (1.0, 1.0, 1.0))])
    painted = _mix(nt, 1.0, colour, shaded.outputs['Color'], blend='MULTIPLY')

    soiled = _mix(nt, _maths(nt, 'MULTIPLY', grime.outputs['Fac'], dirt).outputs[0],
                  painted.outputs['Color'], (0.20, 0.19, 0.17, 1.0))

    surface = _mix(nt, worn.outputs[0], soiled.outputs['Color'], steel)
    nt.links.new(surface.outputs['Color'], bsdf.inputs['Base Color'])

    metal = _maths(nt, 'MULTIPLY', worn.outputs[0], 0.85, clamp=True)
    nt.links.new(metal.outputs[0], bsdf.inputs['Metallic'])

    rough = _ramp(nt, worn.outputs[0],
                  [(0.0, (roughness, roughness, roughness)), (1.0, (0.30, 0.30, 0.30))])
    varied = _mix(nt, 0.35, rough.outputs['Color'], peel.outputs['Color'], blend='OVERLAY')
    nt.links.new(varied.outputs['Color'], bsdf.inputs['Roughness'])

    height = _mix(nt, 0.25, peel.outputs['Fac'], grime.outputs['Fac'])
    _bump(nt, bsdf, height.outputs['Color'], strength=0.18, distance=0.0025)

    return mat


def galvanised(name, tint=(0.62, 0.64, 0.66, 1.0), scale=22.0):
    """
    Hot-dip zinc: the spangle pattern is the zinc freezing into crystals.

    Every structural part of a lift that is not painted is this, and it is
    what stops a tower reading as a grey plastic tube.
    """
    mat, nt, bsdf = _mat(name, roughness=0.42, metallic=1.0, base=tint)

    vector = _coords(nt, 1.0)

    spangle = _voronoi(nt, vector, scale * 1.6, feature='F1', randomness=1.0)
    grain = _noise(nt, vector, scale * 12.0, detail=6.0, roughness=0.6)
    weather = _noise(nt, vector, scale * 0.4, detail=8.0, roughness=0.65)

    crystal = _ramp(nt, spangle.outputs['Distance'],
                    [(0.0, (0.52, 0.54, 0.57)), (0.35, (0.72, 0.74, 0.76)),
                     (0.75, (0.60, 0.61, 0.63))])
    aged = _mix(nt, _maths(nt, 'MULTIPLY', weather.outputs['Fac'], 0.55).outputs[0],
                crystal.outputs['Color'], (0.40, 0.41, 0.42, 1.0))

    nt.links.new(aged.outputs['Color'], bsdf.inputs['Base Color'])

    rough = _ramp(nt, spangle.outputs['Distance'],
                  [(0.0, (0.52, 0.52, 0.52)), (0.6, (0.34, 0.34, 0.34))])
    varied = _mix(nt, 0.4, rough.outputs['Color'], weather.outputs['Color'], blend='OVERLAY')
    nt.links.new(varied.outputs['Color'], bsdf.inputs['Roughness'])

    height = _mix(nt, 0.3, spangle.outputs['Distance'], grain.outputs['Fac'])
    _bump(nt, bsdf, height.outputs['Color'], strength=0.22, distance=0.0016)

    return mat


def steel(name, tint=(0.56, 0.57, 0.58, 1.0), roughness=0.25, brushed=True, scale=60.0):
    """Bare machined steel: shafts, bolts, sheave rims, a ski's edges."""
    mat, nt, bsdf = _mat(name, roughness=roughness, metallic=1.0, base=tint)

    vector = _coords(nt, 1.0)

    if brushed:
        lines = _wave(nt, vector, scale * 6.0, distortion=6.0, detail=3.0, bands='Z')
        rough = _ramp(nt, lines.outputs['Fac'],
                      [(0.0, (roughness * 0.7,) * 3), (1.0, (roughness * 1.5,) * 3)])
        nt.links.new(rough.outputs['Color'], bsdf.inputs['Roughness'])
        _bump(nt, bsdf, lines.outputs['Fac'], strength=0.1, distance=0.0004)

    smudge = _noise(nt, vector, scale * 0.5, detail=6.0)
    shaded = _mix(nt, _maths(nt, 'MULTIPLY', smudge.outputs['Fac'], 0.4).outputs[0],
                  tint, (0.34, 0.34, 0.35, 1.0))
    nt.links.new(shaded.outputs['Color'], bsdf.inputs['Base Color'])

    return mat


def rubber(name, colour=BLACK, roughness=0.72, scale=90.0):
    """Moulded rubber: the grip on a pole, a seat mat, a sheave liner."""
    mat, nt, bsdf = _mat(name, roughness=roughness, base=colour)

    vector = _coords(nt, 1.0)

    pebble = _voronoi(nt, vector, scale * 3.0, feature='F1')
    dust = _noise(nt, vector, scale * 0.6, detail=6.0)

    shaded = _mix(nt, _maths(nt, 'MULTIPLY', dust.outputs['Fac'], 0.25).outputs[0],
                  colour, (0.09, 0.09, 0.10, 1.0))
    nt.links.new(shaded.outputs['Color'], bsdf.inputs['Base Color'])

    rough = _ramp(nt, pebble.outputs['Distance'],
                  [(0.0, (roughness + 0.12,) * 3), (1.0, (roughness - 0.14,) * 3)])
    nt.links.new(rough.outputs['Color'], bsdf.inputs['Roughness'])

    _bump(nt, bsdf, pebble.outputs['Distance'], strength=0.35, distance=0.0015)

    return mat


def plastic(name, colour, roughness=0.34, scale=70.0, coat=0.3):
    """Injection-moulded plastic: seat shells, binding parts, pole baskets."""
    mat, nt, bsdf = _mat(name, roughness=roughness, base=colour)
    bsdf.inputs['Coat Weight'].default_value = coat
    bsdf.inputs['Coat Roughness'].default_value = 0.15

    vector = _coords(nt, 1.0)

    texture = _voronoi(nt, vector, scale * 6.0, feature='F1')
    scuff = _noise(nt, vector, scale * 1.2, detail=7.0)
    exposure = _edges(nt, 0.50, 0.58)

    shaded = _mix(nt, _maths(nt, 'MULTIPLY', scuff.outputs['Fac'], 0.18).outputs[0],
                  colour, (colour[0] * 0.55, colour[1] * 0.55, colour[2] * 0.55, 1.0))
    polished = _mix(nt, _maths(nt, 'MULTIPLY', exposure, 0.35).outputs[0],
                    shaded.outputs['Color'],
                    (min(1.0, colour[0] * 1.6), min(1.0, colour[1] * 1.6),
                     min(1.0, colour[2] * 1.6), 1.0))
    nt.links.new(polished.outputs['Color'], bsdf.inputs['Base Color'])

    rough = _ramp(nt, texture.outputs['Distance'],
                  [(0.0, (roughness + 0.10,) * 3), (1.0, (roughness - 0.08,) * 3)])
    nt.links.new(rough.outputs['Color'], bsdf.inputs['Roughness'])

    _bump(nt, bsdf, texture.outputs['Distance'], strength=0.18, distance=0.0008)

    return mat


def seat_pad(name, colour, roughness=0.55, scale=45.0):
    """The moulded pad on a chair: dimpled, matte, a season of grime in it."""
    mat, nt, bsdf = _mat(name, roughness=roughness, base=colour)

    vector = _coords(nt, 1.0)

    dimples = _voronoi(nt, vector, scale * 2.2, feature='F1', randomness=0.35)
    wear = _noise(nt, vector, scale * 0.5, detail=7.0, roughness=0.6)
    exposure = _edges(nt, 0.50, 0.57)

    faded = _mix(nt, _maths(nt, 'MULTIPLY', wear.outputs['Fac'], 0.45).outputs[0],
                 colour, (colour[0] * 0.6, colour[1] * 0.6, colour[2] * 0.62, 1.0))
    polished = _mix(nt, _maths(nt, 'MULTIPLY', exposure, 0.5).outputs[0],
                    faded.outputs['Color'], (0.16, 0.16, 0.17, 1.0))
    nt.links.new(polished.outputs['Color'], bsdf.inputs['Base Color'])

    rough = _ramp(nt, wear.outputs['Fac'],
                  [(0.0, (roughness + 0.15,) * 3), (1.0, (roughness - 0.12,) * 3)])
    nt.links.new(rough.outputs['Color'], bsdf.inputs['Roughness'])

    _bump(nt, bsdf, dimples.outputs['Distance'], strength=0.3, distance=0.0012)

    return mat


def concrete(name, tint=(0.52, 0.51, 0.49, 1.0), scale=18.0):
    """Footings and the pad a station stands on."""
    mat, nt, bsdf = _mat(name, roughness=0.78, base=tint)

    vector = _coords(nt, 1.0)

    aggregate = _voronoi(nt, vector, scale * 4.0, feature='F1')
    stain = _noise(nt, vector, scale * 0.5, detail=8.0, roughness=0.6)
    pores = _noise(nt, vector, scale * 20.0, detail=4.0)

    shaded = _ramp(nt, aggregate.outputs['Distance'],
                   [(0.0, (0.44, 0.43, 0.41)), (0.5, (0.58, 0.57, 0.55)),
                    (1.0, (0.50, 0.49, 0.47))])
    stained = _mix(nt, _maths(nt, 'MULTIPLY', stain.outputs['Fac'], 0.5).outputs[0],
                   shaded.outputs['Color'], (0.33, 0.32, 0.30, 1.0))
    nt.links.new(stained.outputs['Color'], bsdf.inputs['Base Color'])

    height = _mix(nt, 0.4, aggregate.outputs['Distance'], pores.outputs['Fac'])
    _bump(nt, bsdf, height.outputs['Color'], strength=0.4, distance=0.004)

    return mat


def timber(name, tint=(0.34, 0.22, 0.13, 1.0), scale=8.0):
    """Sawn larch: the cladding and decking round a station."""
    mat, nt, bsdf = _mat(name, roughness=0.62, base=tint)

    vector = _coords(nt, 1.0)

    stretched = nt.nodes.new("ShaderNodeMapping")
    stretched.inputs['Scale'].default_value = (1.0, 1.0, 0.06)
    nt.links.new(vector, stretched.inputs['Vector'])

    rings = _noise(nt, stretched.outputs['Vector'], scale * 3.0, detail=8.0,
                   roughness=0.6, distortion=1.4)
    fibre = _noise(nt, stretched.outputs['Vector'], scale * 40.0, detail=4.0)

    grained = _ramp(nt, rings.outputs['Fac'],
                    [(0.25, (tint[0] * 0.55, tint[1] * 0.55, tint[2] * 0.55)),
                     (0.6, (tint[0], tint[1], tint[2])),
                     (0.85, (min(1, tint[0] * 1.35), min(1, tint[1] * 1.35), min(1, tint[2] * 1.35)))])
    nt.links.new(grained.outputs['Color'], bsdf.inputs['Base Color'])

    rough = _ramp(nt, fibre.outputs['Fac'], [(0.0, (0.72,) * 3), (1.0, (0.52,) * 3)])
    nt.links.new(rough.outputs['Color'], bsdf.inputs['Roughness'])

    height = _mix(nt, 0.5, rings.outputs['Fac'], fibre.outputs['Fac'])
    _bump(nt, bsdf, height.outputs['Color'], strength=0.35, distance=0.003)

    return mat


def glass(name, tint=(0.72, 0.78, 0.82, 1.0)):
    """A station window. Baked flat, because a game asset cannot refract."""
    mat, nt, bsdf = _mat(name, roughness=0.06, metallic=0.0, base=tint)
    bsdf.inputs['Specular IOR Level'].default_value = 0.9
    return mat


def topsheet(name, base, accent, highlight, length=1.7, half_width=0.06):
    """
    The printed top of a ski or a board, under a clear coat.

    The graphic is laid out rather than sprayed on: a field of the base
    colour, a swash of the accent over the shovel, and a pair of thin
    stripes down the length. It has to read at two metres and from above,
    because directly above and slightly behind is the only place the
    player ever sees it from.
    """
    mat, nt, bsdf = _mat(name, roughness=0.18, base=base)
    bsdf.inputs['Coat Weight'].default_value = 0.9
    bsdf.inputs['Coat Roughness'].default_value = 0.07

    vector = _coords(nt, 1.0)
    axes = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(vector, axes.inputs['Vector'])

    # Along the plank, 0 at the tip and 1 at the tail; and across it,
    # 0 in the middle and 1 at either edge.
    along = _maths(nt, 'MULTIPLY_ADD', axes.outputs['Y'], 1.0 / length)
    along.inputs[2].default_value = 0.5

    across = _maths(nt, 'DIVIDE', axes.outputs['X'], half_width)
    edgeward = _maths(nt, 'ABSOLUTE', across.outputs[0])

    wobble = _noise(nt, vector, 3.0, detail=4.0, roughness=0.5)
    waver = _maths(nt, 'MULTIPLY_ADD', wobble.outputs['Fac'], 0.16)
    waver.inputs[2].default_value = -0.08
    wavy = _maths(nt, 'ADD', along.outputs[0], waver.outputs[0])

    shovel = _ramp(nt, wavy.outputs[0],
                   [(0.06, (1, 1, 1)), (0.34, (1, 1, 1)), (0.40, (0, 0, 0))])
    stripe = _ramp(nt, edgeward.outputs[0],
                   [(0.40, (0, 0, 0)), (0.44, (1, 1, 1)), (0.58, (1, 1, 1)),
                    (0.62, (0, 0, 0))])
    tail = _ramp(nt, along.outputs[0],
                 [(0.72, (0, 0, 0)), (0.86, (1, 1, 1))])

    grain = _noise(nt, vector, 180.0, detail=5.0)
    scuff = _noise(nt, vector, 18.0, detail=7.0, roughness=0.6)

    field = _mix(nt, tail.outputs['Color'], base,
                 (base[0] * 0.45, base[1] * 0.45, base[2] * 0.5, 1.0))
    swashed = _mix(nt, shovel.outputs['Color'], field.outputs['Color'], accent)
    striped = _mix(nt, stripe.outputs['Color'], swashed.outputs['Color'], highlight)
    printed = _mix(nt, 0.10, striped.outputs['Color'], grain.outputs['Color'], blend='OVERLAY')
    nt.links.new(printed.outputs['Color'], bsdf.inputs['Base Color'])

    rough = _ramp(nt, scuff.outputs['Fac'], [(0.0, (0.13,) * 3), (1.0, (0.28,) * 3)])
    nt.links.new(rough.outputs['Color'], bsdf.inputs['Roughness'])

    _bump(nt, bsdf, grain.outputs['Fac'], strength=0.06, distance=0.0003)

    return mat


def sintered_base(name, colour=(0.045, 0.048, 0.052, 1.0), scale=8.0):
    """
    The running surface: black sintered polyethylene with stone structure
    ground into it, running the length of the ski. It is the one surface on
    a ski that is meant to be slightly rough — structure is what stops it
    suctioning to wet snow — so it must not be a mirror.
    """
    mat, nt, bsdf = _mat(name, roughness=0.36, base=colour)

    vector = _coords(nt, 1.0)

    structure = _wave(nt, vector, scale * 40.0, distortion=8.0, detail=2.0, bands='X')
    wax = _noise(nt, vector, scale * 4.0, detail=6.0)

    shaded = _mix(nt, _maths(nt, 'MULTIPLY', wax.outputs['Fac'], 0.3).outputs[0],
                  colour, (0.10, 0.10, 0.11, 1.0))
    nt.links.new(shaded.outputs['Color'], bsdf.inputs['Base Color'])

    rough = _ramp(nt, structure.outputs['Fac'], [(0.0, (0.30,) * 3), (1.0, (0.44,) * 3)])
    nt.links.new(rough.outputs['Color'], bsdf.inputs['Roughness'])

    _bump(nt, bsdf, structure.outputs['Fac'], strength=0.15, distance=0.0003)

    return mat


def webbing(name, colour, scale=120.0):
    """Binding straps: a woven ladder of nylon, matte and fibrous."""
    mat, nt, bsdf = _mat(name, roughness=0.68, base=colour)

    vector = _coords(nt, 1.0)

    warp = _wave(nt, vector, scale, distortion=0.0, detail=1.0, bands='X')
    weft = _wave(nt, vector, scale, distortion=0.0, detail=1.0, bands='Y')
    weave = _mix(nt, 0.5, warp.outputs['Fac'], weft.outputs['Fac'], blend='DIFFERENCE')
    fuzz = _noise(nt, vector, scale * 4.0, detail=5.0)

    shaded = _mix(nt, _maths(nt, 'MULTIPLY', weave.outputs['Color'], 0.35).outputs[0],
                  colour, (colour[0] * 0.5, colour[1] * 0.5, colour[2] * 0.5, 1.0))
    nt.links.new(shaded.outputs['Color'], bsdf.inputs['Base Color'])

    rough = _ramp(nt, fuzz.outputs['Fac'], [(0.0, (0.74,) * 3), (1.0, (0.60,) * 3)])
    nt.links.new(rough.outputs['Color'], bsdf.inputs['Roughness'])

    _bump(nt, bsdf, weave.outputs['Color'], strength=0.4, distance=0.0008)

    return mat


def grip_foam(name, colour=(0.06, 0.065, 0.07, 1.0), scale=55.0):
    """A ski pole's grip: dense moulded foam with ribs pressed into it."""
    mat, nt, bsdf = _mat(name, roughness=0.78, base=colour)

    vector = _coords(nt, 1.0)

    ribs = _wave(nt, vector, scale * 1.6, distortion=0.4, detail=1.0, bands='Z')
    cells = _voronoi(nt, vector, scale * 8.0, feature='F1')

    shaded = _mix(nt, _maths(nt, 'MULTIPLY', ribs.outputs['Fac'], 0.3).outputs[0],
                  colour, (0.14, 0.14, 0.15, 1.0))
    nt.links.new(shaded.outputs['Color'], bsdf.inputs['Base Color'])

    rough = _ramp(nt, cells.outputs['Distance'], [(0.0, (0.86,) * 3), (1.0, (0.68,) * 3)])
    nt.links.new(rough.outputs['Color'], bsdf.inputs['Roughness'])

    height = _mix(nt, 0.35, ribs.outputs['Fac'], cells.outputs['Distance'])
    _bump(nt, bsdf, height.outputs['Color'], strength=0.45, distance=0.0016)

    return mat


def anodised(name, tint=(0.42, 0.45, 0.50, 1.0), roughness=0.30, scale=40.0):
    """
    Anodised aluminium: pole shafts, binding baseplates, sheave hubs.

    Slightly darker and much more even than steel, because the oxide layer
    is the surface rather than the metal under it.
    """
    mat, nt, bsdf = _mat(name, roughness=roughness, metallic=0.85, base=tint)

    vector = _coords(nt, 1.0)

    drawn = _wave(nt, vector, scale * 20.0, distortion=3.0, detail=2.0, bands='Z')
    marks = _noise(nt, vector, scale * 2.0, detail=6.0)
    exposure = _edges(nt, 0.50, 0.57)

    scuffed = _mix(nt, _maths(nt, 'MULTIPLY', exposure, 0.4).outputs[0],
                   tint, (0.68, 0.69, 0.70, 1.0))
    nt.links.new(scuffed.outputs['Color'], bsdf.inputs['Base Color'])

    rough = _ramp(nt, drawn.outputs['Fac'],
                  [(0.0, (roughness * 0.8,) * 3), (1.0, (roughness * 1.35,) * 3)])
    varied = _mix(nt, 0.3, rough.outputs['Color'], marks.outputs['Color'], blend='OVERLAY')
    nt.links.new(varied.outputs['Color'], bsdf.inputs['Roughness'])

    _bump(nt, bsdf, drawn.outputs['Fac'], strength=0.08, distance=0.0003)

    return mat


def shell_fabric(name, colour, roughness=0.62, scale=140.0, sheen=0.4):
    """
    Technical outerwear: a woven face fabric with a slight sheen and a
    seam's worth of wear on the edges.

    A jacket is not a painted surface. The weave is what stops a large flat
    panel of colour reading as plastic, and the sheen is what says the
    fabric is coated rather than cotton.
    """
    mat, nt, bsdf = _mat(name, roughness=roughness, base=colour)
    bsdf.inputs['Sheen Weight'].default_value = sheen
    bsdf.inputs['Sheen Roughness'].default_value = 0.35

    vector = _coords(nt, 1.0)

    warp = _wave(nt, vector, scale, distortion=0.3, detail=1.0, bands='X')
    weft = _wave(nt, vector, scale, distortion=0.3, detail=1.0, bands='Z')
    weave = _mix(nt, 0.5, warp.outputs['Fac'], weft.outputs['Fac'], blend='DIFFERENCE')

    creases = _noise(nt, vector, 26.0, detail=7.0, roughness=0.6)
    exposure = _edges(nt, 0.50, 0.58)

    shaded = _mix(nt, _maths(nt, 'MULTIPLY', creases.outputs['Fac'], 0.30).outputs[0],
                  colour, (colour[0] * 0.55, colour[1] * 0.55, colour[2] * 0.58, 1.0))
    rubbed = _mix(nt, _maths(nt, 'MULTIPLY', exposure, 0.28).outputs[0],
                  shaded.outputs['Color'],
                  (min(1.0, colour[0] * 1.5 + 0.06), min(1.0, colour[1] * 1.5 + 0.06),
                   min(1.0, colour[2] * 1.5 + 0.06), 1.0))
    nt.links.new(rubbed.outputs['Color'], bsdf.inputs['Base Color'])

    rough = _ramp(nt, weave.outputs['Color'],
                  [(0.0, (roughness + 0.10,) * 3), (1.0, (roughness - 0.10,) * 3)])
    nt.links.new(rough.outputs['Color'], bsdf.inputs['Roughness'])

    height = _mix(nt, 0.45, weave.outputs['Color'], creases.outputs['Fac'])
    _bump(nt, bsdf, height.outputs['Color'], strength=0.30, distance=0.0010)

    return mat


def lens(name, tint, roughness=0.05):
    """
    A goggle lens: a mirror with a colour behind it.

    It is modelled as metal because that is what a mirror coating is, and
    because a transparent lens in a baked game asset is a dark hole.
    """
    mat, nt, bsdf = _mat(name, roughness=roughness, metallic=1.0, base=tint)

    vector = _coords(nt, 1.0)
    sweepy = _noise(nt, vector, 3.0, detail=4.0)

    shaded = _mix(nt, 0.35, tint,
                  (min(1.0, tint[0] * 1.8 + 0.12), min(1.0, tint[1] * 1.8 + 0.12),
                   min(1.0, tint[2] * 1.8 + 0.12), 1.0))
    graded = _mix(nt, sweepy.outputs['Fac'], tint, shaded.outputs['Color'])
    nt.links.new(graded.outputs['Color'], bsdf.inputs['Base Color'])

    return mat
