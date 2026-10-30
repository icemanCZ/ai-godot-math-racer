# Algorithms

## Terrain

1. Choose a random `Settings.Seed` once per launch (an explicit `--seed=NUMBER` can reproduce a run). Initialize FastNoiseLite with that seed, frequency 0.013 and three fractal octaves. Seeded phases also change the broad outer hill profile. `WorldPlan` builds a new road graph and river together: random control points, variable outer-loop bend counts, an optional shortcut, mirrored river banks and a seeded bridge location. Village plots follow the generated main road. Forest region centers, dominant species and roadside animal search origins use dedicated seeded streams.
2. Evaluate `h(x,z) = 0.65 × (7 × noise(x,z) + 2.4 × sin(0.023x))`.
3. Grade the road corridors, then sample a 445 by 445 vertex grid across 1,110 by 1,110 metres. Cache these heights, emit two triangles per cell, compute normals and vary vertex colors with a second sampling of the same noise.
4. Derive a static concave collision mesh from the visible mesh. All placement queries interpolate the exact same grid triangles: use the first triangle when local coordinates satisfy u+v <= 1, otherwise the second. Roads, decorations and collision therefore share the same surface height.

Time and storage: O(grid resolution squared). Physics uses the engine's static collision acceleration structure. Terrain is not regenerated per frame.

## Roads

Generated control points form connected loops and bridge approaches. For each point, compute a tangent from its neighbors and use 19% of that vector as the cubic Bézier handles; wrap neighbors for closed loops. Sample the baked curve every 1.4 metres. Index segments and their road widths in spatial cells, widening branch mouths and rounding junctions. Render the union of these distance fields on a shared 0.75 m triangular lattice with interpolated contour edges, 13 cm above terrain. Clip only the wooden bridge opening. This removes overlapping ribbons and repeated polygon subtraction, which could erase road sections at sharp joins. Batch the result in 48 m chunks and index the same triangles for containment and tyre-track projection. Disable road shadow casting to prevent lifted surface self-shadow artifacts.

Before terrain meshes and collision are generated, `RoadGrading` samples the same shared Bézier curves at 2 m intervals and indexes their segments in 24 m spatial cells. Within 7.5 m of a road, blend 75% of the original hills with 25% of a gentle base profile; fade the correction back to the untouched landscape over 7.5–14 m. This preserves hill height and allows steeper roads while reducing broad cuts through hills. Every road samples the same height field, so branches, intersections and closed seams share an elevation. A wider smooth grade connects this field to the bridge bank height. Keep the river channel and bridge clearance shaping authoritative. Visible terrain, colliders, road ribbons, village plots and vegetation then use the same cached grid heights.

