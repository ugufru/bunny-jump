\ autoplay.fs - scripted play-through for headless checks with make shot
\
\ Same game as bunny-jump.fs, but the keyboard is replaced by a script so
\ make shot SHOT_PROG=autoplay SHOT_AT=n shows real hops at vsync call n
\ (one per pass through flip, plus the eat holds). The script climbs the
\ level: hop right to the right ledge, left to the left ledge, right to the
\ carrot ledge, right again to reach the carrot.

INCLUDE build/sprites.fs
INCLUDE game.fs

\ Script: pairs of (passes, keys), keys bit 0 left, bit 1 right, bit 2 hop
\ (true on that pass only, like hop-pressed?).
DATA[PY script
bytes.fromhex("0A00" "0106" "3B00" "0105" "3B00" "0106" "3B00" "0106" "FF00" "FF00")
]DATA

VARIABLE sp        \ current script pair
VARIABLE sp-left   \ passes left in it

: auto-input  ( -- )
  sp @ 1 + C@
  DUP 1 AND in-left !
  DUP 2 AND in-right !
  4 AND in-hop !
  sp-left @ 1 - DUP sp-left !
  0= IF  sp @ 2 + DUP sp !  C@ sp-left !  THEN ;

: auto-main  ( -- )
  rg-init
  init-tables
  0 hearts !
  start-level
  script sp !  script C@ sp-left !
  \ Frame-rate stats for #19, read from the RAM dump: show-page (fast.fs)
  \ adds fields per flip at $7A00, counts flips at $7A02, and stores the
  \ page shown at $7A04.
  $7A00 6 0 FILL
  BEGIN
    auto-input
    step
    flip
    break?
  UNTIL
  exit-basic ;

auto-main
