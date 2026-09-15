#!/usr/bin/env python3
"""png2rg6.py - convert bunny sheet frames and ASCII art to RG6 sprite records.

Usage: python3 tools/png2rg6.py tools/frames.json build

Reads crop boxes, color map and sequences from frames.json and writes:
  build/sprites.fs    DATA[PY blocks, one per frame, anchor CONSTANTs,
                      sequence CONSTANTs and an spr lookup word
  build/preview.png   every frame in the 4 RG6 artifact colors on white, one
                      row per sequence, each artifact pixel drawn 2 wide by
                      1 tall

Record layout (coco/lib/sprite.fs layout, drawn opaque by blit in blit.fs):
  byte 0  w   width in artifact pixels, always a multiple of 4
  byte 1  h   height in scanlines
  then w/4 bytes per row, 2 bits per pixel, MSB first
  code 0 black, 1 blue, 2 red/orange, 3 white

The game has a white background (user decision, issue #3), and the kernel
spr-draw treats code 0 as transparent, so records are opaque: transparent
source pixels become white. Frames are padded with white so both ox and w are
multiples of 4, which keeps the byte-aligned blit on byte boundaries whenever
the anchor x is a multiple of 4.

Source rows are doubled: an RG6 artifact pixel is 2 dots wide and 1 scanline
tall, so a 1:1 copy would squash the art to half height.

Anchors: spr-<name>-ox and spr-<name>-oy are the signed offset of the record's
top-left corner from the anchor, in artifact pixels and scanlines. The anchor
is the bottom centre of the feet on the baseline (the centre of the frame's
sheet cell on the sheet baseline), so draw at x = ax + ox, y = ay + oy.
"""

import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

PX_W, PX_H = 6, 3     # preview size of one artifact pixel (2:1, TV aspect)
T = -1                # transparent while converting; packed as white
WHITE = 3
ART_CODES = {".": T, "k": 0, "c": 1, "b": 1, "o": 2, "r": 2, "w": 3}


def build_color_map(colors):
    return {tuple(rgb): int(code) for code, rgbs in colors.items() for rgb in rgbs}


def nearest(rgb, table):
    return min(table.items(),
               key=lambda kv: sum((a - b) ** 2 for a, b in zip(kv[0], rgb)))[1]


