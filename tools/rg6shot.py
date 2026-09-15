#!/usr/bin/env python3
"""rg6shot.py - render the RG6 page from an XRoar RAM dump as a PNG.

Usage: python3 tools/rg6shot.py build/shot.ram build/shot.png [base] [sx] [sy]

The dump is a raw image of RAM from address 0 (XRoar -trap-snap with a .ram
name). base defaults to $0600 (kernel VRAM_BASE). The page is 192 rows of
32 bytes; each byte holds 4 artifact pixels of 2 bits, MSB first. Each
artifact pixel is drawn sx wide by sy tall (default 4x2, so 512x384, which
keeps the TV aspect: one artifact pixel is two dots wide).

Palette is the artifact colour set XRoar shows under -tv-type ntsc
-tv-input cmp-br (TV_INPUT_CMP_KBRW: black, blue, red, white for codes
00 01 10 11). Pass --rb for the cmp-rb order (red and blue swapped).

--page-ptr=ADDR reads a 16-bit page address from the dump at ADDR and renders
that page instead, when it is base or $6000: the game flips between two
pages (issue #19) and stores the page on screen there.
"""

import sys

from PIL import Image

# Approximate XRoar NTSC artifact colours for CSS=1 (white foreground).
BLACK, BLUE, RED, WHITE = (0, 0, 0), (40, 110, 255), (255, 110, 30), (255, 255, 255)
W, H, BYTES_PER_ROW = 128, 192, 32


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    rb = "--rb" in sys.argv[1:]
    if len(args) < 2:
        sys.exit("usage: rg6shot.py dump.ram out.png [base] [sx] [sy] [--rb]")
    ram = open(args[0], "rb").read()
    base = int(args[2], 0) if len(args) > 2 else 0x0600
    sx = int(args[3]) if len(args) > 3 else 4
    sy = int(args[4]) if len(args) > 4 else 2
    rgb = [BLACK, RED, BLUE, WHITE] if rb else [BLACK, BLUE, RED, WHITE]
    for a in sys.argv[1:]:
        if a.startswith("--page-ptr="):
            ptr = int(a.split("=", 1)[1], 0)
            shown = int.from_bytes(ram[ptr:ptr + 2], "big")
            if shown in (base, 0x6000):
                base = shown

    size = H * BYTES_PER_ROW
    page = ram[base:base + size]
    if len(page) < size:
        sys.exit(f"dump is only {len(ram)} bytes, no RG6 page at ${base:04X}")

    img = Image.new("RGB", (W, H), BLACK)
    px = img.load()
    for y in range(H):
        row = page[y * BYTES_PER_ROW:(y + 1) * BYTES_PER_ROW]
        for i, b in enumerate(row):
            for p in range(4):
                px[i * 4 + p, y] = rgb[(b >> (6 - 2 * p)) & 3]
    img = img.resize((W * sx, H * sy), Image.NEAREST)
    img.save(args[1])
    print(f"wrote {args[1]} ({img.width}x{img.height}) from ${base:04X}")


if __name__ == "__main__":
    main()
