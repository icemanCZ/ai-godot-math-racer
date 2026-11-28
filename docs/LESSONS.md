# Lessons learnt while working on the game

What earlier work found out the hard way and what no check enforces: how the tools behave, how to prove that a change
left the valley as it was, and what was measured and tried for the frame rate. A new lesson gets a `###` section of its
own here, with the date it was found; what a check can enforce goes into a check instead (`repo-checks`,
`contract-checks`, `unit-checks`).

## Running the game and the checks

### A fresh worktree is built first

A new git worktree has no `.godot` and no built assembly, and even `./test.ps1 -List` hangs there, because it asks the
unbuilt game for its suites. The first command in a new worktree is `./test.ps1 -Build ...` or `./build.ps1`. A Godot
left over from a bare `-List` may be stopped.

### Stray Godot processes may be stopped

Nobody runs the game by hand beside an agent, so a Godot process that is left over (`Godot_v4.7.2-stable_mono_win64*`)
may be stopped with `Stop-Process`. The `.claude/hooks/guard.ps1` hook lets such commands through.

### A capture can stay alive after it saved its picture

`./run.ps1 -Capture -View NAME` writes `artifacts/NAME.png` and prints `CAPTURE: Ok`, but the Godot process sometimes
stays alive and the command hangs until its timeout (2026-11-23, 2026-11-24). What works: start the capture in its own
process, wait until the picture exists, read it, then stop the Godot processes:

```powershell
$capture = './run.ps1 -Capture -SkipBuild -View line -Seed 1'
Start-Process powershell -ArgumentList '-NoProfile', '-Command', $capture -WindowStyle Hidden
```

Never pipe or redirect the output of `run.ps1 -Capture` in Windows PowerShell 5.1 (`2>&1`, `*>`, `| Select-Object`):
Godot's harmless lines on stderr become terminating errors and the run stops before the picture is written. The status
views of the museum and the toymaker were dropped because their captures hung; the collector panels are captured with
`farm-status` and `garden-status`.

### One seed listed twice in one run shares a folder

`./test.ps1 -Suite X -Seeds 7,7` gives both cases the same label and so the same folder under `artifacts/game-tests`,
and they fail each other. Repeating a seed means running the command again, not listing the seed twice.

### Calling the scripts from an agent's shell

The PowerShell tool of Claude Code reports exit code 1 for `./test.ps1` and `./build.ps1` even when every case passed,
because the Godot import writes to stderr (2026-11-28). Read the summary line (`Hotovo za …: N OK, 0 selhalo`), or call
the script from the Bash tool as `powershell -NoProfile -ExecutionPolicy Bypass -Command "./test.ps1 -Suite a, b"`,
whose exit code is the script's. With `-File` instead of `-Command` a list such as `-Suite a, b` or `-Seeds 1, 42` is
bound as one string and the run picks the wrong suites.

### Hooks start with the next session

Claude Code reads `.claude/settings.json` when a session starts and watches only folders that had a settings file then.
The hooks and permissions written into a new `.claude` folder do nothing in the session that wrote them (2026-11-28);
test a hook by piping a sample of its JSON input into it, and expect it to run from the next session on.

## Generating the world

### Proving that a speed-up leaves the valley as it was

The world fingerprints (`Tests/Worlds`) do not cover the visual parts of the vegetation or the road field. When world
generation was made faster (2026-11-19), exactness was proven in two ways, both temporary code that was not committed:

- the old and the new computation ran side by side and any bitwise difference was logged (used for
  `Roads.SampleField`);
- an FNV hash of every vegetation batch transform was printed as `VEGHASH` and compared with a detached baseline
  worktree at `HEAD`, on the seeds 1, 42 and 7421.

Godot's debug GodotSharp is not optimised, so hot loops are rewritten in plain floats (`FlatMath`, `FlatBasis`,
`FlatRandom`), and any reordering of float operations would move trees without a word. A rewrite of Godot maths in
generation code therefore gets a bit-exact check on random inputs in `unit-checks` (as `FlatMathMatchesGodot` and
`FlatRandomMatchesGodot` do) and a before and after hash of what it generates on several seeds.

### No generated Godot resource in a static field

A generated `AudioStreamWav` cached in a `static` field of a node class, and so shared by every world the checks build,
made the headless game crash at quit on some seeds (exit codes -1073741819 and -1073741795 after `..._PASSED`), seen as
an occasional failure of `world-pet-checks-7421` (2026-11-26, the shepherd's `SheepFlock`). Keeping the stream in an
instance field fixed it, as `Game.Treasure`'s beeper does. Static materials and meshes elsewhere are fine. When a check
fails only by a crash after it printed that it passed, compare with a baseline worktree at the parent commit and suspect
a static Godot resource.

Such a crash also happens once in a while with no static resource behind it: `event-checks-42` crashed at quit with
-1073741795 after `EVENT_CHECKS_OK` in one full run of 146 cases under load (2026-11-28), then passed four times alone
and in the next full run, on a change that only moved code into new classes. Run the case again first, alone and in a
full run; look for a static resource only when the crash comes back.

## Frame rate

### What a frame costs and what was tried

Measured with `./benchmark.ps1` on a GTX 1060 and an i5-14600KF at 1080p. Resolution hardly matters; geometry and draw
calls do.

- 2026-11-20: vegetation has a far level of detail (`Vegetation.FarDistance` 130 m, on 64 m block centres: simpler
  meshes, no outline, no flowers). The stutter while driving was the streaming of grass tiles, about 0.7 ms a tile in
  Godot's `RandomNumberGenerator` interop; `FlatRandom` (bit-exact PCG32, checked in `unit-checks`) and a time slice of
  1.5 ms a frame in `Grass._Process` fixed it. `DistantDetails` gives small plain meshes a visibility range of 250 times
  their size (at least 60 m). The square went from 45 to 89 FPS, day driving from 66 to 100.
- 2026-11-23, after the valley grew six times: the game is bound by the GPU, scripts take 1.8 to 2.3 ms a frame. What
  `-Off` showed: shadows 3.3 to 3.6 ms of GPU, plants 2.6 to 4.5 ms (triangles, not draw calls), the village about 3 900
  draw calls, grass 1.2 to 2.4 ms, wildlife almost nothing. Commit `156cdab` then brought two shadow splits, no shadow
  below half a metre, a ring buffer for `WheelTracks`, sleeping far wildlife, shared outland batch meshes and
  `StaticMeshes`: day 92 → 114 FPS, square 85 → 106, night 80 → 107, borders 134 → 162.
- What did not help: low-poly shadow casters (a shadows-only proxy gave patches of self-shadow on near crowns for about
  3 %), a softer shadow filter, and merging the village's parts beyond `StaticMeshes` (about 900 draw calls fewer; the
  rest are characters, pets, the zoo, the hidden friends and the playground). Still open: outland plant blocks are never
  freed (17 000 nodes and 660 MB more on the way to the borders).
