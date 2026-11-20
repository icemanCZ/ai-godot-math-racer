shader_type spatial;
render_mode cull_disabled;

// A walkway from a house to its street (Village.Path): rounded paving stones in warm colours with sandy joints, a wavy
// outline instead of a ruled one, and at the street the stones give way to the road's gravel.
// UV.x runs across from -1 to 1, UV.y along in metres from the house; COLOR.r is 1 over the road and COLOR.g is the
// half width in metres divided by four.
#include "res://Shaders/Gravel.gdshaderinc"

const float STONE = 0.46;

varying vec2 ground_position;

global uniform float road_wetness;

void vertex() {
    ground_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

// The nearest and second nearest stone centres of a jittered grid, and a number for the nearest stone.
vec3 cobbles(vec2 q) {
    vec2 n = floor(q);
    vec2 f = fract(q);
    float f1 = 8.0;
    float f2 = 8.0;
    vec2 id = vec2(0.0);
    for (int j = -1; j <= 1; j++) {
        for (int i = -1; i <= 1; i++) {
            vec2 g = vec2(float(i), float(j));
            vec2 r = g + vec2(0.18) + gravel_hash(n + g + 41.0) * 0.64 - f;
            float d = dot(r, r);
            if (d < f1) {
                f2 = f1;
                f1 = d;
                id = n + g;
            } else if (d < f2) {
                f2 = d;
            }
        }
    }
    return vec3(sqrt(f1), sqrt(f2), fract(gravel_hash(id + 7.0).x * 5.31));
}

vec3 stone_tint(float pick) {
    vec3 c = vec3(0.84, 0.75, 0.60);
    c = mix(c, vec3(0.83, 0.67, 0.53), step(0.35, pick));
    c = mix(c, vec3(0.74, 0.71, 0.66), step(0.6, pick));
    c = mix(c, vec3(0.80, 0.64, 0.59), step(0.82, pick));
    return c;
}

void fragment() {
    vec2 p = ground_position;
    float half_width = max(COLOR.g * 4.0, 0.3);
    float across = UV.x * half_width;
    float along = UV.y;
    // A wavy outline: the edge wanders a little way in, and the corners at the house are rounded.
    float wander = gravel_noise(vec2(along * 1.6, UV.x > 0.0 ? 3.0 : 9.0)) * 0.22 + 0.04;
    float rim = half_width - abs(across) - wander;
    float corner = 0.35;
    vec2 to_corner = vec2(max(corner - rim, 0.0), max(corner - along, 0.0));
    float rounded = corner - length(to_corner);
    // At the street the stones end raggedly and the road's gravel shows through.
    float street = COLOR.r + (gravel_noise(p * 2.4) - 0.5) * 0.5;
    if (min(rim, rounded) < 0.0 || street > 0.45) {
        discard;
    }

    vec2 q = vec2(across, along) / STONE;
    vec3 cell = cobbles(q);
    // Stones near the street or the rim lie further apart, with more gravel between them.
    float loosen = max(smoothstep(-0.2, 0.45, street), 1.0 - smoothstep(0.0, 0.3, rim));
    float gap = 0.1 + loosen * 0.3;
    float joint_width = fwidth(cell.y - cell.x) * 1.2 + 0.02;
    float joint = 1.0 - smoothstep(gap, gap + joint_width, cell.y - cell.x);
    float dome = 1.0 - smoothstep(0.0, 0.75, cell.x);
    vec3 stone = stone_tint(cell.z) * (0.86 + dome * 0.2 + (gravel_noise(p * 9.0) - 0.5) * 0.06);
    float footprint = max(length(dFdx(p)), length(dFdy(p)));
    vec3 gaps = gravel(p, 0.7, footprint).rgb * 1.1;
    vec3 color = mix(stone, gaps, joint);
    ALBEDO = color * mix(vec3(1.0), vec3(0.7, 0.73, 0.77), road_wetness);
    ROUGHNESS = mix(0.9, 0.55, road_wetness);
    SPECULAR = 0.2;
}
