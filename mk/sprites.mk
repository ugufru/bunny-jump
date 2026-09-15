# mk/sprites.mk: sprite pipeline (issue #3). Included by the Makefile.
#
# tools/png2rg6.py turns the bunny sheet and assets/extra/*.txt art into
# build/sprites.fs (RG6 sprite records plus anchor constants) and
# build/preview.png. make preview opens the preview.

SPRITE_SRC = tools/png2rg6.py tools/frames.json assets/bunnysheet5.png \
             $(wildcard assets/extra/*.txt)

PROG_DEPS += build/sprites.fs

build/sprites.fs: $(SPRITE_SRC) | build
	python3 tools/png2rg6.py tools/frames.json build

build/preview.png: build/sprites.fs

preview: build/preview.png
	open $<

.PHONY: preview
