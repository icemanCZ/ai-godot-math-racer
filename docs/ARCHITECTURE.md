# Architecture and extension guide

MathRacer is a code-only Godot 4.7.2 .NET project. The root node in `Main.tscn` runs the `Game` script. C# generates the
world, illustrations and interface at startup. Player-facing text is Czech.

## Source layout

A folder under `Scripts` is a namespace: `Scripts/Saves/SaveStore.cs` is in `MathRacer.Saves`. The names are chosen so
that no namespace has the name of a type (`World`, `Village` and `Game` are classes, hence `Valley`, `Settlement` and
`App`), and `contract-checks` fails when a file is in the wrong namespace, lies loose in `Scripts`, or a namespace
clashes with a type. A class that is written in several files (`Game`, `GameUi`, `World`, `Village`, `BuildProject`,
`Works`, `HousePlot`) has all its parts in the folder of the class, with one exception:
`Diagnostics/Game.Diagnostics.cs` is a part of `Game` and so is in `MathRacer.App`, which is why it can be left out of
the release build together with its folder.

| Folder (namespace `MathRacer.<folder>`) | What lives there |
| --- | --- |
| `App` | `Game` and every `Game.*.cs`, `Texts`, `Interaction`, `GameControls`, `Settings`, `LaunchOptions`, `GameState` |
| `Ui` | `GameUi` and every `GameUi.*.cs`, `UiIntent`, `DeliveryPicture`, `TaskMarker` |
| `Graphics` | `Art` (procedural meshes and icons), `Obstacles` (collision helpers), `Celebration`, `DistantDetails` (visibility ranges of small meshes, no shadow below half a metre), `StaticMeshes` (merges the village's plain parts per parent and look) |
| `Quizzes` | `IChallenge` and the generators of the five kinds of question (`Mathematics`: arithmetic and comparing numbers; `Letters`), `QuizType`, `QuizSession` |
| `Saves` | `SaveData` and its sections, `SaveDataV4`, `SaveRules`, `SaveMigrations`, `SaveStore`, `PostcardProgress` |
| `Valley` | `World` and every `World.*.cs`, the terrain, roads, river, vegetation, grass, weather, wildlife |
| `Settlement` | `Village` and every `Village.*.cs`, `HousePlot`, `PlotPlans` |
| `Buildings` | `BuildProject`, `BuildPlans`, the mill, the tower, the jetty, the ford, the boat, the works |
| `Tasks` | `HaulTrip`, `ITripTarget`, `Expedition`, `RouteGuide`, `TaskSlot`, `DrivingEvents` |
| `Vehicles` | `Vehicle`, `VehicleModel`, `FollowCamera`, `WheelTracks`, `Trailer` |
| `Garage` | the car items and looks (`Customization` of old), `CarRewards`, `Economy` |
| `Characters` | `Character`, the roles, `ActorIds`, `CharacterCatalog`, the companions and the golem |
| `Diagnostics` | the headless checks, the capture views and `DiagnosticSuites` |

The namespaces depend on one another in both directions (every one of them can reach every other), mostly because
`Settings`, `GameState` and the constants of `Game` live in `App`. `docs/REFACTORING_PLAN.md` lists what a first step
towards layers would be.

## Module boundaries

### `Game` and its feature partials

Compose the scene, handle input and state transitions, coordinate activities and restore the active panel. The state
that more than one feature shares is in four small objects that hold no scene nodes: `Interaction` (the state, the state
before a pause, the character the player is talking to), `QuizSession` (which quiz kinds are on, the seeded generators,
the open question, the new question after a wrong answer and the trip that waits for it), `TaskSlot` (the one delivery,
collection or trailer trip under way, and how a new one replaces it) and `Economy` (coins, owned garage items, the coins
of the open reward panel). `Game` keeps the properties its features already used (`State`, `Delivery`, `Haul`,
`CurrentChallenge`, `Coins`), which forward to them. Per-frame work is a list (`Game.Systems.cs`, `RegisterSystems`) and
the panel of each interaction state is registered in `Game.Panels.cs`; a new state or tick is added there, and
`contract-checks` fails when an interaction state has no panel.

### `ActorRole` and its subclasses (`ActorRoles.cs`)

Give each character one role: roadside friend, house resident, trader, witch, sorcerer or collector (farmer, gardener,
gem collector, toymaker). A role picks the `Game` flow for talking to the character and for its solved quiz, and
supplies its marker icon and talk hint. Every builder, source and works role has an icon of its own, drawn as a vector
outline by `Art.RoleTaskIcon`; sites without a worker (the sand pit, every second site) and the two driving courses put
the same kind of icon on a `TaskMarker`. New character types add a role instead of new `Is…` checks in `Game`.

### `CarRewards` and `RoadsideProgress`

Hold the owned garage items and the player's coins that buy them (prices live on `CarReward`), and which roadside
friends have unlocked or finished their expedition. `WorldCoins` scatters collectible coins along the roads; their
positions are not saved. `Game` owns one of each and saves them.

### `GameUi` and its feature partials

Build panels and HUD elements, manage menu focus and emit player intents. The partials are split by area (`Input`,
`Challenges`, `Residents`, `Garage`, `Hints`, `DeliveryHud`, and one per feature); a button never calls the game, it
sends a `UiIntent` through the single `Intended` event and `Game.HandleIntent` (`Game.Intents.cs`) is the one switch
that answers it. A new button is a new `UiIntent` record and a new case there.

### Panels for children who cannot read

The players are small children who do not read yet, so a panel is made to be understood from pictures and the voice;
its text is only for a grown-up looking on. A panel offers a task or a ride with the green check mark
(`GameUi.AcceptButton`, its words read aloud when it is chosen) and leaves with the red X (`BackButton`); no other
picture is used for these two answers. What to do and what the check mark does is said aloud by the character when the
panel opens (`Speak`), not written as a paragraph. Progress is a row of pictures that light up (loads, stamps, sheep,
prints, medals) or a picture with a count; the stamps of a keeper are `KeeperStamps`, and the pet that a task brings is
`PetGoal`: what is counted, how many of how many, an arrow and the pet itself in a small 3D portrait, grey until it is
earned. A place is the picture of its border (`BorderIcon`: the sea, the mountains, the river, the forest), keys are
drawn as key caps with arrows (`BalloonKeys`), and a corner panel during a drive shows its state in pictures as well,
like the strip of a rally stage with its gates and the car (`RallyStrip`).

### `Texts`

The fixed Czech texts of `Game*.cs` and `GameUi*.cs` as constants (names made from the text without diacritics). Texts
with values inside are methods (`Texts.NewPet(name)`). The texts of data tables (`CharacterCatalog`, `Works`,
`Village.*`, the word lists of the questions) stay next to the colours and numbers of their entry.

### `World`, `WorldLayout` and `Village`

Generate seeded routes, plots and interactive actors. `World` registers actors under stable save IDs and exposes them
for proximity checks and placement. Names, greetings and colours of recurring characters live in `CharacterCatalog`; its
order assigns actor IDs.

### `LaunchOptions` and `Settings`

`LaunchOptions` is the only reader of the command line. `Settings` holds tuning constants and the only state that
outlives a scene reload: the world seed and the quiz settings carried into a new game.

### `WorldTuning.CoreRadius`, `WorldTuning.CorePlayableRadius` and `ValleyBounds`

The old valley, the core, is the terrain square of ±555 m and the playable circle of 525 m that every saved valley was
made with; its generation keeps to these two constants, so that the valley can grow around it without moving anything
(`docs/MAP_EXPANSION_PLAN.md`). `ValleyBounds` is where the valley ends while it is played, a square of ±1130 m: the car
stops at its edge (and is put back on the road if it ever gets 30 m beyond it), and the grass, the lamps, the fireflies,
the wild animals and the hunt keep inside it. `contract-checks` lists the files that may ask it. Round the core lies the
outland (`Terrain.Outland.cs`): one grid of 2.5 m up to `WorldTuning.OutlandRadius` (±1275 m), worked out once in
`BuildMeshes` from the final core and drawn in 80 m chunks up to 260 m and in 320 m blocks of 10 m beyond, then coarse
scenery of 10 m up to `SceneryRadius` (±1675 m) and a ring of 25 m to the horizon (3 km). Its collision is streamed
round the car (`StreamColliders`, the system "outland colliders"). Its meshes only draw it, so they are built after the
valley, a slice at a time (`BuildOutlandLand`), like the verges of the roads: `World.FarParts` (`DeferredBuild`) runs
them in the system "far parts", a run without a window drops them, and a capture or the benchmark builds them at once.
`RiverCourse` keeps the course of the core and eases the river beyond it onto a slow bend without random numbers.
`ValleyBorders` (made last in `WorldPlan`, with random numbers of its own) shapes the natural borders beyond ±700 m
along either axis and nothing nearer: mountains in the north where both rivers rise, the sea with a beach in the south,
a meandering border river on the river's side (`WorldPlan.Side`) and wooded hills on the other; each line waves within
fixed bounds, so that it never enters `ValleyBounds`. `BorderWater` draws the sea and the border river, `BorderForest`
the trees of the borders in MultiMesh batches without collision. `outland-checks` checks all of it, and the views
`border-*` of `run.ps1 -Capture` show it.

### `World.Outland.cs`, `OutlandLayout`, `OutlandRoadbed` and `Vegetation.Outland.cs`

The land between the core and the borders (`docs/MAP_EXPANSION_PLAN.md`, phase 4) is made after everything of the core,
with random numbers and counters of its own, so that the core's fingerprint stays as it is. `OutlandLayout` (in
`WorldPlan`) places a ring road round the core and its two bridges over the river. `World.PlanOutlandRoads` lays the
roads beyond the core: the ring, up to four joins from the old roads through ground where nothing of the core stands
(`OccupiedSpots`), and a road out to each border. They are a network of their own, `World.OutlandRoutes`, beside
`World.Routes`. `OutlandRoadbed` grades them: it smooths them, keeps the grade gentle both ways, pins their ends to the
old road they join and keeps them above the river's floodplain (`River.ValleyCeiling`). The outland ground is shaped
under them; the core is not, but for one strip: an old road is cut into its slope, so where a join leaves it
`Terrain.GradeJoins` (`Terrain.Joins.cs`) cuts a ramp of its own up out of the cut, off the road's surface and the
river, short of the 500 m grid, and through every point of the fingerprints' 25 m grid at that point's own height, and
`Vegetation.ClearDrawn` takes the flowers and their rocks off the joins; `route-checks` drives every join.
`Vegetation.GenerateOutland` only places the outland's plants at the core's density, with counts and tree positions of
its own (`OutlandTreeCount`, `OutlandTreePositions`, which `NearTrunk`, the grass, the animals and the wayside use), and
draws every tree once as one simple shape (`BuildDistantTrees`, from 300 m). The plants themselves, with their trunks,
rocks and trampling, are built a 64 m block at a time round the camera and then kept (`StreamOutland`, the system
"outland plants"; `BuildOutlandAround` for the checks), with random numbers of the block and batch keys of their own
(`BatchKey.Group`), since their exact shapes need not be the same from one load to the next. A built block shows full
shapes up to 130 m and simpler ones without outline up to `OutlandDetailRange` (300 m). All of it is drawn under
`OutlandPlants`, which `benchmark.ps1 -Off outland-plants` hides. A tree keeps where its parts are as runs in its
batches (`TreeRun`) and reads their resting places back from the batches only when the car hits it.
`Wildlife.GenerateOutland` and the second `Wayside` (`OutlandWayside`) do the same for animals and wayside things, and
the grass grows up to the edge of the valley. `Tests/Worlds/seed-N.outland.txt` (`WorldFingerprint.OutlandLines`) pins
all of it. `outland-checks` checks the roads, the bridges and the plants, and `benchmark.ps1` drives the ring
(`jizda-pas`) and the roads to the borders (`jizda-k-hranicim`).

