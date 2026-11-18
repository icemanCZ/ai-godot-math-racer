# Tests: save fixtures and world fingerprints

These files protect saved games. **Nothing here is edited or regenerated to make a test pass.** If a check in this
folder fails, the code is wrong, or a migration is missing.

## `Saves/vN/` – real saves of one format version

Each file is a save written by the game itself (`./run.ps1`-style launch with `--save-fixtures --seed=42`), one for
each situation a player can quit in: driving, every kind of quiz, an expedition at several points, every kind of
trailer trip empty and loaded, each panel, a bandit encounter, a course run, and a save with a lot of progress.

- `v3/` is the format of the game before the refactoring (`SaveData.CurrentVersion = 3`), `v4/` the one with the trip as one object and `v5/` the one the game writes now, with a section for each part of the game. `--save-fixture-checks` loads all three folders: a v3 or v4 file goes through the migrations into the sections of version 5 (for v4 the check also flattens the result again and compares every field with the file), a v5 file loads as it is.
- A new save format gets **its own folder** (`v4`, `v5`), made by the new code. The old folders stay exactly as they
  are, so every version of the format keeps being loaded by the newest game.
- `--save-fixture-checks --seed=42` loads every file, restores it, checks that the game captures the same save back,
  lets the game run, and carries on from where the player quit (the quiz is answered, the trailer trip driven to its
  end, the delivery or the course run finished). The names in `SaveFixtureFiles.Required` must all exist.
- `--save-fixtures --seed=42` writes the missing fixtures and **keeps the ones that exist**. To add a situation, add its
  name to `SaveFixtureFiles.Required` and a scenario to `SaveFixtures`. Make fixtures of a new format with the
  new code, in a new folder (`--fixture-folder=v4`).

The six `quiz-before-…-trip` fixtures were added after the game learnt to keep the trip that waits for an open quiz
(`SaveData.Departure`, `SaveData.DepartureState`). The saves made before that have no such field and load as before.
They are made by the new code but have the same format version 3, because the fields are optional.

`v4/` and `v5/` have the same 39 situations as `v3/`, made by the game of their time (`v4/` writes the trailer trip as one
`Trip` instead of a pair of fields for each kind, `v5/` writes sections instead of one flat object). `v3/` and `v4/` were not
touched: a save of an old format still has to load into the new game.

## `Worlds/seed-N.txt` – what a seed places in the world

`--write-world-fingerprint --seed=N` writes the file (an existing one is kept). `--world-fingerprint-checks --seed=N`
generates the world again and compares it line by line: characters, village, plots, works, source sites, pickups,
roads, terrain and trees. A saved game stores only the seed, so a change here changes the valleys of existing
games. The files were made by the code before the refactoring.

`Worlds/seed-N.glassworks.txt` is the same kind of file for what was added to the world later: the glassworks and its worker.
It is planned after everything else and its trees are cleared after generation (`Vegetation.ClearArea`), so that the older files stay as they are; the world-fingerprint check compares both files.

## Fields added to version 5 afterwards

A committed v5 file is "exactly what the game writes", so a field that was added later would break that check. Such a field has a
default and is listed in `LegacySaves.AddedAfterVersion5`; the check takes it out of what the game writes when the old file does not have
it. So far: `Hauls` (the loads hauled, which earn the pets of the sources and works), `Buildings.GlassStock` and `Village.Glazed`.
`world-pet-checks` also loads a save made without them.

## Other checks that guard saves

- `--contract-checks`: the numbers of `GameState`, the actor ID ranges and which role each ID has, and that
  `Game.ShowPanel` has a panel for every interaction state.
- `--save-checks`: round trips, migrations from versions 1 and 2, damaged files, and the copies that the game
  keeps of a save it cannot load or one from an older version (`progress.vN.bak`, `progress.nenacteno-HASH.json`).
- `--unit-checks`: about a second, no valley is generated. Every committed save of every version loads and round trips
  (a v5 file is exactly what the game writes, a v4 file loses no field), 16 damaged saves are refused, every kind of question
  has the right number of right answers and the same questions for one seed, and the actor ID ranges do not overlap.
