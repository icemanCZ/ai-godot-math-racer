# Validation record

## Building foundations and twelve-item collections — 30 October 2026

All nine village buildings now sit above the highest terrain sample across their full foundation footprint. Solid plinths extend below the lowest sample; raised approach paths have matching mesh collision. Residents and the garage entrance use the actual walkway triangle height so sloped approaches cannot bury them. Farm placement checks the entire building and enclosure against all road branches; walkways stop at the first road edge.

Farm, garden, museum and toy workshop now have twelve trips each. Added six named animals, six flower varieties, six gems and six distinct toy models. Three-by-four display layouts fit within the existing reserved plots; pickup offsets, UI counts, completion checks, full previews and save validation follow catalogue lengths. Existing six-item progress continues with six remaining trips.

Build passed without warnings/errors. Village checks passed on seeds 1, 42 and 7421, including terrain clearance for every foundation and physics raycasts beneath all eight residents. Seed-42 collection, garden and farm checks complete all twelve trips and round-trip progress through disk saves. Garage and save checks passed. Collision checks passed on seed 1. The previously obstructed branch (seed 1, route 4) passes the focused driving check in both directions; the diagnostic now accepts --road-driving-route=N for reproductions. Inspected full farm, garden, museum and toy workshop previews.

## Gem collector and toymaker — 28 October 2026

Added Jáchym's gem museum with six display pedestals and Pepa's toy workshop with six playground displays. Each resident offers six independent math/letter-gated collection trips. Faceted gems and six different toy models appear at off-road pickup clearings, on the car after collection and permanently at the destination after return. All four specialist residents share the active-expedition limit. Pending quizzes, cargo and both new collection counts persist; older saves default to empty new collections.

Build passed without warnings/errors. Collection checks passed on seeds 1, 42 and 7421 for all twelve trips, incorrect/abandoned quizzes, pause, exact disk save/restore at each stage, cross-resident blocking, off-road placement, road pass-bys, duplicate pickups/handovers, reset, complete collections and older saves. Seed-42 farm, garden, village and save checks passed. New building and display footprints are checked against road surfaces. Rendered museum and toy workshop previews are saved under `artifacts/`.

## Gardener and off-road expedition targets — 28 October 2026

Added gardener Vítek, a separate garden building and six flower beds. Each trip starts after a math/letter quiz; a marked flower is collected in the meadow, shown on the car and planted on return. Garden progress and pending trips persist, while older saves default to zero planted beds. Farmer and gardener trips share the one-active-expedition rule.

Both animal and flower pickup targets now use deterministic clearings beyond every road surface, at least 7.5 m from road centerlines, grounded on terrain and clear of trunks. Special pickups require approaching within 2.8 m. Garden placement validates its whole footprint against all road branches, existing plots and nearby benches; planted beds are checked against road surfaces.

Build passed without warnings/errors. Garden checks passed on seeds 1, 42 and 7421, covering all six trips, wrong/abandoned quizzes, blocked trips, passing targets on the road, empty returns, duplicate collection/handover, cargo, saves and completed gardens. Seed-42 farm, village and save checks passed. Visually inspected `artifacts/garden-full.png` and `artifacts/garden-expedition.png` for the garden, planted beds and off-road pickup.

## Farm expeditions — 28 October 2026

Added a separate red farm building, farmer Tonda and fenced enclosure along the village road. Six named animals are unlocked through alternating math and letter challenges before departure, caught by approaching their marked roadside spot, visibly transported on the car, and transferred to the enclosure on return. Existing expeditions block another departure. Farm residents, pending quizzes and caught animals persist; old saves default to an empty farm.

Build passed with zero warnings/errors. Farm checks cover all six departures, wrong answers, postponing quizzes, blocked departures, empty returns, duplicate pickups/handovers, resetting the car, disk save/restore at each stage and completed-farm interaction. Farm checks passed on seeds 1, 42 and 7421; seed-42 village, save and delivery regression checks also passed. Rendered and inspected `artifacts/farm-full.png` for the barn, farmer, enclosure and six animals, and `artifacts/farm-question.png` for the pre-departure quiz.

## Xbox controller — 26 October 2026

- Added analog left-stick steering, RT/LT throttle/reverse with deadzones, X handbrake, Y reset, right-stick camera orbit, A interaction/confirmation, B back and Menu pause. All menus support D-pad and left-stick navigation with a visible focus border, and control hints follow the input device.
- `--controller-checks` injects joypad button/axis events through Godot's normal input and GUI routing. `CONTROLLER_CHECKS_PASSED` verifies title activation, deadzones, partial trigger strength, driving, steering, braking, reverse, reset, camera orbit, pause/resume, safe initial pause focus, opening a task without accidentally answering it on A release, answer navigation, answer confirmation and reward selection. Output: `artifacts/controller-input-tests.log`.
- Physical Xbox USB/Bluetooth hardware has not been tested. The checks validate game behavior with simulated controller events.
- Full `build.ps1 -Test` also passed (`CONTROLLER_CHECKS_PASSED`, `COLLISION_CHECKS_PASSED`, `WILDLIFE_CHECKS_PASSED`, `SMOKE_TEST_PASSED`, `WHEEL_TRACK_CHECKS_PASSED`); output: `artifacts/controller-tests.log`. Compilation had no errors; NuGet reported unavailable vulnerability metadata (NU1900), and editor import reported the existing settings safe-save restriction.

