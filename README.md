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
| Handbrake | Space (also interacts when a character, marker or garage is in reach) | X |
| Return to the starting road | R | Y |
| Orbit the camera | Hold the right mouse button and move | Right stick |
| Interact | E, Space or click the on-screen button | A |
| Choose a menu item | Mouse or arrow keys, then E, Space or Enter | D-pad or left stick, then A |
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

Roadside animals, village residents, shops and forest bandits cycle evenly through the enabled quiz types. Postcards have their own word questions, with arithmetic as a fallback. If arithmetic is off, a word quiz replaces cargo counting at handover. Wrong choices leave the current question open for another try. In a syllable quiz, select every syllable to enable **Potvrdit**.

## Exploring the valley

- **Roadside friends.** Ten animals with floating question marks offer an original quiz. After solving it, choose a delivery or apple collection. Both choices start immediately. Delivery carries one to three items to another animal. Collection starts with an empty rack and guides the player to three apples and back. Navigation uses a directional arrow, road arrows and visual progress. Handover pays 10 coins. A solved opening quiz can be used to start its expedition later.
- **Coins.** Finished tasks pay coins instead of car items: 10 for a roadside delivery or the golem, 8 for a shop order and 5 for a house task, collector delivery, postcard stamp, bandit encounter or driving course. Forty-five spinning coins are scattered at random along the roads; each is worth one coin and reappears elsewhere about 40 seconds after it is collected. The counter below the animal tally shows the total.
- **The garage.** Buy car models (40 coins), paints (15) and accessories (20), switch between owned items, or equip a companion. Items you can afford are gold; dearer ones stay grey. Companions are earned by collections, not bought. The main garage catalogue has four car models, eight paints and fourteen standard accessories. Some activities unlock special items. Car previews show the actual 3D models. Garage visits and pause preserve active expeditions.
- **Village houses.** Vojtík, Róza, Hugo and Fialka each offer ten tasks with a shuffled mix of quizzes and apple collections. Each success adds a visible improvement such as shutters, a balcony or party decorations. The task order is stable for the saved valley.
- **Collectors and companions.** Farmer Tonda, gardener Vítek, gem collector Jáchym and toymaker Pepa each have twelve off-road expeditions. Deliveries fill the farm pen, flower beds, museum pedestals and playground. Completing a collection earns a companion. Baltazar’s tower has a separate six-piece expedition for the golem. Enabled companions follow the car as it drives and wander nearby with short pauses when it stops.
- **Materials, works and workers.** The quarry, the clay pit and the log yard each have a worker (Králíček lomař Kvido, Liška hlinařka Hanka, Medvěd dřevorubec Drvoš) and never run out. Talk to one and press play: the worker loads your trailer (a counting question, or a word quiz) and you drive the load to the matching works, where it is tipped into the yellow ring. The sawmill (*Pila*, Medvěd Pilař) turns logs into planks, the brickworks (*Cihelna*, Liška Cihlářka) soil into bricks and the gravel works (*Štěrkovna*, Zajíc Štěrkař) stones into gravel. Each load makes two units. The stock stands in plain view on the works' yard, one bundle, pallet or heap per unit, up to twelve, and starts empty; a full works asks for nothing more. The trailer is lent for every trip and unhitched afterwards. Field paths lead to every site.
- **Four buildings, ten deliveries each.** Bóďa the beaver builds the bridge; the miller (*Mlynář Bruno*) builds a windmill, the stargazer (*Hvězdář Kryštof*) a lookout tower and the fisherman (*Rybář Vilda*) a jetty. Each stands beside its own road or field path. Where a hill or a bank is too steep, the way is cut into the ground. Every delivery takes one unit of goods from a works (or a raw load from the pits and the log yard), so the planks need logs first, and the mill, tower and jetty can all be under construction at once. The builder's panel shows the ten loads and which are done, and which works is short of goods when the next one cannot start. The last delivery of the mill, tower and jetty is special: the farmer gives cloth for the sails, the gem collector the stone tip of the tower and the toymaker the flag of the jetty. Answer the collector's quiz to take it. Finishing a mill, tower or jetty pays 30 coins and a garage item that cannot be bought (a sack of flour, binoculars, the water-blue paint). A finished mill also turns its sails with the wind, and the baker pays 2 coins more for every order. The stargazer lets you look round the valley from the finished tower: the camera turns on the platform while tall beams light the coins and the friends who wait for a quiz, then he rests for a minute. A finished jetty has a little sailing boat that sails up and down the river beside it, and the fisherman's duck becomes a companion that follows the car.
- **The beaver's bridge.** Bóďa lives beside the river, at least 150 m from the main bridge. Roads lead down to the water on both banks. The bridge needs three loads of gravel for the piers and abutments, three of bricks for the ramps (one goes to the far bank over the main bridge), three of planks for the deck and a load of sand. The finished bridge is a shortcut across the river. The trailer then stays in the garage, where it can be hitched or unhitched and painted red or blue. A load slows the car slightly but never falls out.
- **Village shops.** František the baker, Kamil the shopkeeper and Oliver the confectioner offer repeatable deliveries of bread, parcels and pastries. Answer a quiz, follow the road arrows and deliver the order to earn coins.
- **Postcards.** The fountain square has an album with six illustrated clues. Near a roadside envelope, press E, Space or A to take a picture of the valley and the car while driving or stopped. Solve the card’s quiz to earn a stamp. Three stamps unlock the Valley Map garage sticker.
- **Driving courses.** The Rainbow Gates and Hay Bale Slalom courses each have six gates on outlying road loops. Run either course at your own pace and repeat it from the starting marker. An active delivery stays loaded while driving a course.
- **Forest bandits.** A group of three can run out from cover when the player drives past a suitable forest road. Solve three questions to send them back. After a short break, they can appear beside a different road. The encounter preserves any cargo on the car.
- **Weather and wildlife.** The valley hosts 240 butterflies, birds, rabbits, foxes and deer. Animals move away from the car and eventually return to their home area. Weather cycles between clear skies, wind, rain and storms. The village witch offers weather selection. Tree trunks, branches, crowns, leaves and fruit bend together in wind.

