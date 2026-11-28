# Working in this repository

MathRacer is a code-only Godot 4.7.2 .NET game for children (Czech texts). `docs/ARCHITECTURE.md` explains the modules,
`docs/REFACTORING_PLAN.md` the structural changes that were made and why, `Tests/README.md` what protects saved games,
and `docs/LESSONS.md` what earlier work found out the hard way (captures that hang, proving the valley unchanged,
crashes at quit, what the frame costs). Read the lesson before repeating the work it is about. A new feature beyond the
old valley (a character, its trailer trips, its panel, save, checks and fingerprint) follows the skill
`.claude/skills/outland-feature/SKILL.md`, which lists every place such a feature plugs in.

## Run the checks in parallel, with `./test.ps1`

Every check is a headless Godot process that takes from half a minute to a few minutes, so checks are **always run in
parallel** through the shared runner (16 at a time by default). Do not run them one after another in a shell loop, and
do not write a runner of your own.

```powershell
./test.ps1 -List                                  # the suites and the seeds each one runs with
./test.ps1                                        # every suite, as build.ps1 -Test does, without building
./test.ps1 -Suite ford-checks, works-checks       # a name matches every suite that starts with it ("save", "plot")
./test.ps1 -Suite track-checks -Seeds (1..40)     # one suite on many seeds, in parallel
./test.ps1 -Build -Suite save                     # build and import first, after changing C#
./build.ps1 -Test                                 # build, import and run every suite; use before a commit
# A tool, not a suite: here it loads one save file, never the live one.
./test.ps1 -Tool load-save -Seeds 123 -Argument '--save-file=PATH'
```

- Tools (`load-save-checks`, `save-fixtures`, `write-world-fingerprint`) are run the same way with `-Tool`, the seed
  with `-Seeds` and the game's own arguments with `-Argument`. Never start Godot by hand for a check, a tool or a
  fixture.
- After changing C# code the checks run the **built** game, so use `-Build` (or `./build.ps1 -Test`), not a bare run. In
  a new worktree even `-List` needs a build first.
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

## The rules are checked, not only written

A rule that can be checked by a program is checked, and the failure says how to put it right; a new rule of this file
gets a check where it can. What holds the rules today:

- `repo-checks` (a few seconds, no valley; `./test.ps1 -Suite repo`) fails when the C# code is not formatted as
  `dotnet csharpier check Scripts` wants, when a C# file has more than 1000 lines, when a line of any Markdown file is
  longer than 120 characters, when a plan `docs/*_PLAN.md` has no `**Stav:**` line or is missing from `docs/PLANS.md`,
  and when a commit, the index or the working tree changes, deletes or renames a file of `Tests/Saves` or
  `Tests/Worlds`.
- `contract-checks` holds the structure: folders and namespaces, panels of the interaction states, actor IDs, the list
  of suites. `unit-checks` and the fingerprint and fixture suites hold saves, questions and the generated valley.
- `.claude/settings.json` runs two hooks for Claude Code. `.claude/hooks/guard.ps1` refuses an edit of a file in
  `Tests/Saves` or `Tests/Worlds`, a Godot started by hand without `--no-save`, and `run.ps1`, `run.cmd`, `run.bat` or
  `god-mode.bat` without `-Capture`. `.claude/hooks/format.ps1` formats each C# file after it was written. The same file
  allows `./test.ps1`, `./build.ps1`, `dotnet build` and `dotnet csharpier` without asking.

## Saved games must never break

A save from any earlier version must load into the newest game and carry on. Fixtures in `Tests/Saves/vN` and the world
fingerprints in `Tests/Worlds` are **never edited or regenerated** to make a check pass; a new save format gets a folder
of its own and a migration, and migrations are never deleted. Old saves must load; new saves need not load in an old
game. Details are in `Tests/README.md` and `docs/REFACTORING_PLAN.md`.

## Code

- Format C# with `dotnet csharpier format Scripts` (120 columns) before committing.
- Every plan in `docs/*_PLAN.md` has a `**Stav:**` line under its title and a line in `docs/PLANS.md`, which also
  keeps the open debts in one place.
- Wrap every Markdown file at 120 columns as well, code blocks included. A module or feature gets a `###` section of its
  own in `docs/ARCHITECTURE.md`, never a table cell: a table row cannot be wrapped and grows into a line of thousands of
  characters that no diff or reader can show. A table whose rows do not fit becomes a list.
- A source file goes in a folder of `Scripts` and declares that folder's namespace (`Scripts/Saves/X.cs` is in
  `MathRacer.Saves`); the list of folders is in `docs/ARCHITECTURE.md`. Every part of a partial class stays in the
  folder of the class. `contract-checks` enforces it. Add the `using` of another folder's namespace explicitly; there is
  no `global using`.
- A file that grows past 1000 lines gives a part with a responsibility of its own to a smaller class (rules, a builder,
  texts, the staging of a view) and calls it, as `TripRules`, `ZooPens` or `OutlandTexts` do. A smaller class is
  recommended over a new partial file, which keeps the class as big and as tangled and only spreads it over more
  files; a partial (`Game.X.cs` for a feature's part of the game) is fine where the part needs the class's own state.
- Saved state that is new is a section in `SaveData.cs` with defaults, a rule listed in `SaveRules.All` (the rules of
  what lies beyond the old valley are in `OutlandRules`, those of the trailer trip in `TripRules`) and its capture and
  restore in `Game.Persistence.cs`; `SaveStore` is not touched. The flat save of versions 1 to 4 is `SaveDataV4`: it
  and `SaveMigrations` are never changed to make a check pass, and `LegacySaves` (diagnostics) makes old-shaped saves
  for the checks of the migrations.
- A scene-free class holds state that several features share (`Economy`, `QuizSession`, `TaskSlot`, `Interaction`);
  `Game` forwards to them. New per-frame work is registered in `Game.Systems.cs`, a new interaction state registers its
  panel in `Game.Panels.cs`, a new character is a role in `ActorRoles.cs` with its ID in `ActorIds.cs`, and a new kind
  of trailer trip is an `ITripTarget` (`TripTarget.cs`).
- A new button in a panel is a record in `UiIntent.cs` and a case in `Game.HandleIntent` (`Game.Intents.cs`); `GameUi`
  has one event, `Intended`, and no panel calls the game. A fixed Czech text goes into `Texts.cs`, or into
  `OutlandTexts.cs` when it belongs to what lies beyond the old valley. A distance that places something in the valley
  is named in `WorldTuning` and changing it moves objects, so the world fingerprints fail on purpose.
- A generated Godot resource (a sound, a texture) is kept per instance, never in a `static` field: a shared one crashed
  the game at quit (`docs/LESSONS.md`).
- `unit-checks` (about a second, no valley) reads every committed save and the question generators; run it first
  (`./test.ps1 -Suite unit`) after touching saves or questions.