Road proximity is the minimum horizontal distance to stored samples. Road containment and tyre-track height use the rendered triangle index, including tight bends and unified junctions. `WorldLayoutChecks` checks graph connectivity and the absence of dead ends across 128 seeds. `RoadPassabilityChecks` samples every route at 1 m intervals in three lanes, verifies visible road coverage, and rejects grades above `RoadGrading.MaxGrade`, currently 65% (about 33 degrees, below the vehicle's 48-degree floor limit). The regression suite audits several fixed seeds and uses the actual car and scenery collision to drive every full route in both directions.

## Village along the road

`Roads.SampleMain(distance)` returns a position and a central-difference tangent on the same baked curve that generates the visible main road. `Village` places two pairs of plots at distances 63 and 117 m, on opposite sides before and after the crossroads at 90 m. House centers are offset 11 m from the centerline along the horizontal perpendicular. Rotate each house so its local +Z entrance faces the road. This keeps placement tied to the road when its control points change.

Connect each doorway to the road edge with a 1.8 m footpath, sampled every 0.6 m and projected onto terrain. Split each front fence around the entrance, with the fence 4.8 m in front of the house center. Vegetation rejects candidates inside the oriented plot bounds, keeping houses, yards and access paths clear. The rabbit stands on the verge near the village entrance. Buildings retain their collision bodies.

## Vegetation placement

A dedicated seeded random generator selects bounded coordinates, scale and rejection. Reject candidates close to roads and landmark clearings. Keep more candidates in forest regions and 14 dense groves, enforcing minimum trunk spacing. All tree placement passes (dense groves, scattered forest and roadside accents) select species from the same spatial map: seven inner grove centers and the 14 outer grove centers define adjoining regions. Compare distances to each center after gentle sinusoidal distortion, preserving the center itself while giving region edges organic shapes. Each region has one dominant tree type, so roadside accents and scattered trees reinforce nearby groves instead of randomly mixing all seven species. Override the map near the river with a continuous willow ribbon.

Batch crown, trunk, flower and ground meshes by shape, material and 64 m spatial cell into MultiMesh instances. Fine leaf, flower, bark, fern and mushroom details have an 85 m visibility range. Store all transforms before assigning instance count, then upload once. A separate seeded art random stream handles fine detail. Identical seed and generation order produce identical placement.

## River and bridge

`WorldPlan` chooses the river bank, preferred offset (115–155 m), phase and bridge Z (25–100 m). `RiverCourse` then plans the actual course against the same unmodified landscape heights used by `Terrain`: dynamic programming samples cross-valley candidates every 4 m and downstream rows about 8 m apart, penalizing high ground, departure from the seeded preference and sharp lateral moves. Smooth the selected route and limit lateral change to 0.22 m per downstream metre. Road loops and bridge anchors are built only after this course exists, so they follow the resulting banks. Half-width is `5.5 + 0.7 sin(0.037z + phase)`, and water level is `-0.8 - 0.008z`, descending continuously downstream. The channel extends from one world edge to the other. `WorldLayoutChecks` checks reproducibility, gradual bends, connected roads, boundary clearance and bridge placement across 128 seeds, and compares routing over natural terrain with the previous terrain-independent curves.

Before terrain vertices and collision are built, form a broad floodplain with a rounded upper height envelope of `water + 0.85 + 0.12d + 0.0008d²`, where d is distance past the sandy bank. This lets the valley meet hill feet gradually instead of cutting tall walls beside the water. Blend the small channel itself into a riverbed 1.2 m below water and low sandy banks. The water ribbon is sampled every metre and subdivided into 20 strips across its width. It extends slightly under the banks to hide mesh seams. Store channel depth in vertex color for the water shader. Vegetation rejects the river corridor; separate reeds and stones decorate its banks. Valley regressions sample both shorelines along the entire river, rejecting tall canyon-like banks, alongside the full road passability audit.

The bridge bank mask follows the straight deck rectangle instead of the river's changing centerline. Keep terrain 32 cm below the bridge reference height over the deck plus a terrain-cell margin, including interpolated collision triangles. Blend the approaches out over 23–36 m along the crossing and 7–11 m across it. Three-metre wooden ramps at both ends meet the dirt road and have matching sloped collision boxes. Vehicle pitch and tyre-track projection use the same ramp surface profile. Smoke tests sample the full deck footprint for terrain/road clearance and drive across the bridge.

### Water appearance

`Shaders/RiverWater.gdshader` separates rendering from river generation. Blend turquoise shallows into deeper blue using the interpolated channel depth. Two crossed sine waves and advected smooth value noise make low-amplitude surface displacement, fading near shore. Estimate wave gradients with finite differences and transform their normal into view space for moving specular highlights. Layer independently moving noise fields for broken glints and soft shoreline foam, avoiding repetitive straight stripes. A view-angle tint adds a stylized sky sheen. This is not a screen-space reflection of scenery.

The shader uses an opaque surface and no screen/depth textures, so it works with the Compatibility renderer. `deep_color`, `shallow_color`, `foam_color`, `flow_speed` and `wave_height` are shader parameters for later art tuning. Visible waves do not modify physics or bridge collision.

A 38 m wooden bridge crosses at z=46 and is connected to the meadow path. Deck and railings have collision. Terrain grading levels the approaches; road triangles underneath the central deck are omitted. Vehicle pitch uses the deck height while crossing. Entering deep water outside the bridge returns the vehicle safely to the starting road. The crossing smoke test drives from one bank to the other and checks the vehicle stays above water midway.

## Arcade driving

At each fixed physics tick:

1. Read throttle and steering actions. Select forward or reverse target speed.
2. Move the current scalar speed toward the target using acceleration, coast deceleration, or stronger braking.
3. Update heading with signed speed, so steering reverses naturally when reversing. Fade steering in at low speed and reduce it at high speed.
4. Interpolate horizontal velocity toward heading times speed with `alpha = 1 - exp(-grip * delta)`. Space lowers grip from 18 to 1.5 and increases steering by 70%, allowing sideways handbrake slides. Limit horizontal velocity to absolute scalar speed so braking stops all sliding too. Integrate gravity vertically, then call `MoveAndSlide`. Floor snapping maintains contact with slopes.
5. Reset when outside the circular play boundary or below the world, or after five seconds of sustained throttle without movement. The capsule and yaw-only physics body cannot overturn.
6. Sample the road plane over the chassis length and width. Pitch and bank the chassis collision box to follow that plane, keeping the capsule upright; this prevents the long box from catching on an uphill face. Interpolate matching pitch and bank on the visual model, adding gentle steering roll. Spin wheel meshes by distance travelled.

Maximum forward speed is 15 m/s, reverse 6 m/s. Parameters live in `Settings`. Space overrides throttle with zero target speed. R restores the safe initial pose. No damage or loss states exist.

## Follow camera

Desired position is 10.5 m behind the heading and 5.7 m above the car. Clamp desired height to terrain plus 3 m. Smooth position and look target with `alpha = 1 − exp(−3.5 × delta)` for frame-rate independent response. Aim ahead of the vehicle. Terrain pitch never changes the camera's up axis. Building collision avoidance is a future spring-arm improvement.

While exploring, right-button mouse dragging adds a wrapped horizontal orbit and vertical tilt clamped to 10–70 degrees. The camera orbits the car's 1.4 m focus point at a fixed radius; looking around centers the car rather than aiming ahead, keeping accessories visible. After 3 seconds without mouse movement, yaw and pitch decay toward the driving view with `alpha = 1 − exp(−2 × delta)`. Releasing the button, opening a menu or losing application focus restores the previous mouse mode.

## Sky and clouds

`Atmosphere` builds a saturated blue procedural sky with a lighter blue horizon and low-density blue distance haze. World materials use stronger greens, coral reds, turquoise and bright flower accents. The sky and palette colors are converted from sRGB to linear values before use in the 3D materials.

Place 24 cloud clusters around a ring with radii 155–240 m and heights 19–29 m. A dedicated seeded random stream varies size, height and distance without changing vegetation placement. Each cloud reuses one rounded sphere mesh and one unshaded white material across four overlapping lobes. Clouds cast no shadows, so their undersides stay bright and they do not introduce dark patches across the driving routes. Their altitude keeps them visible above the horizon from the normal follow camera.

## Interaction and state

While exploring, select the first animal within the configured three-dimensional radius (8 m). Display the interaction hint only when one is found. E enters Challenge immediately and disables movement; there is no intermediate invitation or confirmation panel. Closing resumes exploration. Escape stores the previous state, enters Paused and disables driving. Resume restores the previous panel/state, including question, rejected choices and reward. Menus remain responsive because the scene tree itself is not paused.

## Arithmetic and car appearance rewards

Use a separate seeded `System.Random` stream so world generation and question generation do not affect each other. Select addition or subtraction with equal probability. For addition choose left in 1..9 and right in 1..(10-left). For subtraction choose left in 2..10 and right in 1..left. Operands and results are within 0..10; subtraction never produces negative answers.

Start the choice list with the correct answer. Shuffle offsets -2, -1, +1, +2 and append the first two distinct in-range neighboring values. Fisher–Yates shuffle the three choices so the correct answer is not tied to one button position. Store choices in a read-only collection.

Wrong submissions leave the question intact, record the rejected number and disable its button. The child may keep trying during the current visit or leave; leaving consumes the visit. A correct answer enters Reward and randomly draws one model, one paint and one accessory, shuffles the cards and rejects the previous unordered set. This requires constant extra memory as the catalogue grows. Each preview renders the actual selected geometry in an isolated SubViewport with studio lighting and a front or rear camera chosen for visibility of the reward. Selecting a preset changes body, cabin roof and wheel/bumper trim materials immediately, then resumes driving. Keeping the existing look or leaving a question also resumes driving. State guards reject submissions or cosmetic selection outside their appropriate states. Existing shared materials are reused by reference rather than mutated.

Appearance selection lasts for the current game run; it is preserved by R and water recovery but not saved between launches.

## Validation and capture

`build.ps1` builds C#, imports headlessly, and loads five frames. `-Test` adds the isolated `Diagnostics/SmokeTest` node: hold acceleration for 90 physics ticks, check displacement and terrain contact, brake against throttle, reverse while steering, reset, open/close a dialogue, pause/resume and assert generated content. `ChallengeChecks` verifies 1,000 deterministic generated questions, bounds, unique choices, both operations and all correct-answer slots. It emits actual UI button signals for all animals, incorrect/correct answers, cancellation, pause restoration and all three appearance choices, then checks actual mesh material assignment. Finally drive across the river bridge and check support and arrival at the opposite bank. The run ends at tick 465. A failed assertion logs an error and returns a nonzero exit code. Physical mouse/keyboard usability still benefits from manual playtesting.

`run.ps1 -Capture` adds the isolated `Diagnostics/VisualCapture` node, starts the rendered game, waits three seconds and the renderer's frame completion signal, reads the viewport and writes `artifacts/gameplay.png`. `-View title`, `village`, `pond`, `rabbit`, `fox` or `bear` selects a diagnostic view. This requires a working graphical session; headless rendering is not treated as visual verification. Captures are excluded from Git and Godot asset imports.


