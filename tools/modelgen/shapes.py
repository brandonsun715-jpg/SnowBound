"""
Shapes that are maths rather than Blender.

A ski, a snowboard and a pole shaft are all the same idea: a cross-section
swept along a line, with the section's width, thickness and height each a
curve of their own. Writing that once means the board is the ski with
different numbers, which is very nearly true of the real objects too.
"""

import math


def keyed(keys):
    """
    A smooth curve through a list of (position, value) keys.

    Catmull-Rom, so the curve passes through every key rather than near it:
    a ski's waist width is a measured number and the curve has to hit it.
    """
    keys = sorted(keys)
    xs = [k[0] for k in keys]
    ys = [k[1] for k in keys]

    def at(index):
        return ys[min(max(index, 0), len(ys) - 1)]

    def value(s):
        if s <= xs[0]:
            return ys[0]
        if s >= xs[-1]:
            return ys[-1]

        i = 0
        while i < len(xs) - 2 and s > xs[i + 1]:
            i += 1

        span = xs[i + 1] - xs[i]
        t = (s - xs[i]) / span if span > 1e-9 else 0.0

        p0, p1, p2, p3 = at(i - 1), at(i), at(i + 1), at(i + 2)
        return 0.5 * ((2 * p1) + (-p0 + p2) * t +
                      (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                      (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t)

    return value


# The eight points of a ski or snowboard section, and what each face
# between them is made of: the base, a steel edge, the sidewall, and the
# printed top. Four materials on one swept shape.
BASE, EDGE, SIDEWALL, TOP = 0, 1, 2, 3
PLANK_GROUPS = [BASE, EDGE, SIDEWALL, TOP, TOP, TOP, SIDEWALL, EDGE]


def plank(length, width, thickness, base, stations=90, edge=0.005,
          sidewall=0.0025, chamfer=0.009):
    """
    A pressed plank: ski or board.

    Everything is a function of s, which runs 0 at the tail to 1 at the
    tip. `width` is the half-width, so the sidecut is one curve rather than
    two that have to agree. Tip is -Y, which is forward in Unity.
    """
    rings = []

    for step in range(stations + 1):
        s = step / stations
        w = max(width(s), 0.0015)
        t = max(thickness(s), 0.0015)
        z = base(s)
        y = length * (0.5 - s)

        inset = min(sidewall, w * 0.35)
        shoulder = min(chamfer, w * 0.45)
        lip = min(edge, t * 0.45)
        fall = min(0.004, t * 0.35)

        section = [
            (-w, 0.0),
            (+w, 0.0),
            (+w, lip),
            (+w - inset, t - fall),
            (+w - inset - shoulder, t),
            (-w + inset + shoulder, t),
            (-w + inset, t - fall),
            (-w, lip),
        ]

        rings.append([(x, y, z + h) for x, h in section])

    return rings, PLANK_GROUPS


def sidecut(tail, waist, nose, waist_at=0.47, nose_at=0.88, tail_at=0.07, ends=0.004):
    """Half-widths down a plank: tail, waist and shovel, rounded off at both ends."""
    return keyed([
        (0.00, ends),
        (tail_at * 0.45, tail * 0.86),
        (tail_at, tail),
        (waist_at * 0.55 + tail_at * 0.45, (tail + waist) * 0.5),
        (waist_at, waist),
        (waist_at * 0.35 + nose_at * 0.65, (waist + nose) * 0.52),
        (nose_at, nose),
        (nose_at + (1.0 - nose_at) * 0.55, nose * 0.80),
        (1.00, ends),
    ])


def circle(radius, z=0.0, count=24, phase=0.0, centre=(0.0, 0.0)):
    return [(centre[0] + radius * math.cos(phase + 2 * math.pi * i / count),
             centre[1] + radius * math.sin(phase + 2 * math.pi * i / count), z)
            for i in range(count)]


def rounded_rect(half_x, half_y, radius, z=0.0, per_corner=5):
    """A rectangle with radiused corners, as a closed loop of points."""
    radius = min(radius, half_x * 0.99, half_y * 0.99)
    points = []

    corners = [(half_x - radius, half_y - radius, 0.0),
               (-half_x + radius, half_y - radius, math.pi * 0.5),
               (-half_x + radius, -half_y + radius, math.pi),
               (half_x - radius, -half_y + radius, math.pi * 1.5)]

    for cx, cy, start in corners:
        for i in range(per_corner + 1):
            a = start + (math.pi * 0.5) * i / per_corner
            points.append((cx + radius * math.cos(a), cy + radius * math.sin(a), z))

    return points


def _norm(v):
    length = math.sqrt(sum(c * c for c in v))
    return tuple(c / length for c in v) if length > 1e-9 else (0.0, 0.0, 1.0)


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0])


def oval(rx, ry, count=16, square=0.0):
    """
    A cross-section for a limb or a body: an ellipse, optionally squared
    off toward a rounded rectangle.

    People are not round. A thigh is wider than it is deep, a chest is much
    wider than it is deep, and a padded sleeve is nearly square with the
    corners knocked off — which is what `square` does.
    """
    exponent = 1.0 - 0.55 * square
    points = []

    for i in range(count):
        angle = 2 * math.pi * i / count
        c, s = math.cos(angle), math.sin(angle)
        points.append((rx * math.copysign(abs(c) ** exponent, c),
                       ry * math.copysign(abs(s) ** exponent, s)))

    return points


def sweep(section, path, up=(0.0, 0.0, 1.0), scale=None, sections=None):
    """
    Carry a cross-section along a path, turning it to follow the corners.

    Straps, handrails, bails and cable guards are all a section swept along
    a line, and doing it properly — with the section square to the path at
    every step — is the difference between a strap that bends and a strap
    that creases.
    """
    rings = []

    for i, point in enumerate(path):
        ahead = path[min(i + 1, len(path) - 1)]
        behind = path[max(i - 1, 0)]
        tangent = _norm(tuple(a - b for a, b in zip(ahead, behind)))

        side = _cross(tangent, up)
        if sum(c * c for c in side) < 1e-9:
            side = _cross(tangent, (1.0, 0.0, 0.0))
        side = _norm(side)
        lift = _norm(_cross(side, tangent))

        factor = scale(i / max(1, len(path) - 1)) if scale else 1.0
        shape = sections[i] if sections else section

        rings.append([(point[0] + (side[0] * u + lift[0] * v) * factor,
                       point[1] + (side[1] * u + lift[1] * v) * factor,
                       point[2] + (side[2] * u + lift[2] * v) * factor)
                      for u, v in shape])

    return rings


def arc(centre, radius, start, end, steps=16, plane='XZ', squash=1.0):
    """A run of points around a circle, for a strap or a bail to follow."""
    points = []

    for i in range(steps + 1):
        a = math.radians(start + (end - start) * i / steps)
        c, s = math.cos(a) * radius, math.sin(a) * radius * squash

        if plane == 'XZ':
            points.append((centre[0] + c, centre[1], centre[2] + s))
        elif plane == 'YZ':
            points.append((centre[0], centre[1] + c, centre[2] + s))
        else:
            points.append((centre[0] + c, centre[1] + s, centre[2]))

    return points
