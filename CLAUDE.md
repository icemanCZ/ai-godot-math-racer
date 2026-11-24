# Working in this repository

MathRacer is a code-only Godot 4.7.2 .NET game for children (Czech texts). `docs/ARCHITECTURE.md` explains the modules,
`docs/REFACTORING_PLAN.md` the structural changes that were made and why, `Tests/README.md` what protects saved games.

## Run the checks in parallel, with `./test.ps1`

Every check is a headless Godot process that takes from half a minute to a few minutes, so checks are **always run in
parallel** through the shared runner (16 at a time by default). Do not run them one after another in a shell loop, and do
not write a runner of your own.

```powershell
./test.ps1 -List                                  # the suites and the seeds each one runs with
./test.ps1                                        # every suite, as build.ps1 -Test does, without building
./test.ps1 -Suite ford-checks, works-checks       # a name matches every suite that starts with it ("save", "plot")
./test.ps1 -Suite track-checks -Seeds (1..40)     # one suite on many seeds, in parallel
./test.ps1 -Build -Suite save                     # build and import first, after changing C#
./build.ps1 -Test                                 # build, import and run every suite; use before a commit
./test.ps1 -Tool load-save -Seeds 123 -Argument '--save-file=PATH'   # a tool, not a suite: here one save file, never the live one
```

- Tools (`load-save-checks`, `save-fixtures`, `write-world-fingerprint`) are run the same way with `-Tool`, the seed with `-Seeds`
  and the game's own arguments with `-Argument`. Never start Godot by hand for a check, a tool or a fixture.
- After changing C# code the checks run the **built** game, so use `-Build` (or `./build.ps1 -Test`), not a bare run.
- Each case keeps its log in `artifacts/game-tests/<run>/<case>/godot.log`; a failed case prints the end of its log.
- From a git worktree the scripts find Godot in the main checkout's `.tools`; otherwise set `GODOT_PATH`.
- A check run never touches the player's save (`LaunchOptions.UsesPlayerSave`); do not start the game itself without
  `--no-save` or `--seed`, because its autosave would overwrite the real `progress.json`.
- A suite is added in one place, `Scripts/Diagnostics/DiagnosticSuites.cs` (flag, how to create it, seeds). The game
  starts it from there, `test.ps1` and `build.ps1` read the list back, and `contract-checks` fails when a suite file is
  not listed.
- Frame rate and graphics load are measured with `./benchmark.ps1` (a real window, one run after another so the runs do
  not share the GPU; `-Off plants,shadows` shows what a part costs). It is not a check and is not part of the test run.
- Seeds matter: a check that is run without a seed uses a random one, so pass seeds when a failure must be repeated.

## Saved games must never break

A save from any earlier version must load into the newest game and carry on. Fixtures in `Tests/Saves/vN` and the world
fingerprints in `Tests/Worlds` are **never edited or regenerated** to make a check pass; a new save format gets a folder
of its own and a migration, and migrations are never deleted. Old saves must load; new saves need not load in an old
game. Details are in `Tests/README.md` and `docs/REFACTORING_PLAN.md`.

## Code

- Format C# with `dotnet csharpier format Scripts` (120 columns) before committing.
- Wrap the prose of `docs/*.md` at 120 columns as well. A module or feature gets a `###` section of its own in
  `docs/ARCHITECTURE.md`, never a table cell: a table row cannot be wrapped and grows into a line of thousands of
  characters that no diff or reader can show.
- A source file goes in a folder of `Scripts` and declares that folder's namespace (`Scripts/Saves/X.cs` is in
  `MathRacer.Saves`); the table is in `docs/ARCHITECTURE.md`. Every part of a partial class stays in the folder of the class.
  `contract-checks` enforces it. Add the `using` of another folder's namespace explicitly; there is no `global using`.
- Saved state that is new is a section in `SaveData.cs` with defaults, a rule in `SaveRules.All` and its capture and restore in
  `Game.Persistence.cs`; `SaveStore` is not touched. The flat save of versions 1 to 4 is `SaveDataV4`: it and `SaveMigrations` are
  never changed to make a check pass, and `LegacySaves` (diagnostics) makes old-shaped saves for the checks of the migrations.
- A scene-free class holds state that several features share (`Economy`, `QuizSession`, `TaskSlot`, `Interaction`);
  `Game` forwards to them. New per-frame work is registered in `Game.Systems.cs`, a new interaction state registers its
  panel in `Game.Panels.cs`, a new character is a role in `ActorRoles.cs` with its ID in `ActorIds.cs`, and a new kind of
  trailer trip is an `ITripTarget` (`TripTarget.cs`).
- A new button in a panel is a record in `UiIntent.cs` and a case in `Game.HandleIntent` (`Game.Intents.cs`); `GameUi` has one
  event, `Intended`, and no panel calls the game. A fixed Czech text goes into `Texts.cs`. A distance that places something in
  the valley is named in `WorldTuning` and changing it moves objects, so the world fingerprints fail on purpose.
- `unit-checks` (about a second, no valley) reads every committed save and the question generators; run it first
  (`./test.ps1 -Suite unit`) after touching saves or questions.
