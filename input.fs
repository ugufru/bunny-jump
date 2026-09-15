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

\ Per-frame state, updated by read-input. Non-zero means pressed.
VARIABLE in-left
VARIABLE in-right
VARIABLE in-space      \ space state at the latest sample
VARIABLE in-hop        \ non-zero only on the frame space went down
VARIABLE in-break
VARIABLE in-lpress     \ non-zero only on the frame left went down (#23)
VARIABLE in-rpress     \ non-zero only on the frame right went down

\ read-input - sample the keyboard once per frame. CODE for #19: one pass
\ over the four matrix columns instead of four Forth KEY-HELD? calls. Like
\ KBD-SCAN it only writes $FF02, so the field-sync flag is left alone.
CODE read-input  \ ( -- )
        LDB     #$DF            ; column 5: left arrow, row 3
        STB     $FF02
        LDB     $FF00
        COMB
        ANDB    #$08            ; B = left now
        LDA     FVAR_in_left+1
        COMA
        PSHS    A               ; not left before
        TFR     B,A
        ANDA    ,S+             ; A = went down this frame
        STA     FVAR_in_lpress+1
        CLR     FVAR_in_lpress
        STB     FVAR_in_left+1
        CLR     FVAR_in_left
        LDB     #$BF            ; column 6: right arrow, row 3
        STB     $FF02
        LDB     $FF00
        COMB
        ANDB    #$08            ; B = right now
        LDA     FVAR_in_right+1
        COMA
        PSHS    A
        TFR     B,A
        ANDA    ,S+
        STA     FVAR_in_rpress+1
        CLR     FVAR_in_rpress
        STB     FVAR_in_right+1
        CLR     FVAR_in_right
        LDB     #$7F            ; column 7: space, row 3
        STB     $FF02
        LDB     $FF00
        COMB
        ANDB    #$08            ; B = space now
        LDA     FVAR_in_space+1
        COMA
        PSHS    A               ; not space before
        TFR     B,A
        ANDA    ,S+             ; A = went down this frame
        STB     FVAR_in_space+1
        CLR     FVAR_in_space
        STA     FVAR_in_hop+1
        CLR     FVAR_in_hop
        LDB     #$FB            ; column 2: BREAK, row 6
        STB     $FF02
        LDB     $FF00
        COMB
        ANDB    #$40
        CLRA
        STD     FVAR_in_break
        LDB     #$FF            ; release the strobe, as KBD-SCAN does
        STB     $FF02
        ;NEXT
;CODE

\ Frame-sampled queries (valid after read-input).
: left?        ( -- f )  in-left @ ;
: right?       ( -- f )  in-right @ ;
: hop-pressed? ( -- f )  in-hop @ ;
