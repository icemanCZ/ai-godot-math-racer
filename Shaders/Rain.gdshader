shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, world_vertex_coords;

uniform float rain_time;
uniform float intensity;
uniform vec2 drift;
varying float opacity;

void vertex() {
    vec3 seed = INSTANCE_CUSTOM.rgb;
    vec3 fall = vec3(drift.x, -18.0, drift.y);
    float time = rain_time * (0.85 + INSTANCE_CUSTOM.a * 0.25);
    vec3 volume = vec3(48.0, 22.0, 48.0);
    vec3 origin = MODEL_MATRIX[3].xyz - vec3(24.0, 9.0, 24.0);
    vec3 offset = mod(seed * volume + fall * time - origin, volume);
    vec3 center = origin + offset;
    vec3 right = INV_VIEW_MATRIX[0].xyz;
    vec3 up = INV_VIEW_MATRIX[1].xyz;
    vec2 slope = vec2(dot(fall, right), dot(fall, up));
    vec2 axis = normalize(slope + vec2(0.0, -0.001));
    VERTEX = center + (right * axis.x + up * axis.y) * (UV.y - 0.5) * (0.6 + seed.y * 0.7)
        + (right * -axis.y + up * axis.x) * (UV.x - 0.5) * 0.045;
    float distance_to_eye = distance(center, CAMERA_POSITION_WORLD);
    float edge = smoothstep(0.0, 3.0, min(offset.y, 22.0 - offset.y));
    opacity = edge * smoothstep(1.6, 4.0, distance_to_eye) * (1.0 - smoothstep(15.0, 23.0, distance_to_eye));
}

void fragment() {
    ALBEDO = vec3(0.74, 0.86, 0.98);
    ALPHA = intensity * opacity * sin(UV.y * 3.14159) * 0.62;
}
