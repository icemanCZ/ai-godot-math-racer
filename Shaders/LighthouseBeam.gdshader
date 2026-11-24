shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, shadows_disabled, depth_draw_never;

// The beam of the lighthouse: a long open cone whose tip is at the lamp (the cylinder's top, +Y). It is brightest at the
// lamp and fades out towards its far end and towards its edges, so it looks like light in the air rather than a solid
// cone; near the eye it fades too, so a camera that stands in it does not see the screen fill with light.
uniform float intensity = 1.0;
uniform float beam_length = 120.0;
uniform vec3 beam_color : source_color = vec3(1.0, 0.93, 0.68);
varying float along;

void vertex() {
    along = clamp(0.5 - VERTEX.y / beam_length, 0.0, 1.0);
}

void fragment() {
    float facing = abs(dot(normalize(NORMAL), normalize(VIEW)));
    float fade = pow(1.0 - along, 1.6);
    float near_eye = smoothstep(3.0, 18.0, length(VERTEX));
    ALBEDO = beam_color;
    ALPHA = clamp(intensity * fade * facing * facing * near_eye * 0.55, 0.0, 1.0);
}
