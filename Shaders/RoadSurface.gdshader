shader_type spatial;
render_mode cull_disabled;

uniform vec4 earth_color : source_color = vec4(0.65, 0.47, 0.27, 1.0);
uniform vec4 dust_color : source_color = vec4(0.83, 0.69, 0.45, 1.0);
uniform vec4 stone_color : source_color = vec4(0.90, 0.81, 0.63, 1.0);
uniform vec4 verge_color : source_color = vec4(0.46, 0.49, 0.24, 1.0);

varying vec2 ground_position;

vec2 hash2(vec2 p) {
    return fract(sin(vec2(dot(p, vec2(127.1, 311.7)),
                          dot(p, vec2(269.5, 183.3)))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash2(cell).x, hash2(cell + vec2(1.0, 0.0)).x, u.x),
               mix(hash2(cell + vec2(0.0, 1.0)).x, hash2(cell + vec2(1.0)).x, u.x), u.y);
}

void vertex() {
    // World coordinates keep the grain scale consistent across all road widths and junctions.
    ground_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    vec2 p = ground_position;
    float patches = noise(p * 0.48);
    float grain = noise(p * 13.0);
    // Fade fine details with pixel footprint to avoid sparkling in the distance.
    float footprint = max(length(dFdx(p)), length(dFdy(p)));
    float detail = 1.0 - smoothstep(0.025, 0.14, footprint);
    vec3 color = mix(earth_color.rgb, dust_color.rgb, 0.38 + patches * 0.55);
    color *= 1.0 + (grain - 0.5) * 0.17 * detail;

    float wander = (noise(vec2(UV.y * 0.15, 9.0)) - 0.5) * 0.055;
    float wheel_distance = abs(abs(UV.x + wander) - 0.43);
    float tracks = 1.0 - smoothstep(0.055, 0.16, wheel_distance);
    color *= 1.0 - tracks * COLOR.r * (0.10 + patches * 0.045);

    // Scattered small stones, with soft antialiased outlines and no extra geometry.
    vec2 cells = p * 9.0;
    vec2 cell = floor(cells);
    vec2 random = hash2(cell);
    vec2 center = vec2(0.23) + random * 0.54;
    float radius = mix(0.065, 0.16, random.x);
    float aa = clamp(footprint * 9.0, 0.025, 0.3);
    float stone = (1.0 - smoothstep(radius - aa, radius + aa,
                   length((fract(cells) - center) * vec2(1.0, 1.35))))
                   * step(0.64, random.y) * detail;
    color = mix(color, stone_color.rgb * (0.82 + random.x * 0.18), stone * 0.65);

    float edge_noise = noise(p * 3.2);
    float verge = smoothstep(0.78 + edge_noise * 0.13, 1.02, abs(UV.x));
    color = mix(color, verge_color.rgb * (0.86 + patches * 0.24), verge * COLOR.r * 0.65);
    ALBEDO = color;
    ROUGHNESS = 0.96;
    SPECULAR = 0.15;
}