### `Terrain`, `Roads`, `RoadGrading` and `River`

Provide elevation, road surface and clearance data for meshes, collision, placement, navigation and driving. `Verges`
draws the gravel verge beside every road (`RoadVerge.gdshader`, the same gravel as the road's last metre in
`Gravel.gdshaderinc`): `Roads.BuildVerges` and `Tracks.BuildVerges` build it last in `World.Generate`, on the final
ground; it is no part of the drivable surface, fades out where a field way begins, and field paths open out into a
rounded mouth that turns into the road's dirt (`RoadDirt.gdshaderinc`). The grass is shorter on the verge and absent
from its packed gravel, but `Grass.CanGrow` is unchanged, so nothing placed moves. Buildings cover their wall boxes with
`Art.Facade`/`Art.Wall` (`Facade.gdshader`: plaster with brick patches, bricks, upright boards, overlapping boards, or
shingles on roof panels), cottages and plots use `Art.RoofTiles` (`RoofTiles.gdshader`) and the village walkways
`Walkway.gdshader`; cottage fences have round knobs, hearts and a flowering arch over the gate (`Village.GateArch`,
scenery only). `Wayside` scatters dandelions, toadstools and pebbles just beyond the verges from a random generator of
its own, last in `World.Generate`, so nothing else moves. Characters (`Character._Ready`, deferred until they are
dressed), cars (`VehicleModel.BuildDisplay`) and the trailer get a soft outline from `Art.Outline` (`Outline.gdshader`,
the meshes' overlay); `World.OutlineScenery` gives it to the buildings, fences, rides, zoo, train, boat, pets and hidden
friends (parts smaller than 0.3 m left out on buildings), the plants have `FoliageOutline.gdshader`, which sways with
them (`FoliageSway.gdshaderinc`, shared with `Foliage.gdshader`), and `Wildlife` outlines only the animals within 45 m
of the car. Cottages and finished plot houses smoke (`Art.ChimneySmoke`), `Flutters` keeps butterflies (fine days) and
wind-blown leaves round the car (system `flutters` in `Game.Systems.cs`), the clouds are shaded puffs (`Cloud.gdshader`)
that drift round the valley, and the sun's shadows are softer and lighter.

### `Vehicle`, `FollowCamera` and `VehicleAppearance`

Handle movement, camera and cosmetic state. `VehicleModel` shares geometry between gameplay and garage previews.

### `Expedition` and `DrivingEvents`

Handle cargo and vehicle courses.

### `RouteGuide`

Plans the road route to one target and owns the road arrows and the final off-road arrow. Expeditions and ford trips
each create one.

### `Trailer`, `Ford`, `BuildProject` and `World.Ford.cs`

The towed trailer follows the car's hitch along `World.DriveSurface` without collision of its own; it is lent for every
trip and won as the bridge's reward. `BuildPlans` lists the ten deliveries of each of the four buildings. `BuildProject`
holds the stage; `Ford` (the bridge) is one, its deck becomes solid after the tenth. `World.PlanFord` plans the ford,
its sand pit, quarry and clay pit and the beaver. `Game.Ford.cs` runs the trips, which share the single task slot with
expeditions (`Game.TaskActive`).

### `HaulTrip`, `Works` and `World.Works.cs`

`HaulTrip` is one trailer trip (source, loading question, drop ring) for an `ITripTarget`: a `BuildProject` (a
building), a `Works` (raw material), a `HousePlot` (a building plot) or a `HouseTripTarget` (a house's planks or
bricks). The target gives the load, the drop ring and the contractor, and does what the finished delivery does
(`FinishDump`); `Game.Trips.cs` starts every trip (`StartTrip`) and carries on from the quiz that comes before it
(`StartDeparture`). A new kind of trip is a new target, one name in `TripKinds` and a way to find it again from a save
(`Game.TripTarget`). `World.PlanIndustry` places the log yard and three `Works` (sawmill, brickworks, gravel works;
`WorksCatalog` holds what each takes in and makes). The works first try the village edge (`World.WorksBand`, measured
with `Village.PlotDistance`, which the quiet belt around the houses would otherwise forbid) and fall back to passes that
relax their spacing, slope and road rules until all fit. `World.PlanExtraSources` then adds a second site for each raw
material (`ExtraSources`, at least 110 m from the first); `HaulTrip.Source` is the nearest site when the trip starts and
`World.SourceSites` lists them all, so an empty trailer is loaded at whichever one it reaches, and the trip adopts the
site it reached. Each second site has a worker (`SourceRole` for the load, IDs 54–57 for stones, soil, sand and logs;
the sand pit's worker sends the sand to the glassworks and asks the bridge's loading question): `World.PickupActor(load,
source)` names whoever asks, and `World.AskerIds` lists the IDs a saved loading question may come from. The quarry, clay
pit and log yard have workers (`SourceRole`, IDs 51–53) who start supply trips and ask the loading question; each works
has a worker (`WorksRole`, IDs 45–47) who shows the stock and loads goods. A stock of up to 12 units is shown one stack
or heap per unit. `Works.Take` is how a building takes goods.

### `SitedProject`, `Mill`, `Lookout`, `Jetty` and `World.Builds.cs`

The mill, lookout tower and jetty are `SitedProject`s: each stands at a `BuildSite` (unloading ring, road point,
direction, builder spot) and has ten stage nodes, with solid parts that only block the car once built.
`World.PlanBuilds` picks the sites (the tower and mill try the band near the village, `World.LandmarkBand`, preferring
high ground and short distance, then anywhere; the jetty a gentle bank) with a way from a road, in passes that allow
deeper cuts. `PlanWay` keeps the way within `PathMaxGrade` by cutting it into the slope; `Terrain.Grade` applies the
cuts before anything is placed on the terrain, so `PlanFord` plans everything first. Builders (`BuilderRole`, IDs 44 and
48–50) give the work. The special last load comes from a collector, whose quiz loads it (`Game.CompleteChallenge`).
`Game.Rewards.cs` holds the completion rewards (coins, the garage items from `CarAccessory.FlourSack`,
`CarAccessory.Binoculars` and `CarLook.Water`, the baker's bonus and the mill's sails); `Game.Lookout.cs` the look from
the tower (`FollowCamera.ShowPanorama`, `WorldCoins.Flash`, `Character.Ping`): on a starry night the view is lifted to a
`Constellation`, by day `HiddenFriends.Ping` gives every friend who still hides a beam and a fixed-size orange question
mark seen through the trees; the roof is on `Lookout.RoofLayer`, which the camera leaves out while it stands under it
(`run.ps1 -Capture -View lookout-day` or `lookout-night` shows it); `Boat` the jetty's boat. The jetty's duck is a
`CollectorPet` of kind `Duck` owned by the world (`World.Duck`, `World.AllPets`), whose count is the jetty's stage.

### `HousePlot`, `PlotPlans` and `Village.Plots.cs`

The village has six building plots (`PlotPlans.Count`). `Village.GeneratePlots` runs last, after the square and before
the architectural details, so the rest of the village keeps its old positions: it lists lots beside every road, nearest
to the village centre first, takes the first six that are clear (`PlotIsClear`: off roads and the river, clear of
everything reserved so far and of the roadside benches and signs) and reserves it (`IsReserved`, so trees and later
placement avoid it). It tries ground inside the village's existing terrain platform first and leaves the shared `plots`
list alone, so the platform and the search for the works and landmarks are unchanged. `HousePlot` is one lot: a
staked-out site with a newcomer (`PlotResidentRole`, actor IDs from `World.FirstPlotResidentId`,
`CharacterCatalog.PlotResidents`) and six stage nodes (slab, lower walls, upper walls, roof frame, roof, finishing),
with solid parts that block the car once built. `PlotPlans.CargoOf` lists the six loads (gravel, two of bricks, three of
planks), always goods from a works. A plot's trip is a `HaulTrip` whose drop ring lies in front of the lot, on a walkway
to the street; `Game.Plots.cs` runs it in state `PlotStatus`, shares the task slot with the other trailer trips, sends
the player for raw material when the works is empty (`BeginMissingSupply`) and, after the sixth load, pays `PlotCoins`
and lets the newcomer walk to the doorstep.

### `Playground`, `PlaygroundPlans` and `World.Playground`

The village playground, a sixth kind of trailer trip (`TripKinds.Playground`, `Departure.Playground`,
`GameState.PlaygroundStatus` 22, actor ID `ActorIds.Playground` 67 for its keeper, `PlaygroundRole`). It is planned last
in `PlanFord` with its own seeded search (`Settings.Seed + 2390`) and, like the glassworks, the farm and the bakery,
reserves no ground: the plants on `InPlaygroundZone` are taken out afterwards (`Plants.Generate`) and
`CreateHiddenFriends` skips the zone, so no older actor, tree or friend of an existing valley moves. The search tries
the village edge (`WorksBand`, then `PlaygroundBand`) with the strict rules, then looser ones (`PlaygroundPasses`) and
finally anywhere on the bank, so that every valley with works gets one; `YardIsFree` rejects a spot whose yard or whole
field path would touch the yard of a works or of another site (`FindSpur`'s `acceptWay`). `PlaygroundPlans` lists the
six loads, always goods (gravel, planks, planks, bricks, planks, bricks). `Playground` is scene-only: six stage nodes,
each with one toy on a pad of its own and the small animal (a scaled `Character`, never registered as an actor) who
plays on it; `_Process` swings the swings, tilts the seesaw, turns the roundabout and walks the slide's child through
its cycle, and `Tick` makes the children cheer when the car comes within 30 m. The progress is
`SaveData.Playground.Stage` (rule `HasInvalidPlaygroundState`, trips of kind `playground` with index 0,
`LegacySaves.AddedAfterVersion5`). `Game.Playground.cs` runs the trips like `Game.Plots.cs`; finishing pays
`PlaygroundPlans.Coins` and the accessory `CarAccessory.Balloons`. Checked by `playground-checks` (and
`content-checks`).

### `Railway`, `RailwayPlans` and `World.Railway`

The little railway round the playground, a seventh kind of trailer trip (`TripKinds.Railway`, `Departure.Railway`,
`GameState.RailwayStatus` 25, actor ID `ActorIds.Station` 70 for its stationmaster, `StationRole`). It needs no search
of its own: `World.BuildRailway` raises it at the end of `World.Generate` on the playground's site, in the yard's frame
(+Z away from the road), and everything lies inside the ground that `InPlaygroundZone` already clears (a rounded
rectangle of track from x -12 to 12 and z -3.5 to 19.8, the station beside the ring at x 3.6 to 8.8, the tunnel hill on
the front straight at x -7.9 to -3.5, left of the path to the ring), so no tree, friend or older actor moves and the
world fingerprints hold. The ring where the load is tipped out is the playground's own (`Railway.Drop`, `Road`), and the
stationmaster stands on the platform, further from the ring than the playground's keeper so that she is still the one
nearest there. The stationmaster is registered last, next to the mayor and the lamp maker, and left out of the actors
that postcard stops and driving courses keep clear of. `RailwayPlans` lists the six loads, always goods (gravel, planks,
bricks, planks, bricks, planks). `Railway` is scene-only: `BuildTrack` walks the loop once and keeps a point every half
metre with the smoothed height of the ground; the bed, the sleepers and the rails are single meshes built from those
points, so the train (an engine and two wagons that follow it by arc length, wheels turning, steam puffs from a pool of
nine, "Tuuut!" when it leaves the tunnel or when the car arrives) runs on exactly the same line. The parts come with the
deliveries: bed, sleepers and rails with the crossing signs, the station with platform and clock (the engine sets off
after the third delivery), bench, lamp, semaphore and the first wagon, the tunnel, the second wagon with flags. The
station and the tunnel hill are solid once built; the track is not. The progress is `SaveData.Railway.Stage` (rule
`HasInvalidRailwayState`, trips of kind `railway` with index 0, `LegacySaves.AddedAfterVersion5`). `Game.Railway.cs`
runs the trips like `Game.Playground.cs`; finishing pays `RailwayPlans.Coins` and earns the pet
`CollectorPetKind.Stationmaster` (Výpravčí Píšťalka, a world pet owned once `Railway.Stage` is 6, made in
`World.BuildRailway` and waiting on the platform; the kind is saved by name). Checked by `railway-checks` (and
`content-checks`, `contract-checks`); `run.ps1 -Capture -View railway-N` or `railway-close` shows it.

### `Zoo`, `ZooPlans` and `World.Zoo`

The village zoo, an eighth kind of trailer trip (`TripKinds.Zoo`, `Departure.Zoo`, `GameState.ZooStatus` 26, actor ID
`ActorIds.ZooKeeper` 71 for its keeper, `ZooRole`). It is planned last in `PlanFord`, after the playground, with its own
seeded search (`Settings.Seed + 2391`) and, like the playground, reserves no ground: the plants on `InZooZone` are taken
out afterwards and `CreateHiddenFriends` skips the zone, so no older actor, tree or friend moves and the world
fingerprints hold. It is much larger than a works (31 m deep, 28 m wide), so besides `FindSpur`'s rules the search tests
the whole area (`ZooFootprintFits`: water, houses, roads, steep ground, friends) and keeps clear of the playground
(`AreaIsFree`, which `YardIsFree` now shares). The keeper is registered last, after the stationmaster. `ZooPlans` lists
twelve loads of goods: **every one of the six enclosures needs two deliveries of two different goods**
(`ZooPlans.PenOf`); the first builds its fence and ground (`stages[2 * pen]`), the second brings the animals and their
fittings (`stages[2 * pen + 1]`). `Zoo` is scene-only: it begins as a construction site (`marks`: tape, barrier,
counting sign, heaps, mixer, hut and a digging excavator) that is cleared with the last delivery, when the gate (`gate`)
appears. Every animal is a `Beast` (home, pacing, an `Animate` callback that moves its parts: waddling penguins, a
swinging monkey, an elephant whose spray is a pool of twelve drops, a nodding giraffe, flamingos that dip their necks, a
panda that rolls over) and all of them hop when the car comes within 34 m. Three visitors walk the path. The progress is
`SaveData.Zoo.Stage` (rule `HasInvalidZooState`, trips of kind `zoo` with index 0, `LegacySaves.AddedAfterVersion5`).
`Game.Zoo.cs` runs the trips like `Game.Playground.cs`; finishing pays `ZooPlans.Coins` and earns the pet
`CollectorPetKind.PandaCub` (Pandička Bambulka). Checked by `zoo-checks` (and `content-checks`, `contract-checks`);
`run.ps1 -Capture -View zoo-N`, `zoo-close` or `zoo-pet` shows it.

### `TreasureRole`, `TreasureHunt`, `TreasureSite` and `World.Treasure`

The pirate captain's treasure hunt, an activity without building. The captain (`ActorIds.Treasurer` 72, `TreasureRole`,
`GameState.TreasureStatus` 27, `Village.Treasurer`) stands on the village square behind the fountain, in the square's
frame and without random draws like the mayor and the lamp maker (more than `InteractionRange` from both), is registered
after the lamp maker and is left out of the actors that postcard stops and driving courses keep clear of. Talking to her
opens a panel with a green check mark (`UiIntent.StartTreasureHunt`) that gives out **one** treasure: `TreasureHunt`
(scene-free; `Found` and `Active`) is `Active` from then until its chest is open, and a new treasure is given out only
when the player drives back to her, never by itself. The place of treasure n is `World.TreasureSpot(n)`: the valley's
seed and the places before it decide (golden angle round the village, four distances, off roads, clear of everything
built, level enough to drive up), computed from a list that is built on demand, so only `SaveData.Treasure` (`Found`,
`Active`; rule `HasInvalidTreasure`; `LegacySaves.AddedAfterVersion5`) is saved and no generated object moves (the world
fingerprints hold). While a hunt is on, `Game.TickTreasure` shows the detector in the corner (`GameUi.TreasureHud`:
word, colour and bar from `TreasureHunt.WarmthAt` and `Closeness`) and beeps (`MakeBeep`, a generated tone; pitch and
rate grow as the car nears). `TreasureSite` is scene-only: a heap of earth, a red cross that shows within 22 m, a chest
with a swinging lid and sparkles. Within `TreasureHunt.DigRadius` the talk button digs (`Game.Interact`,
`TreasureDigAvailable`): the chest asks an ordinary question on the captain's behalf (`nearby` is the captain, state
`Challenge`, saved like any question; `TreasureRole.CompleteQuiz`), and the right answer opens the chest, pays 20 coins
(40 for every fifth, `TreasureHunt.Coins`) and ends the hunt. Checked by `treasure-checks` (and `contract-checks`);
`run.ps1 -Capture -View treasure-captain`, `treasure-hunt` or `treasure-chest` shows it.

