\ bunny-jump-1x.fs - the game with 1x1 sprite rows (issue #20 experiment)
\
\ Same game as bunny-jump.fs, but the art keeps source rows 1:1 instead of
\ doubling them, so the bunny is half as tall (squashed on the TV).
\ Build and run: make run-bunny-jump-1x

INCLUDE build/sprites-1x.fs
INCLUDE game.fs

main
