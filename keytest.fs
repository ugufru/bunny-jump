\ keytest.fs - input debug build for issue #4
\
\ Three boxes across the middle of the RG6 screen:
\   left box   white while LEFT arrow is held, black otherwise
\   middle box toggles white/red once per SPACE press (holding does nothing)
\   right box  white while RIGHT arrow is held, black otherwise
\ BREAK exits to BASIC.

INCLUDE build/coco-libs.fs
INCLUDE input.fs

32 CONSTANT rg-row     \ bytes per VRAM row

\ box ( col color -- ) fill 6 bytes wide, 32 rows tall starting at row 80.
: box  ( col color -- )
  112 80 DO
    OVER I rg-row * + vram-base +  6  ROT DUP >R  FILL  R>
  LOOP 2DROP ;

: on/off  ( f -- color )  IF $FF ELSE $00 THEN ;

VARIABLE hop-color

: main  ( -- )
  rg-init
  $FF hop-color !
  BEGIN
    vsync
    read-input
    hop-pressed? IF hop-color @ $55 XOR hop-color ! THEN
    4  left?  on/off box
    13 hop-color @   box
    22 right? on/off box
    break?
  UNTIL
  exit-basic ;

main