## Solid scenery collisions — 25 October 2026

- Added static capsules for all 3,858 tree trunks, fence posts and signposts, convex rock footprints in the meadow and along the river, and boxes matching benches and individual fence rails. Openings in the village fences are retained. Houses and bridge rails keep their existing colliders; soft foliage and animals remain passable.
- The car now has a chassis box alongside its rounded ground contact. Wall impacts reduce arcade speed according to the approach angle, with tangential sliding handled by Godot. Low rocks have upright collision sides so they stop the car instead of acting as ramps.
- Added `--collision-checks` to `build.ps1 -Test`: six fixed-tick scenarios exercise forward and reverse impacts, speed loss and reversing away, using generated trunk, rock and bench shapes plus fence, wall and bridge rail dimensions. The existing handbrake tests now run on an isolated flat platform because the formerly decorative signpost ahead of spawn blocks that trajectory.
- Final `build.ps1 -Test` passed with zero C# warnings/errors and `COLLISION_CHECKS_PASSED`, `WILDLIFE_CHECKS_PASSED`, `SMOKE_TEST_PASSED` and `WHEEL_TRACK_CHECKS_PASSED`. Output: `artifacts/collision-tests.log`. Existing sandbox restrictions on global Godot settings/log writes remain; all checks completed successfully. Manual collision feel and sustained frame rate have not been measured.

## Wild fauna — 25 October 2026

- Added 240 seeded butterflies, birds, rabbits, foxes and deer, independently of the 30 challenge friends. Wildlife wanders, flees from nearby or predicted approaching vehicle positions, follows the terrain and checks trunks, river banks, buildings, boundaries and friend clearings. Flying species gain height while escaping; fear decays after the car leaves. Menus and pause freeze wildlife.
- `build.ps1 -Test` passed with zero C# warnings/errors, `WILDLIFE_CHECKS_PASSED`, `SMOKE_TEST_PASSED` and `WHEEL_TRACK_CHECKS_PASSED`. Checks exercise all five species escaping, anticipation of approaching motion in both directions, safe terrain contact, calming down, counts and pause gating. Log: `artifacts/wildlife-tests.log`.
- Captured and inspected `artifacts/wildlife.png` using Compatibility/OpenGL: generated spotted deer are visible beside the road. Capture returned `Ok`. Existing sandbox restrictions on global Godot settings/log/cache writes remain. Sustained FPS and extended manual chase behavior have not been measured.

## New example from reward panel — 25 October 2026

- Replaced **Najít jiné zvířátko** with **Nový příklad**. It immediately opens a different question at the same animal; only a correct answer draws a new reward set.
- `build.ps1 -Test` passed with zero C# warnings/errors, `SMOKE_TEST_PASSED` and `WHEEL_TRACK_CHECKS_PASSED`. UI signal checks cover repeated questions, incorrect answers, pause/resume, reward selection guards and changed reward sets. Output: `artifacts/new-challenge-tests.log`.
- Existing sandbox errors writing global Godot settings/log files remain; all validation steps completed successfully.

## Retro vehicle model — 25 October 2026

- Replaced the ellipsoid body and protruding bubble windows with rounded mesh coachwork, a fitted cabin, inset glazing with gaskets and highlights, wheel arch lips, mirrors, door handles, bonnet details, bumpers, grille, indicators and separate tail lights. Wheels now have cylindrical tyres, shoulder rings, tread blocks and eight-spoke rims.
- Body, roof and trim use cached vehicle paint materials. Reward recoloring still reaches every registered part; all four accessory toggles remain compatible, including larger wheels. Vehicle collision and driving parameters are unchanged.
- C# build completed with zero warnings/errors. The full headless regression returned `SMOKE_TEST_PASSED`, including driving, bridge contact, reward recoloring and accessory combinations (`artifacts/car-smoke.log`). The final light placement adjustment was followed by another successful build and rendered capture.
- Inspected Compatibility/OpenGL captures from front and rear and with all accessories. Reproduce with `./run.ps1 -Capture -View car`, `-View car-rear`, or `-View decorated`. The existing sandbox shader-cache warning remains; captures return `Ok`.

Environment inspected and project implemented on 25 October 2026, Windows x64.

## Confirmed by execution