### `FireflyHunt`, `FireflyField`, `World.Fireflies`, `Game.Fireflies` and `StarPicture`

The witch's firefly hunt on a starry night, an activity without building and without a new character: it is switched on
only at the witch, with a button of its own. When `Weather.StarsOut` the witch opens a panel (`GameState.FireflyStatus`
28, `Game.OpenWeatherChallenge` → `OpenFireflies`; by day she still asks the weather question at once) with the
collection (`GameUi.FireflyPanel`, one `StarPicture` for every `Constellation`), a green check mark
(`UiIntent.StartFireflyHunt`, `BeginFireflyHunt`), a sun (`UiIntent.ChangeWeather`, `BeginWeatherQuiz`: the old question
and weather choice) and the way back. `FireflyHunt` (scene-free) counts what is caught: `Caught` (saved as
`SaveData.Fireflies`, rule `HasInvalidFireflies`, `LegacySaves.AddedAfterVersion5`) gives the stars that shine
(`PerStar` 10 fireflies each, in the order of the points of the pictures in `Constellation.All`: `StarAt`, `LitStars`),
the lantern's strength and whether the collection is `Complete`; `Active` is never saved and ends with the night
(`Game.TickFireflies`, which also draws the collection once the stars are out and after every load). `FireflyField` (a
child of the world, `World.Sparks`; `Setup(vehicle)` is called in `Game._Ready`) holds the lantern on a pole above the
car (a halo and an `OmniLight3D` that grow with the strength) and six catchable fireflies that blink and drift above
ground that `World.FireflySpot` finds 14 to 38 m from the car (open ground, off the water and the sites; a random
generator of its own, nothing is generated from the seed, so the world fingerprints hold); driving within `CatchRadius`
catches one, which flies into the lantern, and a new one appears. `Atmosphere.ShowCollection` hangs the collection in
the night sky, each picture at its own turn of the sky (`CollectionAzimuth`): shining stars are bright and joined by
lines, the others are faint dots, and the newest swells for five seconds. Checked by `firefly-checks` (and
`contract-checks`); `run.ps1 -Capture -View fireflies`, `fireflies-sky` or `fireflies-witch` shows it.

