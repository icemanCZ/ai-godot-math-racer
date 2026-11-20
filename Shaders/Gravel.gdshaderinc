// The gravel of a road's verge, shared by RoadSurface (its last metre) and RoadVerge (the band beside it), so the strip
// runs on across the road's edge without a seam. Everything is worked out in world metres.

const vec3 GRAVEL_BED = vec3(0.55, 0.49, 0.40);

vec2 gravel_hash(vec2 p) {
    return fract(sin(vec2(dot(p, vec2(127.1, 311.7)),
                          dot(p, vec2(269.5, 183.3)))) * 43758.5453);
}

float gravel_noise(vec2 p) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(gravel_hash(cell).x, gravel_hash(cell + vec2(1.0, 0.0)).x, u.x),
               mix(gravel_hash(cell + vec2(0.0, 1.0)).x, gravel_hash(cell + vec2(1.0)).x, u.x), u.y);
}

// How much packed gravel (x) and how dense the pebbles (y) are at signed distance s from the road's edge, in metres,
// negative on the road. The inner and outer rims wander, so the strip never looks ruled.
vec2 gravel_profile(vec2 p, float s) {
    float wander = gravel_noise(p * 0.55);
    float ripple = gravel_noise(p * 2.1) - 0.5;
    float inner_rim = -0.3 - wander * 0.22 + ripple * 0.1;
    float inner = smoothstep(inner_rim - 0.3, inner_rim + 0.1, s);
    float outer_rim = 0.16 + wander * 0.3 + ripple * 0.16;
    float outer = 1.0 - smoothstep(outer_rim - 0.1, outer_rim + 0.1, s);
    float scatter = mix(0.16, 0.85, outer) * (1.0 - smoothstep(0.7, 1.15, s));
    return vec2(inner * outer, inner * scatter);
}

// A soft, warm palette of pebbles: cream, dove grey, rose, blue-grey and sand.
vec3 gravel_tint(float pick) {
    vec3 c = vec3(0.74, 0.69, 0.60);
    c = mix(c, vec3(0.57, 0.57, 0.56), step(0.3, pick));
    c = mix(c, vec3(0.68, 0.56, 0.51), step(0.5, pick));
    c = mix(c, vec3(0.50, 0.55, 0.61), step(0.68, pick));
    c = mix(c, vec3(0.70, 0.58, 0.43), step(0.84, pick));
    return c;
}

// One layer of round pebbles, one per cell at most; rgb is the pebble's colour, a its coverage. `density` is the share of
// cells that hold one and `footprint` the size of a pixel in metres, which softens the outlines in the distance.
vec4 gravel_layer(vec2 p, float cells_per_metre, float density, float footprint, float seed) {
    vec2 q = p * cells_per_metre;
    vec2 cell = floor(q);
    vec2 r = gravel_hash(cell + seed);
    vec2 r2 = gravel_hash(cell + seed + 17.31);
    vec2 center = vec2(0.32) + r * 0.36;
    float radius = mix(0.24, 0.37, r2.x);
    float angle = r2.y * 6.2832;
    vec2 d = fract(q) - center;
    vec2 e = mat2(vec2(cos(angle), sin(angle)), vec2(-sin(angle), cos(angle))) * d * vec2(1.0, 1.3);
    float rim = length(e) / radius;
    float aa = clamp(footprint * cells_per_metre * 1.4 / radius, 0.05, 0.6);
    float present = step(1.0 - density, fract(r.y * 13.7));
    float mask = (1.0 - smoothstep(1.0 - aa, 1.0 + aa, rim)) * present;
    // A rounded top: lit on one side, a soft darker rim all round, a small gleam.
    float dome = sqrt(max(0.0, 1.0 - rim * rim));
    float side = dot(d / radius, vec2(-0.55, -0.45));
    float shade = 0.8 + dome * 0.22 + side * 0.16;
    shade *= mix(0.84, 1.0, 1.0 - smoothstep(0.7, 1.0, rim));
    float gleam = (1.0 - smoothstep(0.0, 0.3, length(d / radius - vec2(-0.3, -0.25)))) * 0.07;
    return vec4(gravel_tint(fract(r.x * 7.13 + r2.y)) * shade + gleam, mask);
}

// The packed bed with two layers of pebbles on it. `density` thins the pebbles out; far away they fade into the bed's
// average colour, which keeps the distance calm.
vec4 gravel(vec2 p, float density, float footprint) {
    float detail = 1.0 - smoothstep(0.03, 0.12, footprint);
    vec3 bed = GRAVEL_BED * (0.84 + gravel_noise(p * 4.0) * 0.14 + gravel_noise(p * 23.0) * 0.08);
    vec3 color = bed;
    float coverage = 0.0;
    vec4 small = gravel_layer(p, 12.0, density * 0.9, footprint, 3.0);
    color = mix(color, small.rgb, small.a * detail);
    coverage = max(coverage, small.a);
    vec4 big = gravel_layer(p, 6.5, density * 0.7, footprint, 11.0);
    color = mix(color, big.rgb, big.a * detail);
    coverage = max(coverage, big.a);
    // In the distance the bed takes on the pebbles' lighter average instead of drawing them.
    color = mix(mix(bed, vec3(0.63, 0.58, 0.50), density * 0.45), color, detail);
    return vec4(color, mix(density * 0.5, coverage, detail));
}
