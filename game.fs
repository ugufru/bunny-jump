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

\ ---- Frame-rate probe (#17) -------------------------------------------
\ When probe-on is set, probe counts fields that ended since the last probe
\ or vsync into the cell at addr (clearing the field-sync flag the way
\ vsync does), one counter per spot, so the RAM dump shows where time goes. Called between steps that each take less than one field, so
\ no field is missed: fps = 60 * passes / (passes + count). Off in play.
VARIABLE probe-on

: probe  ( addr -- )
  probe-on @ IF
    $FF03 C@ $80 AND IF  $FF02 C@ DROP  1 OVER +!  THEN
  THEN  DROP ;

\ ---- Sprite helpers ----------------------------------------------------
: sx8     ( c -- n )  DUP 127 > IF 256 - THEN ;
: f-ox    ( n -- ox )  2* spr-oxy + C@ sx8 ;
: f-oy    ( n -- oy )  2* spr-oxy + 1 + C@ sx8 ;
: rec-wb  ( addr -- wbytes )  C@ 2 RSHIFT ;

\ spr@ - frame index to record address through a table filled once by
\ init-spr-tab. The generated spr word tests up to 19 indexes in turn,
\ about 4,000 cy at worst, too slow to call every frame (#17).
DATA[PY spr-tab
bytes.fromhex("00" * 64)
]DATA

: init-spr-tab  ( -- )
  spr-count 0 DO  I spr  I 2* spr-tab + !  LOOP ;

: spr@  ( n -- addr )  2* spr-tab + @ ;
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
\ move-sprite blits the new frame (opaque, so it covers its own box), then
\ erases to white only the strips of the old box outside the new one, then
\ platforms and hearts that either box touched are redrawn by repair.
\ Box values are byte columns and rows, all below 256; the CODE word keeps
\ them in the low byte of each VARIABLE.
VARIABLE d-frame  VARIABLE d-x  VARIABLE d-y  VARIABLE d-w  VARIABLE d-h
VARIABLE n-x  VARIABLE n-y  VARIABLE n-w
VARIABLE ox1  VARIABLE ox2  VARIABLE oy1  VARIABLE oy2
VARIABLE nx1  VARIABLE nx2  VARIABLE ny1  VARIABLE ny2
VARIABLE mv-c1  VARIABLE mv-c2  VARIABLE mv-r1  VARIABLE mv-r2

\ move-sprite - draw record at top-left x,y (x a multiple of 4) and erase
\ what is left of the previous box (d-x d-y in pixels, d-w in bytes, d-h
\ rows; d-w 0 means nothing drawn yet). Updates d-x d-y d-w d-h, and leaves
\ ox1 ox2 oy1 oy2 (old) and nx1 nx2 ny1 ny2 (new) for repair. CODE for
\ #17: the Forth version cost about 2.7 fields per moving frame.
CODE move-sprite  \ ( rec x y -- )
        PSHS    X,U
        LDY     4,U             ; Y = record
        LDB     1,U             ; new top row
        STB     FVAR_ny1+1
        ADDB    1,Y
        STB     FVAR_ny2+1      ; bottom row, exclusive
        LDA     ,Y              ; width in pixels
        LSRA
        LSRA
        STA     FVAR_n_w+1      ; bytes per row
        LDB     3,U             ; new left pixel
        LSRB
        LSRB
        STB     FVAR_nx1+1
        PSHS    A
        ADDB    ,S+
        STB     FVAR_nx2+1
; ---- blit, two bytes at a time
        LDB     FVAR_ny1+1
        CLRA
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA                    ; D = top * 32
        ADDD    FVAR_rv
        ADDB    FVAR_nx1+1
        ADCA    #0
        STD     FVAR_blt_dst
        LDB     1,Y
        STB     FVAR_blt_h+1    ; rows left
        LEAX    2,Y             ; X = pixel data
@row    LDU     FVAR_blt_dst
        LDB     FVAR_n_w+1
        LSRB                    ; B = pairs, carry = odd byte
        BCC     @even
        LDA     ,X+
        STA     ,U+
        TSTB
        BEQ     @rowend
@even   PSHS    B
@pair   LDD     ,X++
        STD     ,U++
        DEC     ,S
        BNE     @pair
        LEAS    1,S
@rowend LDD     FVAR_blt_dst
        ADDD    #32
        STD     FVAR_blt_dst
        DEC     FVAR_blt_h+1
        BNE     @row
; ---- erase the old box minus the new one
        LDB     FVAR_d_w+1
        BNE     @old
        LDB     FVAR_ny1+1      ; nothing drawn before: old box = new
        STB     FVAR_oy1+1
        LDB     FVAR_ny2+1
        STB     FVAR_oy2+1
        LBRA    @store
@old    LDB     FVAR_d_x+1
        LSRB
        LSRB
        STB     FVAR_ox1+1
        ADDB    FVAR_d_w+1
        STB     FVAR_ox2+1
        LDB     FVAR_d_y+1
        STB     FVAR_oy1+1
        ADDB    FVAR_d_h+1
        STB     FVAR_oy2+1
; top strip: columns ox1..ox2, rows oy1..min(oy2,ny1)
        LDA     FVAR_ox1+1
        STA     FVAR_mv_c1+1
        LDA     FVAR_ox2+1
        STA     FVAR_mv_c2+1
        LDA     FVAR_oy1+1
        STA     FVAR_mv_r1+1
        LDA     FVAR_oy2+1
        CMPA    FVAR_ny1+1
        BLS     @t1
        LDA     FVAR_ny1+1
@t1     STA     FVAR_mv_r2+1
        LBSR    @fill
; bottom strip: columns ox1..ox2, rows max(oy1,ny2)..oy2
        LDA     FVAR_oy1+1
        CMPA    FVAR_ny2+1
        BHS     @b1
        LDA     FVAR_ny2+1
@b1     STA     FVAR_mv_r1+1
        LDA     FVAR_oy2+1
        STA     FVAR_mv_r2+1
        LBSR    @fill
; middle rows max(oy1,ny1)..min(oy2,ny2)
        LDA     FVAR_oy1+1
        CMPA    FVAR_ny1+1
        BHS     @m1
        LDA     FVAR_ny1+1
@m1     STA     FVAR_mv_r1+1
        LDA     FVAR_oy2+1
        CMPA    FVAR_ny2+1
        BLS     @m2
        LDA     FVAR_ny2+1
@m2     STA     FVAR_mv_r2+1
; left strip: columns ox1..min(ox2,nx1)
        LDA     FVAR_ox2+1
        CMPA    FVAR_nx1+1
        BLS     @l1
        LDA     FVAR_nx1+1
@l1     STA     FVAR_mv_c2+1
        LBSR    @fill
; right strip: columns max(ox1,nx2)..ox2
        LDA     FVAR_ox1+1
        CMPA    FVAR_nx2+1
        BHS     @r1
        LDA     FVAR_nx2+1
@r1     STA     FVAR_mv_c1+1
        LDA     FVAR_ox2+1
        STA     FVAR_mv_c2+1
        LBSR    @fill
@store  LDU     2,S             ; U = data stack as it was on entry
        LDD     2,U
        STD     FVAR_d_x
        LDD     ,U
        STD     FVAR_d_y
        CLRA
        LDB     FVAR_n_w+1
        STD     FVAR_d_w
        LDB     FVAR_ny2+1
        SUBB    FVAR_ny1+1
        STD     FVAR_d_h
        PULS    X,U
        LEAU    6,U
        ;NEXT
; fill: white columns mv-c1..mv-c2-1, rows mv-r1..mv-r2-1; nothing if empty
@fill   LDB     FVAR_mv_c2+1
        SUBB    FVAR_mv_c1+1
        BLS     @fret
        STB     FVAR_blt_w+1
        LDB     FVAR_mv_r2+1
        SUBB    FVAR_mv_r1+1
        BLS     @fret
        STB     FVAR_blt_h+1
        LDB     FVAR_mv_r1+1
        CLRA
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA
        ADDD    FVAR_rv
        ADDB    FVAR_mv_c1+1
        ADCA    #0
        TFR     D,X
@frow   TFR     X,U
        LDB     FVAR_blt_w+1
        LDA     #$FF
@fbyte  STA     ,U+
        DECB
        BNE     @fbyte
        LEAX    32,X
        DEC     FVAR_blt_h+1
        BNE     @frow
@fret   RTS
;CODE

VARIABLE rlo  VARIABLE rhi

: repair  ( -- )
  ny1 @ oy1 @ MIN rlo !
  ny2 @ oy2 @ MAX rhi !
  #plats 0 DO
    I plat 2 + C@  DUP rhi @ <  SWAP 4 + rlo @ >  AND
    IF I plat draw-plat THEN
  LOOP
  rlo @ 14 < IF draw-hearts THEN ;

\ draw-frame - show frame n at the bunny's anchor, if anything changed.
: draw-frame  ( n -- )
  DUP f-ox ax + n-x !
  DUP f-oy ay + n-y !
  DUP d-frame @ =  n-x @ d-x @ = AND  n-y @ d-y @ = AND
  IF DROP EXIT THEN
  $7006 probe
  DUP d-frame !
  spr@ n-x @ n-y @ move-sprite
  $7008 probe
  repair ;

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
  DUP spr@  SWAP DUP f-ox carrot-x +  SWAP f-oy carrot-y +  blit ;

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
  $7004 probe
  cur-frame draw-frame
  at-carrot? IF
    eat complete
    break? 0= IF start-level THEN
  THEN ;

: main  ( -- )
  rg-init
  init-spr-tab
  3 lives !
  start-level
  BEGIN
    vsync
    read-input
    step
    break?
  UNTIL
  exit-basic ;
