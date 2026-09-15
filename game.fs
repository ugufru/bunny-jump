\ game.fs - bunny-jump game code, shared by bunny-jump.fs and autoplay.fs
\
\ Hop the bunny up the platforms to the carrot. A left/right arrow press
\ turns the bunny, or steps it when it already faces that way; space hops
\ (straight up, or up-left/up-right with an arrow held); BREAK quits. White background, black platforms, drawn with the opaque byte blit.
\
\ Art: PixelFarm, Stephen 'Redshrike' Challener (see CREDITS.md).
\ Work tracking: issues.jsonl (#5 hop, #6 platforms, #7 carrot, #8 hearts,
\ #18 hop feel, #19 page flipping and frame timing).

INCLUDE build/coco-libs.fs
\ Sprite data (build/sprites.fs or a variant) is INCLUDEd by the program
\ file before this one, so the same game can run with different art.
INCLUDE blit.fs
INCLUDE input.fs

\ ---- Tuning (#5, #6, #18) ----------------------------------------------
\ Positions and velocities are in 1/16 artifact pixel (scanline) units per
\ 60 Hz field. A hop lifts about hop-v*hop-v / (2*gravity) / 16 = 48 rows,
\ enough to clear the 36-40 row gaps between platforms, reaching the top in
\ hop-v/gravity = 14 fields (was 21 with gravity 4, hop-v 82, which the
\ user found slow and floaty). A diagonal hop to the next ledge covers
\ about 35 pixels sideways.
8   CONSTANT gravity        \ added to vy every field
111 CONSTANT hop-v          \ upward speed at take-off
28  CONSTANT hop-dx         \ sideways speed of a diagonal hop
64  CONSTANT step-dx        \ one step: 4 pixels, one VRAM byte (#23)
9   CONSTANT step-fields    \ step animation length in 60 Hz fields (#24)
28  CONSTANT min-ax         \ anchor x limits keep every frame on screen
100 CONSTANT max-ax         \   (widest frame reaches ax-28 .. ax+28)
30  CONSTANT min-ay         \ ceiling: the tallest 1x frame reaches ay-29

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

\ spr@ - frame index to record address through a table filled once by
\ init-tables. The generated spr word tests up to 19 indexes in turn,
\ about 4,000 cy at worst, too slow to call every frame (#17).
DATA[PY spr-tab
bytes.fromhex("00" * 64)
]DATA

: spr@  ( n -- addr )  2* spr-tab + @ ;
: rec-h   ( addr -- h )  1 + C@ ;

\ ---- Hearts (#8, #22) --------------------------------------------------
\ The bunny starts with no hearts and earns one per carrot eaten. At most
\ max-hearts are drawn, 12 pixels apart along the top left.
VARIABLE hearts
10 CONSTANT max-hearts

: draw-hearts  ( -- )
  hearts @ max-hearts MIN ?DUP IF
    0 DO  spr-heart  I 12 * 4 +  2  blit  LOOP
  THEN ;

\ ---- Bunny state (#5) --------------------------------------------------
VARIABLE bx   VARIABLE by       \ anchor, 1/16 px
VARIABLE vx   VARIABLE vy
VARIABLE grounded
VARIABLE facing                 \ 0 right, 1 left

: ax  ( -- x )  bx @ 4 RSHIFT $FFFC AND ;   \ byte aligned for blit
: ay  ( -- y )  by @ 4 RSHIFT ;

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
VARIABLE missed         \ fields that ended mid-pass, for flip (fast.fs)

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
; ---- blit, two bytes at a time. U walks the destination and the counters
; sit on the stack: ,S pair count, 1,S rows left, 2,S pairs per row, 3,S odd
; byte flag, 4,S bytes to skip to the next row (#19: ~42 cy per row, was 95).
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
        TFR     D,U             ; U = first destination byte
        LDA     FVAR_n_w+1      ; bytes per row
        LDB     #32
        PSHS    A
        SUBB    ,S+
        PSHS    B               ; skip = 32 - bytes per row
        ANDA    #1
        PSHS    A               ; odd byte flag
        LDA     FVAR_n_w+1
        LSRA
        PSHS    A               ; pairs per row
        LDA     1,Y
        PSHS    A               ; rows left
        LEAS    -1,S            ; pair counter
        LEAX    2,Y             ; X = pixel data
@row    TST     3,S
        BEQ     @pairs
        LDA     ,X+
        STA     ,U+
@pairs  LDB     2,S
        BEQ     @rowend
        STB     ,S
@pair   LDD     ,X++
        STD     ,U++
        DEC     ,S
        BNE     @pair
@rowend LDB     4,S
        LEAU    B,U             ; to the start of the next row
        DEC     1,S
        BNE     @row
        LEAS    5,S
        LDA     $FF03           ; a field ended during the blit? (#19)
        BPL     @ontime
        LDA     $FF02
        INC     FVAR_missed+1
@ontime
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

INCLUDE fast.fs

\ repair - redraw only the platforms and hearts the last move touched.
: repair  ( -- )
  repair-mask ?DUP IF
    #plats 0 DO  DUP 1 I LSHIFT AND IF I plat draw-plat THEN  LOOP
    $80 AND IF draw-hearts THEN
  THEN ;

\ draw-frame - show frame n at the bunny's anchor, if anything changed.
: draw-frame  ( n -- )
  DUP frame-pos
  DUP d-frame @ =  n-x @ d-x @ = AND  n-y @ d-y @ = AND
  IF DROP EXIT THEN
  DUP d-frame !
  spr@ n-x @ n-y @ move-sprite
  repair ;

\ ---- Physics (#5, #6) --------------------------------------------------
VARIABLE step-t     \ fields left in the step animation, 0 when idle (#24)

\ anim-frame - the frame to show: during a step, hop2, hop3, hop4 for 3
\ fields each (the sheet's hop cycle, played in place), else cur-frame.
: anim-frame  ( -- n )
  step-t @ ?DUP IF
    DUP 6 > IF DROP 1 ELSE 3 > IF 2 ELSE 3 THEN THEN
    facing @ IF seq-hopl + THEN
  ELSE
    cur-frame
  THEN ;

: hop!  ( -- )
  hop-v NEGATE vy !  0 grounded !  0 step-t !
  left? IF  hop-dx NEGATE vx !  1 facing !
  ELSE right? IF  hop-dx vx !  0 facing !
  ELSE 0 vx ! THEN THEN ;

\ supported? - is there a platform top at the feet row under the foot x?
: supported?  ( -- f )
  #plats 0 DO
    I plat 2 + C@ ay = IF
      bx @ 4 RSHIFT  DUP I plat C@ 1 - >  SWAP I plat 1 + C@ 1 + <  AND
      IF -1 EXIT THEN
    THEN
  LOOP
  0 ;

\ step-or-turn - an arrow press on the ground (#23), dir 0 right, 1 left.
\ Facing the other way: turn around only. Facing that way: step step-dx,
\ and fall if the step leaves the ledge.
: step-or-turn  ( dir -- )
  DUP facing @ = IF
    IF step-dx NEGATE ELSE step-dx THEN
    bx @ +  min-ax 16 * MAX  max-ax 16 * MIN  bx !
    step-fields step-t !
    supported? 0= IF  0 grounded !  0 vy !  0 vx !  0 step-t !  THEN
  ELSE
    facing !
  THEN ;

\ physics - grounded: space hops (diagonally with an arrow held), else an
\ arrow press turns or steps. Airborne: physics-air (fast.fs)
\ moves the bunny and lands it on one-way platforms, one step per 60 Hz
\ field that passed since the last flip, so a slow frame does not slow the
\ hop.
: physics  ( -- )
  grounded @ IF
    step-t @ IF  step-t @ fields @ - 0 MAX step-t !  THEN
    hop-pressed? IF
      hop!
    ELSE
      in-lpress @ IF 1 step-or-turn THEN
      in-rpress @ IF 0 step-or-turn THEN
    THEN
  ELSE
    physics-air
  THEN ;

\ ---- Carrot (#7) -------------------------------------------------------
: draw-carrot  ( n -- )
  DUP spr@  SWAP DUP f-ox carrot-x +  SWAP f-oy carrot-y +  blit ;

: white-carrot  ( -- )
  spr-carrot3  carrot-x spr-carrot3-ox +  carrot-y spr-carrot3-oy +  white-box ;

: hold  ( frames -- )  0 DO vsync LOOP ;

: at-carrot?  ( -- f )
  grounded @  ay carrot-y = AND  ax carrot-x 28 - > AND ;

\ eat-step - show bunny frame and carrot frame (seq-carrot + 4 = eaten) on
\ both pages, so the change stays whichever page is showing. The carrot is
\ redrawn before the bunny: the munch box ends exactly at the carrot tip,
\ so neither erases the other (#25).
: eat-step  ( frame carrot -- )
  2 0 DO
    white-carrot
    DUP seq-carrot seq-carrot-len + < IF DUP draw-carrot THEN
    OVER draw-frame
    repair
    draw-hearts
    flip
  LOOP
  2DROP ;

\ eat - stand left of the carrot and munch it down bite by bite. Like the
\ bunny demo, the bunny steps forward after each bite by what the bite took
\ off the carrot tip: each bite trims 4 pixels, so one step-dx (#25).
: eat  ( -- )
  carrot-x 16 - 16 * bx !  carrot-y 16 * by !  0 facing !  1 grounded !
  0 step-t !
  0 seq-carrot eat-step
  20 hold
  4 0 DO
    I IF step-dx bx +! THEN   \ follow the tip the last bite left
    seq-munch I +  seq-carrot I + 1 +  eat-step
    12 hold
  LOOP
  1 hearts +!             \ a heart for the carrot (#22)
  0  seq-carrot seq-carrot-len +  eat-step
  30 hold ;

\ complete - wait for space (the same level again) or BREAK. Text is #13.
: complete  ( -- )
  BEGIN
    vsync read-input
    hop-pressed? in-break @ OR
  UNTIL ;

\ ---- Level flow --------------------------------------------------------
\ init-tables - fill spr-tab and copy the tuning constants and table
\ addresses into the cells the CODE words in fast.fs read.
: init-tables  ( -- )
  spr-count 0 DO  I spr  I 2* spr-tab + !  LOOP
  gravity k-gravity !
  min-ax 16 * k-min-bx !  max-ax 16 * k-max-bx !
  min-ay 16 * k-min-by !
  #plats k-plats !  level level-addr !  spr-oxy oxy-addr !
  seq-hopl k-hopl !
  $6000 rv-alt ! ;

\ reset-page - draw the static screen on the drawing page and forget the
\ bunny it showed.
: reset-page  ( -- )
  white-screen
  draw-level
  draw-hearts
  seq-carrot draw-carrot
  -1 d-frame !  0 d-x !  191 d-y !  0 d-w !  0 d-h ! ;

: start-level  ( -- )
  start-x 16 * bx !  start-y 16 * by !
  0 vx !  0 vy !  1 grounded !  0 facing !  0 step-t !
  reset-page swap-page
  reset-page swap-page
  0 missed !  1 fields ! ;

\ step - one pass of play after input has been sampled; flip follows.
: step  ( -- )
  physics
  anim-frame draw-frame
  grounded @ IF
    at-carrot? IF
      eat complete
      in-break @ 0= IF start-level THEN
    THEN
  THEN ;

: main  ( -- )
  rg-init
  init-tables
  0 hearts !
  start-level
  BEGIN
    read-input
    step
    flip
    in-break @
  UNTIL
  exit-basic ;
