\ autoplay.fs - scripted play-through for headless checks with make shot
\
\ Same game as bunny-jump.fs, but the keyboard is replaced by a script so
\ make shot SHOT_PROG=autoplay SHOT_AT=n shows real hops at frame n.
\ The script climbs the level: hop right to the right ledge, left to the
\ left ledge, right to the carrot ledge, right again to reach the carrot.

INCLUDE game.fs

\ Script: pairs of (frames, keys), keys bit 0 left, bit 1 right, bit 2 hop
\ (true on that frame only, like hop-pressed?). Frames count loop passes.
DATA[PY script
bytes.fromhex("0A00" "0106" "3B00" "0105" "3B00" "0106" "3B00" "0106" "FF00" "FF00")
]DATA

VARIABLE sp        \ current script pair
VARIABLE sp-left   \ frames left in it

: auto-input  ( -- )
  sp @ 1 + C@
  DUP 1 AND in-left !
  DUP 2 AND in-right !
  4 AND in-hop !
  sp-left @ 1 - DUP sp-left !
  0= IF  sp @ 2 + DUP sp !  C@ sp-left !  THEN ;

: auto-main  ( -- )
  rg-init
  3 lives !
  start-level
  script sp !  script C@ sp-left !
  BEGIN
    vsync
    auto-input
    step
    break?
  UNTIL
  exit-basic ;

auto-main
