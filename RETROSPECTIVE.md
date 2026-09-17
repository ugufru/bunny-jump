# Retrospective: building bunny-jump in one session

Written from the console session that produced this repo, start to finish:
from "I have a concept for a game" to a public repo with a playable
platformer running at 60 frames per second.

## What happened

**Planning.** An Explore subagent read the bunny demo and the coco library.
Its first useful finding was a correction: the bunny demo is CG3 at 128x96
with buff, cyan, magenta and orange, not the 128x192 four-color screen you
described. The 128x192 artifact mode and its sprite library existed in coco,
unused by the demo. Two questions shaped everything after: whether to double
the sprite rows (an artifact pixel is two dots wide and one scanline tall, so
1:1 art is squashed) and what color the carrot could be in a palette of
black, white, blue and red. You chose doubled rows, then asked what patterns
give orange or green, which set the carrot as red with blue greens.

**Twelve issues, then more.** The plan became issues.jsonl and a ranked
roadmap before any code. Issue 1 was a scaffold that put four color bands on
screen, which you confirmed read black, blue, red, white. That one screen
settled the artifact phase question that the rest of the art depended on.

**Three agents in parallel, then a direction change.** With the scaffold
committed I ran a workflow: one agent for headless screenshots, one for the
sprite converter, one for the keyboard. While they ran you asked for a white
background with black outlines and red and blue accents. Workflow agents are
not reachable once running, so the sprite agent finished with the old
white-on-black mapping and I redid the color mapping and the opaque drawing
path afterward.

**The core mechanic.** Physics, platforms, the carrot and hearts went in as
one pass in Forth, verified with a scripted autoplay build and contact sheets
of headless screenshots. You played it and said it was not terrible.

**Performance.** You asked whether it ran at 60 fps. It did not: about 22.
That became the longest thread of the session and ended at a steady 60.

**Features after the mechanic.** Earned hearts, stepping with an animation,
the bunny following the carrot as it eats.

**Release.** README, BSD 2-Clause license matching the bunny demo, credits
for the derived art, a screenshot, and a public repo.

## What worked

**Headless verification, again.** Window capture never worked once this
session: ScreenCaptureKit stalled every attempt, including by window id.
`make shot` (XRoar headless, trap at the Nth vsync, dump RAM, render the page
to a PNG) carried every check. Better than that, `autoplay.fs` runs the same
game from a key script, so a hop, a landing or a carrot bite could be
screenshotted at a chosen frame and assembled into contact sheets.

**Measuring instead of eyeballing.** Twice I nearly drew the wrong conclusion
from a thumbnail. The clearest case: after adding stepping, the right step
looked like it had not happened. Measuring the bunny's pixel columns showed
24, 23, 19, 20, 24, exactly the turn, step, turn, step that was intended. The
mirrored left-facing frame had shifted the outline by a pixel and fooled me.

**Measuring the frame rate properly.** The compiler's cycle estimates are
worst-case paths and were misleading: they said the draw path cost about
17,000 cycles when the real pass cost about 40,000. Counting fields at
several points in the loop, using the same field-sync flag vsync polls, gave
the truth and showed where the time went. Every optimization after that was
aimed at a measured number, and the last one took the pass from about 53 fps
to 60.

**Page flipping paid twice.** It removed the flicker you called unacceptable,
and it also made the remaining slow frames invisible, since a late frame now
repeats instead of tearing.

**Closing at about 80%.** The carrot goal shipped without a LEVEL COMPLETE
banner (the coco font is one bit per dot and fringes into color on this
screen, so it needs its own issue). The 60 fps issue closed at 40 fps with
the remainder filed, because you said we were ok. The art notes on the 1x1
bunny became their own issue rather than holding up the switch.

## What went wrong

