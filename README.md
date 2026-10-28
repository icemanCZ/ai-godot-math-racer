# MathRacer – Kouzelné údolí

A peaceful, code-generated 3D driving prototype for children, with Czech interface and no editor workflow. Built with C# and Godot Compatibility rendering.

## Requirements and startup

- Windows x64, OpenGL 3.3 capable graphics.
- **Godot 4.7.2 .NET** (not the standard GDScript-only edition).
- **.NET SDK 10**. This project targets `net10.0`.
- Internet for initial NuGet restore, or a prepopulated package cache.

The development environment includes portable Godot in `.tools/Godot_v4.7.2-stable_mono_win64/`. This directory is ignored by Git. On another machine, download the .NET archive from [Godot](https://godotengine.org/download/windows/) and extract it into `.tools`, or set `$env:GODOT_PATH` to the full console executable path.

Run from PowerShell:

```powershell
./build.ps1 -Test
./run.ps1
```

If Windows blocks PowerShell scripts, use `./run.cmd` (or double-click `run.cmd`). The launcher applies `ExecutionPolicy Bypass` only to its own PowerShell process; it does not change your system policy. The equivalent build/test command is `./build.cmd -Test`.

For visual capture: `./run.ps1 -Capture`. Output: `artifacts/gameplay.png`. For a Windows release: install the matching **4.7.2 .NET export templates**, then run `./build.ps1 -Export`. Missing templates are a real export error; source builds do not imply a working export.

The Windows preset produces a single portable `exports/MathRacer.exe` for Windows x64, with game data and the .NET runtime embedded. Players can double-click this file without installing Godot or .NET. The preset uses matching official export binaries stored in `.tools/windows-export-templates/` (`windows_debug_x86_64.exe` and `windows_release_x86_64.exe`); this local tools directory is ignored by Git.

Each launch chooses a fresh random seed shared by terrain, vegetation, wildlife and task generators. A new road graph is built from generated control points: loop shapes and numbers of bends vary, and an optional shortcut changes the connections. The river can lie on either side of spawn, with a different course and bridge position; routes on both banks join at that crossing. Village plots follow the generated main road. Hill profiles, forest locations and dominant tree species, and roadside animal locations also vary. The seed stays fixed throughout that run, including pauses and vehicle resets, and is printed as `WORLD_READY seed=...` in the console.

For reproducing a particular world during development, pass `--seed=NUMBER` after Godot's `--` separator (for example alongside `--smoke-test`). Ordinary launches always choose a random seed.

## Controls

Xbox controller (USB or Bluetooth, paired in Windows): left stick steers and also accelerates when pushed forward or reverses when pulled back. RT accelerates, LT reverses, X applies the handbrake, Y returns to the start, and the right stick orbits the camera. A starts the game, talks to animals and confirms menu choices. Use the D-pad or left stick to choose answers and rewards. B leaves a task/reward or returns from pause; during driving B opens pause. The Menu button pauses/resumes. Sticks and triggers have deadzones to prevent drift, with analog steering and throttle. On-screen hints switch when using the controller or keyboard. No mouse is needed for menus. `./build.ps1 -Test` includes simulated joypad input and menu navigation checks; physical USB/Bluetooth compatibility still needs a real controller check.

W / Up accelerates; S / Down brakes then reverses; A/D or Left/Right steer. Space applies the handbrake: steer while braking to slide sideways, then release Space to regain grip. R returns to the starting road, E talks to a nearby animal, Escape pauses. Hold the right mouse button and move the mouse to orbit around the car and inspect its accessories (horizontal rotation and vertical tilt). After 3 seconds without mouse movement, the camera smoothly returns behind the car. Menus and pause release the mouse. Click Hrát to begin. There are no timers, damage or game-over conditions.

At an animal, press E / controller A, or click the large on-screen button, to open its original task. Fifteen animals offer addition/subtraction within 10 and fifteen offer uppercase Czech initials with three illustrated choices from the 32-picture catalogue. A correct answer opens a choice between Rozvoz (delivery) and Dovoz (collecting apples and bringing them back). Both choices are available at every animal. Selecting either task starts it immediately and resumes driving, with no confirmation dialog. The Dovoz preview shows the empty cargo platform; Rozvoz shows loaded cargo. Cosmetic rewards are granted only after completing the chosen expedition. Choosing Později preserves the solved question; return to that animal to choose a task without answering again.

There are two expedition types, chosen by the player: delivery of 1–3 carrots, apples or honey jars to another animal, or collecting three apples and bringing them back to the requester. Delivery cargo is loaded immediately. Collection starts with an empty roof rack and three large marked apples on existing clear road centers. Driving close to an apple picks it up once, removes it from the world and adds it to the car. The top navigation text counts collected apples from 0 / 3 to 3 / 3. Navigation leads to the next missing apple, then switches back to the requester after all three are loaded. An unfinished collection cannot be handed in.

Both expedition types use Czech text instructions, visible 3D cargo and a compact navigation panel at the top of the screen. A camera-relative arrow accompanies the destination or collection progress text. Green arrows on the road point along the route. Their positions and directions are anchored to the road at world generation, independently of shortest-path samples; approaching or leaving a section only shows or hides its arrows. Arrival opens a counting task with cargo pictures and numbered answers, followed by four cosmetic choices: one paint, one vehicle and two distinct upgrades. Reward buttons use a palette, vehicle or wrench symbol instead of captions, alongside actual 3D previews. Original questions remain available while an expedition is active; solving one unlocks its task choice for a later visit and preserves the current cargo and progress. Only one expedition can be active, and expedition rewards never offer another expedition. Pauses, resets and model swaps preserve collected items. Original-task visits and expedition completions are tracked independently; finishing an expedition never consumes another original question. There are no deadlines or penalties, and progress lasts for the current run.

The catalogue contains four cars (retro, beach buggy, pickup and station wagon), eight paints (including ocean and racing stripes), and fourteen accessories. The ten new accessories are a spoiler, roof rack, suitcase, surfboard, extra lamps, front protection bar, star antenna, twin horns, sports exhausts and spare wheel. Each card renders the actual 3D car and selected reward in a softly lit studio; rear accessories are shown from behind. Choose one reward or keep the current appearance. Each animal has one original question visit and one expedition per run. The flow is question, task choice, expedition completion, then one cosmetic reward. Preview the task choice with `./run.ps1 -Capture -View task-choice`. Opening an original question consumes its visit, as before; expedition completion is separate. Other rewards, pauses and vehicle resets never unlock visited animals. A top-right HUD counts down the remaining original-task animals from 30 to zero and shows a completion message after all visits; free driving remains available. Available animals have a bold floating yellow question mark with a thick dark outline and a larger constant screen size, naturally occluded by terrain, trees and other objects. Active expedition destinations and collectible apples use turquoise exclamation marks. Pausing preserves the offered choices. Accessories combine and survive paint changes, model swaps and vehicle resets for the current run. Model swaps preserve paint and all four shared wheels. Larger wheels are cosmetic and can be restored to standard size when offered again.

## Prototype content

The river chooses a route through lower natural terrain before roads and the bridge are placed. A broad floodplain blends into hill feet, with low sandy banks beside the water, so the river no longer cuts a narrow canyon through the outer hills. Terrain, water, bridge approaches, vegetation and wildlife share this generated course.

Roads follow 75% of the original hill profile, with a narrow terrain correction fading out over 7.5–14 m and smooth bridge approaches. The allowed grade is 65% (about 33 degrees). The terrain and collision mesh share cached heights, keeping the road surface visible and continuous. `./build.ps1 -Test` checks connected road graphs without dead ends across 128 seeds, audits full-width grades and road coverage on several generated maps, and drives every route in both directions using the real car physics.

The valley also contains 240 free-roaming butterflies, birds, rabbits, foxes and deer. They wander beside routes and across meadows, then run or fly away when the car approaches, including in reverse. Escape steering checks tree trunks, village plots, water, the world boundary and the challenge animals' clearings. After the danger passes they calm down and wander back toward their home area. Wingbeats, hopping and running are animated; distant wildlife sleeps and its meshes are culled. Wildlife freezes during menus, questions and pause. The 30 friends with yellow question marks still offer math or letter tasks. Preview deliveries with `./run.ps1 -Capture -View delivery-offer`, `-View delivery`, `-View delivery-question` or `-View delivery-reward`. `./build.ps1 -Test` checks every delivery on seeds 1, 42 and 7421, including declines, retries, pause, resets and cosmetic swaps. Preview a generated deer with `./run.ps1 -Capture -View wildlife`.

The car leaves two soft brown tyre tracks with a tread pattern while driving forward or reversing. Tracks follow the ground, roads and bridge, fade after 25 seconds of driving, and use a fixed segment limit. Stopping adds no marks and resets break the trail. Only actual river water suppresses tracks; low grassland remains dry. Preview with `./run.ps1 -Capture -View tracks`, or `-View tracks-right` to replay a gentle right turn from spawn into low ground. `./build.ps1 -Test` includes four driving regressions for right turns.

Approximately ten times the original playable area (1,110 m terrain width), rolling outer hills and two additional countryside road loops, winding road loop and branches, colorful village with four houses, clustered forest, flowers, fruit trees, conifers, rocks, mushrooms, fences, signs, benches and a winding river with sandy banks and a driveable wooden bridge. Coral toy car with slope-following arcade movement, smooth camera, 30 rabbits/foxes/bears beside the roads, three fur colors per species, with reward challenges, Czech title and pause panels. Geometry and materials are procedural; no external art assets.

Vegetation has seven storybook tree silhouettes: branching oak, apple tree, layered spruce, ivory-barked birch, pink blossom tree, drooping willow and golden broadleaf. Three shrub variants add leafy, berry and flowering undergrowth. Seeded groves, roadside accents and minimum trunk spacing keep the scenery varied, with clearances around roads, village plots and animals. Organic scalloped crown meshes have soft underside shading, painted leaf flecks and a gentle breeze. Close-up details include leaf sprays, five-petal blossoms, apples with stems, bark ridges, moss, ferns and spotted mushrooms. Shared meshes are batched by material and 64 m spatial cells for visibility culling; fine detail batches have an 85 m visibility range. These are original procedural models; no external downloads or asset licenses are required. Capture the roadside vegetation with `./run.ps1 -Capture -View vegetation`, or use `-View vegetation-detail` for a close-up.

Tree impacts briefly shake the struck tree and release 5–9 fluttering leaves in its crown colors. Stronger impacts produce a larger sway; sustained low-speed contact does not keep shedding leaves. Leaves settle and disappear, and effects pause outside exploration. Preview with `./run.ps1 -Capture -View tree-impact`.

## Structure and algorithms

Dense procedural meadow grass has seeded height and color variation, traveling wind gusts and flutter. The car bends and flattens nearby blades in forward and reverse; a short wake springs back over 1.6 seconds. Teleports clear the wake. Roads, river banks, bridge, village plots, trunks and animal clearings remain open. Grass streams in 16 m MultiMesh tiles using three persistent, disjoint density layers. Each root keeps its shape, width and position: extra near tufts smoothly shrink over 50–90 m, middle tufts over 110–170 m, and the sparse base layer over 225–270 m. Tiles load beyond those fade bands and newly streamed blades grow in over 0.7 seconds. Camera movement never swaps an existing tile mesh. Tile creation uses a complexity budget, and distant blades skip the car wake loop. A pale yellow-green palette, minimal root darkening and soft emission fill keep the grass light, including in shadow. Blade animation runs in a shared Compatibility shader without individual physics bodies or shadow draw calls. Preview a drive through the meadow with `./run.ps1 -Capture -View grass`.

See [architecture and extension guide](docs/ARCHITECTURE.md) and [algorithm descriptions](docs/ALGORITHMS.md). Responsibilities are separated under `Scripts/`; `Game` is the composition root. Task contracts and mathematics live in `Scripts/Mathematics`, Czech letter tasks and vector pictures in `Scripts/Letters`, appearance presets and application in `Scripts/Customization`. Persistent progression is a future extension.

## Known limitations and next steps

- River water has no buoyancy or swimming behavior. Entering deep water returns the vehicle to the start. Use the wooden bridge to cross.
- Tree trunks, rocks, fences, benches, signposts, houses and bridge rails block the car. Impacts reduce speed; the car can slide along obstacles and reverse away. Soft foliage and animals remain passable.
- Camera has terrain clearance but no building occlusion raycast.
- Pause freezes driving, while ambient animation continues.
- Five seconds of throttle without movement triggers recovery; R resets immediately. Circular world boundary resets to start.
- No audio, save data or permanent cosmetic unlocks yet.
- 60 FPS at 1080p is a target, not a measured guarantee. See `docs/VALIDATION.md` for actual checks.

Next: child playtesting, driving tuning, camera obstacle avoidance, difficulty settings, more cosmetics and appearance persistence.





### Garáž
Na začátku vesnice stojí garáž označená domečkem. Přijeď ke vjezdu a stiskni E / A. Katalog obsahuje všech 26 laků, modelů a doplňků: získané jsou barevné a volitelné, nezískané šedé. Vybraná odměna z výpravy se přidá do sbírky pro aktuální hru. Lak a model lze měnit, doplňky zapínat a vypínat; fajfka označuje nasazenou položku. Katalog se posouvá kolečkem myši nebo pravou páčkou. Pauza i návštěva garáže zachovávají aktivní výpravu. Zachycení nabídky: ./run.ps1 -Capture -View garage; budovy: -View garage-building.
