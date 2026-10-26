# Algorithms

## Terrain

1. Initialize FastNoiseLite with `Settings.Seed`, frequency 0.013 and three fractal octaves.
2. Evaluate `h(x,z) = 0.65 × (7 × noise(x,z) + 2.4 × sin(0.023x))`.
3. Sample a 141 by 141 vertex grid across 350 by 350 metres, emit two triangles per cell, compute normals and vary vertex colors with a second sampling of the same noise.
4. Derive a static concave collision mesh from the visible mesh. All placement queries interpolate the exact same grid triangles: use the first triangle when local coordinates satisfy u+v <= 1, otherwise the second. Roads, decorations and collision therefore share the same surface height.

Time and storage: O(grid resolution squared). Physics uses the engine's static collision acceleration structure. Terrain is not regenerated per frame.

## Roads

Landmark control points form a main loop and two branches. For each point, compute a tangent from its neighbors and use 19% of that vector as the cubic Bézier handles; wrap neighbors for the closed loop. Sample the baked curve every 1.4 metres. Compute the horizontal perpendicular from a central derivative at each endpoint, offset by half the road width, and project onto terrain with a 13 cm lift. Subdivide width into eight strips to follow terrain curvature. Shared endpoint derivatives keep neighboring strips joined without wedge gaps. Disable road shadow casting to prevent lifted surface self-shadow artifacts.

Road proximity is the minimum horizontal distance to all stored samples. This is O(number of route samples) per placement query and runs only during generation. For a substantially larger world, replace it with a spatial hash. The current width and clearance mask prevent trees being placed across a road. Terrain is gentle rather than flattened under roads; very different noise settings would require road grading.

## Village along the road

`Roads.SampleMain(distance)` returns a position and a central-difference tangent on the same baked curve that generates the visible main road. `Village` places four staggered plots at distances 62, 68, 90 and 98 m, alternating sides. House centers are offset 11 m from the centerline along the horizontal perpendicular. Rotate each house so its local +Z entrance faces the road. This keeps placement tied to the road when its control points change.

Connect each doorway to the road edge with a 1.8 m footpath, sampled every 0.6 m and projected onto terrain. Split each front fence around the entrance, with the fence 4.8 m in front of the house center. Vegetation rejects candidates inside the oriented plot bounds, keeping houses, yards and access paths clear. The rabbit stands on the verge near the village entrance. Buildings retain their collision bodies.

## Vegetation placement

A dedicated seeded random generator selects bounded coordinates, scale and rejection. Reject candidates close to roads and landmark clearings. Keep more candidates in forest regions and 14 dense groves, enforcing minimum trunk spacing. All tree placement passes (dense groves, scattered forest and roadside accents) select species from the same spatial map: seven inner grove centers and the 14 outer grove centers define adjoining regions. Compare distances to each center after gentle sinusoidal distortion, preserving the center itself while giving region edges organic shapes. Each region has one dominant tree type, so roadside accents and scattered trees reinforce nearby groves instead of randomly mixing all seven species. Override the map near the river with a continuous willow ribbon.

Batch crown, trunk, flower and ground meshes by shape, material and 64 m spatial cell into MultiMesh instances. Fine leaf, flower, bark, fern and mushroom details have an 85 m visibility range. Store all transforms before assigning instance count, then upload once. A separate seeded art random stream handles fine detail. Identical seed and generation order produce identical placement.

## River and bridge

The previous raised circular pond is removed. `River` owns the authoritative channel profile: center X is `98 + 14 sin(0.022z) + 5 sin(0.051z)`, variable half-width is `5.5 + 0.7 sin(0.037z)`, and water level is `-0.8 - 0.008z`. The channel extends from one world edge to the other.

Before terrain vertices and collision are built, blend the natural terrain into a riverbed 1.2 m below water, then into raised banks using smoothstep. Blend the outer bank back into the natural hills. Color the shoreline sand. The water ribbon is sampled every metre and subdivided into 20 strips across its width. It extends slightly under the banks to hide mesh seams. Store channel depth in vertex color for the water shader. Vegetation rejects the river corridor; separate reeds and stones decorate its banks.

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
6. Sample height ahead and behind to calculate a cosmetic pitch; interpolate it on the model independently of collision. Roll gives gentle turning feedback. Spin wheel meshes by distance travelled.

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


