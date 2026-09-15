# bunny-jump

A platformer proof of concept for the Tandy Color Computer: hop the bunny up
the platforms to the carrot. RG6 graphics (128x192 artifact pixels, black,
white, red and blue). Derived from the bunny demo in ~/github/bunny.

## Controls

- Left / right arrows: direction
- Space: hop (straight up, or up-left / up-right with an arrow held)
- BREAK: quit to BASIC

## Prerequisites

- A checkout of ~/github/coco (Forth kernel and `tools/fc.py`); override with
  `make COCO=/path/to/coco`
- lwasm (to build the kernel)
- XRoar, with `bas12.rom` and `extbas11.rom` in `~/.xroar/roms`
- Python 3 with Pillow (sprite conversion)

## Build and run

```sh
make run       # build bunny-jump.bin and run it in XRoar
make issues    # rebuild issues.html from issues.jsonl and roadmap.jsonl
```

Work is tracked in `issues.jsonl` and ranked in `roadmap.jsonl`; open
`issues.html` to browse them.
