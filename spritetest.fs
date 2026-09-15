\ spritetest.fs - draw a row of generated RG6 sprites (issue #3)
\
\ White screen; every frame is placed by its anchor (bottom centre of the
\ feet on the baseline) using the spr-NAME-ox and spr-NAME-oy constants, so
\ frames of different sizes stand on the same line. Drawn opaque with blit
\ (blit.fs), so anchor x values are multiples of 4. BREAK exits to BASIC.

INCLUDE build/coco-libs.fs
INCLUDE build/sprites.fs
INCLUDE blit.fs

\ place - draw a sprite with its anchor at x y.
: place  ( addr ox oy x y -- )
  ROT + >R + R> blit ;

: top-row  ( -- )
  spr-hop1   spr-hop1-ox   spr-hop1-oy    20 90 place
  spr-hop3   spr-hop3-ox   spr-hop3-oy    56 90 place
  spr-hop1l  spr-hop1l-ox  spr-hop1l-oy  100 90 place ;

: bottom-row  ( -- )
  spr-munch1  spr-munch1-ox  spr-munch1-oy   20 170 place
  spr-carrot3 spr-carrot3-ox spr-carrot3-oy  40 170 place
  spr-heart   spr-heart-ox   spr-heart-oy    72 170 place
  spr-hop1r   spr-hop1r-ox   spr-hop1r-oy   100 170 place ;

: main  ( -- )
  rg-init
  white-screen
  top-row
  bottom-row
  BEGIN
    vsync
    KEY? 3 =
  UNTIL
  exit-basic ;

main
