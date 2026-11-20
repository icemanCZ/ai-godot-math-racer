shader_type spatial;
render_mode world_vertex_coords, diffuse_burley, specular_schlick_ggx;

#include "res://Shaders/FoliageSway.gdshaderinc"

uniform vec4 leaf_color : source_color = vec4(0.3, 0.6, 0.3, 1.0);
uniform bool tree_motion = false;
uniform bool painted_leaves = true;
varying vec3 leaf_position;

void vertex() {
    leaf_position = VERTEX;
    foliage_sway(VERTEX, NORMAL, INSTANCE_CUSTOM, MODEL_MATRIX, TIME, tree_motion);
}

void fragment() {
    vec3 p = leaf_position;
    // Broad painted patches and small leaf-shaped flecks, with no external texture.
    float patch = sin(p.x * 5.5 + sin(p.y * 4.0)) * sin(p.z * 5.0 - p.y * 3.0);
    float fleck = smoothstep(0.66, 0.94, sin(p.x * 23.0 + p.z * 9.0) * sin(p.y * 25.0 - p.z * 17.0));
    ALBEDO = leaf_color.rgb * (painted_leaves ? 0.96 + patch * 0.075 + fleck * 0.14 : 1.0);
    ROUGHNESS = 0.91;
    // Soft transmission makes backlit leaves warm instead of metallic.
    BACKLIGHT = painted_leaves ? leaf_color.rgb * 0.18 : vec3(0.0);
}
