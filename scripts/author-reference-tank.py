"""Build Iron Dominion's original tank in Blender and export reviewable OBJ/SVG assets."""
import argparse
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector

FINISHES = (
    ("Paint", "#bac0af", 163, 26),
    ("Steel", "#696d63", 127, 142),
    ("Rubber", "#494943", 211, 2),
    ("Optics", "#284552", 54, 35),
    ("BareMetal", "#a2a18e", 106, 178),
    ("Identification", "#dddacf", 160, 5),
    ("Canvas", "#747461", 223, 1),
    ("Track", "#6c6757", 182, 71),
)

materials = []
parts = {}


def point(x, y, z):
    """Game coordinates with the Blender vertical axis substituted."""
    return (x, -z, y)


def mark(obj, section, finish, name):
    obj.name = name
    obj.data.materials.append(materials[finish])
    parts[section].append(obj)
    return obj


def bevel(obj, width):
    bpy.context.view_layer.objects.active = obj
    modifier = obj.modifiers.new("Machined edges", "BEVEL")
    modifier.width = width
    modifier.segments = 1
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def box(section, name, at, size, finish=0, edge=0.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=point(*at))
    obj = bpy.context.object
    obj.scale = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if edge:
        bevel(obj, edge)
    return mark(obj, section, finish, name)


def disc(section, name, at, radius, depth, finish, axis="y", sides=16):
    direction = {"x": (1, 0, 0), "y": (0, 0, 1), "z": (0, 1, 0)}[axis]
    rotation = Vector(direction).to_track_quat("Z", "Y").to_euler()
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=depth, location=point(*at), rotation=rotation)
    obj = bpy.context.object
    for polygon in obj.data.polygons:
        polygon.use_smooth = len(polygon.vertices) == 4
    bevel(obj, min(0.022, depth * 0.20))
    return mark(obj, section, finish, name)


def shell(section, name, rings, finish, edge):
    count = len(rings[0][1])
    vertices = [point(x, y, z) for y, outline in rings for x, z in outline]
    faces = [tuple(range(count)), tuple(reversed(range(count * (len(rings) - 1), count * len(rings))))]
    for row in range(len(rings) - 1):
        for side in range(count):
            first = row * count + side
            next_side = row * count + (side + 1) % count
            faces.append((next_side, first, first + count, next_side + count))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    if edge:
        bevel(obj, edge)
    return mark(obj, section, finish, name)


def outline(width, nose, tail, clip):
    return [(-width + clip, nose), (width - clip, nose), (width, nose + clip), (width, tail - clip),
            (width - clip, tail), (-width + clip, tail), (-width, tail - clip), (-width, nose + clip)]


def hull():
    group = "hull"
    shell(group, "Lower hull", [
        (0.34, outline(1.21, -2.15, 2.19, 0.22)), (0.88, outline(1.26, -2.47, 2.38, 0.21)),
    ], 1, 0.052)
    shell(group, "Sloped armor and glacis", [
        (0.87, outline(1.40, -2.64, 2.50, 0.26)),
        (1.22, outline(1.40, -2.48, 2.45, 0.30)),
        (1.46, outline(1.32, -1.86, 2.30, 0.26)),
    ], 0, 0.036)
    for side in (-1, 1):
        for index in range(7):
            z = (index - 3) * 0.655
            radius = 0.359 if index in (0, 6) else 0.384
            disc(group, "Road wheel tire", (side * 1.42, 0.457, z), radius, 0.58, 2, "x")
            disc(group, "Wheel rim", (side * 1.72, 0.457, z), radius * 0.74, 0.065, 1, "x")
            disc(group, "Wheel hub", (side * 1.768, 0.457, z), 0.116, 0.044, 4, "x", 12)
        for panel in range(5):
            z = (panel - 2) * 0.93
            box(group, "Beveled skirt plate", (side * 1.48, 1.014, z), (0.57, 0.36, 0.88), edge=0.038)
            box(group, "Lower skirt strip", (side * 1.747, 0.87, z), (0.05, 0.071, 0.76), 1, 0.013)
        box(group, "Front light housing", (side * 0.89, 1.173, -2.441), (0.39, 0.19, 0.20), 1)
        box(group, "Front light lens", (side * 0.89, 1.183, -2.556), (0.25, 0.104, 0.032), 4, 0.009)
        box(group, "Rear light", (side * 0.98, 1.091, 2.49), (0.22, 0.13, 0.087), 1, 0.019)
        box(group, "Rear stowage", (side * 0.99, 1.53, 1.17), (0.34, 0.16, 0.65), 6, 0.035)
    box(group, "Recessed engine grille", (0, 1.468, 1.96), (2.33, 0.045, 0.54), 1, 0.012)
    for rib in range(-6, 7):
        box(group, "Engine cooling rib", (rib * 0.158, 1.507, 1.96), (0.071, 0.048, 0.50), 4, 0.009)
    box(group, "Hull identification", (0, 1.482, 1.34), (1.04, 0.039, 0.21), 5, 0.008)
    disc(group, "Driver hatch", (-0.46, 1.485, -1.52), 0.29, 0.096, 0)
    for x in (-0.50, -0.16, 0.18):
        box(group, "Driver periscope", (x, 1.514, -1.90), (0.18, 0.12, 0.17), 3, 0.016)


