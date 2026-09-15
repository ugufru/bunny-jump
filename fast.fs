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

\ physics-air - one airborne frame: gravity, sideways move clamped to the
\ screen, ceiling, then land on the first platform whose top the feet
\ crossed while falling (one-way platforms: x1 <= foot x <= x2).
CODE physics-air  \ ( -- )
        PSHS    X
        LDD     FVAR_by
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
        BLE     @done           ; rising or level: no landing
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
@done   PULS    X
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
