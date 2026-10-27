import argparse
import base64
import glob
import gzip
import hashlib
import json
import os
import struct
import webbrowser

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
IMAGE_EXT = (".png", ".jpg", ".jpeg")
SKIP_IMAGES = ("loading_", "paint_tool_icon")
GROUPS = [("reactor_", "reactor"), ("heavy_gyro_", "heavy_gyro"), ("pump_", "pump"), ("vent_", "vent"), ("excavator_", "excavator"),
          ("hydrogen_thruster", "hydrogen_thruster"), ("thruster_mounts", "hydrogen_thruster")]
TEXTURES = [("heavy_gyro_", "reactor_palette.png"), ("paint_tool", "reactor_palette.png"), ("jetpack", "reactor_parts.png"),
            ("workbench_body", "workbench_albedo.jpg"), ("blueprint_tool_body", "blueprint_tool_albedo.jpg"),
            ("thruster_mounts", "hydrogen_albedo.png"), ("hydrogen_thruster", "hydrogen_thruster_albedo.jpg")]
EMISSIVE = ("screens", "glow")
FALLBACK_TEXTURE = "door_albedo.png"
GAME_EXPORT = os.path.join(os.environ.get("STELLARDRIVE_DIR", r"C:\Program Files (x86)\Steam\steamapps\common\StellarDrive Demo"), "UserData", "Mining", "export")


def read_mesh(path):
    with open(path, "rb") as f:
        data = f.read()
    if data[:4] != b"MSH1":
        raise ValueError(f"{path} is not an MSH1 mesh")
    verts, count = struct.unpack_from("<ii", data, 4)
    return verts, count // 3, base64.b64encode(gzip.compress(data, 9)).decode()


def collect(paths):
    meshes, images = [], []
    for path in paths:
        if os.path.isdir(path):
            meshes += sorted(glob.glob(os.path.join(path, "*.bin")))
            images += sorted(p for p in glob.glob(os.path.join(path, "*")) if p.lower().endswith(IMAGE_EXT))
        elif path.lower().endswith(".bin"):
            meshes.append(path)
        elif path.lower().endswith(IMAGE_EXT):
            images.append(path)
    if not images or any(os.path.isfile(p) for p in paths):
        images += glob.glob(os.path.join(ROOT, "src", "*", "Assets", "*.*"))
    seen, unique = set(), []
    for p in images:
        name = os.path.basename(p)
        if p.lower().endswith(IMAGE_EXT) and name not in seen and not name.startswith(SKIP_IMAGES):
            seen.add(name)
            unique.append(p)
    return meshes, unique


def pick(name, rules, default):
    return next((value for prefix, value in rules if name.startswith(prefix)), default)


def guess_texture(name, textures):
    if name + ".png" in textures:
        return name + ".png"
    texture = pick(name, TEXTURES, FALLBACK_TEXTURE)
    return texture if texture in textures else ""


def image_data(path):
    mime = "image/png" if path.lower().endswith(".png") else "image/jpeg"
    with open(path, "rb") as f:
        return f"data:{mime};base64," + base64.b64encode(f.read()).decode()


def game_parts(export, textures):
    manifest = os.path.join(export, "manifest.json")
    if not os.path.isfile(manifest):
        return []
    with open(manifest, encoding="utf-8") as f:
        parts = json.load(f)["parts"]
    meshes, keys, seen = [], {}, {}
    for name in sorted({m["texture"] for p in parts for m in p["meshes"] if m["texture"]}):
        with open(os.path.join(export, name), "rb") as f:
            digest = hashlib.md5(f.read()).hexdigest()
        if digest not in seen:
            key = "game/" + name.rsplit("_", 1)[0]
            seen[digest] = next(k for k in (f"{key}{'' if i == 0 else f'_{i + 1}'}.png" for i in range(len(seen) + 1)) if k not in textures)
            textures[seen[digest]] = image_data(os.path.join(export, name))
        keys[name] = seen[digest]
    for part in sorted(parts, key=lambda p: (p["module"], p["label"])):
        group = f"{part['label']} #{part['id']}"
        for m in part["meshes"]:
            verts, tris, data = read_mesh(os.path.join(export, m["file"]))
            meshes.append({"name": m["name"], "group": group, "module": part["module"], "source": "game", "verts": verts, "tris": tris,
                           "texture": keys.get(m["texture"], ""), "emissive": m["emissive"], "data": data})
    return meshes


def build(paths, out, export):
    mesh_paths, image_paths = collect(paths)
    if not mesh_paths:
        raise SystemExit("no .bin meshes found")
    textures = {os.path.basename(p): image_data(p) for p in image_paths}
    meshes = []
    for p in mesh_paths:
        name = os.path.splitext(os.path.basename(p))[0]
        verts, tris, data = read_mesh(p)
        meshes.append({"name": name, "group": pick(name, GROUPS, name), "module": os.path.basename(os.path.dirname(os.path.dirname(p))), "source": "asset",
                       "verts": verts, "tris": tris, "texture": guess_texture(name, textures), "emissive": any(e in name for e in EMISSIVE), "data": data})
    game = game_parts(export, textures) if export else []
    meshes = game + meshes
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview_template.html"), encoding="utf-8") as f:
        html = f.read()
    payload = json.dumps({"meshes": meshes, "textures": textures}, separators=(",", ":"))
    html = html.replace("/*DATA*/null", payload)
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    with open(out, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"{len(meshes) - len(game)} asset meshes, {len({m['group'] for m in game})} game parts, {len(textures)} textures -> {out}")


def main():
    parser = argparse.ArgumentParser(description="Build a browser preview of MSH1 meshes and their textures.")
    parser.add_argument("paths", nargs="*", help="asset folders, .bin meshes or textures (default: every src/*/Assets)")
    parser.add_argument("-o", "--out", default=os.path.join(ROOT, "preview", "models.html"))
    parser.add_argument("--export", default=GAME_EXPORT, help="folder written by tools/dev-test.ps1 -Mode export")
    parser.add_argument("--no-game", action="store_true", help="only the .bin assets, without parts exported from the game")
    parser.add_argument("--no-open", action="store_true")
    args = parser.parse_args()
    paths = args.paths or sorted(glob.glob(os.path.join(ROOT, "src", "*", "Assets")))
    build(paths, args.out, None if args.no_game else args.export)
    if not args.no_open:
        webbrowser.open("file:///" + os.path.abspath(args.out).replace("\\", "/"))


if __name__ == "__main__":
    main()
