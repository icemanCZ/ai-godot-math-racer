# Architecture and extension guide

MathRacer is a code-only Godot 4.7.2 .NET project. The root node in `Main.tscn` runs the `Game` script. C# generates the world, illustrations and interface at startup. Player-facing text is Czech.

## Module boundaries

| Module | Responsibility |
| --- | --- |
| `Game` and its feature partials | Compose the scene, handle input and state transitions, coordinate activities and restore the active panel. |
| `GameUi` and its feature partials | Build panels and HUD elements, manage menu focus and emit player intents. |
| `World`, `WorldLayout` and `Village` | Generate seeded routes, plots and interactive actors. `World` registers actors under stable save IDs and exposes them for proximity checks and placement. |
| `Terrain`, `Roads`, `RoadGrading` and `River` | Provide elevation, road surface and clearance data for meshes, collision, placement, navigation and driving. |
| `Vehicle`, `FollowCamera` and `VehicleAppearance` | Handle movement, camera and cosmetic state. `VehicleModel` shares geometry between gameplay and garage previews. |
| `Expedition` and `DrivingEvents` | Handle cargo, route guidance and vehicle courses. |
| `IChallenge` and generators in `Mathematics` and `Letters` | Provide question data, seeded generation and answer validation. |
| `SaveData`, `SaveStore` and `Game.Persistence.cs` | Define the JSON data, validate and migrate it, and translate between data and live scene state. |
| `Scripts/Diagnostics` | Launch headless checks, prepare screenshot views and exercise game entry points. |

## Start and game state

`Game._Ready` generates the world, creates the vehicle and camera, initializes the world systems and binds UI events. The diagnostic partial handles command-line modes. Road audit builds the terrain, roads and village. Road-driving checks build the world and vehicle. The other suites and capture views use the initialized game.

`Game` owns the current state and activates the vehicle during exploration. The UI sends intents such as submitting an answer or choosing an expedition. Pause records the previous state. Resume restores its panel, question and selections. Loading a save reuses the same panel restoration.

## Questions and activities

- Add question data and generation alongside the existing quiz types. `Game.Quizzes.cs` selects an enabled type, rotates through language activities and opens the question. `Game.SubmitAnswer` handles answers and forwards successful interactions to the appropriate activity. `GameUi` renders the choices. Keep a new quiz's pending question and partial selections restorable from disk.
- Roadside friends have one opening quiz per world. Solving it unlocks a delivery or apple collection whose choice can be postponed. Cargo handover opens a counting task or, with arithmetic disabled, an enabled language task. Completing that task grants cosmetic choices.
- Village houses draw from seeded schedules of ten tasks. Shops, collectors and the sorcerer have their own departure and handover rules. Their active trips use the same `Expedition` slot as roadside trips. Keep checks for opening an activity, completing it and restoring it aligned when adding a new flow.
- Expedition routes follow a graph of existing road samples. Guidance arrows use fixed anchors supplied by `Roads`. Changing position, pickup or destination updates the visible arrows while retaining their road placement.

## Seeded worlds and saves

- `WorldLayout` owns the seeded route and plot plan. `World` generates the village once to discover the settlement footprint, levels that area, then rebuilds the roads and village before spawning roadside animals. Use the shared terrain and road measurements when placing new scenery.
- Register persistent actors in `World.RegisterActors`. The roadside animals use their array indices. Resident IDs start at 30 because older saves reserved 0–29 for roadside animals. The bandit representative has a separate ID and is restored with its encounter in `Game`.
- `SaveData` contains data only. `Game.Persistence.cs` snapshots the current state, recreates active expeditions and restores cosmetics, progress and panels. When adding persisted fields, supply defaults for older saves and validate the new state in `SaveStore`. Existing numeric `GameState` values and actor IDs are part of the JSON format.
- `SaveStore` validates each candidate before migration, uses the previous backup if the main file is damaged and writes updates through a temporary file. Diagnostic modes, captures and explicit seeds bypass the player save.

## Verifying changes

Run `./build.ps1 -Test` for the headless suite. Add a new diagnostic to `Scripts/Diagnostics`, wire its command-line flag in `Game.Diagnostics.cs`, and include it in `build.ps1` if it belongs in the suite. Use `DiagnosticArtifacts.PathFor` for diagnostic files so each parallel run gets its own directory. For persistence changes, exercise a round trip and relevant older save data. For visible changes, run `./run.ps1 -Capture -View <view>` and inspect the image in `artifacts`.
