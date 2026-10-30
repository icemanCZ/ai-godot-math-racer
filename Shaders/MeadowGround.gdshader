shader_type spatial;
render_mode cull_disabled, diffuse_burley;

varying vec3 ground_position;

float speckle(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float ground_noise(vec2 p) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(speckle(cell), speckle(cell + vec2(1.0, 0.0)), u.x),
               mix(speckle(cell + vec2(0.0, 1.0)), speckle(cell + vec2(1.0, 1.0)), u.x), u.y);
}

void vertex() {
    ground_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec2 p = ground_position.xz;
    float patch = sin(p.x * 0.18 + sin(p.y * 0.13) * 2.0) * sin(p.y * 0.21 - p.x * 0.08);
    float close_detail = 1.0 - smoothstep(22.0, 65.0, distance(ground_position, CAMERA_POSITION_WORLD));
    float grain = ground_noise(p * 3.2) - 0.5;
    // Subtle mottling joins the meadow blades and stays quiet on distant hills.
    ALBEDO = COLOR.rgb * (0.97 + patch * 0.065 + grain * close_detail * 0.035);
    ROUGHNESS = 1.0;
}
