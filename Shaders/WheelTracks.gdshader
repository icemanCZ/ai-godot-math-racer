shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;

uniform float track_time = 0.0;
varying float born;

void vertex() {
    born = UV2.x;
}

void fragment() {
    float edge = smoothstep(0.0, 0.15, UV.x) * smoothstep(0.0, 0.15, 1.0 - UV.x);
    float tread = mix(0.65, 1.0, step(0.35, fract(UV.y * 7.0 + abs(UV.x - 0.5) * 0.8)));
    float fade = 1.0 - smoothstep(18.0, 25.0, track_time - born);
    ALBEDO = vec3(0.19, 0.14, 0.09);
    ALPHA = edge * tread * fade * 0.42;
}
