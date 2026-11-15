shader_type spatial;
render_mode world_vertex_coords, diffuse_burley, specular_schlick_ggx;

global uniform vec2 weather_wind;
uniform vec4 leaf_color : source_color = vec4(0.3, 0.6, 0.3, 1.0);
uniform bool tree_motion = false;
uniform bool painted_leaves = true;
varying vec3 leaf_position;

void vertex() {
    leaf_position = VERTEX;
    if (tree_motion) {
        // Every part of a tree shares its root, including bark, leaves, blossoms and fruit.
        vec3 root = INSTANCE_CUSTOM.rgb;
        float tree_size = max(INSTANCE_CUSTOM.a, 0.01);
        float height = max(0.0, (VERTEX.y - root.y) / (tree_size * 6.0));
        float gust = sin(TIME * 2.8 - dot(root.xz, vec2(0.09, 0.07)));
        vec2 breeze = vec2(sin(TIME * 1.15 + root.x * 0.35 + root.z * 0.24),
            cos(TIME * 0.85 + root.x * 0.2)) * 0.045;
        vec2 sway = breeze + weather_wind * (0.35 + gust * 0.27);
        VERTEX.xz += sway * height * height * tree_size;
        NORMAL = normalize(vec3(NORMAL.x, NORMAL.y - dot(NORMAL.xz, sway) * height / 3.0, NORMAL.z));
    } else {
        float breeze = sin(TIME * 1.15 + leaf_position.x * 0.35 + leaf_position.z * 0.24);
        VERTEX.xz += (MODEL_MATRIX * vec4(breeze * 0.035, 0.0,
            cos(TIME * 0.85 + leaf_position.x * 0.2) * 0.02, 0.0)).xz;
        float gust = sin(TIME * 2.8 - VERTEX.x * 0.09 - VERTEX.z * 0.07);
        VERTEX.xz += weather_wind * (0.23 + gust * 0.29);
    }
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