def sheet_offset(frame, cfg):
    """Top-left of a sheet frame relative to its anchor, in source pixels."""
    x, y = frame["box"][0], frame["box"][1]
    return x - (frame["cell_x"] + cfg["cell_w"] // 2), y - cfg["baseline_y"]


def read_art(path):
    rows = [line.rstrip("\n") for line in open(path)
            if line.strip() and not line.startswith("#")]
    w = max(len(r) for r in rows)
    try:
        return [[ART_CODES[ch] for ch in r.ljust(w, ".")] for r in rows]
    except KeyError as e:
        sys.exit(f"{path}: unknown pixel character {e}")


def trim_columns(rows, ox):
    used = [x for x in range(len(rows[0])) if any(r[x] != T for r in rows)]
    if not used:
        return rows, ox
    return [r[used[0]:used[-1] + 1] for r in rows], ox + used[0]


def trim_rows(rows, oy):
    used = [y for y, r in enumerate(rows) if any(c != T for c in r)]
    if not used:
        return rows, oy
    return rows[used[0]:used[-1] + 1], oy + used[0]


def align4(rows, ox):
    """Pad with transparent columns so ox and the width are multiples of 4."""
    left = ox % 4
    right = (-(left + len(rows[0]))) % 4
    return [[T] * left + r + [T] * right for r in rows], ox - left


def source_pixels(sheet, frame, cfg, table, unmapped):
    """Rows of codes (T for transparent) plus ox, oy, all in source pixels."""
    if "art" in frame:
        rows = read_art(frame["art"])
        ox, oy = frame["ox"], frame["oy"]
    else:
        x, y, w, h = frame["box"]
        rows = []
        for yy in range(h):
            row = []
            for xx in range(w):
                r, g, b, a = sheet.getpixel((x + xx, y + yy))
                code = T
                if a:
                    code = table.get((r, g, b))
                    if code is None:
                        unmapped.add((r, g, b))
                        code = nearest((r, g, b), table)
                row.append(code)
            rows.append(row)
        ox, oy = sheet_offset(frame, cfg)
    if frame.get("trim", True):
        rows, ox = trim_columns(rows, ox)
        rows, oy = trim_rows(rows, oy)
    return rows, ox, oy


def pack_rows(rows):
    data = bytearray()
    for row in rows:
        c = [WHITE if p == T else p for p in row]
        for i in range(0, len(c), 4):
            data.append((c[i] << 6) | (c[i + 1] << 4) | (c[i + 2] << 2) | c[i + 3])
    return bytes(data)


def make_frame(name, pixels, ox, oy):
    pixels, ox = align4(pixels, ox)
    h, w = len(pixels), len(pixels[0])
    if w > 128 or h > 192:
        sys.exit(f"{name}: {w}x{h} does not fit the screen")
    data = bytes([w, h]) + pack_rows(pixels)
    return {"name": name, "w": w, "h": h, "ox": ox, "oy": oy,
            "pixels": pixels, "data": data}


def convert_frame(sheet, frame, cfg, table, unmapped):
    src, ox, oy = source_pixels(sheet, frame, cfg, table, unmapped)
    doubled = [list(r) for r in src for _ in range(2)]
    return make_frame(frame["name"], doubled, ox, oy * 2)


def mirror_frame(base, name):
    """base flipped left to right; the anchor stays at the same screen x."""
    pixels = [row[::-1] for row in base["pixels"]]
    return make_frame(name, pixels, -(base["ox"] + base["w"]), base["oy"])


def recolor_frame(base, name, mapping):
    m = {int(k): v for k, v in mapping.items()}
    pixels = [[m.get(c, c) for c in row] for row in base["pixels"]]
    return make_frame(name, pixels, base["ox"], base["oy"])


def write_forth(path, frames, sequences):
    out = [
        "\\ sprites.fs - generated by tools/png2rg6.py from tools/frames.json.",
        "\\ Do not edit. Record: w h (w a multiple of 4), then w/4 bytes per row",
        "\\ of 2bpp pixels, MSB first (0 black, 1 blue, 2 red, 3 white), opaque.",
        "\\ spr-NAME-ox and spr-NAME-oy: signed offset of the top-left corner",
        "\\ from the anchor (bottom centre of the feet on the baseline); ox is a",
        "\\ multiple of 4.",
        "",
    ]
    for f in frames:
        out += [f"DATA[PY spr-{f['name']}",
                f"bytes.fromhex(\"{f['data'].hex()}\")",
                "]DATA",
                f"{f['ox']} CONSTANT spr-{f['name']}-ox",
                f"{f['oy']} CONSTANT spr-{f['name']}-oy",
                ""]
    oxy = bytes(b & 0xFF for f in frames for b in (f["ox"], f["oy"]))
    out += ["\\ spr-oxy - ox oy as signed bytes, two per frame index, so code",
            "\\ that picks frames by index can find the anchor offsets.",
            "DATA[PY spr-oxy",
            f"bytes.fromhex(\"{oxy.hex()}\")",
            "]DATA", ""]
    names = [f["name"] for f in frames]
    out.append(f"{len(frames)} CONSTANT spr-count")
    for seq, members in sequences.items():
        idx = [names.index(m) for m in members]
        if idx != list(range(idx[0], idx[0] + len(idx))):
            sys.exit(f"sequence {seq} must list consecutive frames in frame order")
        out.append(f"{idx[0]} CONSTANT seq-{seq}")
        out.append(f"{len(members)} CONSTANT seq-{seq}-len")
    out += ["", "\\ spr - frame index (0..spr-count-1) to sprite record address.",
            ": spr  ( n -- addr )"]
    for i, n in enumerate(names[1:], 1):
        out.append(f"  DUP {i} = IF DROP spr-{n} EXIT THEN")
    out += [f"  DROP spr-{names[0]} ;", ""]
    path.write_text("\n".join(out))


def write_preview(path, frames, sequences, cfg):
    rgb = [tuple(c) for c in cfg["preview_rgb"]]
    by_name = {f["name"]: f for f in frames}
    cols = max(len(m) for m in sequences.values())
    left = max(-f["ox"] for f in frames) + 3
    cell_w = left + max(f["ox"] + f["w"] for f in frames) + 3
    top = max(-f["oy"] for f in frames) + 6
    cell_h = top + max(f["oy"] + f["h"] for f in frames) + 10
    img = Image.new("RGB", (cols * cell_w * PX_W, len(sequences) * cell_h * PX_H), rgb[WHITE])
    draw = ImageDraw.Draw(img)
    for row, members in enumerate(sequences.values()):
        for col, name in enumerate(members):
            f = by_name[name]
            cx, cy = col * cell_w + left, row * cell_h + top
            # record box, baseline and anchor marks, outside the sprite palette
            bx, by = (cx + f["ox"]) * PX_W, (cy + f["oy"]) * PX_H
            draw.rectangle([bx - 1, by - 1, bx + f["w"] * PX_W, by + f["h"] * PX_H],
                           outline=(210, 210, 210))
            draw.line([(col * cell_w * PX_W, cy * PX_H),
                       ((col + 1) * cell_w * PX_W - 1, cy * PX_H)], fill=(150, 190, 150))
            draw.rectangle([cx * PX_W, cy * PX_H + 2, cx * PX_W + 2, cy * PX_H + 14],
                           fill=(0, 200, 0))
            for yy, line in enumerate(f["pixels"]):
                for xx, code in enumerate(line):
                    if code != T:
                        px = (cx + f["ox"] + xx) * PX_W
                        py = (cy + f["oy"] + yy) * PX_H
                        draw.rectangle([px, py, px + PX_W - 1, py + PX_H - 1], fill=rgb[code])
    img.save(path)


def main():
    if len(sys.argv) != 3:
        sys.exit("usage: png2rg6.py frames.json outdir")
    cfg = json.loads(Path(sys.argv[1]).read_text())
    outdir = Path(sys.argv[2])
    sheet = Image.open(cfg["sheet"]).convert("RGBA")
    table = build_color_map(cfg["colors"])
    unmapped = set()
    frames = []

    def earlier(f, key):
        base = next((c for c in frames if c["name"] == f[key]), None)
        if base is None:
            sys.exit(f"{f['name']}: {f[key]} must be listed earlier")
        return base

    for f in cfg["frames"]:
        if "mirror" in f:
            frames.append(mirror_frame(earlier(f, "mirror"), f["name"]))
        elif "recolor" in f:
            frames.append(recolor_frame(earlier(f, "recolor"), f["name"], f["map"]))
        else:
            frames.append(convert_frame(sheet, f, cfg, table, unmapped))

    outdir.mkdir(parents=True, exist_ok=True)
    write_forth(outdir / "sprites.fs", frames, cfg["sequences"])
    write_preview(outdir / "preview.png", frames, cfg["sequences"], cfg)

    total = 0
    for f in frames:
        total += len(f["data"])
        print(f"  {f['name']:8} {f['w']:2}x{f['h']:<2} ox={f['ox']:+3} oy={f['oy']:+3} {len(f['data'])} B")
    print(f"  {len(frames)} frames, {total} B")
    if unmapped:
        print(f"  warning: unmapped colors used nearest match: {sorted(unmapped)}")


if __name__ == "__main__":
    main()
