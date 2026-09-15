\ fast.fs - per-frame game logic as CODE words (#17)
\
\ The Forth versions of physics, the frame anchor lookup and the repair test
\ ran every moving frame and, with the blit, pushed each pass past one
\ 60 Hz field. INCLUDEd by game.fs after the bunny state and drawing
\ VARIABLEs. CODE words can only name VARIABLEs (FVAR_*), so tuning
\ constants and data addresses are copied into the k-* and *-addr cells
\ by init-tables in game.fs.

VARIABLE k-gravity      \ added to vy each frame, 1/16 px
VARIABLE k-min-bx       \ anchor x limits, 1/16 px
VARIABLE k-max-bx
VARIABLE k-min-by       \ ceiling, 1/16 px
VARIABLE k-plats        \ platform count
VARIABLE level-addr     \ level table: x1 x2 y bytes per platform
VARIABLE oxy-addr       \ spr-oxy: signed ox oy bytes per frame index
VARIABLE old-foot       \ foot row before this frame's move
VARIABLE new-foot       \ foot row after it
VARIABLE foot-x         \ anchor x in pixels
VARIABLE k-hopl         \ first left-facing hop frame index (seq-hopl)
VARIABLE fields         \ 60 Hz fields since the last flip, 1 to 4

\ physics-air - airborne steps, one per field in fields (#19): gravity,
\ sideways move clamped to the screen, ceiling, then land on the first
\ platform whose top the feet crossed while falling (one-way platforms:
\ x1 <= foot x <= x2). Stops early on landing.
CODE physics-air  \ ( -- )
        PSHS    X
        LDA     FVAR_fields+1
        PSHS    A               ; steps left
@step   LDD     FVAR_by
        LSRA
        RORB
        LSRA
        RORB
        LSRA
        RORB
        LSRA
        RORB
        STD     FVAR_old_foot
        LDD     FVAR_vy
        ADDD    FVAR_k_gravity
        STD     FVAR_vy
        LDD     FVAR_bx
        ADDD    FVAR_vx
        CMPD    FVAR_k_min_bx
        BGE     @x1
        LDD     FVAR_k_min_bx
@x1     CMPD    FVAR_k_max_bx
        BLE     @x2
        LDD     FVAR_k_max_bx
@x2     STD     FVAR_bx
        LSRA
        RORB
        LSRA
        RORB
        LSRA
        RORB
        LSRA
        RORB
        STD     FVAR_foot_x
        LDD     FVAR_by
        ADDD    FVAR_vy
        CMPD    FVAR_k_min_by
        BGE     @y1
        LDD     FVAR_k_min_by
        CLR     FVAR_vy
        CLR     FVAR_vy+1
@y1     STD     FVAR_by
        LDD     FVAR_vy
        LBLE    @done           ; rising or level: no landing
        LDD     FVAR_by
        LSRA
        RORB
        LSRA
        RORB
        LSRA
        RORB
        LSRA
        RORB
        STD     FVAR_new_foot
        LDX     FVAR_level_addr
        LDA     FVAR_k_plats+1
        PSHS    A               ; platforms left
@plat   CLRA
        LDB     2,X             ; D = platform top
        CMPD    FVAR_old_foot
        BLO     @next           ; feet were already below it
        CMPD    FVAR_new_foot
        BHI     @next           ; feet have not reached it
        LDB     FVAR_foot_x+1
        CMPB    ,X
        BLO     @next
        CMPB    1,X
        BHI     @next
        CLRA
        LDB     2,X
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA
        STD     FVAR_by         ; stand on the top
        CLRA
        CLRB
        STD     FVAR_vy
        STD     FVAR_vx
        INCB
        STD     FVAR_grounded
        BRA     @pop
@next   LEAX    3,X
        DEC     ,S
        BNE     @plat
@pop    LEAS    1,S
@done   LDD     FVAR_grounded
        BNE     @exit           ; landed: no more steps
        DEC     ,S
        LBNE    @step           ; long: the step body is over 127 bytes
@exit   LEAS    1,S
        PULS    X
        ;NEXT
;CODE

\ cur-frame - frame index for the bunny state: hop1 grounded, hop2 rising
\ (vy < -40), hop4 falling (vy > 40), hop3 in between, plus k-hopl when
\ facing left. CODE for #19; the Forth version cost about 1,000 cy.
CODE cur-frame  \ ( -- n )
        LDD     FVAR_grounded
        BEQ     @air
        CLRB
        BRA     @face
@air    LDD     FVAR_vy
        CMPD    #-40
        BGE     @notup
        LDB     #1
        BRA     @face
@notup  CMPD    #40
        BLE     @top
        LDB     #3
        BRA     @face
@top    LDB     #2
@face   TST     FVAR_facing+1
        BEQ     @push
        ADDB    FVAR_k_hopl+1
@push   CLRA
        LEAU    -2,U
        STD     ,U
        ;NEXT
;CODE

\ frame-pos - set n-x n-y to the top-left of frame n at the bunny anchor:
\ n-x = (bx/16 rounded down to a multiple of 4) + ox, n-y = by/16 + oy.
CODE frame-pos  \ ( n -- )
        PSHS    X
        LDX     FVAR_oxy_addr
        LDB     1,U
        ASLB
        ABX                     ; X = ox oy of frame n
        LEAU    2,U
        LDD     FVAR_bx
        LSRA
        RORB
        LSRA
        RORB
        LSRA
        RORB
        LSRA
        RORB
        ANDB    #$FC
        PSHS    D
        LDB     ,X
        SEX
        ADDD    ,S++
        STD     FVAR_n_x
        LDD     FVAR_by
        LSRA
        RORB
        LSRA
        RORB
        LSRA
        RORB
        LSRA
        RORB
        PSHS    D
        LDB     1,X
        SEX
        ADDD    ,S++
        STD     FVAR_n_y
        PULS    X
        ;NEXT
;CODE

\ repair-mask - which things the old and new bunny boxes touched, from the
\ ox1..ny2 cells move-sprite leaves: bit i for platform i (rows y..y+3),
\ bit 7 for the hearts (rows 0..13). Also sets rlo and rhi.
CODE repair-mask  \ ( -- mask )
        PSHS    X
        LDA     FVAR_ny1+1
        CMPA    FVAR_oy1+1
        BLS     @lo
        LDA     FVAR_oy1+1
@lo     STA     FVAR_rlo+1
        LDA     FVAR_ny2+1
        CMPA    FVAR_oy2+1
        BHS     @hi
        LDA     FVAR_oy2+1
@hi     STA     FVAR_rhi+1
        CLRB                    ; B = mask
        LDX     FVAR_level_addr
        LDA     #1
        PSHS    A               ; 1,S = bit for this platform
        LDA     FVAR_k_plats+1
        PSHS    A               ; ,S = platforms left
@plat   LDA     2,X
        CMPA    FVAR_rhi+1
        BHS     @next           ; platform starts below the boxes
        ADDA    #4
        CMPA    FVAR_rlo+1
        BLS     @next           ; platform ends above the boxes
        ORB     1,S
@next   ASL     1,S
        LEAX    3,X
        DEC     ,S
        BNE     @plat
        LEAS    2,S
        LDA     FVAR_rlo+1
        CMPA    #14
        BHS     @done
        ORB     #$80
@done   CLRA
        LEAU    -2,U
        STD     ,U
        PULS    X
        ;NEXT
;CODE

\ ---- Page flipping and frame timing (#19) -----------------------------
\ Two RG6 pages: page 0 at vram-base ($0600) and page 1 at $6000 ($6000-
\ $77FF: above the app, below the data stack at $7E00, inside 32K). Each
\ pass draws into the hidden page (rv, used by blit, fill-box and
\ move-sprite), then flip shows it at vsync and swaps, so the visible page
\ is never mid-draw. rv-alt and the a-* cells hold the other page's base and
\ the bunny box that page last showed, so each page erases its own bunny.
\
\ Timing: missed counts fields that ended during a pass (field-check here
\ and a check inside move-sprite); show-page turns that into fields, the
\ number of 60 Hz fields since the last flip (1 to 4), and physics runs
\ that many steps so the hop keeps its speed when a frame is slow.
\ Stats for autoplay checks: $7A00 total fields, $7A02 flips, $7A04 the
\ page last shown (read by tools/rg6shot.py --page-ptr).
VARIABLE rv-alt
VARIABLE a-frame  VARIABLE a-x  VARIABLE a-y  VARIABLE a-w  VARIABLE a-h

\ field-check - count a field that ended since the last check or vsync.
CODE field-check  \ ( -- )
        LDA     $FF03
        BPL     @done
        LDA     $FF02           ; clear the flag, as vsync does
        INC     FVAR_missed+1
@done   CLRA
        ;NEXT
;CODE

\ show-page - point the SAM display offset at rv, set fields, keep stats.
CODE show-page  \ ( -- )
        PSHS    X
        LDD     FVAR_missed
        ADDD    #1
        CMPD    #4
        BLS     @cap
        LDD     #4
@cap    STD     FVAR_fields
        ADDD    $7A00
        STD     $7A00
        LDD     $7A02
        ADDD    #1
        STD     $7A02
        CLRA
        CLRB
        STD     FVAR_missed
        LDD     FVAR_rv
        STD     $7A04
        LSRA                    ; A = rv / 512, the SAM F offset
        LDB     #7
        LDX     #$FFC6          ; F0 clear; +1 sets, +2 is the next bit
@bit    LSRA
        BCC     @clr
        STA     1,X
        BRA     @next
@clr    STA     ,X
@next   LEAX    2,X
        DECB
        BNE     @bit
        PULS    X
        ;NEXT
;CODE

\ swap-page - make the other page the drawing page.
CODE swap-page  \ ( -- )
        LDD     FVAR_rv
        LDY     FVAR_rv_alt
        STY     FVAR_rv
        STD     FVAR_rv_alt
        LDD     FVAR_d_frame
        LDY     FVAR_a_frame
        STY     FVAR_d_frame
        STD     FVAR_a_frame
        LDD     FVAR_d_x
        LDY     FVAR_a_x
        STY     FVAR_d_x
        STD     FVAR_a_x
        LDD     FVAR_d_y
        LDY     FVAR_a_y
        STY     FVAR_d_y
        STD     FVAR_a_y
        LDD     FVAR_d_w
        LDY     FVAR_a_w
        STY     FVAR_d_w
        STD     FVAR_a_w
        LDD     FVAR_d_h
        LDY     FVAR_a_h
        STY     FVAR_d_h
        STD     FVAR_a_h
        ;NEXT
;CODE

\ flip - end of a pass: count a late field, wait for vsync, show the page
\ just drawn, draw into the other one next.
: flip  ( -- )  field-check vsync show-page swap-page ;
