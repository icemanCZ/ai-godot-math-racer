shader_type spatial;
render_mode cull_disabled;

#include "res://Shaders/Gravel.gdshaderinc"
#include "res://Shaders/RoadDirt.gdshaderinc"

// Blue of the vertex colour: how far into the road a point is, 0 at its edge and 1 from this many metres in.
const float EDGE_DEPTH = 1.2;

uniform vec4 earth_color : source_color = vec4(0.65, 0.47, 0.27, 1.0);
uniform vec4 dust_color : source_color = vec4(0.83, 0.69, 0.45, 1.0);
uniform vec4 stone_color : source_color = vec4(0.90, 0.81, 0.63, 1.0);

varying vec2 ground_position;

void vertex() {
    // World coordinates keep the grain scale consistent across all road widths and junctions.
    ground_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

global uniform float road_wetness;

void fragment() {
    vec2 p = ground_position;
    // Fade fine details with pixel footprint to avoid sparkling in the distance.
    float footprint = max(length(dFdx(p)), length(dFdy(p)));
    vec3 color = road_dirt(p, footprint, earth_color.rgb, dust_color.rgb, stone_color.rgb);
    float patches = dirt_noise(p * 0.48);

    float wander = (dirt_noise(vec2(UV.y * 0.15, 9.0)) - 0.5) * 0.055;
    float wheel_distance = abs(abs(UV.x + wander) - 0.43);
    float tracks = 1.0 - smoothstep(0.055, 0.16, wheel_distance);
    color *= 1.0 - tracks * COLOR.r * (0.10 + patches * 0.045);

    // The last metre turns to the verge's gravel, which RoadVerge carries on beside the road.
    vec2 profile = gravel_profile(p, -COLOR.b * EDGE_DEPTH);
    vec4 pebbles = gravel(p, profile.y, footprint);
    color = mix(color, mix(GRAVEL_BED * (0.94 + patches * 0.1), pebbles.rgb, pebbles.a), profile.x);
    color = mix(color, pebbles.rgb, pebbles.a * (1.0 - profile.x) * profile.y);
    ALBEDO = color * mix(vec3(1.0), vec3(0.68, 0.72, 0.76), road_wetness);
    ROUGHNESS = mix(0.96, 0.65, road_wetness);
    SPECULAR = 0.15;
}
