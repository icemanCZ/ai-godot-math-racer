// How the foliage moves in the breeze and the weather's wind, shared by Foliage (the plants) and FoliageOutline (their
// outline), so the outline never parts from the leaves. Used with render_mode world_vertex_coords: `vertex` and `normal`
// are in world space. `custom` is the instance's custom data: the root of the tree and its size.

global uniform vec2 weather_wind;

void foliage_sway(inout vec3 vertex, inout vec3 normal, vec4 custom, mat4 model, float time, bool tree_motion) {
    vec3 rest = vertex;
    if (tree_motion) {
        // Every part of a tree shares its root, including bark, leaves, blossoms and fruit.
        vec3 root = custom.rgb;
        float tree_size = max(custom.a, 0.01);
        float height = max(0.0, (rest.y - root.y) / (tree_size * 6.0));
        float gust = sin(time * 2.8 - dot(root.xz, vec2(0.09, 0.07)));
        vec2 breeze = vec2(sin(time * 1.15 + root.x * 0.35 + root.z * 0.24),
            cos(time * 0.85 + root.x * 0.2)) * 0.045;
        vec2 sway = breeze + weather_wind * (0.35 + gust * 0.27);
        vertex.xz += sway * height * height * tree_size;
        normal = normalize(vec3(normal.x, normal.y - dot(normal.xz, sway) * height / 3.0, normal.z));
    } else {
        float breeze = sin(time * 1.15 + rest.x * 0.35 + rest.z * 0.24);
        vertex.xz += (model * vec4(breeze * 0.035, 0.0, cos(time * 0.85 + rest.x * 0.2) * 0.02, 0.0)).xz;
        float gust = sin(time * 2.8 - vertex.x * 0.09 - vertex.z * 0.07);
        vertex.xz += weather_wind * (0.23 + gust * 0.29);
    }
}
