shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

uniform float wetness;
uniform float rain_amount;
varying vec2 ground_position;

void vertex() {
    ground_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float angle = atan(p.y, p.x + 0.00001);
    float edge = length(p) + sin(angle * 5.0) * 0.055 + cos(angle * 3.0) * 0.075;
    float mask = 1.0 - smoothstep(0.82, 1.0, edge);
    float fresnel = pow(1.0 - abs(dot(NORMAL, VIEW)), 2.4);

    vec2 cell = floor(ground_position * 2.8);
    vec2 seed = fract(sin(vec2(dot(cell, vec2(61.7, 17.3)), dot(cell, vec2(29.2, 31.1)))) * 11397.83);
    float phase = fract(TIME * 1.6 + seed.x);
    float radius = distance(fract(ground_position * 2.8), mix(vec2(0.3), vec2(0.7), seed));
    float ring = (1.0 - smoothstep(0.014, 0.055, abs(radius - phase * 0.28))) * (1.0 - phase);
    vec3 tint = mix(vec3(0.16, 0.21, 0.27), vec3(0.49, 0.61, 0.69), fresnel);
    ALBEDO = tint + ring * rain_amount * 0.19;
    ALPHA = mask * wetness * (0.58 + fresnel * 0.28);
}
