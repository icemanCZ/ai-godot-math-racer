shader_type spatial;
render_mode unshaded, cull_front, shadows_disabled;

// A soft dark outline drawn around characters and cars (Art.Outline): the mesh is drawn once more as an overlay, pushed
// out along its normals by `thickness` metres and seen only from behind, so a thin rim shows round its edges, as in a
// drawn picture. It thins out in the distance so far-off figures do not turn dark.
uniform vec4 outline_color : source_color = vec4(0.24, 0.16, 0.15, 1.0);
uniform float thickness = 0.025;

void vertex() {
    vec3 view_position = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    vec3 view_normal = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
    float fade = 1.0 - smoothstep(25.0, 70.0, -view_position.z);
    view_position += view_normal * thickness * fade;
    POSITION = PROJECTION_MATRIX * vec4(view_position, 1.0);
}

void fragment() {
    ALBEDO = outline_color.rgb;
}
