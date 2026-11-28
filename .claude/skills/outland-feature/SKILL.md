---
name: outland-feature
description: >-
  Add a new feature of the valley beyond the old one to MathRacer: a character who wants something built, trailer trips
  that build it, a panel, its save section, its checks, its world fingerprint and its capture views. Use when the user
  asks for a new place, character, building or activity like the balloon, the sheep, the ferry, the castle or the great
  railway, from the plan to the last check.
---

# A new feature beyond the old valley

The post, the dragon, the balloon, the tracks, the sheep, the rally, the map, the shells, the ferry, the castle and the
great railway were all made the same way; the great railway (`6674845`) touched 66 files. This is that way as a list.
Work through it in order and tick each step off on the task list; a step that does not apply is said to be skipped, not
silently left out. `CLAUDE.md` holds the rules; this skill says where each one lands for a feature.

## 1. The plan first, and the user decides

- Write `docs/<NAME>_PLAN.md` in Czech, wrapped at 120 columns, with `**Stav:** návrh` under the title, and add its line
  to `docs/PLANS.md`. Read one finished plan first (`docs/CASTLE_PLAN.md`, `docs/MAINLINE_PLAN.md`) and follow its
  parts: what the child does, the character (a Czech animal name and title), where it stands, the loads and trips, the
  panel, the questions, the reward and the pet, what is saved, what is checked, what it costs the frame.
- Show the plan to the user and build nothing until they agree. Then set `**Stav:** rozpracováno`.

## 2. Classes of its own first

The behaviour of the feature goes into classes of its own, in the folder of what they are: the plans and numbers
(`XPlans` in `Buildings`), the site and its placement (`XSites` in `Valley`), the building or scene node (`Buildings`),
a scene-free class for the run or the log of the activity (`TrackingHunt`, `SheepHerd`, `ExplorerLog`, `RallyRun`) and
the panel's pictures (`Ui`). Earlier features also added `Game.X.cs`, `GameUi.X.cs`, `World.X.cs` and `Character.X.cs`
partials for the part that needs the private state of those classes; that is fine, and it is recommended to keep them
thin, so that they call into the new classes instead of holding the feature. No file may grow past 1000 lines
(`repo-checks`).

## 3. Where a feature plugs in

Each line is a place an earlier feature had to touch. Grep for the last feature's name (`MainLine`, `Engineer`,
`Castle`) to see the shape of each change.

- **The world.** `World.cs` plans the site after everything planned before it, with random numbers of its own, so no
  older place moves (`PlanMainLine`), and registers the character last (`RegisterEngineer`). Zones that others must keep
  clear of are added to `World.Borderfolk.cs` (`InMainLineZone`). A distance that places something is named in
  `WorldTuning`.
- **The character.** A role in `ActorRoles.cs` (`EngineerRole`: icon, whether it invites talk), its ID in `ActorIds.cs`
  as the next number with `Last` moved on (IDs are part of the save format and never change), its entry in
  `CharacterCatalog.cs`, and its model. Leave the role out of the postcard stops (`Game.Postcards.cs`) and the driving
  courses (`DrivingEvents.cs`) unless the plan wants it there; `Game.Post.cs` guides it when it waits.
- **The interaction state.** The next number of `GameState` in `Game.cs` (numbers are saved and never change), its
  panel registered in `Game.Panels.cs`, and if the character is busy elsewhere, the check that keeps him from being
  talked to in `Game.cs`.
- **Buttons.** Each button is a record in `UiIntent.cs` and a case in `Game.HandleIntent` (`Game.Intents.cs`); the panel
  only raises `GameUi.Intended`.
- **Texts.** Every fixed Czech text goes into `OutlandTexts.cs`; texts with values inside are methods.
- **Questions.** A new moment that asks a question is a value of `Departure` in `Game.Quizzes.cs`, such as `MainLine`
  and `MainLineRide` (its numbers are saved and never change).