### `HunterRole`, `AnimalHunt`, `HunterPen`, `HutPlans`, `HuntField` and `World.Hunter`

The hunter at the edge of the village (`ActorIds.Hunter` 73, `HunterRole`, `GameState.HunterStatus` 29, Jezevec lovec
Hubert), with a big fenced pen beside a road. `PlanHunter` runs after `PlanFord` and before the plants with its own
seeded search (`Settings.Seed + 2393`; the village's edge first, 35–150 m from the houses, then wider and steeper) and
reserves no ground: the plants on `InHunterZone` are taken out afterwards and hidden friends and fireflies skip it, so
nothing older moves and the world fingerprints hold. **The hut comes first**: `HutPlans` lists four loads of goods
(gravel, planks, planks, bricks) that raise the log hut in the pen in stages (`HunterPen.SetStage`: stakes, foundation,
walls with door and window, roof, smoking chimney), a ninth kind of trailer trip (`TripKinds.Hunter`,
`Departure.Hunter`, `HunterPen` is the `ITripTarget`, `Game.HunterHut.cs` like the zoo's, coins `HutPlans.Coins`); a
valley without the works has the hut standing. Then the hunter's panel (`UiIntent.StartHunt`) sends the player out with
a net: `AnimalHunt` (scene-free: the animals in the pen by kind, whether a hunt is on, the animal on the car) and
`HuntField` (the herd of three rabbits, foxes or deer in a meadow 170–320 m away that flee a close car, the net bundle
on the roof, the flying, lying or wrapping net and its beacon). The space bar throws the net (`Game.Interact`,
`NetThrowAvailable`; aimed at the animal ahead within 20 m, else 11 m ahead): on an animal it wraps it, the talk button
loads it (`LoadCatch`) and driving to the gate (or talking to the hunter) takes it into the pen, which ends the hunt; a
miss lies in the grass to be picked up (`PickUpNet`), one in the river is lost, and the hunter gives a new net for a
lost or abandoned one. `GameUi.HuntHud` points to the next goal. Eight animals earn the pet `CollectorPetKind.Fawn`
(Koloušek Puntík). Saved as `SaveData.Hunt` (`Penned`, `Active`, `Carrying`, `Hut`; rule `HasInvalidHunt`, trips of kind
`hunter` with index 0, `LegacySaves.AddedAfterVersion5`); where the net and the herd are is not saved, so after a load
the net is on the car and the herd runs in a new meadow. Checked by `hunt-checks` (and `contract-checks`).

### `BorderSites`, `World.Borderfolk`, `BorderKeeperRole`, `BorderHome` and `RouteGuide` over both networks

The four keepers of the borders (`ActorIds.FirstBorderKeeper` 74 to 77 in the order of `Border`: Sea, Mountains, River,
Forest; `BorderKeeperRole`, `GameState.KeeperStatus` 30, `CharacterCatalog.BorderKeepers`). `PlanBorderfolk` runs after
the outland roads are laid and before their plants, without random numbers: each keeper's homestead (`BorderHomeSite`,
`BorderHome`) stands at the end of the road to its border (`World.BorderRoads`), the lighthouse's place
(`LighthouseSite`) on the shore beyond the keeper of the sea and the coast quarry (`CoastQuarrySite`, `CoastQuarry`)
beside the road to the sea. `InBorderfolkZone` keeps the outland's plants, grass, wayside things and wild animals off
them; trees are still planned there, so `OutlandTreePositions` and the outland fingerprint do not change, and the new
places are held by `Tests/Worlds/seed-N.coast.txt`. `RouteGuide` runs Dijkstra over the core's and the outland's roads
at once, so every arrow leads to the borders. Checked by `borderfolk-checks` and `route-checks`.

### `Post`, `LetterTrip`, `PostmanRole`, `PostOffice`, `World.PostOffice` and `Game.Post`

The post to the four corners (`docs/POST_AND_LIGHTHOUSE_PLAN.md`). The postman (`ActorIds.Postman` 78, Zajíc pošťák
Pepa, `GameState.PostStatus` 31) stands in front of his post office (`Buildings/PostOffice.cs`) beside a village road.
`PlanPostOffice` runs after `PlanHunter` and before the plants, without random numbers, nearest the square, with every
point of the house within 90 m of a house of the village (hidden friends, treasures, fireflies and herds keep
`Village.TaskAnimalClearance` from every house, so none of them moves); the plants on `InPostOfficeZone` are taken out,
and grass, wayside things and wild animals skip it. Without a place the postman stands on the square
(`Village.PlacePostmanOnSquare`). His panel shows the album of stamps and gives out one letter (`UiIntent.TakeLetter`)
for the keeper with the fewest; `LetterTrip` draws the arrows and `GameUi.LetterHud` the envelope. At the keeper an
ordinary question hands the letter over (`Game.CompleteKeeperQuiz`: a stamp, `Post.LetterCoins`, the full album
`Post.AlbumCoins`) and gives a reply for a house of the village (`Post.ReplyCoins` when it is brought). A stamp from
every keeper earns the pigeon (`CollectorPetKind.Pigeon`). Scene-free `Post` is saved as `SaveData.Post` (`Delivered`,
`Carrying`, `Reply`; rule `HasInvalidPost`, `LegacySaves.AddedAfterVersion5`). Checked by `post-checks`.

### `Lighthouse`, `LighthousePlans`, `NightBoat`, `Game.Lighthouse` and `ITripTarget.SourceFor`

The lighthouse on the shore. The first letter to the keeper of the sea makes him want it; from then his panel
(`GameState.LighthouseStatus` 32, `UiIntent.LighthouseTrip`, `Departure.Lighthouse`) raises it in eight trailer trips
(`TripKinds.Lighthouse`): five of stone loaded at the coast quarry, where the keeper asks the loading question
(`ITripTarget.SourceFor` gives a trip a source of its own, and `HaulTrip.Asker` is then the target's contractor;
`CompleteKeeperQuiz` loads the stone when the car stands at the quarry), then planks, bricks and glasses from the works.
The eighth pays `LighthousePlans.Coins` and earns the gull (`CollectorPetKind.Gull`). By night (`Weather.Darkness`) the
lantern turns a beam (`Shaders/LighthouseBeam.gdshader`) and a boat with lit windows sails past (`NightBoat`);
`benchmark.ps1 -Off lighthouse` measures them. A valley without the works has it standing from the start. Saved as
`SaveData.Lighthouse` (`Stage`; rule `HasInvalidLighthouse`: no stage before the first letter). Checked by
`lighthouse-checks`.

### `Dragon`, `DragonLair`, `DragonPlans`, `MountainSlope`, `DragonFlight`, `World.Dragon` and `Game.Dragon`

The dragon under the mountains (`docs/DRAGON_PLAN.md`). `World.PlanDragon` chooses his ledge (`DragonSite`) from the
roads alone, without random numbers: beside the homestead at the end of the road to the mountains, looking back down the
valley, where the car can drive there from the road's end; else, where the road ends in a cut, on the bank above its
last stretch, looking down at it, with the drop ring and the place under his chin on the road. The mountain slope
(`MountainSlopeSite`, `MountainSlope`) lies beside the same road near the ring; a valley without a slope has no dragon.
Both are left out of the plants, the grass and the wayside things through `InDragonZone`, which `InBorderfolkZone` asks
first, so nothing that was there before moves. The places are held by `Tests/Worlds/seed-N.dragon.txt`.

He is two nodes: `Dragon` (`Characters`) is the body, which breathes, blinks, looks at the car, puffs rings of smoke
while he has a cold and a little flame once he is well, sneezes (`Sneeze`) and flies; a `Character` without a body of
its own (`Character.CreateDragon`, `DragonRole`, `ActorIds.Dragon` 79) stands under his chin, so that the car meets him
where it can drive. `DragonLair` holds the ledge, the cave and every solid part, so the body can leave it.

His panel (`GameState.DragonStatus` 33, `GameUi.DragonPanel`) has three stages. Six trailer trips (`TripKinds.Dragon`,
`Departure.Dragon`, `UiIntent.DragonTrip`) raise the nest with sand, stones and logs from the slope, where the dragon
asks the loading question from afar (`ITripTarget.SourceFor`, `CompleteDragonQuiz`). Then the riddle
(`UiIntent.DragonRiddle`) is three quizzes in a row; each answer is saved at once and makes him breathe in deeper, and
the third makes him sneeze: the fire by the nest catches, the egg hatches, `DragonPlans.Coins` are paid and the little
dragon comes (`CollectorPetKind.Dragonling`). From then on `UiIntent.DragonFlight` takes the player round the valley on
his back: `DragonFlight` is a closed curve high above the ground that `TickDragon` follows for `DragonFlight.Duration`
seconds, the camera rides on his neck (`FollowCamera.Ride`), the coins, the animals and the hidden friends below are
lit, and the pedals end it early. Only the first flight pays (`DragonPlans.FlightCoins`). Saved as `SaveData.Dragon`
(`Stage`, `Riddle`, `Flown`; the egg has hatched when `Riddle` is 3; rule `HasInvalidDragon`,
`LegacySaves.AddedAfterVersion5`). A flight is not saved: a load puts him back on his ledge. Checked by `dragon-checks`;
`run.ps1 -Capture` shows him with the views `dragon`, `dragon-nest` and `dragon-flight`.

### `HotAirBalloon`, `BalloonStation`, `BalloonStars`, `BalloonFlight`, `BalloonRide` and `World.Balloon`

The balloon station far from the village (`docs/BALLOON_PLAN.md`). `World.PlanBalloon` tries both sides of every road
of the land beyond the old valley every 8 m and, of the places where the station fits (`BalloonFits`: 480 m from the
village, off the roads, the rivers, the sand and the bridges, 160 m from the homesteads, the lighthouse and the dragon,
a nearly level field the car can drive onto), takes the one whose field lies highest, without random numbers. The
station is left out of the plants, the grass and the wayside things through `InBalloonZone`, which `InBorderfolkZone`
asks, so nothing that was there before moves; `Tests/Worlds/seed-N.balloon.txt` holds the place and the flight.

`BalloonStation` is the landing target, the hut, the windsock, the supplies, the sign and the four ropes. The balloon
(`HotAirBalloon`, a node of its own, about 25 m tall with a rainbow envelope whose small shader glows in its own colours
by night) floats on the ropes `BalloonStation.Hover` (9 m) above the target, over the trees, so that it is seen from
the village; after a landing it stands on the target and rises on the ropes again (`TouchDown`, `Moor`). Seen from
afar, the balloonist's marker rides above the balloon (`Character.RaiseFarMarker`).

Liška balonářka Amálka (`BalloonistRole`, `ActorIds.Balloonist` 80) opens her panel (`GameState.BalloonStatus` 34,
`GameUi.BalloonPanel`); the green check mark (`UiIntent.BalloonFlight`) asks a question, and the right answer
(`CompleteBalloonQuiz`) lets the balloon go. `BalloonFlight` is the fixed way the wind takes, a closed curve from the
field towards the middle of the valley and back, its floor 14 m above the highest ground near it; `BalloonRide` is one
flight without scene nodes, stepped by the system `balloon` (`Game.TickBalloon`): the pedal is the burner, reverse the
valve (the space bar is the talk button, which lands the balloon at once after a grace of 1.5 s), and the eight stars of
`BalloonStars` hang along the way at heights that change from flight to flight (`BalloonFlight.StarHeight`). The
camera rides behind the basket (`FollowCamera.Ride` with an eye and a look of its own), and the coins, the animals and
the hidden friends below are lit. Every star pays a coin, all eight a bonus, the first flight more
(`BalloonPlans`), and twenty stars bring the rooster (`CollectorPetKind.Rooster`). Saved as `SaveData.Balloon`
(`Flights`, `Stars`; rule `HasInvalidBalloon`); a flight is not saved, a load puts the balloon back on its ropes.
Checked by `balloon-checks`; `run.ps1 -Capture` shows it with the views `balloon`, `balloon-night`, `balloon-far`,
`balloon-far-night`, `balloon-panel` and `balloon-flight`.

### `TrackingTrail`, `TrackingPath`, `TrackingPrints`, `TrackingDens`, `TrackingHunt` and `World.Tracking`

The forest keeper's book of tracks (`docs/TRACKING_PLAN.md`). `World.PlanTracking` runs once every plant of the land
beyond the old valley is planned, with random numbers of its own (`Settings.Seed + 8123`), and lays six trails
(`TrackingTrail`, the animals of `TrackingAnimal`) one after another: from beside the end of the forest keeper's road
(`TrailStarts`, `StartOpen`) to a den some 270 to 440 m further round the valley, and from each den to the next. A den
(`DenFits`) lies on level dry ground between the ring road and the borders, off the roads, the rivers, the sand and the
bridges, 200 m from the other dens and 90 m from the homesteads, the lighthouse, the dragon and the balloon, with no
planned tree anywhere on it; a den after which none fits is chosen again elsewhere, and where the six cannot be found
from one start, the next start is tried. The dens are left out of the plants, the grass and the wayside things through
`InTrackingZone`, which `InBorderfolkZone` asks, so nothing that was there before moves;
`Tests/Worlds/seed-N.tracking.txt` holds the dens and the ways.

`TrackingPath` finds a way with A* on a grid of 4 m over the cells where the car can drive (`TrailOpen`: out of the
water, the sand and the bridges, 3.2 m from every trunk, slopes up to 0.3), with the roads and the woods costing more,
searches only the ellipse in which the way can be at most 1.6 times the straight line (so a search with no way ends
soon), pulls the way straight where the straight line is open, rounds its corners and walks every finished piece metre
by metre; a way that fails is turned down. The whole plan takes 0.2 to 0.6 s.

`TrackingPrints` lays the prints of the trail followed along its way at the animal's stride, left and right in turn,
drawn 3.2 times larger than life in one MultiMesh, and shows only 40 in front of the furthest one reached and 8 behind
it; the next four pulse golden and golden sparkles (a billboard MultiMesh) hover over the next stretch, above the grass
that hides the prints from afar. `TrackingDens` builds the six dens (a bush, a burrow in a bank, a birch with ferns, a
burrow under a stump, a wallow, rocks with a fallen trunk) with their animals, which peep out of the den of the trail
followed and sit in front of it once found.

Medvědice hajná Božena, the keeper of the forest border, shows the book in her panel once the post has brought her a
letter (`TrackingWanted`; `GameState.TrackingStatus` 35, `GameUi.TrackingPanel`), and her green check mark
(`UiIntent.TrackingStart`) sends the player out. `TrackingHunt` is the hunt without scene nodes: whether she has sent
the player out, how many animals are found and the furthest print reached (`Step`, 6 m), with the arrow of the corner
panel (`GameUi.TrackingHud`) back to the trail when the car is 24 m from it (`Lost`, `Lead`); it is stepped by the
system `tracking` (`Game.TickTracking`). At the den a bodiless voice (`Character.CreateTrackingVoice`,
`TrackingAnimalRole`, `ActorIds.TrackingAnimal` 81) stands where the animal is talked to; `Character.Away` keeps it out
of the characters beside the car while no trail is followed. Its question (`CompleteTrackingQuiz`) pays coins and starts
the next trail, and the sixth animal pays a bonus (`TrackingPlans`) and brings the squirrel
(`CollectorPetKind.Squirrel`). Saved as `SaveData.Tracking` (`Started`, `Found`; rule `HasInvalidTracking`); the
furthest print is found again from where the car stands. Checked by `tracking-checks`; `run.ps1 -Capture` shows it with
the views `tracking`, `tracking-panel`, `tracking-trail`, `tracking-night`, `tracking-lost`, `tracking-den`,
`tracking-dens` and `tracking-dens-night`.

### `SheepHerd`, `SheepFlock`, `SheepPlans`, `World.Sheep` and `Game.Sheep`

The shepherd's lost sheep (`docs/SHEEP_PLAN.md`). `World.PlanSheep` runs after the trails are planned, with random
numbers of its own (`Settings.Seed + 8311`), and chooses eight meadows, one in every eighth of the land round the valley
between the ring road and the borders (anywhere round the valley where none fits in its eighth). A meadow
(`SheepSpotFits`) lies on open, nearly level, dry ground off the roads, the rivers, the sand and the bridges, 160 m from
the other meadows, 150 m from the shepherd's homestead and 80 m from the other homesteads, the lighthouse, the dragon
and the balloon, with no trunk on it; `SheepWay` asks `TrackingPath` for a way the car can drive to it from the nearest
open point of a road beyond the old valley. The meadows take no ground: no plant and no grass is left out for them, so
nothing that was there moves. A valley where the eight cannot all be found gets no sheep.
`Tests/Worlds/seed-N.sheep.txt` holds the meadows; planning takes some 40 ms.

Zajíc pastýř Bohouš, the keeper of the mountain border, shows his fold from the start, with no letter needed
(`SheepWanted`; `GameState.ShepherdStatus` 36, `GameUi.ShepherdPanel`), and his green check mark (`UiIntent.SheepStart`)
sends the player out. `SheepHerd` is the herd without scene nodes: the status of every sheep (`SheepStatus`: lost,
following, waiting, home) and how far the followers are behind the car along its way, kept in a `CompanionPath` of 480
points. A lost or waiting sheep within `FindRadius` (9 m) joins; the followers line up behind the car at `Slot` and run
at most `Run` (12 m/s), so a car that drives faster leaves them behind: past `LagWarning` the corner panel asks for a
slower drive, and a sheep `StrayDistance` (60 m) behind its place stops and waits with a beacon over it until the car
comes back. It is stepped by the system `sheep` (`Game.TickSheep`), whose corner panel (`GameUi.SheepHud`) points to a
sheep left behind first, then home once three sheep follow or none is left to find, else to the nearest lost sheep.

`SheepFlock` draws the eight sheep with bells, bleats and beacons, grazing on their meadows (hidden until the player is
sent out), trotting behind the car or waiting. The shepherd's second button (`UiIntent.SheepPen`) asks a question first
(`Departure.Sheep`); then the followers run to the fold of his homestead (`SheepPlans.FoldSpots`, beside the three sheep
of the model that never ran off), jump over the front rail one after another and count themselves. Every sheep pays
`SheepPlans.CoinsPerSheep`, the whole flock `FlockCoins` and the sheepdog puppy (`CollectorPetKind.Sheepdog`). Saved as
`SaveData.Sheep` (`Started`, `Home`, `Following`; rule `HasInvalidSheep`); a following or waiting sheep is put right
behind the car after a load. Checked by `sheep-checks`; `run.ps1 -Capture` shows it with the views `sheep`,
`sheep-panel`, `sheep-herd`, `sheep-lost` and `sheep-fold`.

### `RallyCourse`, `RallyRun`, `RallyGates`, `RallyPlans`, `World.Rally` and `Game.Rally`

The ferrywoman's rally round the land beyond the old valley (`docs/RALLY_PLAN.md`). `World.PlanRally` runs after the
sheep are planned, with no random numbers, and samples the ring road every 2 m into a `RallyCourse` (position, forward
and right at a distance along it, wrapping round; `Project` finds where the car is on it, `LaneAt` which of the three
lanes it is in). Four start arches stand some 30 m past the roads to the borders (`ArchFits`: off and 25 m from the
bridges, 22 m from every junction, on dry, open posts), the river's first, and cut the ring into four stages
(`RallyStage`). Each stage gets three gates near its quarters (`PlanGates`, `GateFits`): 190 m past its start, 150 m
apart, 90 m before its finish, on a straight stretch 40 m from the bridges and junctions, away from the sheep, dens,
balloon, quarry and lighthouse, with all three lanes over 13 m across dry, free of trunks and near the height of the
road. The course takes no ground and its gates have no collision, so nothing that was there moves.
`Tests/Worlds/seed-N.rally.txt` holds it; planning takes some 15 ms.

Liška převoznice Vendula, the keeper of the river border, shows her race card once the postman has brought her a letter
(`RallyWanted`; `GameState.RallyStatus` 37, `GameUi.RallyPanel`), with every stage's medal, the pictures of the borders
it runs between, its best time and a green check mark for each (`UiIntent.RallyStart`). `RallyRun` is the drive of one
stage without scene nodes: it waits for the car to cross the start arch forwards, then reports the gates crossed with
the lane taken and the finish (`RallyEventKind`), counts the time driven plus `RallyPlans.PenaltySeconds` for each wrong
or missed gate, gives up when the car is 45 m off the course for 4 s or 30 m behind the start, and goes on into the next
stage after a finish. It is stepped by the system `rally` (`Game.TickRally`): within `RallyQuestionAhead` (170 m) of the
next gate it asks `QuizSession.NextGate` (any enabled kind but syllables, which are put together, not chosen), and
`RallyGates` draws the three answers on the boards of that gate with small viewports (text or a `WordPicture`) while the
corner panel (`GameUi.RallyHud`, on the left so the gate ahead stays in sight) shows the question with the lanes'
colours above the strip of the stage (`RallyStrip`). A finish earns a medal by the average speed with the penalties
(`MedalFor`: 11.5 m/s gold, 8.5 m/s silver), coins by the medal, `NewMedalCoins` for a better medal, `LapCoins` for four
stages in one go and, with gold on all four, the otter (`CollectorPetKind.Otter`). Saved as `SaveData.Rally` (`Best`,
tenths of a second per stage, 0 when not driven; rule `HasInvalidRally`); medals are worked out from the times, and a
drive under way is not saved. Checked by `rally-checks`; `run.ps1 -Capture` shows it with the views `rally`,
`rally-gate` and `rally-panel`.

### `MayorRole`, `TownDecorations` and `Village.Mayor`

The mayor (`ActorIds.Mayor` 68, `MayorRole`, `GameState.MayorShop` 23) stands on the village square and sells eight
decorations for coins. Both she and the decorations are made in `Village.GenerateMayor`, right after the square, with no
random draws; a decoration has one `TownSlot` per copy, each in the frame of a `TownPlace` (the square, a house, the
garage or a shop; z towards the road) and inside the ground that place already reserves, which `mayor-checks` tests with
`Village.IsReserved`, so nothing else in a valley moves and the world fingerprints hold (they leave out actor IDs from
64 on). She is registered last in `World.Generate`, after every search that keeps clear of the actors, and she is left
out of the actors that the postcard stops and the driving courses keep clear of, so those do not move either; she also
stands more than `InteractionRange` from the album stop, or she would be the `nearby` character there.
`TownDecorations.All` lists the decorations (ids are saved and never renamed; slots in the square's frame; one `Build`
method that draws a copy, with solid parts only when asked, and only boxes and cylinders, never the capsules of trunks,
which `collision-checks` takes its test shapes from); each is built hidden with its collision shapes disabled and shown
by `Village.AddDecoration`/`RestoreDecorations`. The shop panel (`GameUi.Mayor.cs`, `DecorationPreview` for the
pictures) sends `UiIntent.BuyDecoration`; `Game.Mayor.cs` spends the coins (`Economy.Spend`). The state is
`SaveData.Decorations.Owned` (rule `HasInvalidDecorations`, `LegacySaves.AddedAfterVersion5`). Checked by `mayor-checks`
(and `content-checks`, `contract-checks`); `run.ps1 -Capture -View mayor` or `mayor-shop` shows it.

### `GlassmakerRole`, `GlassLamps`, `LampField` and `Village.Glassmaker`

The glass lamp maker (`ActorIds.Glassmaker` 69, `GlassmakerRole`, `GameState.GlassmakerShop` 24) stands on the square
opposite the mayor, in the square's frame and without random draws (`Village.GenerateGlassmaker`, right after the mayor;
registered after her in `World.Generate`, which also hands `LampField.Surface` the world's `DriveSurface`), and is left
out of the actors that the postcard stops and the driving courses keep clear of. `GlassLamps.All` lists the two lamps
(`candle`, `lamp`; ids are saved and never renamed): one `Build` method each draws a lamp of boxes, cylinders and balls
with a see-through glass material and returns its `Glow` node (the flame or bulb, a billboard `Halo` and an
`OmniLight3D` that is off), so the same drawing serves the shop picture (`LampPreview`), the lamps on the maker's table
and the lamps on the ground. A lamp has no solid body. `LampField` (a child of the village, which stands at the origin,
so lamps keep world coordinates; `Village.CreateLamps` makes it in `World.Generate`, not in `Village.Generate`, because
a node of this class in the village that the road audit generates made that process crash now and then when it quit)
holds the bag (`MaxCarried` 8, the first kind in it is put down first) and the standing lamps (`MaxPlaced` 30):
`PutDown` needs dry ground inside `ValleyBounds` and 0.9 m of free space, `NearestIndex` and `PickUp` take one up again.
`Weather.TickNight` calls `Village.SetLampGlow(Darkness, car)`: above a darkness of 0.3 every lamp shows its glow, but
only the five nearest to the car get a real light (the project uses the compatibility renderer, which can light few
things at once). `Game.Lamps.cs` handles the shop (`UiIntent.TakeLamp`), the talk button (`Game.Interact`) taking a
standing lamp up when the car is within `LampReach` of it and, as the last thing it does, putting the next lamp of the
bag down (`PutDownLamp`), both only where no character is near and no load waits (`LampPickupAvailable`; there is no key
of its own for lamps), and the corner panel of the bag (`GameUi.Glassmaker.cs`). The state is `SaveData.Lamps`
(`Carried` kinds and `Placed` spots with kind, x, z and yaw; the height is taken from the ground again; rule
`HasInvalidLamps`; `LegacySaves.AddedAfterVersion5`). Checked by `glass-checks` (and `content-checks`,
`contract-checks`); `run.ps1 -Capture -View glass`, `glass-night`, `glass-shop` or `glass-close` shows it.

### `CollectorPet`, `WorldPetCatalog`, `HaulTally` and `World.Pets.cs`

A pet is a `CollectorPet` of one `CollectorPetKind`. The four collectors' pets count a village collection; the others
belong to the world (`World.WorldPets`, the duck first): one for each raw material (ten loads of it tipped out, at a
works or a building), each works (ten loads taken in, twenty pieces made) and the bridge, the mill and the tower (their
completion; the jetty's is the duck). `HaulTally` (scene-free, saved as `SaveData.Hauls`) counts the loads;
`Game.FinishSupplyDump` and `Game.FinishBuildDump` add to it and `Game.EarnPets` announces a pet that has just been
earned. A pet waits beside its worker or builder (`World.CreateWorldPets`, called last in `World.Generate`), trails the
car in a place of its own (`WorldPetCatalog.Trail`) and is switched off in the garage like the others. A kind is saved
by name, so names are never changed.

### `Glassworks`, `World.PlanGlassworks`

The fourth works (`WorksCatalog.GlassIndex`, `Product.Glass`, `HaulLoad.Glass`, actor ID `ActorIds.Glassworks` = 64)
turns sand into glasses. The sand pit's worker sends the sand there (`SourceRole`, `OpenSource`). Every valley with a
ford has one: `World.PlanGlassworks` plans it last in `PlanFord`, like the others (the village edge where there is room,
else the bank; a field path and yard in `Tracks`), with its own seeded search and without reserving ground in
`haulClearings`, so older sites, actors and the plants are generated exactly as before. The plants that stand on its
footprint or way are planted all the same and then taken out (`Vegetation.ClearArea`, `World.InGlassworksZone`):
`TreePositions` still lists the trees as generated, which keeps the world fingerprints valid, and `NearTrunk` ignores
the cleared ones. `World.WorksFor` and `World.AllWorks` reach it, `World.Industry` is still the three original works.
Its glasses go to the residents of finished houses: the tenth improvement is followed once by an order for glasses
(`Game.HouseMaterial` returns `HaulLoad.Glass`, `Village.IsGlazed`, saved as `VillageSave.Glazed`), paid like a task.
They also go, again and again, to the lamp maker on the square (`Game.Glassmaker.cs`): his shop starts a trip of its own
kind (`GlassmakerTripTarget`, `TripKinds.Glassmaker`, index 0, after a quiz with `Departure.Glassmaker`) that ends on
`Village.GlassmakerDrop` and pays `Game.GlassmakerCoins`, and with no glasses he sends the player for sand
(`BeginMissingSupply` from `GameState.GlassmakerShop`). The trip needs no saved field: the save holds the trip like any
other, and a valley without the glassworks drops it.

### `WheatFarm`, `Bakery`, `FlourMill` and `World.Farm`

The fifth chain: wheat from a farm, flour from the mill, bread from a bakery, the baker's orders. `HaulLoad.Wheat` (11,
a raw load) and `HaulLoad.Bread` (12, goods) were appended, `Product.Bread` is the fifth works
(`WorksCatalog.BreadIndex`, `Total` 5; the actor IDs are `ActorIds.Bakery` 65 and `ActorIds.FieldFarmer` 66, after the
glassworks). `World.PlanFarm` runs last in `PlanFord`, after the glassworks, with its own seeded search, plans the farm
first (`FarmBand`: beyond the works and the landmarks, then a wider band, then anywhere) and then the bakery at the
village edge, and returns nothing unless both fit. Like the glassworks they reserve no ground in `haulClearings`; the
plants on `InFarmZone` and `InBakeryZone` are cleared after generation (`Plants.Generate`), so no older tree moves.
`WheatFarm` is scene-only: four `MultiMesh` fields (`Wheat.gdshader`, stalks that sway with the weather), a barn, sacks
and a scarecrow; `World.BuildFarm` places the farmer through `PlaceSourceWorker` with `SourceCatalog.Field` (not in
`SourceCatalog.All`, whose length gives the other IDs), so `SourceRole(Wheat)` works like the other sources and
`Game.OpenSource` finds the bakery through `WorksCatalog.ForInput`. `FlourMill` (scene-free, `World.FlourMill`, built by
`World.BuildFlourMill` where the valley has the bakery and the mill) joins the finished mill to the chain: the farmer's
wheat is a trip of its own kind (`TripKinds.Mill`, index 0, only once the mill is complete and has room) that ends in
`Game.FinishMillDump`, and its flour (`HaulLoad.Flour` 13, sacks drawn by `Mill.ShowFlour`, saved as
`BuildingsSave.FlourStock`, rule `HasInvalidFlour`) is the bakery's input: the miller (`BuilderRole`,
`GameState.BuildStatus` once the mill is complete, `Game.OpenMiller`) starts the supply trip of the bakery
(`TripKinds.Supply`, index 4) and asks its loading question, and `Game.CompleteHaulLoading` takes the sack.
`Works.Input` is the spec's input except at the bakery of a valley without the mill, which takes the wheat itself. A
supply trip of the bakery from a save made before the mill may hold the farmer's question about wheat:
`SaveRules.ActiveTripAsker` accepts it and the farmer still loads the trailer. Once the mill grinds, the baker of the
village shop (`TraderRole` 0, `Game.BakesFromBakery`) takes `Expedition.OrderAmount` loaves from the bakery when his
order sets off (`Game.StartTrade`); without enough bread `GameState.TradeStatus` shows `GameUi.BakerNeedsBread` and
`Departure.Missing` from that state sends the player for what `Game.BreadSupply` names (flour, else wheat). The bakery
stock is `BuildingsSave.BreadStock`. A glazed finished house then asks for bread (`Game.HouseMaterial`, `Village.Bake`,
`VillageSave.Baked`, `SaveRules.IsBreadOrder`), as it asked for glasses, and with no bread sends the player along the
chain through `Game.BreadSupply`. The tally keeps wheat and baked loads apart from the arrays whose length old saves
have (`HaulsSave.Wheat`, `Baked`); the new fields (`FlourStock` too) are listed in `LegacySaves.AddedAfterVersion5`.
Pets: `WorldPetCatalog.Wheat` and `Bakery`.

