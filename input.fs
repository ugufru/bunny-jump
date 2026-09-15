\ input.fs - keyboard input for bunny-jump (issue #4)
\
\ Direct matrix scanning via KEY-HELD? ( col row -- f ) from
\ coco/lib/keyboard.fs, so held keys read as held every frame.
\ Matrix positions match kernel KEY_TABLE (kernel.asm): arrows $1C-$1F in
\ columns 3-6 row 3, space $20 column 7 row 3, BREAK $03 column 2 row 6.
\
\ KBD-SCAN writes the column strobe to $FF02 (a write, so it does not clear
\ the VDG field-sync flag that vsync polls) and restores $FF afterwards.
\ It keeps no debounce state, so it never eats keys. break? is used instead
\ of KEY? because KEY? decodes only the first pressed key (holding an arrow
\ would hide BREAK) and applies shift handling we do not need.
\
\ Requires: build/coco-libs.fs (keyboard.fs).

\ flag ( n -- f ) normalize nonzero row bits to a true flag.
: flag  ( n -- f )  0= 0= ;

\ Live matrix reads, usable any time.
: left-held?   ( -- f )  KB-C5 KB-R3 KEY-HELD? flag ;
: right-held?  ( -- f )  KB-C6 KB-R3 KEY-HELD? flag ;
: space-held?  ( -- f )  KB-C7 KB-R3 KEY-HELD? flag ;
: break?       ( -- f )  KB-C2 KB-R6 KEY-HELD? flag ;

\ Per-frame state, updated by read-input.
VARIABLE in-left
VARIABLE in-right
VARIABLE in-space      \ space state at the latest sample
VARIABLE in-hop        \ true only on the frame space went down

\ read-input - sample the keyboard once per frame (call right after vsync).
: read-input  ( -- )
  left-held?  in-left !
  right-held? in-right !
  space-held? DUP in-space @ INVERT AND in-hop !
  in-space ! ;

\ Frame-sampled queries (valid after read-input).
: left?        ( -- f )  in-left @ ;
: right?       ( -- f )  in-right @ ;
: hop-pressed? ( -- f )  in-hop @ ;
