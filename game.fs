\ game.fs - bunny-jump game code, shared by bunny-jump.fs and autoplay.fs
\
\ Hop the bunny up the platforms to the carrot. Left/right arrows face,
\ space hops (straight up, or up-left/up-right with an arrow held), BREAK
\ quits. White background, black platforms, drawn with the opaque byte blit.
\
\ Art: PixelFarm, Stephen 'Redshrike' Challener (see CREDITS.md).
\ Work tracking: issues.jsonl (#5 hop, #6 platforms, #7 carrot, #8 hearts).

INCLUDE build/coco-libs.fs
INCLUDE build/sprites.fs
INCLUDE blit.fs
INCLUDE input.fs

\ ---- Tuning (#5, #6) ---------------------------------------------------
\ Positions and velocities are in 1/16 artifact pixel (scanline) units.
\ A hop lifts about hop-v*hop-v / (2*gravity) / 16 = 52 rows, enough to
\ clear the 36-40 row gaps between platforms.
4   CONSTANT gravity        \ added to vy every frame
82  CONSTANT hop-v          \ upward speed at take-off
20  CONSTANT hop-dx         \ sideways speed of a diagonal hop
28  CONSTANT min-ax         \ anchor x limits keep every frame on screen
100 CONSTANT max-ax         \   (widest frame reaches ax-28 .. ax+28)
58  CONSTANT min-ay         \ ceiling: the tallest frame reaches ay-58

\ ---- Level (#6) --------------------------------------------------------
\ Platforms as x1 x2 y bytes: x1 and x2+1 are multiples of 4, y is the top
\ row; each is 4 rows tall. Floor, right ledge, left ledge, carrot ledge.
DATA[PY level
bytes.fromhex("007FBC" "447B94" "043B6C" "3C7B48")
]DATA
4   CONSTANT #plats
36  CONSTANT start-x
188 CONSTANT start-y
104 CONSTANT carrot-x       \ carrot anchor (left edge, on the ledge)
72  CONSTANT carrot-y

: plat  ( i -- addr )  3 * level + ;

\ draw-plat - fill a platform's 4 rows with black.
: draw-plat  ( addr -- )
  DUP C@  OVER 2 + C@        ( addr x1 y )
  ROT DUP 1 + C@ 1 +  SWAP C@ -  2 RSHIFT   ( x1 y wbytes )
  4 $00 fill-box ;

: draw-level  ( -- )  #plats 0 DO  I plat draw-plat  LOOP ;

\ ---- Sprite helpers ----------------------------------------------------
: sx8     ( c -- n )  DUP 127 > IF 256 - THEN ;
: f-ox    ( n -- ox )  2* spr-oxy + C@ sx8 ;
: f-oy    ( n -- oy )  2* spr-oxy + 1 + C@ sx8 ;
: rec-wb  ( addr -- wbytes )  C@ 2 RSHIFT ;
: rec-h   ( addr -- h )  1 + C@ ;

\ ---- Hearts (#8) -------------------------------------------------------
VARIABLE lives

: draw-hearts  ( -- )
  lives @ ?DUP IF
    0 DO  spr-heart  I 12 * 4 +  2  blit  LOOP
  THEN ;

\ ---- Bunny state (#5) --------------------------------------------------
VARIABLE bx   VARIABLE by       \ anchor, 1/16 px
VARIABLE vx   VARIABLE vy
VARIABLE grounded
VARIABLE facing                 \ 0 right, 1 left

: ax  ( -- x )  bx @ 4 RSHIFT $FFFC AND ;   \ byte aligned for blit
: ay  ( -- y )  by @ 4 RSHIFT ;

\ cur-frame - hop1 standing; hop2 rising, hop3 near the top, hop4 falling.
: cur-frame  ( -- n )
  grounded @ IF 0 ELSE
    vy @ -20 < IF 1 ELSE  vy @ 20 > IF 3 ELSE 2 THEN  THEN
  THEN
  facing @ IF seq-hopl + THEN ;

\ ---- Drawing -----------------------------------------------------------
\ The new frame is blitted first (opaque, so it covers its own box), then
\ only the strips of the old box outside the new one are erased to white,
\ then platforms and hearts that either box touched are redrawn.
VARIABLE d-frame  VARIABLE d-x  VARIABLE d-y  VARIABLE d-w  VARIABLE d-h
VARIABLE n-x  VARIABLE n-y  VARIABLE n-w  VARIABLE n-h
VARIABLE ox1  VARIABLE ox2  VARIABLE oy1  VARIABLE oy2
VARIABLE nx1  VARIABLE nx2  VARIABLE ny1  VARIABLE ny2
VARIABLE mr1  VARIABLE mr2

\ fill-rect - white byte columns c1..c2-1, rows r1..r2-1; nothing if empty.
: fill-rect  ( c1 c2 r1 r2 -- )
  2DUP < 0= IF 2DROP 2DROP EXIT THEN
  OVER - >R >R
  2DUP < 0= IF 2DROP R> R> 2DROP EXIT THEN
  OVER -  SWAP 4 *  R>  ROT  R>  $FF fill-box ;

: boxes  ( -- )
  d-x @ 2 RSHIFT DUP ox1 !  d-w @ + ox2 !
  d-y @ DUP oy1 !  d-h @ + oy2 !
  n-x @ 2 RSHIFT DUP nx1 !  n-w @ + nx2 !
  n-y @ DUP ny1 !  n-h @ + ny2 ! ;

: erase-uncovered  ( -- )
  d-w @ 0= IF EXIT THEN
  boxes
  ox1 @ ox2 @  oy1 @  oy2 @ ny1 @ MIN  fill-rect
  ox1 @ ox2 @  oy1 @ ny2 @ MAX  oy2 @  fill-rect
  oy1 @ ny1 @ MAX mr1 !  oy2 @ ny2 @ MIN mr2 !
  ox1 @  ox2 @ nx1 @ MIN  mr1 @ mr2 @ fill-rect
  ox1 @ nx2 @ MAX  ox2 @  mr1 @ mr2 @ fill-rect ;

VARIABLE rlo  VARIABLE rhi

: repair  ( -- )
  n-y @ d-y @ MIN rlo !
  n-y @ n-h @ +  d-y @ d-h @ +  MAX rhi !
  #plats 0 DO
    I plat 2 + C@  DUP rhi @ <  SWAP 4 + rlo @ >  AND
    IF I plat draw-plat THEN
  LOOP
  rlo @ 14 < IF draw-hearts THEN ;

\ draw-frame - show frame n at the bunny's anchor, if anything changed.
: draw-frame  ( n -- )
  DUP f-ox ax + n-x !
  DUP f-oy ay + n-y !
  DUP spr DUP rec-wb n-w !  rec-h n-h !
  DUP d-frame @ =  n-x @ d-x @ = AND  n-y @ d-y @ = AND
  IF DROP EXIT THEN
  DUP spr n-x @ n-y @ blit
  d-frame !
  erase-uncovered
  repair
  n-x @ d-x !  n-y @ d-y !  n-w @ d-w !  n-h @ d-h ! ;

\ ---- Physics (#5, #6) --------------------------------------------------
VARIABLE old-foot
VARIABLE py

: hop!  ( -- )
  hop-v NEGATE vy !  0 grounded !
  left? IF  hop-dx NEGATE vx !  1 facing !
  ELSE right? IF  hop-dx vx !  0 facing !
  ELSE 0 vx ! THEN THEN ;

: turn  ( -- )
  left? IF 1 facing ! THEN
  right? IF 0 facing ! THEN ;

\ in-span? - lo <= n < hi. Used instead of the kernel WITHIN, which reads
\ n one cell too deep (LDD 4+2,U at kernel.asm CODE_WITHIN).
: in-span?  ( n lo hi -- f )
  >R  OVER > 0=  SWAP R> <  AND ;

\ land? - while falling, stop on the first platform whose top the feet
\ crossed this frame. Platforms are one-way: hops pass up through them.
: land?  ( -- )
  #plats 0 DO
    I plat 2 + C@ py !
    old-foot @ py @ > 0=
    ay py @ < 0=  AND
    bx @ 4 RSHIFT  I plat C@  I plat 1 + C@ 1 +  in-span?  AND
    IF
      py @ 16 * by !  0 vy !  0 vx !  1 grounded !
      EXIT        \ fc.py adds the UNLOOP for EXIT inside DO
    THEN
  LOOP ;

: physics  ( -- )
  grounded @ IF
    turn
    hop-pressed? IF hop! THEN
  ELSE
    ay old-foot !
    vy @ gravity + vy !
    bx @ vx @ +  min-ax 16 * MAX  max-ax 16 * MIN  bx !
    by @ vy @ +
    DUP min-ay 16 * < IF DROP min-ay 16 *  0 vy ! THEN
    by !
    vy @ 0 > IF land? THEN
  THEN ;

\ ---- Carrot (#7) -------------------------------------------------------
: draw-carrot  ( n -- )
  DUP spr  SWAP DUP f-ox carrot-x +  SWAP f-oy carrot-y +  blit ;

: white-carrot  ( -- )
  spr-carrot3  carrot-x spr-carrot3-ox +  carrot-y spr-carrot3-oy +  white-box ;

: hold  ( frames -- )  0 DO vsync LOOP ;

: at-carrot?  ( -- f )
  grounded @  ay carrot-y = AND  ax carrot-x 28 - > AND ;

\ eat - stand left of the carrot and munch it down bite by bite.
: eat  ( -- )
  carrot-x 16 - 16 * bx !  carrot-y 16 * by !  0 facing !
  0 draw-frame
  seq-carrot draw-carrot
  20 hold
  4 0 DO
    seq-munch I + draw-frame
    12 hold
    white-carrot
    I 3 < IF seq-carrot I + 1 + draw-carrot THEN
    repair
  LOOP
  0 draw-frame
  30 hold ;

\ complete - wait for space (the same level again) or BREAK. Text is #13.
: complete  ( -- )
  BEGIN
    vsync read-input
    hop-pressed? break? OR
  UNTIL ;

\ ---- Level flow --------------------------------------------------------
: start-level  ( -- )
  white-screen
  draw-level
  draw-hearts
  seq-carrot draw-carrot
  start-x 16 * bx !  start-y 16 * by !
  0 vx !  0 vy !  1 grounded !  0 facing !
  -1 d-frame !  0 d-x !  191 d-y !  0 d-w !  0 d-h !
  0 draw-frame ;

\ step - one frame of play after input has been sampled.
: step  ( -- )
  physics
  cur-frame draw-frame
  at-carrot? IF
    eat complete
    break? 0= IF start-level THEN
  THEN ;

: main  ( -- )
  rg-init
  3 lives !
  start-level
  BEGIN
    vsync
    read-input
    step
    break?
  UNTIL
  exit-basic ;