**I built the art at the wrong size, twice over.** You described 1x1 sprites
at the start. I asked, explained the squash, recommended doubling, and you
took the recommendation. Later you asked to see 1x1 anyway, liked it, and we
switched. Doubling was not wasted work (it cost one converter flag), but the
physics constants, the ceiling limit and the level spacing were all tuned for
a bunny twice as tall and had to be retuned. The question I should have asked
first is not "which looks better" but "show me both", which is what you
eventually asked for.

**I fanned out agents before the look was settled.** The white background
decision arrived one minute after the workflow started. Three agents worked
in parallel on a screen whose colors were about to change. The fix took
fifteen minutes and was not hard, but the sprite agent's whole color pass was
thrown away. A one-line check on the background color before fanning out
would have avoided it.

**A kernel bug cost about fifteen minutes of timeouts.** The bunny fell
through every platform. The cause was coco's WITHIN, which loads its first
argument one cell too deep on the stack and therefore compared a flag rather
than the bunny's position. Worse, the fall did not stop: the bunny went off
the bottom of the screen and the blitter wrote past video memory into the
program, so the game hung. Headless shots hung with it, each one burning its
60 second alarm before failing. Two shot batches (12 and 16 shots) ran to the
tool timeout before I diagnosed it. A fail-fast check, stopping a batch after
the first timeout, would have caught it in one minute instead of twenty-five.

**I trusted a kernel primitive without reading it.** The same bug. I had read
WITHIN's comment ("standard Forth WITHIN") and used it. The implementation
did not match the comment. It is now filed as issue 14 here, to fix upstream.

**Assembly branch ranges.** Adding a loop around the physics step pushed a
backward branch past 127 bytes, which lwasm reports as "Byte overflow". Easy
to fix with a long branch, but the error message does not name the branch.

**Shell mistakes that stopped chains.** `grep -c` exits 1 when a count is
zero, so a "no em dashes" check gated a commit chain and stopped it. Headless
Chrome dumped the DOM and then never exited, twice, which is the same
friction the bunny session hit and which I had not carried forward into a
wrapper.

**Compiler quirks found by hitting them.** `DATA[PY` blocks are evaluated a
line at a time, so a script split across two lines failed. An explicit
UNLOOP before EXIT inside a DO loop double-pops the return stack, because the
compiler already emits one.

## Numbers

- Final game: 7,131 bytes of application code, about 6K of that the game.
- 19 sprite frames, 2,908 bytes at 1x1 rows, from 8 sheet crops, 5 ASCII art
  frames, 4 mirrored copies and 2 recolored ones.
- Frame rate while hopping, measured: 22 fps at the start, 29 after the draw
  path moved to assembly, 48 with 1x1 art, 60 after page flipping and the
  last optimizations. The final run: 165 page flips in 167 video fields, with
  both extra fields at level start.
- The blit: about 42 cycles of bookkeeping per row, down from about 95.
- 27 issues filed, 17 closed, in about 20 commits.

## Open items

- Never run on real hardware, only in XRoar.
- No tagged release or prebuilt binary.
- The coco WITHIN bug (issue 14 here) is not reported upstream yet.
- In the step animation, the landing frame reaches about 14 pixels past where
  the bunny settles, because the sprite sheet bakes forward motion into it.
  You accepted it; it could be dropped from the cycle.
- Issue 2 (headless screenshots) is still open only because you have not run
  `make shot` yourself, though it verified nearly everything here.

## What I would do differently

1. Offer to show both options when the choice is visual. One mock render of
   doubled versus 1x1, made in five minutes, would have settled the art size
   before any physics was tuned.
2. Settle the look before fanning out parallel agents, and remember that a
   running workflow cannot be redirected.
3. Make headless batches fail fast. One timeout should stop the batch, since
   a hang usually means the program is broken rather than slow.
4. Read the implementation of any primitive whose result I will build on,
   especially in a kernel young enough to have bugs in it.
5. Measure the frame rate early. The static cycle estimates were the wrong
   tool and pointed at the wrong words; the first real measurement changed
   what I optimized.
