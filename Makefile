# bunny-jump: RG6 platformer proof of concept for the CoCo on the coco
# Forth toolchain.
#
# Mirrors ~/github/bunny/Makefile (itself a copy of coco/make/demo.mk, which
# can't be included from here because it hardcodes ../../ paths). Override
# COCO to point elsewhere.

COCO       ?= $(HOME)/github/coco
FC          = python3 $(COCO)/tools/fc.py
KERNEL_DIR  = $(COCO)/kernel
KERNEL_MAP  = $(KERNEL_DIR)/build/kernel.map
KERNEL_BIN  = $(KERNEL_DIR)/build/kernel.bin
XROAR_ROMS  = -bas ~/.xroar/roms/bas12.rom -extbas ~/.xroar/roms/extbas11.rom
# -tv-type ntsc -tv-input cmp-br turns on RG6 artifact colour.
XROAR_TV    = -tv-type ntsc -tv-input cmp-br
XROAR_EXTRA ?= -kbd-translate
XROAR       = xroar -machine coco2bus -ram 32 $(XROAR_ROMS) $(XROAR_TV)

# coco libraries pulled in through build/coco-libs.fs (bye.fs brings vdg.fs
# and screen.fs with it).
COCO_LIBS   = rg-pixel.fs sprite.fs keyboard.fs bye.fs

NAME    = bunny-jump
BIN     = $(NAME).bin
GEN     = build/coco-libs.fs

# Extra prerequisites for every program. fc.py INCLUDEs are not tracked, so
# shared sources are listed here; mk/*.mk fragments add generated ones.
PROG_DEPS = blit.fs input.fs game.fs fast.fs

all: $(BIN)

build:
	mkdir -p build

# Absolute INCLUDEs keep COCO overridable; fc.py resolves INCLUDE paths
# relative to the including file.
$(GEN): Makefile | build
	printf 'INCLUDE $(COCO)/lib/%s\n' $(COCO_LIBS) > $@

# Optional fragments, one per feature, so work on them doesn't collide:
# mk/sprites.mk (sprite pipeline), mk/shot.mk (headless screenshots).
-include mk/*.mk

# Any top-level program: make foo.bin builds foo.fs, make run-foo runs it.
%.bin: %.fs $(GEN) $(PROG_DEPS) $(KERNEL_MAP) $(KERNEL_BIN)
	$(FC) $< \
	    --kernel     $(KERNEL_MAP) \
	    --kernel-bin $(KERNEL_BIN) \
	    --output     $@

$(KERNEL_MAP) $(KERNEL_BIN):
	$(MAKE) -C $(KERNEL_DIR)

run: $(BIN)
	$(XROAR) $(XROAR_EXTRA) -run $(BIN)

run-%: %.bin
	$(XROAR) $(XROAR_EXTRA) -run $<

cycles: $(NAME).fs $(GEN) $(PROG_DEPS) $(KERNEL_MAP)
	$(FC) $(NAME).fs --kernel $(KERNEL_MAP) --cycles --output build/cycles.bin

# Issue tracker page, rebuilt whenever the tracker or roadmap changes.
# issues.html is committed, not a build product.
issues: issues.html

issues.html: issues.jsonl roadmap.jsonl tools/issues_html.py
	python3 tools/issues_html.py render issues.jsonl issues.html roadmap.jsonl --title="bunny-jump issues"

clean:
	rm -rf build *.bin

.PHONY: all run cycles issues clean