- .NET SDK 10.0.402 was already installed. Godot was absent; portable official Godot **4.7.2 stable .NET**, build `ed1daf0bf`, was downloaded into `.tools`.
- C# build completed with **zero warnings and zero errors**.
- Headless editor import completed, and a standalone five-frame project load returned exit code zero.
- Fixed-tick smoke test passed: movement, terrain contact, brake overriding throttle, reverse, steering, reset, dialogue state, resuming dialogue after pause, disabled driving during pause, exploration resume, three animals and populated route samples.
- A real graphical launch used **OpenGL 3.3 Compatibility / NVIDIA GTX 1060 6 GB**. PNG capture returned `Ok`. This is rendered verification, not a claim that headless mode produces game images.
- River update: removed the circular pond, generated a carved winding channel and sandy banks, and inspected the rendered river capture. Added physical driving checks for support above water and arrival across the bridge.
- Water appearance update: C# build and rendered Compatibility launch succeeded; inspected the river capture with depth-based color, animated wave normals, broken highlights and shore foam. Shader loaded without errors. Collision behavior is unchanged by the visual wave displacement.
- Village layout update: four houses now alternate along the main road, with entrances toward the road, short access paths and front fences with open gates. Inspected a rendered village overview for placement and road clearance; houses and yards are reserved against random vegetation.
- Arithmetic/reward update: build completed with zero warnings/errors. Validated 1,000 seeded questions for bounds, unique choices, deterministic generation, both operations and all three correct-answer positions. UI button signal tests passed for every animal, both wrong options, correct answers, pause/resume, cancellation, all three looks and actual body/roof/trim material replacement. Existing driving and bridge checks also passed. Inspected rendered challenge and reward panels at 1280×720. Appearance is session-only; no saved progression is claimed.
- Inspected captures of normal gameplay, village, rabbit, pond/bridge and Czech title UI. Inspection exposed overbright materials, reversed roof faces, road join gaps, misplaced bridge rails and hidden pond water; these were corrected and affected views recaptured.

- Mouse camera update: `build.ps1 -Test` passed with zero C# warnings/errors and `SMOKE_TEST_PASSED`. Added synthetic mouse checks for side/front/roof views, vertical limits, holding the view before the timeout, automatic return, button release, pause input gating and focus-loss cleanup. Interactive mouse feel has not been manually verified. Editor import still reports the existing sandbox restrictions on global Godot settings/log paths.

## Not yet verified

- Sustained 60 FPS at 1920×1080, long exploration sessions and behavior on other graphics hardware. Captures used a 1280×720 window with scalable 1600×900 UI reference.
- Child usability, manual keyboard/button end-to-end tests, every terrain slope, and automatic recovery in all collision cases.
- Standalone Windows export: matching **Godot 4.7.2 .NET export templates are not installed**. `build.ps1 -Export` is provided but no exported EXE is claimed.
- Camera building occlusion and water navigation are known prototype limitations, documented in README.

## Reproduce

```powershell
./build.ps1 -Test
./run.ps1 -Capture
./run.ps1 -Capture -View rabbit
./run.ps1 -Capture -View pond
./run.ps1 -Capture -View title
```

For ordinary interactive play, use `./run.ps1`. No graphical editor is required.

## Expanded world and reward exploration — 25 October 2026

- Build passed with zero warnings/errors. Final headless smoke run returned `SMOKE_TEST_PASSED`; output is in `artifacts/world-smoke-output.txt`.
- Playable radius increased from 166 m to 525 m (10.0024 times the area); terrain width is now 1,110 m. Terrain is generated in chunks at the original 2.5 m collider resolution. Tested vehicle contact at (-350, -250), beyond the original boundary, as well as driving and bridge crossing.
- Added outer hills, two road loops, 14 dense groves and batched vegetation across the larger world. Animal clearings are reserved against vegetation.
- Tested 30 animals, all three species, three fur colors per species, question/reward UI and cosmetic changes. Tested immediate repeat rejection, cancellation not unlocking the previous animal, and a different animal's correct answer unlocking it.
- Rendered and inspected `artifacts/gameplay.png` and `artifacts/world.png` using Compatibility/OpenGL. Yellow arrows retain their screen size and remain visible through terrain and vegetation; the last rewarding animal hides its arrow until another animal gives a reward.
- Editor import reported sandbox restrictions on the global Godot settings directory; the build, runtime tests and graphical captures completed. Sustained frame rate has not been measured.

- Roadside placement follow-up: all 30 animals are now distributed along the road network, 5.5–7 m from the nearest centerline, clear of village plots and river water. Updated smoke test passed, including proximity checks for every animal; inspected the updated world overview.

## Storybook vegetation — 25 October 2026

- Added seven tree silhouettes with tapered trunks, root flares and forked branches, plus leafy, berry and flowering bushes. Spruces use layered cone meshes, birches have bark markings, and willows have drooping foliage. Groves favor different species and roadside accents expose the variety during driving.
- C# build passed with zero warnings/errors. Headless regression run returned `SMOKE_TEST_PASSED`, including all existing driving, bridge and animal interaction checks. Seed 7421 generates 3,827 trees and 2,142 bushes.
- Rendered and visually inspected `artifacts/vegetation.png` and `artifacts/gameplay.png` with the Compatibility/OpenGL renderer. Tree silhouettes, pastel crowns, branches, root placement and roadside shrubs are visible in the captures.
- Mesh resources are shared and vegetation batches are divided into 64 m cells for renderer culling. Sustained FPS has not been measured. Sandbox restrictions still prevent the global Godot log/cache writes; both runtime verification and image capture completed with exit code zero.

### Vegetation detail pass

