shader_type spatial;
render_mode cull_disabled, diffuse_burley;

// Wheat: stalks with a golden ear at the top that sway in the wind. The sway grows with the height on the stalk and
// stronger weather bends the whole field further.
global uniform vec2 weather_wind;
varying float tint;

void vertex() {
    vec3 root = MODEL_MATRIX[3].xyz;
    float height = clamp(VERTEX.y / 0.85, 0.0, 1.0);
    float wave = sin(TIME * 1.7 - root.x * 0.45 - root.z * 0.33);
    float gust = 0.5 + 0.5 * sin(TIME * 0.7 - root.x * 0.06 + root.z * 0.05);
    float sway = (0.05 + gust * 0.05 + wave * 0.035 + length(weather_wind) * 0.07) * height * height;
    VERTEX.x += sway;
    VERTEX.z += sway * 0.6;
    tint = 0.9 + 0.2 * fract(sin(dot(root.xz, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
    ALBEDO = COLOR.rgb * tint;
    ROUGHNESS = 0.9;
}