### `CarBody`, `VehicleToys` and the models

The garage's "Modely": four cars (`classic`, `buggy`, `pickup`, `wagon`, drawn in `VehicleModel`) and five toys,
`plane`, `tank`, `train`, `rocket` and `ufo` (`VehicleToys`, one method each, sharing a small `Kit` of helpers). Every
model is a `Coachwork_<id>` node and `VehicleAppearance.ApplyModel` shows one of them. A toy takes the garage paints
through `RegisterBody`, `RegisterRoof` and `RegisterTrim`, has racing stripes of its own (`RegisterStripe`, shown only
while its model is on; the stripes of the cars are shared by the four cars), and brings its own wheels where it needs
them: `CarBody.OwnWheels` hides the four tyres, the wheels are added to the vehicle's wheel list so they turn with the
car, and `RegisterWheel` lets the garage's bigger wheels enlarge them. `CarBody.TrackX/TrackZ` say where the tyre marks
and stamps begin (`VehicleAppearance.RearWheel`). Every model but the steam engine has a part that moves as it drives, a
`Gadget` node fed with the car's speed, engine effort and `Vehicle.Turn`: the wind-up key of the retro car, the buggy's
pumping engine, the dog in the pickup and the pinwheel out of the wagon (`CarGadgets`), the tank's turret that runs
ahead into a turn, the plane's propeller and rudder, the rocket's flame and the saucer's ring of lights (in
`VehicleToys`); the steam engine has its smoke. Accessories are drawn once for the car; `CarAccessory.Offset(model,
accessory)` moves the ones that would float on a toy and `PlaceValleyMap` puts the sticker on a surface of it. A model
id is saved (`VehicleSave.Model`), so ids are never renamed; a new model is a `CarBody` entry and a build method, and
`run.ps1 -Capture -View <id>`, `<id>-decorated` or `<id>-rear` shows it. Checked by `CheckModels` in `garage-checks`.

