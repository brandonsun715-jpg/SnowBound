"""
The player: a skier and a snowboarder, built from the same body.

Two things had to be true at once. It has to look like somebody who can
ride — baggy shell over the hips, cuffs breaking over the boots, hood, big
lens, mittens — and it has to bend, because the game sits the player on a
chairlift and turns their shoulders across the board.

So it is not one mesh. It is fifteen parts, each with its origin on the
joint it turns about, parented into a chain: hips carry the thighs, thighs
carry the shins, shins carry the boots, the torso carries the arms and the
head. Unity gets that hierarchy as plain transforms, and PlayerVisual
turns them. No armature, no skinning, nothing that needs an avatar or an
animation clip — which is also why a joint is covered by a cuff, a hem or
a sleeve everywhere it bends.

Built in Blender's axes: Z up, and -Y is the way the rider is facing,
which is +Z once Unity has it.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import math

import kit
import shapes
import surfaces as S

# The one place the skeleton is written down. Everything else is hung off
# these, so a change of stance is a change here rather than in fifty
# numbers spread through the file.
SKI = {
    'ankle': (0.140, 0.000, 0.110),
    'knee': (0.146, -0.140, 0.480),
    'hip': (0.105, 0.040, 0.890),
    'waist': (0.0, 0.018, 1.005),
    'chest': (0.0, -0.028, 1.275),
    'shoulder': (0.205, -0.048, 1.395),
    'elbow': (0.258, -0.178, 1.150),
    'wrist': (0.238, -0.362, 1.078),
    'neck': (0.0, -0.050, 1.480),
    'toe': -0.155,
    'foot_yaw': {-1: 2.0, 1: -2.0},
    'hips': (0.150, 0.124),
    'hood': False,
}

# The board stance: feet where the bindings are, fore and aft, knees
# driven toward the toe edge. The torso stays square to the board — the
# game turns the shoulders across it at runtime.
BOARD = {
    # Front foot toward the nose, which is -Y, and both knees driven the
    # same way — toward the toe edge. A snowboarder's legs are not a
    # mirrored pair, which is the whole reason this is written out.
    'legs': {
        -1: {'hip': (-0.055, -0.090, 0.850), 'knee': (0.055, -0.220, 0.455),
             'ankle': (0.010, -0.278, 0.105)},
        1: {'hip': (0.055, 0.092, 0.845), 'knee': (0.100, 0.210, 0.450),
            'ankle': (0.030, 0.278, 0.105)},
    },
    'foot_yaw': {-1: 16.0, 1: -7.0},
    'hips': (0.124, 0.156),
    'pant_width': 0.94,
    'waist': (0.0, 0.028, 0.945),
    'chest': (0.0, -0.018, 1.215),
    'shoulder': (0.208, -0.040, 1.340),
    'elbow': (0.288, -0.130, 1.115),
    'wrist': (0.276, -0.300, 1.170),
    'neck': (0.0, -0.042, 1.425),
    'toe': -0.165,
    'hood': True,
}

# Trim is the dark colour a hem, a cuff and a scuff guard are really made
# in; accent is the one bright thing on the whole outfit. Getting those the
# wrong way round is what makes a character look like a mascot.
KITS = {
    'ski': dict(
        shell=(0.42, 0.115, 0.022),
        trim=(0.052, 0.050, 0.058),
        accent=(0.86, 0.46, 0.07),
        pants=(0.088, 0.090, 0.104),
        boot=(0.058, 0.060, 0.068),
        hard=(0.028, 0.029, 0.034),
        lens=(0.50, 0.24, 0.045),
        mitt=(0.062, 0.064, 0.074),
    ),
    'board': dict(
        shell=(0.040, 0.215, 0.230),
        trim=(0.048, 0.052, 0.056),
        accent=(0.72, 0.68, 0.58),
        pants=(0.315, 0.282, 0.220),
        boot=(0.042, 0.044, 0.050),
        hard=(0.028, 0.029, 0.034),
        lens=(0.08, 0.24, 0.48),
        mitt=(0.055, 0.058, 0.064),
    ),
}


def materials(kind):
    c = KITS[kind]

    return {
        'shell': S.shell_fabric("RiderShell" + kind, c['shell']),
        'trim': S.shell_fabric("RiderTrim" + kind, c['trim'], roughness=0.58),
        'accent': S.shell_fabric("RiderAccent" + kind, c['accent'], roughness=0.50),
        'pants': S.shell_fabric("RiderPants" + kind, c['pants'], roughness=0.66),
        'mitt': S.shell_fabric("RiderMitt" + kind, c['mitt'], roughness=0.70),
        'gaiter': S.shell_fabric("RiderGaiter" + kind, (0.09, 0.092, 0.105),
                                 roughness=0.78, scale=200.0),
        'helmet': S.plastic("RiderHelmet" + kind, c['hard'], roughness=0.26, coat=0.5),
        'boot': S.plastic("RiderBoot" + kind, c['boot'], roughness=0.34, coat=0.35),
        'sole': S.rubber("RiderSole" + kind),
        'lens': S.lens("RiderLens" + kind, c['lens']),
        'strap': S.webbing("RiderStrap" + kind, (0.10, 0.10, 0.11, 1.0)),
        'metal': S.steel("RiderMetal" + kind, roughness=0.28, scale=120.0),
    }


# ------------------------------------------------------------- geometry


def sleeve(name, path, radii, mat, square=0.35, count=16, ends=True):
    """A soft tube down a path: a leg, an arm, a body, a hood."""
    sections = [shapes.oval(rx, ry, count, square) for rx, ry in radii]
    ob = kit.loft(name, shapes.sweep(None, path, up=(0, 1, 0), sections=sections),
                  closed_ends=ends, mat=mat)
    return kit.smooth(ob, angle=52)


def lower(pose, side):
    """Hip, knee and ankle for one leg, however this stance defines them."""
    if 'legs' in pose:
        joints = pose['legs'][side]
        return joints['hip'], joints['knee'], joints['ankle']

    return (mirrored(pose['hip'], side), mirrored(pose['knee'], side),
            mirrored(pose['ankle'], side))


def turn(point, centre, degrees):
    """Swing a point round a vertical axis: a boot on its binding angle."""
    a = math.radians(degrees)
    dx, dy = point[0] - centre[0], point[1] - centre[1]

    return (centre[0] + dx * math.cos(a) - dy * math.sin(a),
            centre[1] + dx * math.sin(a) + dy * math.cos(a),
            point[2])


def mirrored(point, side, lean=(0.0, 0.0, 0.0)):
    """
    The same joint on the other side of the body, with an option to break
    the symmetry.

    A rider whose two arms are exact mirrors of each other reads as a shop
    dummy. Half a hand's worth of difference is enough to fix it.
    """
    return (point[0] * side + lean[0] * side,
            point[1] + lean[1], point[2] + lean[2])


def between(a, b, t, lift=(0.0, 0.0, 0.0)):
    return (a[0] + (b[0] - a[0]) * t + lift[0],
            a[1] + (b[1] - a[1]) * t + lift[1],
            a[2] + (b[2] - a[2]) * t + lift[2])


def leg(m, hip, knee, ankle, tag, width=1.0):
    """
    Thigh and shin, in baggy pants, with the cuff flaring over the boot.

    Two parts, because a knee that cannot bend cannot be sat down on a
    chairlift; and the cuff is wide enough at the bottom that the join
    disappears inside it.
    """
    thigh = sleeve("Thigh" + tag, [
        between(hip, knee, -0.14), hip,
        between(hip, knee, 0.45), between(hip, knee, 0.92)],
        [(0.106 * width, 0.124 * width), (0.112 * width, 0.130 * width),
         (0.104 * width, 0.122 * width), (0.100 * width, 0.118 * width)],
        m['pants'], square=0.45)

    # The cuff stops short of the ankle and flares, so the boot shows
    # under it. Pants that swallow the boot read as pyjamas.
    shin = sleeve("Shin" + tag, [
        between(knee, ankle, -0.10), knee,
        between(knee, ankle, 0.42), between(knee, ankle, 0.68),
        between(knee, ankle, 0.80)],
        [(0.102 * width, 0.120 * width), (0.104 * width, 0.122 * width),
         (0.100 * width, 0.118 * width), (0.110 * width, 0.128 * width),
         (0.118 * width, 0.138 * width)],
        m['pants'], square=0.45)

    hem = sleeve("Cuff" + tag, [
        between(knee, ankle, 0.74), between(knee, ankle, 0.84, lift=(0, 0.008, 0))],
        [(0.112 * width, 0.132 * width), (0.120 * width, 0.142 * width)],
        m['trim'], square=0.5)

    return thigh, kit.join([shin, hem], "Shin" + tag)


def ski_boot(pose, m, ankle, yaw, tag):
    """A rigid shell boot: sole, shell, a leaning cuff and its buckles."""
    x, y, z = ankle
    parts = []

    shell = sleeve("BootShell" + tag, [
        (x, y + pose['toe'] - 0.02, z - 0.055), (x, y + pose['toe'] + 0.04, z - 0.045),
        (x, y - 0.010, z - 0.030), (x, y + 0.075, z - 0.015),
        (x, y + 0.105, z + 0.020)],
        [(0.038, 0.030), (0.058, 0.062), (0.070, 0.080), (0.068, 0.070),
         (0.055, 0.045)], m['boot'], square=0.55)
    parts.append(shell)

    cuff = sleeve("BootCuff" + tag, [
        (x, y + 0.010, z - 0.020), (x, y + 0.030, z + 0.090),
        (x, y + 0.048, z + 0.165)],
        [(0.068, 0.078), (0.064, 0.072), (0.062, 0.070)], m['boot'], square=0.5)
    parts.append(cuff)

    sole = kit.box("BootSole" + tag, (0.098, 0.310, 0.030),
                   (x, y - 0.020, z - 0.100), mat=m['sole'])
    kit.bevel(sole, 0.008, segments=2)
    parts.append(sole)

    for i, (dy, dz) in enumerate(((0.02, 0.010), (0.03, 0.075), (0.042, 0.140))):
        buckle = kit.box("Buckle" + tag + str(i), (0.104, 0.052, 0.020),
                         (x, y + dy - 0.02, z + dz), mat=m['metal'])
        kit.bevel(buckle, 0.005, segments=2)
        parts.append(buckle)

    strap = kit.box("BootStrap" + tag, (0.098, 0.030, 0.034),
                    (x, y + 0.030, z + 0.180), mat=m['strap'])
    kit.bevel(strap, 0.006, segments=2)
    parts.append(strap)

    return angled(kit.join(parts, "Boot" + tag), ankle, yaw)


def board_boot(pose, m, ankle, yaw, tag):
    """A soft boot: a laced tongue, a padded cuff and a power strap."""
    x, y, z = ankle
    parts = []

    shell = sleeve("BootShell" + tag, [
        (x, y + pose['toe'] - 0.01, z - 0.058), (x, y + pose['toe'] + 0.05, z - 0.042),
        (x, y - 0.005, z - 0.026), (x, y + 0.060, z + 0.035),
        (x, y + 0.070, z + 0.130), (x, y + 0.072, z + 0.180)],
        [(0.042, 0.036), (0.062, 0.066), (0.076, 0.082), (0.078, 0.078),
         (0.070, 0.068), (0.064, 0.062)], m['boot'], square=0.45)
    parts.append(shell)

    tongue = sleeve("BootTongue" + tag, [
        (x, y - 0.055, z + 0.010), (x, y - 0.030, z + 0.120),
        (x, y - 0.018, z + 0.175)],
        [(0.050, 0.020), (0.048, 0.020), (0.046, 0.018)], m['trim'], square=0.6)
    parts.append(tongue)

    sole = kit.box("BootSole" + tag, (0.100, 0.300, 0.034),
                   (x, y - 0.015, z - 0.100), mat=m['sole'])
    kit.bevel(sole, 0.010, segments=2)
    parts.append(sole)

    for i, dz in enumerate((0.055, 0.115, 0.170)):
        lace = kit.box("Lace" + tag + str(i), (0.092, 0.016, 0.012),
                       (x, y - 0.042, z + dz), mat=m['strap'])
        kit.bevel(lace, 0.004, segments=2)
        parts.append(lace)

    return angled(kit.join(parts, "Boot" + tag), ankle, yaw)


def angled(ob, centre, yaw):
    """
    Swing a finished part round a vertical axis through a joint: a boot
    onto its binding angle.

    The mesh is moved so the joint is at the origin, turned, and put back
    by baking the object's own transform — which leaves the part exactly
    where it was, rotated about the right point.
    """
    if abs(yaw) < 0.01:
        return ob

    ob.data.transform(kit.Matrix.Translation(-kit.Vector(centre)))
    ob.location = centre
    ob.rotation_euler = (0.0, 0.0, math.radians(yaw))

    return kit.apply_transform(ob)


def torso(pose, m):
    """
    The jacket: dropped hem, wide through the body, and a hood — up over
    the helmet on the board, bunched at the neck on skis.
    """
    waist, chest, neck = pose['waist'], pose['chest'], pose['neck']
    parts = []

    body = sleeve("Jacket", [
        between(waist, chest, -0.58), between(waist, chest, -0.40),
        between(waist, chest, -0.16), waist,
        between(waist, chest, 0.45), chest,
        between(chest, neck, 0.50), between(chest, neck, 0.92)],
        [(0.176, 0.124), (0.196, 0.140), (0.194, 0.138), (0.180, 0.128),
         (0.188, 0.132), (0.204, 0.142), (0.194, 0.136), (0.118, 0.096)],
        m['shell'], square=0.35, count=20)
    parts.append(body)

    hem = sleeve("Hem", [between(waist, chest, -0.62), between(waist, chest, -0.47)],
                 [(0.180, 0.128), (0.198, 0.142)], m['trim'], square=0.35, count=20)
    parts.append(hem)

    zip_line = kit.box("Zip", (0.016, 0.012, 0.430),
                       (0.0, between(waist, chest, 0.2)[1] - 0.132,
                        between(waist, chest, 0.15)[2] + 0.06), rot=(7, 0, 0), mat=m['accent'])
    kit.bevel(zip_line, 0.003, segments=2)
    parts.append(zip_line)

    pocket = kit.box("Pocket", (0.120, 0.026, 0.044),
                     (0.082, chest[1] - 0.126, chest[2] + 0.010), rot=(4, 0, 0), mat=m['trim'])
    kit.bevel(pocket, 0.007, segments=2)
    parts.append(pocket)

    if pose['hood']:
        # Up over the helmet, and open at the face. A hood is a shell with
        # a rim: modelled as a closed tube it would swallow the goggles,
        # and as a single surface you would see straight through it from
        # the side.
        head_y, head_z = neck[1] - 0.010, neck[2] + 0.135
        path = [(0.0, head_y - 0.086, head_z + 0.026),
                (0.0, head_y - 0.010, head_z + 0.040),
                (0.0, head_y + 0.076, head_z + 0.028),
                (0.0, head_y + 0.140, head_z - 0.020)]

        outer = [(0.130, 0.138), (0.152, 0.160), (0.146, 0.152), (0.066, 0.070)]
        inner = [(0.113, 0.121), (0.135, 0.143), (0.129, 0.135), (0.050, 0.054)]

        rings = shapes.sweep(None, path, up=(0, 0, 1),
                             sections=[shapes.oval(rx, ry, 20, 0.3) for rx, ry in outer])
        lining = shapes.sweep(None, path, up=(0, 0, 1),
                              sections=[shapes.oval(rx, ry, 20, 0.3) for rx, ry in inner])

        hood = kit.loft("Hood", rings + lining[::-1] + [rings[0]],
                        closed_ends=False, mat=m['shell'])
        kit.smooth(hood, angle=52)
        kit.weld(hood)
    else:
        hood = sleeve("Hood", [
            (0.0, neck[1] + 0.062, neck[2] - 0.090),
            (0.0, neck[1] + 0.090, neck[2] - 0.010),
            (0.0, neck[1] + 0.072, neck[2] + 0.050)],
            [(0.126, 0.078), (0.138, 0.094), (0.108, 0.070)],
            m['shell'], square=0.35, count=18)

    parts.append(hood)

    collar = sleeve("Collar", [
        (0.0, neck[1], neck[2] - 0.070), (0.0, neck[1], neck[2] + 0.010)],
        [(0.098, 0.086), (0.090, 0.080)], m['gaiter'], square=0.3, count=16)
    parts.append(collar)

    return kit.join(parts, "Torso")


def head(pose, m):
    """
    Helmet, goggles and a gaiter over the face.

    The goggle is a band swept round the front of the head, which is how a
    real one is made: one curved lens in a soft frame, with the strap round
    the back. Sweeping it means it sits on the face at every angle instead
    of being a flat plate stuck to the front.

    Nobody in the park rides bare-faced, which is convenient: a gaiter and
    a big lens between them mean there is no face to model, and a modelled
    face at this size is worse than none.
    """
    neck = pose['neck']
    cx, cy, cz = 0.0, neck[1] - 0.010, neck[2] + 0.135
    parts = []

    face = sleeve("Face", [
        (cx, cy + 0.012, cz - 0.145), (cx, cy + 0.006, cz - 0.085),
        (cx, cy, cz - 0.025), (cx, cy - 0.004, cz + 0.035)],
        [(0.068, 0.070), (0.082, 0.086), (0.088, 0.092), (0.086, 0.092)],
        m['gaiter'], square=0.35, count=18)
    parts.append(face)

    helmet = sleeve("Helmet", [
        (cx, cy + 0.004, cz - 0.035), (cx, cy, cz + 0.025),
        (cx, cy - 0.004, cz + 0.080), (cx, cy - 0.006, cz + 0.122),
        (cx, cy - 0.008, cz + 0.148)],
        [(0.104, 0.112), (0.116, 0.126), (0.114, 0.123), (0.098, 0.105),
         (0.070, 0.074)], m['helmet'], square=0.25, count=20)
    parts.append(helmet)

    # Round the front of the head: the arc is centred on -Y, which is the
    # way the rider faces.
    def band(radius, start, end, steps=14):
        return shapes.arc((cx, cy + 0.004, cz + 0.036), radius, start, end,
                          steps=steps, plane='XY')

    frame = kit.loft("GoggleFrame", shapes.sweep(
        [(-0.011, -0.054), (0.011, -0.054), (0.011, 0.054), (-0.011, 0.054)],
        band(0.119, -158, -22), up=(0, 0, 1)),
        closed_ends=True, mat=m['gaiter'])
    parts.append(kit.smooth(frame, angle=45))

    glass = kit.loft("GoggleLens", shapes.sweep(
        [(-0.006, -0.043), (0.009, -0.043), (0.009, 0.043), (-0.006, 0.043)],
        band(0.127, -152, -28), up=(0, 0, 1)),
        closed_ends=True, mat=m['lens'])
    parts.append(kit.smooth(glass, angle=45))

    strap = kit.loft("GoggleStrap", shapes.sweep(
        [(-0.007, -0.024), (0.007, -0.024), (0.007, 0.024), (-0.007, 0.024)],
        band(0.122, 16, 164, steps=12), up=(0, 0, 1)),
        closed_ends=True, mat=m['strap'])
    parts.append(kit.smooth(strap, angle=45))

    clip = kit.box("GoggleClip", (0.042, 0.018, 0.030),
                   (cx, cy + 0.112, cz + 0.036), mat=m['accent'])
    kit.bevel(clip, 0.005, segments=2)
    parts.append(clip)

    return kit.join(parts, "Head")


def arm(pose, m, side, tag):
    """Upper arm, forearm and a mitten, all inside one baggy sleeve line."""
    tilt = (0.0, 0.0, 0.0) if side < 0 else (0.012, -0.030, 0.045)

    shoulder = mirrored(pose['shoulder'], side)
    elbow = mirrored(pose['elbow'], side, tilt)
    wrist = mirrored(pose['wrist'], side, (tilt[0] * 1.6, tilt[1] * 1.4, tilt[2] * 1.5))

    upper = sleeve("Arm" + tag, [
        between(shoulder, elbow, -0.30), between(shoulder, elbow, -0.06),
        between(shoulder, elbow, 0.45), between(shoulder, elbow, 0.94)],
        [(0.074, 0.080), (0.078, 0.084), (0.070, 0.076), (0.066, 0.072)],
        m['shell'], square=0.35)

    fore = sleeve("Forearm" + tag, [
        between(elbow, wrist, -0.10), between(elbow, wrist, 0.40),
        between(elbow, wrist, 0.88), between(elbow, wrist, 1.00)],
        [(0.068, 0.074), (0.063, 0.069), (0.065, 0.071), (0.062, 0.068)],
        m['shell'], square=0.35)

    cuff = sleeve("Cuff" + tag, [
        between(elbow, wrist, 0.88), between(elbow, wrist, 1.04)],
        [(0.068, 0.074), (0.064, 0.070)], m['trim'], square=0.35)

    hand = mitten(m, wrist, elbow, side, tag)

    return upper, kit.join([fore, cuff], "Forearm" + tag), hand


def mitten(m, wrist, elbow, side, tag):
    """A mitten: one shape for the fingers and a thumb stuck on the side."""
    reach = (wrist[0] - elbow[0], wrist[1] - elbow[1], wrist[2] - elbow[2])
    length = max(0.0001, sum(c * c for c in reach) ** 0.5)
    step = tuple(c / length for c in reach)

    def along(distance, offset=(0, 0, 0)):
        return (wrist[0] + step[0] * distance + offset[0],
                wrist[1] + step[1] * distance + offset[1],
                wrist[2] + step[2] * distance + offset[2])

    palm = sleeve("Mitt" + tag, [
        along(-0.020), along(0.030), along(0.080), along(0.115), along(0.140)],
        [(0.056, 0.062), (0.062, 0.070), (0.060, 0.068), (0.052, 0.060),
         (0.030, 0.036)], m['mitt'], square=0.45)

    thumb = sleeve("Thumb" + tag, [
        along(0.020, (side * 0.030, 0, 0)), along(0.055, (side * 0.062, 0, 0)),
        along(0.075, (side * 0.078, 0, 0))],
        [(0.024, 0.028), (0.022, 0.026), (0.016, 0.019)], m['mitt'], square=0.4,
        count=12)

    band = sleeve("MittCuff" + tag, [along(-0.034), along(0.002)],
                  [(0.064, 0.070), (0.068, 0.076)], m['trim'], square=0.45)

    return kit.join([palm, thumb, band], "Hand" + tag)


def hips(pose, m):
    """The seat of the pants, which the jacket hem mostly covers."""
    waist = pose['waist']
    left, right = lower(pose, -1)[0], lower(pose, 1)[0]

    rx, ry = pose['hips']
    base = (left[2] + right[2]) * 0.5

    seat = sleeve("Hips", [
        (0.0, waist[1] + 0.010, base - 0.070),
        (0.0, waist[1] + 0.004, base + 0.010),
        (0.0, waist[1], waist[2] + 0.030)],
        [(rx, ry), (rx * 1.05, ry * 1.05), (rx * 0.98, ry * 0.98)],
        m['pants'], square=0.5, count=18)

    return seat


# ---------------------------------------------------------------- build


def build(kind):
    """
    Every part, with its origin on its joint and hung off its parent.

    The order matters: geometry first, in world space, so the stance is
    written as coordinates rather than as a stack of rotations; joints
    afterwards.
    """
    pose = SKI if kind == 'ski' else BOARD
    m = materials(kind)
    boot = ski_boot if kind == 'ski' else board_boot

    root = hips(pose, m)
    body = torso(pose, m)
    skull = head(pose, m)

    parts = [root, body, skull]
    joints = {'Hips': pose['waist'], 'Torso': pose['waist'], 'Head': pose['neck']}
    tree = [(body, root), (skull, body)]

    for side, tag in ((-1, "Left"), (1, "Right")):
        hip, knee, ankle = lower(pose, side)

        thigh, shin = leg(m, hip, knee, ankle, tag, pose.get('pant_width', 1.0))
        foot = boot(pose, m, ankle, pose['foot_yaw'][side], tag)
        upper, fore, hand = arm(pose, m, side, tag)

        joints["Thigh" + tag] = hip
        joints["Shin" + tag] = knee
        joints["Boot" + tag] = ankle
        joints["Arm" + tag] = mirrored(pose['shoulder'], side)
        joints["Forearm" + tag] = mirrored(pose['elbow'], side)
        joints["Hand" + tag] = mirrored(pose['wrist'], side)

        parts += [thigh, shin, foot, upper, fore, hand]
        tree += [(thigh, root), (shin, thigh), (foot, shin),
                 (upper, body), (fore, upper), (hand, fore)]

    for part in parts:
        kit.pivot(part, joints[part.name])

    for child, parent in tree:
        kit.attach(child, parent,
                   tuple(a - b for a, b in zip(joints[child.name], joints[parent.name])))

    return parts


def make(kind):
    kit.reset()
    return build(kind)
