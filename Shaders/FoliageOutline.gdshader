shader_type spatial;
render_mode world_vertex_coords, unshaded, cull_front, shadows_disabled;

// The soft dark outline of trees and bushes (Vegetation, Art.Outline's look): drawn as the overlay of the plants'
// MultiMesh, it sways with them in the wind (FoliageSway) and is pushed out along the normals, seen only from behind.
// It thins out in the distance, so a far hillside of trees does not turn dark.
#include "res://Shaders/FoliageSway.gdshaderinc"

uniform bool tree_motion = false;
uniform vec4 outline_color : source_color = vec4(0.17, 0.22, 0.14, 1.0);
uniform float thickness = 0.035;

void vertex() {
    foliage_sway(VERTEX, NORMAL, INSTANCE_CUSTOM, MODEL_MATRIX, TIME, tree_motion);
    float distance_to_camera = distance(VERTEX, CAMERA_POSITION_WORLD);
    float fade = 1.0 - smoothstep(35.0, 110.0, distance_to_camera);
    VERTEX += normalize(NORMAL) * thickness * fade;
}

void fragment() {
    ALBEDO = outline_color.rgb;
}
