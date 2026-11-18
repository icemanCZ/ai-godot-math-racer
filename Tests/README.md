# Tests: save fixtures and world fingerprints

These files protect saved games. **Nothing here is edited or regenerated to make a test pass.** If a check in this
folder fails, the code is wrong, or a migration is missing.

## `Saves/vN/` – real saves of one format version

Each file is a save written by the game itself (`./run.ps1`-style launch with `--save-fixtures --seed=42`), one for
each situation a player can quit in: driving, every kind of quiz, an expedition at several points, every kind of
trailer trip empty and loaded, each panel, a bandit encounter, a course run, and a save with a lot of progress.

- `v3/` is the format of the game before the refactoring (`SaveData.CurrentVersion = 3`).
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

## `Worlds/seed-N.txt` – what a seed places in the world

`--write-world-fingerprint --seed=N` writes the file (an existing one is kept). `--world-fingerprint-checks --seed=N`
generates the world again and compares it line by line: characters, village, plots, works, source sites, pickups,
roads, terrain and trees. A saved game stores only the seed, so a change here changes the valleys of existing
games. The files were made by the code before the refactoring.

## Other checks that guard saves

- `--contract-checks`: the numbers of `GameState`, the actor ID ranges and which role each ID has, and that
  `Game.ShowPanel` has a panel for every interaction state.
- `--save-checks`: round trips, migrations from versions 1 and 2, damaged files, and the copies that the game
  keeps of a save it cannot load or one from an older version (`progress.vN.bak`, `progress.nenacteno-HASH.json`).