- Replaced spherical crown surfaces with a shared, smooth-normal scalloped mesh, vertex shading and a Compatibility foliage shader for painted color variation and a subtle breeze. Corrected triangle winding during visual inspection.
- Added close-range leaf sprays, bark ridges, moss, five-petal tree and shrub flowers, apple stems and highlights, berry highlights, terrain-aligned fern clusters and spotted mushrooms. A separate seeded art random stream preserves the previous tree/bush layout and counts.
- Build passed with zero warnings/errors. Rendered and inspected the updated vegetation overview and `artifacts/vegetation-detail.png` using Compatibility/OpenGL; capture returned `Ok`, with no shader errors. Fine detail batches stop drawing beyond 85 m (10 m hysteresis). Sustained FPS remains unmeasured.

### Coherent tree groves

- Dense groves, scattered trees and roadside accents now share one spatial species map with seven inner and fourteen outer grove centers. Gently distorted region boundaries keep neighboring trees of the same type together; riverbank trees form a willow ribbon.
- Build passed with zero warnings/errors. Rendered and inspected the updated `artifacts/world.png` and `artifacts/gameplay.png`; both captures returned `Ok`. The overview shows distinct blossom, golden, conifer and birch areas instead of individual random species scattered throughout them. Seed 7421 now generates 3,858 trees and 2,166 bushes; existing placement clearance rules are retained.

## Expanded rewards and actual 3D previews — 25 October 2026

- Added beach buggy, pickup and station wagon coachwork alongside the original retro car. All four share the original wheel nodes; model swaps preserve paint, accessories, wheel size and driving physics.
- Added ten accessories: spoiler, roof rack, suitcase, surfboard, extra lamps, front protection bar, star antenna, twin horns, sports exhausts and spare wheel. The catalogue now has fourteen combinable accessories.
- Added mint, lavender, tangerine, ocean stripes and racing stripes, bringing the paint catalogue to eight. Twin bonnet and roof stripes are shown only for striped paints.
- Each solved question offers one model, one paint and one accessory, in shuffled order, and rejects the previous set. Draws no longer allocate all three-item combinations.
- Replaced symbolic drawings with larger previews of the actual game geometry, rendered once in isolated, antialiased SubViewports with studio lighting. Rear accessories use a rear camera. Preview construction is deferred until the menu survives a frame.
- Final `build.ps1 -Test` passed with zero C# warnings/errors, `SMOKE_TEST_PASSED` and `WHEEL_TRACK_CHECKS_PASSED`. Regression covers 1,000 seeded draws including all catalogue entries, real UI selection of every model and paint, geometry/visibility of every accessory, stripe toggles, pause, guarded selection and preserved appearance. Output: `artifacts/expanded-rewards-tests.log`.
- Rendered and inspected buggy, pickup, wagon and reward panels through Compatibility/OpenGL. The deterministic `-View accessories` capture shows a buggy, striped paint and surfboard with actual 3D previews. Existing sandbox restrictions on global Godot settings/log/cache writes remain; tests and captures complete successfully. Appearance remains session-only.

## Dense interactive grass — 25 October 2026

- Added streamed 16 m meadow tiles with 28 by 28 seeded tuft candidates, five tapered blades per tuft and GPU wind, gusts and flutter. Car contact follows its orientation and height; eight recent positions preserve a wake that recovers over 1.6 seconds, with reset discontinuities clearing that history.
- C# build passed with zero warnings/errors. Final headless regression returned SMOKE_TEST_PASSED, including new checks for bounded tile/tuft counts and road, water and animal clearing exclusions. Log: artifacts/grass-smoke.log.
- Compatibility/OpenGL rendered artifacts/grass.png successfully with no shader errors. Inspected the dense meadow, bent grass around the car and flattened wake. Initial gameplay capture also confirmed clear road surfaces. Sustained FPS has not been measured.

### Extended grass distance and lighter palette

- Extended grass visibility from 70 m to 270 m, with final height fade at 225–270 m. Three tile LODs use dense five-blade tufts nearby, quarter-density three-triangle tufts past 66 m and one-sixteenth-density two-triangle tufts past 142 m. Root samples are nested; 12 m hysteresis limits repeated LOD swaps. Streaming remains bounded and uses a complexity budget. Distant vertices skip the eight-sample wake loop.
- Raised yellow-green material brightness and softened dark root shading. Rendered and inspected artifacts/gameplay.png through Compatibility/OpenGL: lighter foreground grass and meadow coverage across the middle distance; capture returned Ok without shader errors.
- Build passed with zero warnings/errors. Full regression returned SMOKE_TEST_PASSED, including bounded streaming and populated near, middle and far LOD checks. Logs: artifacts/grass-lod-smoke.log and artifacts/grass-lod-render.log. Sustained FPS has not been measured.

### Smooth density LOD and stronger lightening

- Replaced whole-tile mesh swaps with persistent, disjoint root layers. Each tuft retains its geometry and scale as the camera moves; individual heights fade smoothly across 50–90 m, 110–170 m and 225–270 m. Layers load beyond their visible fade bands, have a removal margin and grow in over 0.7 seconds when newly streamed.
- Raised the palette substantially, removed most dark root shading and added soft emission fill to brighten shaded blades. Rendered and inspected artifacts/gameplay.png with Compatibility/OpenGL; capture returned Ok without shader errors.
- Build passed with zero warnings/errors. Full regression returned SMOKE_TEST_PASSED. Added a 40 m camera move across LOD bands and checked that more than 100 shared resident layers retain identical instance IDs, alongside bounded streaming, all three populated layers and placement checks. Logs: artifacts/grass-smooth-lod-smoke.log and artifacts/grass-smooth-lod-render.log. Sustained FPS remains unmeasured.

