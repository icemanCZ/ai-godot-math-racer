shader_type spatial;
render_mode cull_disabled;

// A field path: two bare wheel ruts with a grassy hump between them and grassy verges.
// COLOR.g is 1 on loading yards, which are trodden bare all over.
uniform vec4 rut_color : source_color = vec4(0.55, 0.40, 0.24, 1.0);
uniform vec4 dust_color : source_color = vec4(0.71, 0.57, 0.37, 1.0);
uniform vec4 grass_color : source_color = vec4(0.44, 0.55, 0.22, 1.0);
uniform vec4 verge_color : source_color = vec4(0.46, 0.53, 0.25, 1.0);

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
    ground_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

global uniform float road_wetness;

void fragment() {
    vec2 p = ground_position;
    float patches = noise(p * 0.6);
    float grain = noise(p * 13.0);
    float footprint = max(length(dFdx(p)), length(dFdy(p)));
    float detail = 1.0 - smoothstep(0.025, 0.14, footprint);
    vec3 dirt = mix(rut_color.rgb, dust_color.rgb, 0.3 + patches * 0.5);
    dirt *= 1.0 + (grain - 0.5) * 0.2 * detail;

    float wander = (noise(vec2(UV.y * 0.12, 4.0)) - 0.5) * 0.12;
    float rut = 1.0 - smoothstep(0.17, 0.3, abs(abs(UV.x + wander) - 0.45));
    rut = max(rut, COLOR.g);
    float tufts = noise(p * 5.5);
    vec3 grass = grass_color.rgb * (0.85 + patches * 0.2 + (tufts - 0.5) * 0.25 * detail);
    // Grass reaches into the ruts in small tufts.
    float bare = smoothstep(0.15, 0.6, rut - (tufts - 0.5) * 0.5);
    vec3 color = mix(grass, dirt, bare);

    float edge_noise = noise(p * 3.2);
    float verge = smoothstep(0.7 + edge_noise * 0.15, 1.0, abs(UV.x));
    color = mix(color, verge_color.rgb * (0.88 + patches * 0.2), verge);
    ALBEDO = color * mix(vec3(1.0), vec3(0.68, 0.72, 0.76), road_wetness);
    ROUGHNESS = mix(0.98, 0.7, road_wetness);
    SPECULAR = 0.1;
}