def turret():
    group = "turret"
    disc(group, "Turret ring", (0, 0.03, 0.03), 1.06, 0.19, 1, sides=24)
    lower = [(-0.75, -1.52), (0.75, -1.52), (1.27, -0.67), (1.25, 0.88),
             (0.96, 1.45), (-0.96, 1.45), (-1.25, 0.88), (-1.27, -0.67)]
    upper = [(-0.59, -1.08), (0.59, -1.08), (1.08, -0.44), (1.09, 0.77),
             (0.88, 1.27), (-0.88, 1.27), (-1.09, 0.77), (-1.08, -0.44)]
    shell(group, "Faceted turret", [(0.13, lower), (0.50, lower), (0.73, upper)], 0, 0.035)
    box(group, "Recoil collar seat", (0, 0.38, -1.42), (0.70, 0.47, 0.37), 1, 0.052)
    for side in (-1, 1):
        for index in range(3):
            box(group, "Applique block", (side * 1.245, 0.41, (index - 1) * 0.49), (0.16, 0.21, 0.44), 0, 0.028)
        disc(group, "Hatch collar", (side * 0.57, 0.749, 0.24), 0.305, 0.09, 1, sides=20)
        disc(group, "Hatch lid", (side * 0.57, 0.805, 0.24), 0.263, 0.074, 0, sides=20)
        box(group, "Hatch hinge", (side * 0.57, 0.847, 0.45), (0.18, 0.06, 0.08), 4, 0.012)
        box(group, "Rear stowage rail", (side * 1.04, 0.48, 1.25), (0.083, 0.35, 0.56), 1)
    box(group, "Rear stowage rack", (0, 0.46, 1.49), (2.10, 0.19, 0.37), 1)
    box(group, "Strapped canvas roll", (0, 0.59, 1.47), (1.54, 0.24, 0.29), 6, 0.072)
    for x in (-0.60, 0, 0.60):
        box(group, "Stowage strap", (x, 0.716, 1.47), (0.056, 0.031, 0.29), 1, 0.008)
    box(group, "Turret identification", (0, 0.758, 1.01), (1.49, 0.035, 0.28), 5, 0.012)
    box(group, "Optical sight housing", (0.59, 0.824, -0.53), (0.37, 0.22, 0.41), 1, 0.044)
    box(group, "Optical sight lens", (0.59, 0.835, -0.748), (0.25, 0.121, 0.04), 3, 0.010)
    disc(group, "Antenna mounting", (-0.81, 0.76, 0.92), 0.084, 0.13, 1, sides=12)
    disc(group, "Antenna", (-0.81, 1.15, 0.92), 0.023, 0.77, 1, sides=8)


def gun():
    group = "gun"
    box(group, "Mantlet", (0, 0, -0.12), (0.56, 0.40, 0.50), edge=0.065)
    disc(group, "Barrel", (0, 0, -1.36), 0.106, 2.55, 1, "z", 20)
    disc(group, "Thermal sleeve", (0, 0, -0.97), 0.145, 1.57, 0, "z", 20)
    for z in (-0.34, -1.26, -1.72, -2.40):
        disc(group, "Sleeve collar", (0, 0, z), 0.15 if z > -2 else 0.122, 0.07, 4, "z")
    disc(group, "Muzzle lip", (0, 0, -2.653), 0.136, 0.094, 1, "z", 20)
    disc(group, "Dark bore", (0, 0, -2.701), 0.089, 0.006, 2, "z", 20)


def uv_atlas(obj):
    # Primitives arrive with Blender's default unwrap. Export only the authored atlas coordinates.
    while obj.data.uv_layers:
        obj.data.uv_layers.remove(obj.data.uv_layers[0])
    uv = obj.data.uv_layers.new(name="Armor atlas")
    finish = materials.index(obj.data.materials[0])
    cell_x, cell_y = finish % 4, finish // 4
    for face in obj.data.polygons:
        axis = max(range(3), key=lambda index: abs((obj.matrix_world.to_3x3() @ face.normal)[index]))
        projection = ((1, 2), (0, 2), (0, 1))[axis]
        for loop in face.loop_indices:
            position = obj.matrix_world @ obj.data.vertices[obj.data.loops[loop].vertex_index].co
            u, v = (min(0.96, max(0.04, 0.5 + position[i] * 0.145)) for i in projection)
            uv.data[loop].uv = ((cell_x + u) / 4, 1 - (cell_y + v) / 2)


