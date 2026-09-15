\ bunny-jump.fs - RG6 platformer proof of concept for the CoCo
\
\ Art: PixelFarm, Stephen 'Redshrike' Challener (see CREDITS.md).
\ Work tracking: issues.jsonl.

INCLUDE build/coco-libs.fs

\ RG6 = 256x192 dots, read as 128x192 artifact pixels on NTSC. Each byte
\ holds 4 artifact pixels of 2 bits: 00 black, 01 blue, 10 red, 11 white
\ (under XRoar -tv-input cmp-br; real hardware picks the phase at power-up).
32   CONSTANT rg-row      \ bytes per VRAM row
6144 CONSTANT rg-size

\ test-bars - four horizontal bands of 48 rows, one per artifact color.
: test-bars  ( -- )
  vram-base               1536 $00 FILL
  vram-base 1536 +        1536 $55 FILL
  vram-base 3072 +        1536 $AA FILL
  vram-base 4608 +        1536 $FF FILL ;

: main  ( -- )
  rg-init
  test-bars
  BEGIN
    vsync
    KEY? 3 =
  UNTIL
  exit-basic ;

main
