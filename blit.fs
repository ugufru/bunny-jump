\ blit.fs - opaque, byte-aligned sprite drawing for RG6 (issue #12)
\
\ Kernel spr-draw treats code 0 as transparent, so it cannot draw the black
\ outlines on the white background, and it works one pixel at a time. These
\ words copy whole VRAM bytes instead.
\
\ Requires: rg-pixel.fs (rv = VRAM base, set by rg-init).

VARIABLE blt-dst    \ blit scratch: start of the current VRAM row span
VARIABLE blt-w      \ blit scratch: bytes per row
VARIABLE blt-h      \ blit scratch: rows left

\ blit - copy a sprite record opaque into VRAM with its top-left at x,y.
\ Record: w h (coco/lib/sprite.fs layout), then ceil(w/4) bytes per row.
\ x is in artifact pixels and must be a multiple of 4; w and h must be
\ non-zero and the box must fit on screen: no clipping.
CODE blit  \ ( addr x y -- )
        PSHS    X,U
        LDD     ,U              ; D = top row
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
        STD     FVAR_blt_dst
        LDD     2,U             ; D = left pixel
        ASRA
        RORB
        ASRA
        RORB                    ; D = left byte
        ADDD    FVAR_blt_dst
        STD     FVAR_blt_dst
        LDY     4,U             ; Y = sprite record
        LDB     ,Y              ; w in pixels
        ADDB    #3
        LSRB
        LSRB
        STB     FVAR_blt_w+1    ; bytes per row
        LDB     1,Y
        STB     FVAR_blt_h+1
        LEAY    2,Y             ; Y = pixel data
@row    LDU     FVAR_blt_dst
        LDB     FVAR_blt_w+1
@byte   LDA     ,Y+
        STA     ,U+
        DECB
        BNE     @byte
        LDD     FVAR_blt_dst
        ADDD    #32             ; down one row
        STD     FVAR_blt_dst
        DEC     FVAR_blt_h+1
        BNE     @row
        PULS    X,U
        LEAU    6,U
        ;NEXT
;CODE

\ fill-box - fill h rows of wbytes VRAM bytes with byte, top-left at x,y
\ (x in artifact pixels, a multiple of 4). wbytes and h must be at least 1.
\ CODE for #17: the Forth DO/FILL version cost a FILL call per row.
CODE fill-box  \ ( x y wbytes h byte -- )
        PSHS    X,U
        LDD     6,U             ; D = top row
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
        TFR     D,X             ; X = start of the top row
        LDD     8,U             ; D = left pixel
        ASRA
        RORB
        ASRA
        RORB                    ; D = left byte
        LEAX    D,X             ; X = first byte to fill
        LDB     3,U
        STB     FVAR_blt_h+1    ; rows left
        LDB     5,U
        STB     FVAR_blt_w+1    ; bytes per row
        LDA     1,U             ; A = fill byte
@row    TFR     X,U
        LDB     FVAR_blt_w+1
@byte   STA     ,U+
        DECB
        BNE     @byte
        LEAX    32,X            ; next row
        DEC     FVAR_blt_h+1
        BNE     @row
        PULS    X,U
        LEAU    10,U
        ;NEXT
;CODE

\ white-box - erase the box a sprite record covers at x,y to white.
: white-box  ( addr x y -- )
  ROT DUP C@ 3 + 2 RSHIFT  SWAP 1 + C@  $FF fill-box ;

\ white-screen - fill all of RG6 VRAM with white.
: white-screen  ( -- )  rv @ 6144 $FF FILL ;