def preview_tracks():
    """Show the assembled running gear in Blender; Godot animates its own tread loop."""
    radius, axle, center = 0.411, 1.94, 0.46
    profile = [(center - radius, -axle)]
    for end in (1, -1):
        for segment in range(13):
            angle = (segment / 12 - 0.5) * math.pi
            profile.append((center + end * radius * math.sin(angle), end * (axle + radius * math.cos(angle))))
    for side in (-1, 1):
        vertices = [point(side * 1.42 + edge * 0.36, y, z) for y, z in profile for edge in (-1, 1)]
        faces = [(row, row + 1, row + 3, row + 2) for row in range(0, len(vertices) - 2, 2)]
        mesh = bpy.data.meshes.new("Preview tread loop")
        mesh.from_pydata(vertices, [], faces)
        mesh.materials.append(materials[7])
        obj = bpy.data.objects.new("Left track preview" if side < 0 else "Right track preview", mesh)
        bpy.context.collection.objects.link(obj)


def atlas(output, kind):
    name = {"albedo": "armor-albedo", "normal": "armor-normal", "orm": "armor-orm"}[kind]
    rng = random.Random(91)
    lines = ['<svg xmlns="http://www.w3.org/2000/svg" width="2048" height="2048" viewBox="0 0 2048 2048">']
    for index, (_, base, roughness, metalness) in enumerate(FINISHES):
        x, y = index % 4 * 512, index // 4 * 1024
        color = base if kind == "albedo" else "#8080ff" if kind == "normal" else f"#eb{roughness:02x}{metalness:02x}"
        lines.append(f'  <rect x="{x}" y="{y}" width="512" height="1024" fill="{color}"/>')
        for patch in range(84):
            px, py = x + rng.randrange(12, 500), y + rng.randrange(16, 1005)
            rx, ry = rng.randrange(1, 12), rng.randrange(1, 27)
            if kind == "albedo":
                tone, opacity = ("#ece6cf", 0.18) if patch % 3 else ("#313129", 0.16)
            elif kind == "normal":
                tone, opacity = ("#91a0fb", 0.63) if patch % 2 else ("#6e70fc", 0.61)
            else:
                tone, opacity = f"#cb{min(250, roughness + 25):02x}{metalness:02x}", 0.46
            lines.append(f'  <ellipse cx="{px}" cy="{py}" rx="{rx}" ry="{ry}" fill="{tone}" opacity="{opacity}"/>')
        if kind == "albedo" and index in (0, 1, 6, 7):
            lines.append(f'  <path d="M{x+30} {y+43}l26 108 31 46m213 223l22 74 39 20m-214 150l10 73" '
                         'fill="none" stroke="#c9bf9d" stroke-width="2" opacity="0.31"/>')
    lines.append("</svg>")
    (output / f"{name}.svg").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--blend", type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    args.output.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for name, color, roughness, metalness in FINISHES:
        material = bpy.data.materials.new(name)
        rgb = tuple(int(color[index:index + 2], 16) / 255 for index in (1, 3, 5))
        material.diffuse_color = (*rgb, 1)
        material.roughness = roughness / 255
        material.metallic = metalness / 255
        materials.append(material)

    for section in ("hull", "turret", "gun"):
        parts[section] = []
    hull()
    turret()
    gun()
    bpy.context.view_layer.update()
    for section, objects in parts.items():
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            uv_atlas(obj)
            obj.select_set(True)
        path = args.output / f"tank-{section}.obj"
        bpy.ops.wm.obj_export(filepath=str(path), export_selected_objects=True,
                             forward_axis="NEGATIVE_Z", up_axis="Y", export_triangulated_mesh=True,
                             export_materials=False, export_normals=True, export_uv=True)
        # The game draws one surface per articulated part. OBJ indices stay global across object records.
        geometry = (line for line in path.read_text().splitlines() if not line.startswith(("o ", "g ")))
        path.write_text(f"o {section}\n" + "\n".join(geometry) + "\n")
    for kind in ("albedo", "normal", "orm"):
        atlas(args.output, kind)
    if args.blend:
        # Assemble the editable parts at their Godot mounting positions in the source preview.
        for section, offset in (("turret", (0, 1.43, 0)), ("gun", (0, 1.81, -1.12))):
            for obj in parts[section]:
                obj.location += Vector(point(*offset))
        preview_tracks()
        bpy.ops.object.select_all(action="DESELECT")
        materials[0].diffuse_color = (0.26, 0.31, 0.22, 1)
        materials[5].diffuse_color = (0.04, 0.38, 0.78, 1)
        for screen in bpy.data.screens:
            for area in screen.areas:
                if area.type == "VIEW_3D":
                    view = area.spaces.active
                    view.shading.color_type = "MATERIAL"
                    view.region_3d.view_location = (0, 0, 0.90)
                    view.region_3d.view_distance = 8
                    view.region_3d.view_rotation = Vector((8, -10, 7)).to_track_quat("Z", "Y")
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(args.blend))


if __name__ == "__main__":
    main()
