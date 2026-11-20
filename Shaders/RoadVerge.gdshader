shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled;

// The gravel verge beside a road (Verges.cs). Red of the vertex colour runs from 0 at the road's edge to 1 at the verge's
// outer rim, green fades the verge out where a field way begins; the packed gravel thins out into scattered pebbles
// and the band fades into the meadow.
#include "res://Shaders/Gravel.gdshaderinc"

const float VERGE_WIDTH = 1.3;

varying vec2 ground_position;

global uniform float road_wetness;

void vertex() {
    ground_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    vec2 p = ground_position;
    float footprint = max(length(dFdx(p)), length(dFdy(p)));
    float s = COLOR.r * VERGE_WIDTH;
    vec2 profile = gravel_profile(p, s);
    vec4 pebbles = gravel(p, profile.y, footprint);
    vec3 bed = GRAVEL_BED * (0.94 + gravel_noise(p * 0.48) * 0.1);
    vec3 color = mix(bed, pebbles.rgb, pebbles.a);
    float alpha = max(profile.x, pebbles.a * profile.y);
    // Never past the rim, however the noise wanders.
    alpha *= (1.0 - smoothstep(0.85, 1.0, COLOR.r)) * COLOR.g;
    ALBEDO = color * mix(vec3(1.0), vec3(0.68, 0.72, 0.76), road_wetness);
    ALPHA = clamp(alpha, 0.0, 1.0);
    ROUGHNESS = mix(0.95, 0.6, road_wetness);
    SPECULAR = 0.15;
}