### `CarTrail`, `TrailShapes`, `TrailStamps`

What the rear wheels leave: the plain `WheelTracks` ribbons (`CarTrail.Plain`, id `tyres`, free) or a row of flowers,
hearts or stars (`CarReward.IsTrail`, bought for `CarReward.TrailPrice`). The chosen id is
`VehicleAppearance.CurrentTrailId` and is saved as `VehicleSave.Trail` (the rule `HasInvalidTrail` wants a known id that
was bought; `LegacySaves.AddedAfterVersion5` lists the field). `IsTrail` is left out of a save while it is false, so
older saves of bought items are unchanged. `TrailShapes` holds the outlines once, the garage draws them
(`DeliveryPicture` `trail-*`) and `TrailStamps` lays them on the ground as one `MultiMesh`; `WheelTracks.Update` hands
over to it while a stamp trail is chosen.

### Ducklings of the jetty

`CollectorPetKind.Duckling1` to `Duckling3` are world pets of the fisherman that count the jetty's stage
(`WorldPetCatalog.DucklingDeliveries`: 3, 6 and 9 deliveries; the duck itself needs ten), made by `World.CreateDuck`
with their own trail offsets behind the duck. They need no saved state of their own.

### `Trampling` and `Petals`

What the car does to the plants it drives over (scenery only, nothing saved). `Vegetation` (the small flowers, the
meadow flowers, the bushes and the ferns) and `Wayside` (dandelions and clocks, whose heads are now a `MultiMesh` of
their own with the same instances) register each plant while they generate it: its foot, its reach and the batch
instances it is made of, the petals marked as heads; no random draw is taken, so the world fingerprints hold, and the
plants of a cleared site are forgotten (`Trampling.Forget`). The system `trampling` (`Game.Trampling.cs`) hands it the
moving car's and the trailer's footprints: a flower is knocked flat in the car's direction, its heads hidden and thrown
as petals (seeds float), it lies 4.5–7.5 s after the last wheel left it, then stands up past upright and its blossom
grows back; a bush or fern is pressed down and springs back with a wobble, throwing leaves. Only moving plants keep
their rest transforms (read back from the batch), and only the near batches are moved. `Petals` is one `MultiMesh` whose
buffer goes to the engine in one call a frame. Nothing grows while the game is paused. Checked by `trampling-checks`;
`run.ps1 -Capture -View trampling` (or `trampling-down`) shows it.