## Single animal visits and remaining counter — 26 October 2026

- Each of the 30 challenge animals now accepts one visit per run. Opening the question consumes the visit and hides its arrow. Leaving without answering also consumes it; wrong answers can still be retried within the open question. Other rewards, vehicle resets and pauses never unlock a visited animal. Removed the repeated-example reward action and changed the leave button to match the one-visit rule.
- Added a top-right remaining-animal counter, updated at the start of each visit, and a completion message at zero. Driving remains available after all visits.
- C# build completed with zero warnings/errors. Updated UI signal regressions exercise all 30 visits, cancellation, wrong answers, pause restoration, model/paint/accessory rewards, keeping the current look, permanent rejection of earlier visits and exact HUD values through zero. Cosmetic catalogue checks now run independently of the finite visits. Smoke, wildlife and collision checks passed; log: `artifacts/single-visits-tests.log`.
- Rendered and inspected `artifacts/gameplay.png` with Compatibility/OpenGL; counter is readable at the top right and capture returned `Ok`. Existing sandbox restrictions on global Godot settings/log/cache writes remain.

## Czech letter and picture tasks — 26 October 2026

- The 30 challenge animals now split into 15 arithmetic tasks and 15 letter tasks, assigned by their stable world index. Both implement the same answer contract and share the single-visit counter, incorrect-answer retry, pause restoration and cosmetic reward flow.
- Letter tasks show one uppercase Czech initial and three clickable vector illustrations without printed words. The catalogue contains auto, banán, citron, dům, hruška, jablko, klíč, loď, míč, ryba, slunce and šnek. A seeded deck covers all twelve initials before repeating; distinct distractors never share the target initial. Pictures are drawn with Godot primitives and require no fonts, downloads or external art assets.
- `build.ps1 -Test` passed with zero C# warnings/errors and `COLLISION_CHECKS_PASSED`, `WILDLIFE_CHECKS_PASSED`, `SMOKE_TEST_PASSED`, `WHEEL_TRACK_CHECKS_PASSED`. Added 1,000 deterministic letter-question checks for exactly one correct picture, unique choices, all answer positions and full initial coverage including Š. UI signal checks verify the assigned types, three rendered picture controls, wrong-answer disabling, pause restoration, rewards, finite visits and the remaining counter. Log: `artifacts/letters-tests.log`.
- Rendered and visually inspected `artifacts/letters.png` and `artifacts/letter-pictures.png` through Compatibility/OpenGL. Both returned `CAPTURE: Ok`; all twelve illustrations and the task layout were inspected. Existing global Godot log/settings/cache sandbox messages remain; all checks completed successfully.

## Twenty additional letter pictures — 26 October 2026

- Expanded the letter-task catalogue from 12 to 32 original vector illustrations. Added motýl, kočka, pes, slon, žirafa, zebra, želva, kachna, deštník, strom, květina, jahoda, mrkev, zmrzlina, hodiny, kniha, tužka, čokoláda, vlak and raketa. New pictures participate both as correct targets and as distractors.
- Kept the existing picture deck and initial filter: every picture is used as a correct target before a repeat, and no distractor starts with the requested letter even when multiple catalogue words share that initial. Updated 1,000 deterministic task checks to cover all 32 targets, unique catalogue IDs and Czech initials Š, Ž and Č.
- C# build completed with zero warnings/errors. The updated smoke tests, wildlife checks and collision checks passed. Log: `artifacts/more-pictures-tests.log`.
- Rendered and visually inspected all 32 drawings in two gallery pages, `artifacts/letter-pictures.png` and `artifacts/letter-pictures-2.png`; both returned `CAPTURE: Ok`. Gallery labels now use dark text and remain exclusive to the diagnostic view. The actual task still shows pictures without written names. Existing global Godot log/settings/cache sandbox messages remain.

## Tree impact feedback — 26 October 2026

- Vehicle slide collisions now identify the struck tree through its solid trunk. Only that tree's batched visual parts rock around its foot with a damped oscillation for 1.6 seconds; original instance transforms are restored exactly. The collider remains fixed.
- Each impact releases 5–9 small leaves in colors sampled from that tree's crowns. Leaves flutter and tumble, settle on the terrain, shrink and are removed after six seconds. A separate random stream preserves world generation. Effects freeze outside exploration, active particles are capped at 96 and low-speed sustained contact does not retrigger them.
- Build completed with zero warnings/errors. Collision regression uses a real generated tree trunk in the isolated driving arena and confirms vehicle-triggered shaking/leaves, solid contact, settling, particle cleanup, no continuous emission while holding throttle and a later impact retriggering the same tree. Existing smoke and wildlife checks passed. Log: `artifacts/tree-impact-tests.log`.
- Rendered and inspected `artifacts/tree-impact.png` with Compatibility/OpenGL; capture returned `Ok`. Reproduce with `./run.ps1 -Capture -View tree-impact`. The diagnostic capture triggers the effect directly for repeatable framing. Existing global editor settings/log sandbox messages remain.

