shader_type spatial;
render_mode unshaded;

// A puff of the fluffy clouds round the valley (Atmosphere): lit white on top, a soft blue-grey shade underneath and a
// faint bright rim at the edges. `tint` follows the weather and the time of day.
uniform vec4 tint : source_color = vec4(1.0);
uniform vec4 shade : source_color = vec4(0.76, 0.82, 0.94, 1.0);

varying vec3 world_normal;

void vertex() {
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
    float light = smoothstep(-0.75, 0.55, world_normal.y);
    vec3 color = mix(shade.rgb, vec3(1.0), light);
    float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
    ALBEDO = (color + rim * 0.08) * tint.rgb;
}
