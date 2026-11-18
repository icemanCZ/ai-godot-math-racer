shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, shadows_disabled, depth_draw_never, world_vertex_coords;

// Fireflies: small warm lights that wander in a box around the node and blink slowly. Like the rain, they are fixed in
// the world and wrap around the node, so the car drives past them instead of dragging them along.
uniform float glow_time;
uniform float intensity;
varying float brightness;

void vertex() {
    vec4 seed = INSTANCE_CUSTOM;
    vec3 volume = vec3(64.0, 5.0, 64.0);
    vec3 origin = MODEL_MATRIX[3].xyz - vec3(32.0, 0.6, 32.0);
    float t = glow_time * (0.35 + seed.w * 0.4);
    vec3 wander = vec3(
        sin(t * 1.3 + seed.x * 40.0) * 2.4,
        sin(t * 0.9 + seed.y * 30.0) * 0.7,
        cos(t * 1.1 + seed.z * 50.0) * 2.4);
    vec3 offset = mod(seed.xyz * volume + wander - origin, volume);
    vec3 center = origin + offset;
    vec3 right = INV_VIEW_MATRIX[0].xyz;
    vec3 up = INV_VIEW_MATRIX[1].xyz;
    float size = 0.16 + seed.w * 0.1;
    VERTEX = center + right * (UV.x - 0.5) * size + up * (0.5 - UV.y) * size;
    float distance_to_eye = distance(center, CAMERA_POSITION_WORLD);
    float edge = smoothstep(0.0, 6.0, min(min(offset.x, volume.x - offset.x), min(offset.z, volume.z - offset.z)));
    float blink = 0.55 + 0.45 * sin(glow_time * (1.1 + seed.w * 1.4) + seed.x * 60.0);
    brightness = blink * edge * smoothstep(1.5, 5.0, distance_to_eye) * (1.0 - smoothstep(30.0, 38.0, distance_to_eye));
}

void fragment() {
    float d = length(UV - vec2(0.5)) * 2.0;
    float glow = clamp(1.0 - d, 0.0, 1.0);
    ALBEDO = vec3(0.86, 1.0, 0.38);
    ALPHA = glow * glow * brightness * intensity;
}