## Wordless delivery expeditions — 27 October 2026

- Added 30 sender deliveries with visible carrots, apples or honey, picture storyboards, road-following breadcrumbs, a direction arrow and matching recipient portraits. Arrival uses picture counting with dot answers; cosmetic rewards use 3D previews and symbol buttons.
- C# build passed with zero warnings/errors. All 30 deliveries passed on seeds 1, 42 and 7421 (`artifacts/delivery-seed1.log`, `delivery-seed42.log`, `delivery-seed7421.log`). Checks cover declining without consuming a visit, preventing overlapping missions, cargo across reset/model swaps/pause, arrival, wrong-answer retention, leaving and reopening counting, sender-only completion, reward selection/skip, cleanup and free driving after all deliveries. The navigation constructor also verifies every road sample can reach the recipient.
- Actual simulated gamepad input passed the updated controller suite (`artifacts/delivery-controller.log`), including opening versus accepting a delivery, automatic arrival, answer navigation, pause restoration and selecting rewards. Existing smoke tests passed (`artifacts/delivery-smoke.log`). Physical controller testing and child playtesting remain manual.
- Rendered and visually inspected the offer, driving navigation/cargo, counting and reward views (`artifacts/delivery-offer.png`, `delivery.png`, `delivery-question.png`, `delivery-reward.png`). All returned `CAPTURE: Ok`. Removed navigation HUD overlap during modal interactions and increased dot contrast. Headless logs retain the environment's existing user-log write restriction; all validation suites exited successfully.

## Original tasks restored; optional reward expeditions and apple collection — 27 October 2026

- Restored the original 15 arithmetic and 15 initial-letter/picture questions as the primary E/A interaction. Their three cosmetic rewards now have a fourth, picture-based optional expedition card. A cosmetic choice never starts an expedition. The fourth choice opens a confirmable storyboard. Expedition completion has its own set and never consumes original task visits. Original questions and cosmetic rewards during an active expedition preserve its cargo and route.
- Added a second expedition type: find three large marked apples on clear road centers and bring them back to the requester. Pickups happen automatically within four meters only while exploring, disappear once collected and add visible cargo to the car. HUD progress fills one marker per apple. Navigation advances to remaining apples and then returns to the requester; early hand-in is rejected. Reset, model swaps and pause preserve the inventory. Even-index animals offer delivery; odd-index animals offer apple collection.
- Build completed with zero warnings/errors. All 30 original tasks plus all 30 optional expeditions passed on seeds 1, 42 and 7421 (`artifacts/optional-expeditions1.log`, `optional-expeditions42.log`, `optional-expeditions7421.log`). These include both expedition types, the fourth reward, pause restoration, original tasks during a delivery, independent progress, all apple pickups, no pickup while paused, reset retention and incomplete hand-in rejection. Existing smoke tests and the updated gamepad suite passed (`artifacts/optional-smoke.log`, `optional-controller.log`).
- Rendered and inspected the four reward cards, collection storyboard, in-world apple pickup and collection counting views (`artifacts/reward.png`, `collection-offer.png`, `collection.png`, `collection-question.png`). All returned `CAPTURE: Ok`. The environment's existing user-log write restriction remains in headless logs; suites exited successfully. Physical-controller and child playtesting remain manual.

## Text navigation and green road arrows — 27 October 2026

- Moved the compact navigation panel from 82 px to 20 px from the top. Destination, cargo and collection progress now use Czech text beside the direction arrow. Restored text for expedition offers, the fourth reward, action buttons and the title start button; expedition counting retains cargo illustrations but uses numbered answers. Original letter-picture tasks are unchanged.
- Replaced yellow road dots with shared flat green arrow geometry, rotated along each next route segment. Arrows follow the road/bridge surface with clearance and are hidden where the route has no further direction.
- Build passed with zero warnings/errors. All 30 original tasks and optional expeditions passed on seed 42 (`artifacts/green-navigation-tests.log`); the gamepad suite passed (`artifacts/green-navigation-controller.log`). Captured driving, collection, offer and reward screens successfully; visually checked the higher text panel and direction of the green road arrows. Existing headless user-log write restrictions remain unchanged.

## Expedition reward icon — 27 October 2026

- Replaced the fourth reward's text-only preview with the same lit 3D car preview used by the other three cards. The car carries the actual expedition cargo and quantity on its roof rack, with a small green destination pennant. Collection previews show three apples; delivery previews show their offered cargo. Czech card labels and the Vyrazit button remain aligned with the other rewards.
- Build succeeded with no C# errors. NuGet emitted NU1900 warnings because its vulnerability-data endpoint was unavailable. Rendered the delivery and apple-collection reward menus (`artifacts/reward.png`, `collection-reward.png`) for visual inspection. The change affects previews only.

## Question, chosen expedition, then symbol rewards — 28 October 2026