### `HiddenFriends`, `HiddenFriend` and `World.Hidden.cs`

The small friends who hide beside the roads: `HiddenFriends.PerKind` (8) of each `HiddenKind` (ladybug, hedgehog, frog;
appended-only numbering). `World.CreateHiddenFriends` places them last, after the plants, with a random generator of its
own (`Settings.Seed + 5151`), so nothing placed earlier moves; the kinds take turns, the spacing shrinks only if a
valley is short of room. `Game.Hidden.cs` finds a friend when the car is within `FindRadius`, says the count of that
kind and earns the pet of a kind at eight (`CollectorPetKind.Ladybug/Hedgehog/Frog`, owned by the witch).
`HiddenSave.Found` holds the numbers found; the rule `HasInvalidHidden` checks them and `LegacySaves.AddedAfterVersion5`
lists the field. Checked by `hidden-checks`.

### `Atmosphere`, `Weather`, `Village.Nightfall` and `Constellation`

The sky belongs to `Atmosphere`: `ApplyWeather(cover, flash, evening, night)` blends the day towards a low orange sun
(the light turns and weakens, so shadows grow long) or a moonlit night, and shows the stars and the moon, which are
painted on a sphere that follows the camera, from a random generator of their own so nothing else generated from the
seed moves. `Weather` owns two things that go on side by side: the weather of the sky (`Current`: clear, rain, wind or a
storm, not saved) and the clock of the day (`Clock`, saved as `SaveData.Day`), which goes round by itself every
`DayLength` (20 minutes): day, evening, night and a short dawn (`TimeOfDay`). The witch's `WeatherKind.Evening` and
`Night` (appended so the numbers are unchanged) move the clock on to them and clear the sky. It eases `evening` and
`nightfall` in over a few seconds (`Begin(kind, instant: true)` and `SetClock(seconds, instant: true)` skip that, for
captures and a loaded game), and every tick `TickNight` puts the fireflies (`Firefly.gdshader`, a wrapping box of
blinking specks around the car, in a `Night` node so the rain stays the only direct `MultiMeshInstance3D`), the car's
`Headlights` spot light and `Village.SetNightLights` in step with `Darkness`. Every window pane of the valley is
`Art.Window` glass (`Art.WindowMat`), which looks like plain glass by day; `Art.SetWindowLight` turns all of them warm
yellow at night at once. A glazed house and a finished plot also get brighter `Art.Lamp` nodes in their windows
(`HousePlot.ShowNightLight`); the houses themselves are not touched. `Constellation.All` lists the star pictures; on a
starry night `Game.BeginLookout` lifts the panorama (`FollowCamera.ShowPanorama(..., pitch)`) and
`Atmosphere.ShowConstellation` draws the next one halfway round the turn.

