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

`MathChallenge` stores operands, operation and three read-only choices; the answer is derived from the operands. `ChallengeGenerator` is independent of Godot and uses a dedicated seeded random stream. `MathChallenge` and `LetterChallenge` implement `IChallenge`, sharing answer validation, rejected choices, pause restoration and the reward flow. `Game` owns the current task and rejected answer set. Even-index animals offer arithmetic; odd-index animals offer letters, giving 15 of each type. Pressing E near an animal opens Challenge directly. `GameUi` emits intents to submit or leave. Incorrect answers keep the same question, disable only the rejected choice and show encouragement. Correct original answers unlock ExpeditionChoice, offering delivery or collection at every animal. Completing the selected expedition and its counting question enters Reward; state guards prevent choosing cosmetics earlier. Pausing restores the exact question, feedback, task choice, offer or reward view. There are no timers or loss states.

Each animal has one visit per run. Opening Challenge consumes the original-question visit and hides its question mark; leaving without solving also consumes the visit. Solving unlocks a separate expedition choice that remains accessible until the expedition is completed, without reopening the original question. The reward panel allows one cosmetic selection or keeping the current appearance. GameUi displays the remaining available animals in a top-right HUD, updated immediately when a visit starts, with a completion message at zero. Exploration remains available after all visits.

`LetterChallengeGenerator` draws from a seeded deck of 32 Czech picture words. Each picture appears as a correct target before the deck repeats; several words may share an initial. Two distinct distractors must start with different letters from the target, and the three pictures are shuffled. `WordPicture` draws original vector illustrations using Godot primitives; it depends on neither font emoji nor downloaded assets. Picture buttons omit written names and dim after a wrong answer. Capture the task with `./run.ps1 -Capture -View letters` or the two illustration gallery pages with `-View letter-pictures` and `-View letter-pictures-2` (16 illustrations each).

When adding difficulty, keep generation separate from UI presentation in `GameUi`. If challenge sessions gain multi-question progression, extract their state from `Game` into a dedicated session object then.

## Appearance and future persistence

Each completed expedition draws one model, one paint and two distinct accessories and rejects the previous unordered set. Reward cards use category symbol buttons instead of written names or actions. `CarBody`, `CarLook` and `CarAccessory` supply stable IDs. `VehicleModel.BuildDisplay` shares geometry construction between gameplay and reward previews. Coachwork swaps preserve the four wheel nodes; paint, optional twin stripes and accessories are independent. `VehicleAppearance` applies cached material references without mutating shared materials or changing physics. `CarLookPreview` lazily builds an isolated 3D studio and renders its viewport once, using a rear camera for rear accessories. The player can keep the current appearance instead. Selections survive resets during the current run.

Persistence and permanent unlocks are not implemented. When needed, add versioned `PlayerProgress` JSON in `user://`, validate cosmetic IDs on load, and migrate older schemas. Separate reward grants from answer validation if progression is introduced.

## Boundaries and constraints

World generation runs once on the main thread because Godot nodes and rendering resources require it. Repeated decorative spheres are batched by material. Trunks, rocks, fences, benches and signposts have simple static collision shapes alongside buildings, terrain and bridge rails. Soft foliage and animals remain passable. Driving uses a CharacterBody3D with rounded ground contact and a box matching the chassis. Wall impacts reduce arcade speed according to the approach angle while MoveAndSlide preserves tangential motion. All runtime text is Czech. Source identifiers and documentation use English.

Before adding functionality, run `./build.ps1 -Test`. For visible changes also run `./run.ps1 -Capture` and inspect the resulting PNG. Export separately requires matching Godot .NET export templates.





## Expeditions and reward flow

Original arithmetic and letter-picture questions remain the primary animal interaction. A correct answer adds the sender to `unlockedExpeditions` and opens `ExpeditionChoice`. The player chooses either delivery or collection; `SelectExpedition` creates and starts it immediately, with no confirmation state or dialog. Leaving the choice via Později preserves it for another visit. The collection preview sets CargoAmount to zero while retaining the platform. Solving another original question during an active expedition unlocks its choice for later without interrupting cargo or awarding cosmetics. `completedDeliveries` prevents repeating finished expeditions; `deliveryReward` identifies rewards whose closure releases the active expedition.

`Expedition` owns either a delivery to another animal or a three-apple collection returning to the sender, selected explicitly through its constructor. It builds a connected graph of existing road samples and recomputes shortest paths when the next pickup or return target changes. Collection apples spawn at separate road centers near existing animal clearings. Pickups run only while exploring and are tracked individually, preventing duplicate loading. Cargo is parented to the vehicle and removed when the expedition closes. `CanDeliver` gates early hand-in. Arrival opens counting; a correct count grants the four cosmetic choices. `DeliveryPicture` draws cargo, navigation and category/action symbols. `DeliveryChecks` covers both expedition types, postponed choices, reward gating and fixed arrow anchors; controller checks exercise the real input and GUI path.

Navigation uses Czech text with a compact panel at 20 px from the top. Road arrows share a flat green mesh and material. Road generation creates NavigationAnchors every six road samples (8.4 m) and maps nearby samples to those fixed anchors. Instances are cached by road anchor and travel direction; transforms are set only when an instance is created. Shortest-path phase changes and collection target changes never determine arrow positions. Moving the car only changes visibility. Available original questions and postponed expedition choices use yellow question marks. Active destinations and collectible apples use turquoise exclamation marks. Art.TaskMarker applies the same larger fixed screen size and 16-pixel dark outline to both marker types.