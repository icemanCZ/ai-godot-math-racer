// The packed dirt of the roads, shared by RoadSurface and the mouth of a FieldPath, which turns into exactly this
// colour where it meets a road, so no line is left between them. Everything is worked out in world metres.

vec2 dirt_hash(vec2 p) {
    return fract(sin(vec2(dot(p, vec2(127.1, 311.7)),
                          dot(p, vec2(269.5, 183.3)))) * 43758.5453);
}

float dirt_noise(vec2 p) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(dirt_hash(cell).x, dirt_hash(cell + vec2(1.0, 0.0)).x, u.x),
               mix(dirt_hash(cell + vec2(0.0, 1.0)).x, dirt_hash(cell + vec2(1.0)).x, u.x), u.y);
}

// The road's dirt at p: large dusty patches, a fine grain and scattered small stones. `footprint` is the size of a
// pixel in metres; fine details fade with it to avoid sparkling in the distance.
vec3 road_dirt(vec2 p, float footprint, vec3 earth, vec3 dust, vec3 stone_color) {
    float patches = dirt_noise(p * 0.48);
    float grain = dirt_noise(p * 13.0);
    float detail = 1.0 - smoothstep(0.025, 0.14, footprint);
    vec3 color = mix(earth, dust, 0.38 + patches * 0.55);
    color *= 1.0 + (grain - 0.5) * 0.17 * detail;
    // Scattered small stones, with soft antialiased outlines and no extra geometry.
    vec2 cells = p * 9.0;
    vec2 cell = floor(cells);
    vec2 random = dirt_hash(cell);
    vec2 center = vec2(0.23) + random * 0.54;
    float radius = mix(0.065, 0.16, random.x);
    float aa = clamp(footprint * 9.0, 0.025, 0.3);
    float stone = (1.0 - smoothstep(radius - aa, radius + aa,
                   length((fract(cells) - center) * vec2(1.0, 1.35))))
                   * step(0.64, random.y) * detail;
    return mix(color, stone_color * (0.82 + random.x * 0.18), stone * 0.65);
}