### `IChallenge` and generators in `Mathematics` and `Letters`

Provide question data, seeded generation and answer validation.

### `SaveData`, `SaveStore`, `SaveRules`, `SaveMigrations` and `Game.Persistence.cs`

`SaveData` is the save of version 5: the seed, the state and the character met, and one section for each part of the
game (`Vehicle`, `Economy`, `Quiz`, `Task`, `Roadside`, `Village`, `Buildings`, `Companions`, `Postcards`, `Events`,
`Bandits`), each plain data with defaults, so a file that leaves a section out still loads. `SaveStore` reads and writes
the file, falls back to the backup and sets aside copies of what the game could lose (`progress.vN.bak`,
`progress.nenacteno-HASH.json`). `SaveRules.All` lists what a save must satisfy, one rule for each part, and `Validate`
runs them. `SaveMigrations` moves an older file forward: a file of versions 1 to 4 is read into the flat `SaveDataV4`,
which is never changed, goes through the steps to version 4 (one question field, coins instead of car rewards, ten
deliveries for each building, one `Trip`) and `ToCurrent` moves it into the sections; `Repair` and `AfterValidation`
mend what any version left behind (a damaged ghost record, a house whose task changed, the album that grew, the ten
roadside friends). `Game.Persistence.cs` captures and restores the sections. Saved state that is new is a new section,
its rule in `SaveRules.All` and its capture and restore; `SaveStore` does not change. Existing numeric `GameState`
values and actor IDs are part of the JSON format.

### `Scripts/Diagnostics`

Launch headless checks, prepare screenshot views and exercise game entry points. `Game` reaches them only through
`partial` hooks, and release exports (`ExportRelease`) leave the folder out of the build.

## Start and game state

`Game._Ready` generates the world, creates the vehicle and camera, initializes the world systems and binds UI events.
The diagnostic partial handles command-line modes. Road audit builds the terrain, roads and village. Road-driving checks
build the world and vehicle. The other suites and capture views use the initialized game.

`Game` owns the current state and activates the vehicle during exploration. The UI sends intents such as submitting an
answer or choosing an expedition. Pause records the previous state. Resume restores its panel, question and selections.
Loading a save reuses the same panel restoration.

### Startup speed

Everything the player sees is generated in `_Ready`, so startup time is the time the C# planning takes. Outside an
exported game (`run.ps1` and every check) Godot loads its `GodotSharp.dll` from `GodotSharp/Api/Debug`, which is built
without optimisation: each `Vector2`, `Vector3`, `Basis` or `Mathf` method is a call that the JIT cannot inline, about
twenty times slower than the same arithmetic in our own code. The loops that run hundreds of thousands of times while a
valley is planned (`Village.IsReserved`, the haul and site searches, `Roads.SampleField` and `SurfaceHeight`,
`Grass.CanGrow`, `Tracks.Contains`, the site zones) are therefore written in plain floats with `FlatMath`, and the
rotations of the tree drawing with `FlatBasis`; both repeat each Godot operation in the same order, so the valley stays
the same, bit for bit. `unit-checks` compares every such form with Godot's on random input and the world fingerprints
pin the result. Keep new hot loops in floats, and read an engine property such as `Position` once outside a loop: each
read is a call into the engine. For the same reason a mesh that is rebuilt often goes to the engine as arrays in one
call (`WheelTracks`), not vertex by vertex. The project is built with `Optimize` even in the Debug configuration for the
same reason. Random numbers drawn while the game runs come from `FlatRandom`, Godot's generator in plain C# (the grass
tiles built as the car drives draw thousands each).

What the GPU and the processor spend per frame is measured with `./benchmark.ps1`. The valley is bound by geometry and
draw calls, not by pixels, so the distance limits matter most: the far blocks of `Vegetation` are drawn with simpler
shapes (`FarDistance`), small single meshes get a visibility range from their size and none under half a metre casts a
shadow (`DistantDetails`), the sun's shadow has two splits, the village's plain parts are merged into one mesh per
parent and look once the valley stands (`StaticMeshes`; characters and animals keep theirs), and new grass tiles are
built within a time slice of each frame so that driving does not stutter. The tyre tracks are a ring of segments of
which only the new one goes to the engine; far wild animals sleep until the car could reach them; far characters stop
waving and bobbing.

## Questions and activities

- Add question data and generation alongside the existing quiz types. `Game.Quizzes.cs` selects an enabled type, rotates
  through language activities and opens the question. `Game.SubmitAnswer` handles answers and forwards successful
  interactions to the appropriate activity. `GameUi` renders the choices. Keep a new quiz's pending question and partial
  selections restorable from disk.
- Roadside friends have one opening quiz per world. Solving it unlocks a delivery or apple collection whose choice can
  be postponed. Cargo handover opens a counting task or, with arithmetic disabled, another enabled task (language or
  comparing numbers). Completing that task grants cosmetic choices.
- Village houses draw from seeded schedules of ten tasks (two each of math, letters, fruit, planks and bricks;
  `VillageTaskKind`). A planks or bricks task is a `HaulTrip` for a `HouseTripTarget` that fetches one unit from the
  works' stock and tips it in front of the house (`Village.HouseDrop`), in state `HouseMaterialOffer`; it is saved as a
  `Trip` of kind `house`, validated against the saved schedule, and `SaveStore.MigrateHouseTasks` returns a save that
  waits at a house whose task changed to driving. Shops, collectors and the sorcerer have their own departure and
  handover rules. Their active trips use the same `Expedition` slot as roadside trips. Keep checks for opening an
  activity, completing it and restoring it aligned when adding a new flow.
- Expedition routes follow a graph of existing road samples. Guidance arrows use fixed anchors supplied by `Roads`.
  Changing position, pickup or destination updates the visible arrows while retaining their road placement.

## Seeded worlds and saves

- `WorldLayout` owns the seeded route and plot plan. `World` generates the village twice on purpose
  (`World.GenerateVillage`): once to discover the settlement footprint, then, after that area is levelled, again on the
  levelled ground, before spawning roadside animals; the world fingerprints pin the result. The distances that place
  benches, signs and roadside friends are named in `WorldTuning`. Use the shared terrain and road measurements when
  placing new scenery.
- Every actor ID is a number in `ActorIds`, which is the only place that holds them (`World.FarmerId` and the like are
  aliases), and `World.Register` refuses an ID that is already taken; `contract-checks` holds the numbers and the role
  of each ID. Register persistent actors in `World.RegisterActors`. The roadside animals use their array indices.
  Resident IDs start at 30 because older saves reserved 0–29 for roadside animals. The bandit representative has a
  separate ID (43) and is restored with its encounter in `Game`. The beaver uses 44 and is registered by
  `World.PlanFord`; the three works' workers use 45–47 and are registered by `World.BuildIndustry`; the builders of the
  mill, tower and jetty use 48–50 and the workers of the quarry, clay pit and log yard 51–53 and those of their second
  sites 54–57; the newcomers of the six building plots use 58–63. `PlanFord` searches in passes that start with the
  original strict rules, so valleys that already had a ford keep it exactly where it was; later passes only add a ford
  to valleys that had none.
- `World.PlanFord` runs after every older actor and pickup has its place, with its own seeded searches, so existing
  valleys keep their roads, characters and pickups. The new clearings still remove some trees, and because rejected
  candidates skip random draws, later tree positions shift. Drivable heights over the bridge and the finished ford come
  from `World.DriveSurface` and `World.CrossingSurface`.
- `SaveData` contains data only. `Game.Persistence.cs` snapshots the current state, recreates active expeditions and
  restores cosmetics, progress and panels. When adding persisted fields, supply defaults for older saves and validate
  the new state in `SaveStore`. Existing numeric `GameState` values and actor IDs are part of the JSON format.
- `SaveStore` validates each candidate before migration, uses the previous backup if the main file is damaged and writes
  updates through a temporary file. Diagnostic modes, captures and explicit seeds bypass the player save.

## Verifying changes

Run `./build.ps1 -Test` for the headless suite, or `./test.ps1` to run chosen suites and seeds in parallel without
rebuilding (`./test.ps1 -List`, `-Suite`, `-Seeds`, `-Build`); both use the same parallel runner in
`scripts-common.ps1`, and `CLAUDE.md` asks that it is always used. Add a new diagnostic to `Scripts/Diagnostics` and
list it in `Scripts/Diagnostics/DiagnosticSuites.cs` (flag, how to create it, seeds); the game starts it from that list
and `build.ps1` reads the list back with `--list-checks`, so nothing else needs to change. `contract-checks` fails when
a suite file is missing from the list. Tests that protect saved games are described in `Tests/README.md`. Use
`DiagnosticArtifacts.PathFor` for diagnostic files so each parallel run gets its own directory. For persistence changes,
exercise a round trip and relevant older save data. For visible changes, run `./run.ps1 -Capture -View <view>` and
inspect the image in `artifacts`.
