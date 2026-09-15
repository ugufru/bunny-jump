\ blittest.fs - test program for blit.fs (issue #12)
\
\ White screen; an 8x8 test record (black border, red ring, blue centre)
\ drawn at the corners and middle. The one at 60,100 is drawn and erased
\ again with white-box, so a white gap should show there. BREAK exits.

INCLUDE build/coco-libs.fs
INCLUDE blit.fs

\ w=8 h=8, then 2 bytes per row.
DATA[PY tst
bytes.fromhex("0808" "0000" "2AA8" "2AA8" "2558" "2558" "2AA8" "2AA8" "0000")
]DATA

: main  ( -- )
  rg-init
  white-screen
  tst   0   0 blit
  tst 120   0 blit
  tst   0 184 blit
  tst 120 184 blit
  tst  40 100 blit
  tst  60 100 blit
  tst  60 100 white-box
  tst  80 100 blit
  BEGIN
    vsync
    KEY? 3 =
  UNTIL
  exit-basic ;

main
