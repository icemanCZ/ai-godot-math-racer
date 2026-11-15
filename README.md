# MathRacer – Kouzelné údolí

MathRacer is a calm, procedurally generated 3D driving game for children. The game uses Czech, with picture choices, symbols and short prompts. It is built in C# with Godot’s Compatibility renderer.

## Running the game

- Windows x64 with OpenGL 3.3 support.
- Godot 4.7.2 .NET and the .NET 10 SDK.
- A NuGet package cache or an internet connection for the first restore.

The local launcher finds the portable Godot .NET installation in `.tools/`. On another machine, download the matching .NET build from [Godot](https://godotengine.org/download/windows/) and extract it into `.tools/`, or set `GODOT_PATH` to the full path of the console executable.

```powershell
./run.ps1
./build.ps1 -Test
```

On Windows, `run.cmd` and `build.cmd` also work when PowerShell script execution is restricted. With the matching .NET export templates installed, `./build.ps1 -Export` builds a standalone Windows x64 `exports/MathRacer.exe`. The game data and runtime are embedded. The project can use release and debug template binaries from `.tools/windows-export-templates/`.

## Controls

| Action | Keyboard and mouse | Xbox controller |
| --- | --- | --- |
| Drive and steer | WASD or arrow keys | Left stick, RT to accelerate and LT to reverse |
| Handbrake | Space | X |
| Return to the starting road | R | Y |
| Orbit the camera | Hold the right mouse button and move | Right stick |
| Interact | E or click the on-screen button | A |
| Choose a menu item | Mouse or arrow keys and Enter | D-pad or left stick, then A |
| Pause or resume | Esc | Menu |
| Back or leave an activity | Back arrow on screen | B |

The camera eases back behind the car after a short pause in camera input. The controller uses analog input with deadzones. Menu navigation is covered by simulated joypad input. Physical USB and Bluetooth compatibility still needs a hardware check.

## Quiz settings

Open the garage near the village entrance with **E / A**. Each quiz type has its own switch, and at least one type must stay enabled. All four types are enabled by default. Their icons show which types are active. The settings are saved and carry over when starting a new valley.

| Garage option | Task |
| --- | --- |
| Initial letters (*Písmenka*) | Pick the picture whose name starts with the displayed capital letter. |
| Syllables (*Slabiky*) | Look at one picture and pick its syllables from six tiles in any order. The game marks the correct selections and advances when all have been found. |
| Reading (*Čtení*) | Match a three-letter uppercase Czech word, such as **LES**, **PES** or **DŮM**, to one of three pictures. |
| Arithmetic (*Příklady*) | Level 1 uses two-number addition and subtraction within 10. Level 2 uses three-number addition with sums up to 10, such as `2 + 3 + 4`. |

Roadside animals, village residents, shops, postcards and forest bandits use the enabled question types. Scheduled arithmetic and word slots use an enabled replacement when their usual type is off. If arithmetic is off, a word quiz replaces cargo counting at handover. Wrong choices leave the current question open for another try.

## Exploring the valley

- **Roadside friends.** Ten animals with floating question marks offer an original quiz. After solving it, choose a delivery or apple collection. Both choices start immediately. Delivery carries one to three items to another animal. Collection starts with an empty rack and guides the player to three apples and back. Navigation uses a directional arrow, road arrows and visual progress. Handover unlocks a choice of cosmetic rewards. A solved opening quiz can be used to start its expedition later.
- **The garage.** Choose among earned car models, paints and accessories, or equip a companion. The main garage catalogue has four car models, eight paints and fourteen standard accessories. Some activities unlock special items. Car previews show the actual 3D models. Garage visits and pause preserve active expeditions.
- **Village houses.** Vojtík, Róza, Hugo and Fialka each offer ten tasks with a shuffled mix of quizzes and apple collections. Each success adds a visible improvement such as shutters, a balcony or party decorations. The task order is stable for the saved valley.
- **Collectors and companions.** Farmer Tonda, gardener Vítek, gem collector Jáchym and toymaker Pepa each have twelve off-road expeditions. Deliveries fill the farm pen, flower beds, museum pedestals and playground. Completing a collection earns a companion. Baltazar’s tower has a separate six-piece expedition for the golem. Enabled companions follow the car as it drives and wander nearby with short pauses when it stops.
- **Village shops.** František the baker, Kamil the shopkeeper and Oliver the confectioner offer repeatable deliveries of bread, parcels and pastries. Answer a quiz, follow the road arrows and deliver the order to earn a choice of rewards.
- **Postcards.** The fountain square has an album with six illustrated clues. Stop at an envelope on the road and take a picture of the valley and the car. Solve the card’s quiz to earn a stamp. Three stamps unlock the Valley Map garage sticker.
- **Driving courses.** The Rainbow Gates and Hay Bale Slalom courses each have six gates on outlying road loops. Run either course at your own pace and repeat it from the starting marker. An active delivery stays loaded while driving a course.
- **Forest bandits.** A group of three can run out from cover when the player drives past a suitable forest road. Solve three questions to send them back. After a short break, they can appear beside a different road. The encounter preserves any cargo on the car.
- **Weather and wildlife.** The valley hosts 240 butterflies, birds, rabbits, foxes and deer. Animals move away from the car and eventually return to their home area. Weather cycles between clear skies, wind, rain and storms. The village witch offers weather selection. Tree trunks, branches, crowns, leaves and fruit bend together in wind.

Only one delivery or collection can be active at a time. The car leaves fading tyre tracks and bends meadow grass as it passes. The river stops the car at the water, and the wooden bridge connects the banks. There are no task timers or game-over conditions.

## Saved progress and world generation

A new game creates a random seed for the valley. Roads, bridge position, woods, village plots and task locations vary with the seed. Continuing recreates the same map. The title screen and console display the seed.

Autosaves store the car position, garage choices, pending questions, collected syllables, cosmetics, companions, bandit encounters, postcards and expedition cargo. Starting a new game replaces the previous valley. On Windows the save is usually `%APPDATA%/Godot/app_userdata/MathRacer – Kouzelné údolí/progress.json`. The game writes it atomically and falls back to the previous `.bak` file if the primary save is damaged.

Pass `--seed=NUMBER` after Godot’s `--` separator to reproduce a valley during development. Explicit seeds, diagnostic checks, captures and `--no-save` bypass the player’s save.

## Development and previews

`./build.ps1 -Test` runs gameplay, save, world generation, course, physics and simulated controller checks. The road audits verify connected loops, gradients and bridge clearance. The road-driving tests drive routes in both directions using the car physics.

`./run.ps1 -Capture -View reading` writes `artifacts/reading.png`. Other useful views include `syllables`, `letters`, `challenge-2`, `garage`, `task-choice`, `delivery`, `square`, `slalom`, `museum-full`, `toys-full`, `garden-full`, `farm-full`, `wildlife`, `tree-impact`, `tree-wind`, `vegetation-detail` and `grass`.

The meshes, pictures and environments are generated in code. See the [architecture guide](docs/ARCHITECTURE.md), [algorithms](docs/ALGORITHMS.md) and [validation notes](docs/VALIDATION.md) for implementation details and recorded checks.

The prototype currently has no audio. The camera clears the ground, while buildings can still block its view. The 1080p performance target and the simplified prompts still need real player testing.