- Correct arithmetic or letter-picture answers now unlock a choice between Rozvoz and Dovoz at every animal. Both tasks open their instructions before starting. Back returns to the choice; Později preserves it for another visit. Original questions solved during an active expedition unlock their task for later without replacing cargo or granting an early reward.
- Completing the selected expedition and its counting task grants one paint, one vehicle and two distinct upgrade options. All four cards retain actual 3D previews and replace names/action captions with palette, vehicle and wrench symbols. One selection or the back symbol resumes driving. Pause preserves exact choices.
- Build succeeded with no C# errors; NuGet reported NU1900 because its vulnerability endpoint was unavailable. Delivery checks passed on seeds 1, 42 and 7421. Final seed-42 checks also cover selecting collection at the first animal, returning from instructions, revisiting postponed choices, withholding early cosmetics, four unique reward options and their category symbols. Simulated controller, smoke, collision, wildlife and wheel-track checks passed.
- Captured and visually inspected `artifacts/task-choice.png` and `artifacts/reward.png`: both task choices and all four symbol reward cards fit within the 1280 × 720 window. Main validation logs: `artifacts/task-flow-full.log` and `artifacts/task-flow-final.log`.
- The full suite exposed a diagnostic startup issue: road-driving mode updated an uninitialized UI. Disabled Game processing for diagnostic-only startup and reran both road-driving seeds (1 and 16); every route passed in both directions with exit code 0.

## Fixed road anchors, direct task start and stronger markers — 28 October 2026

- Corrected the remaining arrow shift: caching individual arrows did not prevent shortest-path sampling from selecting a different spatial phase. Roads now generate fixed anchors every 8.4 m and map samples to them. Guidance uses those anchors and fixed road tangents for both travel directions, independently of the car, destination or path sampling phase. Instances only show or hide.
- Dovoz selection now previews an empty platform. Removed the confirmation dialog and its state/events; selecting Rozvoz or Dovoz immediately creates the expedition and resumes driving. Legacy offer capture aliases now show the task-choice screen.
- Unified question/exclamation styling in Art.TaskMarker: 132-point glyphs, .0011 fixed pixel size and a 16-pixel dark outline, about 71% greater linear screen size than the previous animal markers. Applied the same prominent style to collectible exclamation marks.
- Build passed with no C# errors (existing NuGet NU1900 endpoint warning). Delivery checks passed on seeds 1, 42 and 7421, including moving across adjacent nearest samples while verifying all visible arrows belong to road-defined anchors, unchanged transforms, returning to earlier arrows, empty collection preview and immediate start without a confirmation button. Controller and smoke suites passed. Logs: artifacts/fixed-road-anchors-tests.log, fixed-road-anchors-1.log, fixed-road-anchors-7421.log, direct-start-controller.log and direct-start-smoke.log.
- Captured and visually inspected artifacts/task-choice.png and artifacts/collection.png: collection has an empty platform, and larger outlined question/exclamation marks remain clear against the landscape. Both captures returned CAPTURE: Ok.
## Garage catalogue and configuration — 28 October 2026
Added a reserved roadside garage plot with solid walls, an open entrance and a fixed-size house marker. The catalogue shows all 26 cosmetics with grayscale locked previews, owned selections, equipped indicators and a complete vehicle preview. Starting paint/model are owned; selected expedition rewards remain available during the session. Garage configuration and pause preserve active expeditions. Added --garage-checks to build.ps1 -Test. Seed-42 garage, delivery and controller checks passed; garage and building captures were visually inspected. Build passed with the existing NU1900 vulnerability-endpoint warning.

## Walkway road connections — 28 October 2026
House paths and the wider garage driveway now meet the actual road mesh edge, sampled across their width with a small overlap. Their height follows terrain and the rendered road surface; the garage threshold joins its slab. Full path footprints exclude vegetation. Build and seed-42 garage checks passed. Captured and visually inspected village.png and garage-building.png to verify the connections.

## Village residents and house upgrades — 28 October 2026
Four dedicated village residents offer repeatable alternating math/letter tasks. Each correct answer adds one of ten cumulative visible house details, independently of original roadside visits and expeditions. Residents and roadside friends turn toward nearby players and wave. House levels, resident questions and completion screens persist; older saves default to zero house progress.

Build passed with zero warnings/errors. Seed-42 village checks passed for all 40 upgrades, incorrect answers, abandoned tasks, pause, restoring pending questions and completion screens, disk saves, older saves, and an active collection expedition. Save, delivery, new-game, controller, garage, collision and smoke checks also passed. Updated the collision check's old obstacle count to include the existing garage's three walls. Captured and visually inspected `artifacts/village-upgraded.png` and `artifacts/village-question.png` at 1280 × 720. Headless runs reported the existing restricted user-log path warning; the checks and captures completed successfully.

## Shuffled village tasks, fruit collections and quieter village — 28 October 2026
Every house now has a deterministic shuffle of ten tasks: three arithmetic, three letter-picture, three fruit collection/return tasks, and one additional randomly selected type. Each house uses its own shuffle, reconstructed from the saved world seed; postponement and reload preserve the next task. Fruit tasks offer an empty cargo platform, three marked apples and navigation back to the resident. The house upgrades only after returning the full load and answering the counting question. Incomplete collections show a reminder on early return, preserve individual pickups across saves/reset/pause, and cannot replace another expedition. Cargo is explicitly detached on completion and restore.

