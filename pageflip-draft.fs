\ Draft for #17: page flipping, to be merged into game.fs.
\
\ Two RG6 pages: page 0 at vram-base ($0600, kernel-reserved) and page 1 at
\ $6000 ($6000-$77FF: above the app and the font at $5800, below the data
\ stack at $7E00, inside 32K). Every frame is drawn into the hidden page
\ (rv, which blit and fill-box already use), then flip waits for vsync and
\ points the SAM at it, so the visible page is never mid-draw: no flicker,
\ and an overrun shows as a repeated frame instead of a torn one.
\
\ Each page remembers what bunny it last showed (d-frame d-x d-y d-w d-h are
\ page-indexed cells), so erase-uncovered and repair work unchanged: a page
\ erases the bunny it showed two frames ago.

$6000 CONSTANT page1-base

VARIABLE page                     \ index of the hidden (drawing) page

: page-base  ( n -- addr )  IF page1-base ELSE vram-base THEN ;

\ Per-page cells: two 16-bit cells each, indexed by page.
DATA[PY d-frame-tab
bytes.fromhex("0000" * 2)
]DATA
DATA[PY d-x-tab
bytes.fromhex("0000" * 2)
]DATA
DATA[PY d-y-tab
bytes.fromhex("0000" * 2)
]DATA
DATA[PY d-w-tab
bytes.fromhex("0000" * 2)
]DATA
DATA[PY d-h-tab
bytes.fromhex("0000" * 2)
]DATA

: d-frame  ( -- addr )  page @ 2* d-frame-tab + ;
: d-x      ( -- addr )  page @ 2* d-x-tab + ;
: d-y      ( -- addr )  page @ 2* d-y-tab + ;
: d-w      ( -- addr )  page @ 2* d-w-tab + ;
: d-h      ( -- addr )  page @ 2* d-h-tab + ;

\ use-page - draw into page n from now on.
: use-page  ( n -- )  DUP page !  page-base rv ! ;

\ flip - at the next vsync show the page just drawn, then draw into the
\ other one.
: flip  ( -- )
  vsync
  page @ page-base 9 RSHIFT set-sam-f
  1 page @ - use-page ;

\ start-level changes: for each page (1 use-page ... 0 use-page ...):
\   white-screen draw-level draw-hearts carrot, reset that page's d-* cells
\   (-1 d-frame !  0 d-x !  191 d-y !  0 d-w !  0 d-h !)
\ then show page 0 and leave page 1 hidden.
\ Main loop: BEGIN read-input step flip break? UNTIL (flip replaces vsync).
\ eat: after each change, draw the same state into both pages (draw, flip,
\ draw, flip) so the carrot bites and munch frames land on both.
\ Measure: set-sam-f is Forth (vdg.fs); if it is too slow for the vblank,
\ make a CODE version.
