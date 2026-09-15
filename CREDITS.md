# Credits

## Bunny art

"Bunny Rabbit LPC Style for PixelFarm"
https://opengameart.org/content/bunny-rabbit-lpc-style-for-pixelfarm

PixelFarm (https://bitbucket.org/tebruno99/pixelfarm) Stephen 'Redshrike' Challener

Licensed CC-BY 3.0, CC-BY-SA 3.0 and OGA-BY 3.0. `assets/bunnysheet5.png` is
the original sheet, unmodified.

## Derived art

These are derived from the sheet above and released under CC-BY-SA 3.0:

- The hop and munch frames, converted by `tools/png2rg6.py` to the CoCo RG6
  artifact palette (black, blue, red, white) at 1x1 rows, with left-facing
  mirrors and red recolored variants. They are generated into
  `build/sprites.fs` and are not stored in the repository.
- The carrot bite frames in `assets/extra/carrot*.txt`, carried over from the
  bunny demo (https://github.com/ugufru/bunny).
- The heart in `assets/extra/heart.txt`, drawn for this game.
- `screenshot.png`, a capture of the game using the frames above.

## Toolchain

Built on the CoCo Forth kernel and `fc.py` cross-compiler from the coco
project (https://github.com/ugufru/coco).