- **Trailer trips.** A kind in `TripKinds` (`TripTarget.cs`) with its target as an `ITripTarget`, the accessor in
  `HaulTrip.cs`, the departure and the loading texts in `Game.Ford.cs`, `Game.Trips.cs` and `Game.Works.cs`, and a new
  load, where the trailer carries something new, is the next number of `HaulLoad` (`BuildPlans.cs`, saved, never
  renumbered) with its pile on the trailer in `Trailer.cs`.
- **Per frame.** One `Register("x", TickX)` in `Game.Systems.cs`.
- **The reward.** Coins from the plans, and a pet as a new value of `CollectorPetKind` (`CollectorPet.cs`) with its
  entry in `WorldPetCatalog.cs`; `ContentChecks` counts the pets and needs the new place.

## 4. Saves must keep loading

- A section in `SaveData.cs` with defaults (`SaveData.X`), its rule `HasInvalidX` in `OutlandRules.cs` listed in
  `SaveRules.All`, and its capture and restore in `Game.Persistence.cs`. `SaveStore` is not touched.
- Each new field of version 5 is listed in `LegacySaves.AddedAfterVersion5`, so the committed v5 fixtures still match.
- A save made before the feature must load and play on: the section is absent and its defaults mean "not started".
- Nothing in `Tests/Saves` is edited or regenerated. A new save format would need a folder `Tests/Saves/vN` of its
  own and a migration; a new section does not.
- Run `./test.ps1 -Build -Suite unit, save` early, once the section exists.

## 5. Checks and the world fingerprint

- A suite `XChecks` (one class in `Scripts/Diagnostics`) listed once in `DiagnosticSuites.cs`:
  `new("x-checks", (g, w, v) => new XChecks(g, w, v), Seeds: ThreeSeeds)`. It walks the feature through the game's own
  entry points (talk, intents, trips, the questions, saving and loading in the middle, the reward) and checks each step
  with a message that says what should have happened. Read `MainLineChecks.cs` or `CastleChecks.cs` first.
- A fingerprint of its own in `WorldFingerprint` (`WorldContractChecks.cs`): `XFileFor(seed)` naming
  `Tests/Worlds/seed-N.x.txt` and `XLines(world)`, written by the tool and compared by `world-fingerprint-checks`, which
  must also fail when the file is missing. Write the three new files with the tool only:
  `./test.ps1 -Tool write-world-fingerprint -Seeds 1, 42, 7421`. It keeps every existing file; check with `git status`
  that only the new `seed-N.x.txt` files appeared.
- No older fingerprint may change. If `world-fingerprint-checks` fails on an old file, the feature moved something
  that was there before: change the feature, never the file (`docs/LESSONS.md`, proving the valley unchanged).

## 6. Pictures

- Stage each view in `Game.PrepareCapture` (`Game.Diagnostics.cs`) and frame it in `OutlandFraming.cs`; add the view
  names to the `ValidateSet` of `run.ps1`. Usual views: the stages of the building (`x-0`, `x-4`, …), the finished
  place, close, at night, the panel and the ride.
- Take the pictures with `run.ps1 -Capture -View x-…` as `docs/LESSONS.md` says (never pipe its output; a capture may
  stay alive after it saved the PNG), and look at them before calling the feature done.

## 7. Finish

- `docs/ARCHITECTURE.md` gets a `###` section of its own for the feature (never a table row); `README.md` and
  `docs/PRODUCTION_CHAINS.md` change where the feature adds a load or a chain.
- The plan gets `**Stav:** hotovo (<commit>, <date>)` once committed and a part "Jak to dopadlo" on how the game
  differs from the plan; what stayed open goes to "Otevřené dluhy" in `docs/PLANS.md`.
- `dotnet csharpier format Scripts`, then `./build.ps1 -Test`: every suite, the new one on its three seeds and
  `repo-checks` included. Commit only when the user asks.