Only one delivery, collection or trailer trip is active at a time: accepting a new task switches off the current one. The trailer is hitched only for a ford trip and unhitched afterwards, unless you hitch it for a parade in the garage. The car leaves fading tyre tracks and bends meadow grass as it passes. The river stops the car at the water, and the wooden bridge connects the banks. There are no task timers or game-over conditions.

## Saved progress and world generation

A new game creates a random seed for the valley. Roads, bridge position, woods, village plots and task locations vary with the seed. Every seed gets the beaver's bridge with its sources, the log yard and all three works: if the strict placement rules leave no room, the search relaxes them step by step. Continuing recreates the same map. The title screen and console display the seed.

Autosaves store the car position, garage choices, coins, pending questions, collected syllables, cosmetics, companions, bandit encounters, postcards, expedition cargo, ford progress, the trailer and the stock of every works. Starting a new game replaces the previous valley. On Windows the save is usually `%APPDATA%/Godot/app_userdata/MathRacer – Kouzelné údolí/progress.json`. The game writes it atomically and falls back to the previous `.bak` file if the primary save is damaged.

Pass `--seed=NUMBER` after Godot’s `--` separator to reproduce a valley during development. Explicit seeds, diagnostic checks, captures and `--no-save` bypass the player’s save.

## Development and previews

`./build.ps1 -Test` runs gameplay, save, world generation, course, physics and simulated controller checks in parallel Godot processes: one per logical processor, at most sixteen. Road driving runs one process per route, and scenarios start roughly longest first. Use `-TestJobs 1` to run them in sequence or set a different concurrency limit with `-TestJobs`. Each scenario keeps its log and any test saves in its own folder under `artifacts/game-tests/`. The road audits verify connected loops, gradients and bridge clearance. The road-driving tests drive routes in both directions using the car physics.

C# code is formatted with [CSharpier](https://csharpier.com/), pinned in `dotnet-tools.json` and configured in `.csharpierrc.json` (120-character lines). Run `dotnet tool restore` once, then `dotnet csharpier format Scripts` before committing; `dotnet csharpier check Scripts` reports unformatted files.

`./run.ps1 -Capture -View reading` writes `artifacts/reading.png`. Other useful views include `syllables`, `letters`, `challenge-2`, `garage`, `task-choice`, `delivery`, `square`, `slalom`, `museum-full`, `toys-full`, `garden-full`, `farm-full`, `ford-0`, `ford-3`, `ford-6`, `bridge-roads`, `quarry`, `log-yard`, `mill-4`, `mill-10`, `tower-7`, `tower-10`, `jetty-6`, `jetty-10`, `sawmill`, `brickworks`, `gravel-works`, `works-full`, `works-panel`, `trailer`, `beaver`, `load-question`, `wildlife`, `tree-impact`, `tree-wind`, `vegetation-detail` and `grass`.

The meshes, pictures and environments are generated in code. See the [architecture guide](docs/ARCHITECTURE.md), [algorithms](docs/ALGORITHMS.md) and [validation notes](docs/VALIDATION.md) for implementation details and recorded checks.

When a Czech system voice is available, the game reads short instructions, highlighted menu choices and some activity announcements aloud. Syllable tiles stay silent. The camera clears the ground, while buildings can still block its view. The 1080p performance target and the simplified prompts still need real player testing.
