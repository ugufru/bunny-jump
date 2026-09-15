# mk/shot.mk: headless screenshot of any program (issue #2).
#
#   make shot                          shot bunny-jump.bin at vsync call 1
#   make shot SHOT_PROG=spritetest SHOT_AT=5
#
# Runs XRoar with no window or audio, traps at the SHOT_AT-th call of the
# kernel's vsync word (so the program's loop must call vsync), writes a raw
# RAM dump to build/shot.ram, then renders the RG6 page at vram-base with
# tools/rg6shot.py into build/shot.png. perl alarm guards against a hang.
# --page-ptr=0x7A04 picks the page the game last showed (issue #19); other
# programs leave that cell alone and get the vram-base page.

SHOT_PROG ?= $(NAME)
SHOT_AT   ?= 1
SHOT_RAM   = build/shot.ram
SHOT_PNG   = build/shot.png
VSYNC_PC   = $(shell awk '/Symbol: CODE_VSYNC /{print $$NF}' $(KERNEL_MAP))
VRAM_BASE_ADDR = $(shell awk '/Symbol: VRAM_BASE /{print $$NF}' $(KERNEL_MAP))

shot: $(SHOT_PROG).bin | build
	rm -f $(SHOT_RAM)
	perl -e 'alarm 60; exec @ARGV' $(XROAR) \
	    -ui null -ao null -run $(SHOT_PROG).bin \
	    -trap pc=0x$(VSYNC_PC) -trap-range $(SHOT_AT)-$(SHOT_AT) \
	    -trap-snap $(SHOT_RAM) -trap-timeout 1 -timeout 50 > build/shot.log 2>&1
	python3 tools/rg6shot.py $(SHOT_RAM) $(SHOT_PNG) 0x$(VRAM_BASE_ADDR) --page-ptr=0x7A04

.PHONY: shot