All 30 ordinary task animals, including the greeting rabbit, now use seeded roadside placement outside a 38 m horizontal buffer around every village plot and the garage. Dedicated house residents remain at their houses.

Build passed with zero warnings/errors. Village checks passed all 40 scheduled tasks on seeds 1, 42 and 7421, including disk round trips for math, letters, fruit offers, partial collections, early-return instructions, cargo handover and house rewards. A further 512 generated schedules satisfy the required 3+3+3+1 composition and deterministic shuffle. Save, delivery and controller regression checks passed on seed 42. Captured and visually inspected `artifacts/village-fruit-offer.png`; import and capture completed successfully. Diagnostic logs: `artifacts/village-schedule-checks.log` and `artifacts/village-update-*-*.log`.

## Village trade deliveries (2026-10-30)

- `dotnet build MathRacer.csproj`: passed, no warnings or errors.
- `--trade-checks` passed on seeds 1, 42 and 7421: three shops off roads, arithmetic and wrong-answer gating, repeat deliveries, one active expedition, stable restored destinations and amounts, pause, disk save round trips, all three reward categories, garage ownership, and old-save compatibility.
- Seed 42 regressions passed: village, saves, deliveries, garage, collections, farm, garden, new game and controller.
- Rendered and inspected `artifacts/baker.png`, `artifacts/confectioner.png` and `artifacts/merchant-reward.png` using the actual game renderer.

## Ten roadside task animals (2026-10-30)

The world now generates 10 ordinary quest animals (five math and five letter tasks), with at least 100 m of horizontal clearance from every village plot, including the garage and trade shops. Village actors keep their saved IDs. Thirty-animal saves migrate to the first ten records, preserving village progress and earned cosmetics; interactions and expeditions belonging to removed animals return safely to driving.

Build passed with no warnings or errors. Seed 42 checks passed for saves (including migration from 30 animals), village placement and upgrades, all deliveries, trade rewards, new game, and the gameplay smoke suite.

## Driving events and completion celebrations

- Added `--event-checks` to `build.ps1 -Test`: ordered checkpoints, normal interaction, cancellation, repeat completion protection, disk save/restore, real vehicle support at starts/finishes, cargo ownership and handover, and pause/resume during the house celebration.
- Event logic checked on seeds 1, 42 and 7421. Physics and final integration checked on seed 42.
- Regression suites passed on seed 42: delivery, village, trade, save, farm, garden, collection and controller. Controller checks now wait for the celebration and verify pausing it before choosing a reward.
- Visual captures inspected: `artifacts/slalom.png` and `artifacts/house-celebration.png`. Capture modes: `events`, `slalom`, `house-celebration`.

## Level village terrain (2026-10-30)

- Build passed without warnings or errors.
- Village checks passed for seeds 1, 42 and 7421, including the common terrain elevation at all twelve buildings, level streets, square paving collision and all house tasks.
- Road passability and river bank checks passed for seeds 1, 2, 42, 44, 53 and 7421 using the final leveled terrain. The headless road audit also reports shutdown resource-leak warnings.
- Save checks passed on seed 42, including lifting an older position out of the newly leveled village terrain without changing X/Z.
- Rendered and visually inspected artifacts/square.png: level square, low fountain base and flat adjacent road, with hills retained beyond the village.

Event placement update: both courses now use the outer country loops; continuous route samples must stay at least 160 m from every village building. Event checks passed on seeds 1, 42 and 7421, including real vehicle support at the relocated starts/finishes and smile marker styling.

## Village architectural details (2026-10-30)

- C# build passed with zero warnings and errors.
- Seed 42 village and garage suites passed, covering all 40 earned house upgrades, persistence, raised foundations, square paving and garage access.
- Collision suite passed, including forward/reverse impacts and recovery. The new garage workbench has its own collider under a separate Workbench node; the three wall colliders and entrance remain intact.
- Actual Compatibility renderer captures inspected: village, village-upgraded, baker, square and garage-building. Final upgraded-house capture verifies thin fence pickets and compatibility with all earned decorations.
- Permanent architectural details are generated without consuming random state. Geometry is combined by material per detail group; no additional per-frame animation or physics bodies are created for roof tiles, facade trim or other small decoration. Frame rate was not benchmarked.

## Nature detail and palette pass (2026-10-30)

- C# build passed with zero warnings and errors. Seed 42 smoke and collision suites passed, including grass streaming, clear interaction areas, driving over the bridge, tree impacts and falling-leaf cleanup.
- Rendered and inspected nature, riverbank and vegetation-detail captures using the Compatibility renderer. The final riverbank capture includes the revised water/sky palette and procedural ground material.
- Seed 42 still generates 3905 trees and 2110 bushes. New meadow, fern, moss, cattail and water-lily details use an independent seed stream and add no physics obstacles. Existing tree colliders and impact batch bookkeeping remain intact.
- Small foliage uses the existing spatial batches and 85 m visibility range; small-detail shadows are disabled. The terrain shares one shader material across its chunks. No frame-rate benchmark was performed.
