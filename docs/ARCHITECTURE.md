# Architecture and extension guide

MathRacer is a code-only Godot 4.7.2 .NET project. `Main.tscn` contains only the composition root. All geometry, materials, controls and lighting are generated in C#. No editor steps, external assets or paid dependencies are required.

## Responsibilities

| Module | Owns | Extension point |
| --- | --- | --- |
| `Game` | Composition, input actions, application state, proximity interaction | Route a completed conversation into a challenge state |
| `Settings` | Seed, world size, driving and interaction parameters | Add exported configuration resource when multiple levels exist |
| `World` | World composition, animal placement and landmark decoration | Additional landmark generators |
| `Village` | Road-relative house plots, entrances, footpaths and front fences | More house types and reusable street layouts |
| `River` | Shared channel profile, terrain carving, water shader and driveable crossing | More crossings, tributaries and water effects |
| `Shaders/RiverWater.gdshader` | Depth color, moving wave normals, glints and shoreline foam | Water palette and flow presets |
| `Atmosphere` | Blue sky gradient, daylight, haze and shared white cloud meshes | Weather and time-of-day presets |
| `Terrain` | Authoritative height function, visible mesh and terrain collision | Biomes and road flattening |
| `Roads` | Curves, road mesh, sampled route positions | Additional routes and road proximity acceleration |
| `Vegetation` | Seeded placement, trees, instanced flowers and crowns | Additional species and local density masks |
| `Art` | Shared materials, primitive mesh helpers | Shared generated mesh cache |
| `Obstacles` | Solid trunk capsules, convex rock footprints and boxes for scenery | Additional solid landmarks |
| `Vehicle` | Movement, collision, reset, procedural car | Car appearance builder and cosmetic configuration |
| `FollowCamera` | Independent smoothed position and aim | Obstacle spring arm |
| `Animal` | Character identity, greeting, procedural model, idle animation | Stable animal ID and dialogue data resource |
| `GameUi` | Czech presentation and UI intent events | Challenge panel, accessible settings |
| `MathChallenge` / `ChallengeGenerator` | Immutable question data, seeded arithmetic and three valid choices | Difficulty ranges and more question types |
| `CarBody` / `CarLook` / `CarAccessory` / `VehicleAppearance` | Four models, eight paints, fourteen accessories and registered visual parts | More cosmetic presets |
| `CarLookPreview` | Actual car geometry rendered once in isolated studio SubViewports | Additional reward camera angles |

`Game` coordinates modules; UI emits actions and does not manipulate the car. Terrain height is shared by placement, collision generation, slope presentation and camera clearance. Runtime state is explicit: Title, Exploring, Challenge, Reward, Paused. Pausing disables movement; ambient animal animation remains alive deliberately.

## Mathematics and interaction flow

`MathChallenge` stores operands, operation and three read-only choices; the answer is derived from the operands. `ChallengeGenerator` is independent of Godot and uses a dedicated seeded random stream. `MathChallenge` and `LetterChallenge` implement `IChallenge`, sharing answer validation, rejected choices, pause restoration and the reward flow. `Game` owns the current task and rejected answer set. Even-index animals offer arithmetic; odd-index animals offer letters, giving 15 of each type. Pressing E near an animal opens Challenge directly. `GameUi` emits intents to submit or leave. Incorrect answers keep the same question, disable only the rejected choice and show encouragement. Correct answers enter Reward; state guards prevent choosing a cosmetic before solving. Pausing restores the exact question, feedback or reward view. There are no timers or loss states.

Each animal has one visit per run. Opening Challenge marks it unavailable and hides its arrow; leaving without solving also consumes the visit. No action re-enables a visited animal. The reward panel allows one cosmetic selection or keeping the current appearance. GameUi displays the remaining available animals in a top-right HUD, updated immediately when a visit starts, with a completion message at zero. Exploration remains available after all visits.

`LetterChallengeGenerator` draws from a seeded deck of 32 Czech picture words. Each picture appears as a correct target before the deck repeats; several words may share an initial. Two distinct distractors must start with different letters from the target, and the three pictures are shuffled. `WordPicture` draws original vector illustrations using Godot primitives; it depends on neither font emoji nor downloaded assets. Picture buttons omit written names and dim after a wrong answer. Capture the task with `./run.ps1 -Capture -View letters` or the two illustration gallery pages with `-View letter-pictures` and `-View letter-pictures-2` (16 illustrations each).

When adding difficulty, keep generation separate from UI presentation in `GameUi`. If challenge sessions gain multi-question progression, extract their state from `Game` into a dedicated session object then.

## Appearance and future persistence

Each correct answer draws one model, one paint and one accessory and rejects the previous unordered set. `CarBody`, `CarLook` and `CarAccessory` supply stable IDs. `VehicleModel.BuildDisplay` shares geometry construction between gameplay and reward previews. Coachwork swaps preserve the four wheel nodes; paint, optional twin stripes and accessories are independent. `VehicleAppearance` applies cached material references without mutating shared materials or changing physics. `CarLookPreview` lazily builds an isolated 3D studio and renders its viewport once, using a rear camera for rear accessories. The player can keep the current appearance instead. Selections survive resets during the current run.

Persistence and permanent unlocks are not implemented. When needed, add versioned `PlayerProgress` JSON in `user://`, validate cosmetic IDs on load, and migrate older schemas. Separate reward grants from answer validation if progression is introduced.

## Boundaries and constraints

World generation runs once on the main thread because Godot nodes and rendering resources require it. Repeated decorative spheres are batched by material. Trunks, rocks, fences, benches and signposts have simple static collision shapes alongside buildings, terrain and bridge rails. Soft foliage and animals remain passable. Driving uses a CharacterBody3D with rounded ground contact and a box matching the chassis. Wall impacts reduce arcade speed according to the approach angle while MoveAndSlide preserves tangential motion. All runtime text is Czech. Source identifiers and documentation use English.

Before adding functionality, run `./build.ps1 -Test`. For visible changes also run `./run.ps1 -Capture` and inspect the resulting PNG. Export separately requires matching Godot .NET export templates.




