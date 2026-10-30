shader_type spatial;
render_mode cull_disabled, world_vertex_coords, diffuse_burley;

uniform vec3 car_position;
uniform vec3 car_forward = vec3(0.0, 0.0, -1.0);
uniform float car_speed = 0.0;
uniform float grass_time = 0.0;
// x/z position, remaining recovery, ground height. Fixed storage, including on long drives.
uniform vec4 trail[8];
varying float tint;
varying float tip_height;
varying float meadow_tint;

uniform vec4 grass_low : source_color = vec4(0.43, 0.64, 0.29, 1.0);
uniform vec4 grass_high : source_color = vec4(0.67, 0.77, 0.43, 1.0);

void vertex() {
    vec3 root = MODEL_MATRIX[3].xyz;
    tint = INSTANCE_CUSTOM.r;
    meadow_tint = 0.5 + 0.5 * sin(root.x * 0.035 + sin(root.z * 0.042) * 1.8);
    tip_height = UV.y;
    float weight = UV.y * UV.y;
    float wave = sin(TIME * 1.6 - root.x * 0.21 - root.z * 0.16);
    float gust = 0.5 + 0.5 * sin(TIME * 0.63 - root.x * 0.045 + root.z * 0.031);
    vec2 wind = vec2(0.85, 0.53) * (0.08 + gust * 0.19 + wave * 0.09);
    wind += vec2(sin(TIME * 3.8 + root.z), cos(TIME * 3.1 + root.x)) * 0.025;
    vec2 away = root.xz - car_position.xz;
    vec2 forward = normalize(car_forward.xz);
    vec2 side = vec2(-forward.y, forward.x);
    float body_distance = length(vec2(dot(away, side) / 1.5, dot(away, forward) / 2.6));
    float contact = (1.0 - smoothstep(0.55, 1.25, body_distance)) *
        (1.0 - smoothstep(1.0, 2.8, abs(root.y - car_position.y)));
    vec2 push = normalize(away + side * 0.01) * contact;
    float flatten = contact;
    // Distant LODs keep wind but skip car/wake work outside the interaction area.
    if (distance(root.xz, car_position.xz) < 40.0) {
    for (int i = 0; i < 8; i++) {
        vec2 delta = root.xz - trail[i].xy;
        float wake = (1.0 - smoothstep(0.5, 2.1, length(delta))) * trail[i].z *
            (1.0 - smoothstep(1.0, 2.8, abs(root.y - trail[i].w)));
        if (wake > flatten) {
            flatten = wake;
            push = normalize(delta + vec2(0.01, 0.0)) * wake;
        }
    }
    }
    // Every tuft keeps its original shape. Extra density gradually sinks into the ground
    // per root over broad distance bands, instead of replacing entire 16 m tiles.
    float view_distance = distance(root.xz, CAMERA_POSITION_WORLD.xz);
    float layer = INSTANCE_CUSTOM.g;
    float fade_start = layer < 0.25 ? 50.0 : layer < 0.75 ? 110.0 : 225.0;
    float fade_end = layer < 0.25 ? 90.0 : layer < 0.75 ? 170.0 : 270.0;
    float fade = (1.0 - smoothstep(fade_start, fade_end, view_distance)) *
        smoothstep(0.0, 0.7, grass_time - INSTANCE_CUSTOM.b);
    VERTEX.y = root.y + (VERTEX.y - root.y) * fade * (1.0 - flatten * 0.88);
    VERTEX.xz += (wind * (1.0 - flatten) + push * (0.48 + min(car_speed, 20.0) * 0.008)) * weight * fade;
}

void fragment() {
    vec3 green = mix(grass_low.rgb, grass_high.rgb, tint * 0.55 + meadow_tint * 0.45);
    ALBEDO = green * mix(0.87, 1.14, tip_height);
    // Soft fill preserves the light meadow color even on blades facing away from the sun.
    EMISSION = green * 0.12;
    ROUGHNESS = 0.95;
    BACKLIGHT = green * 0.3;
}
