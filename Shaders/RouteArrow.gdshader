shader_type spatial;
render_mode depth_prepass_alpha;

uniform vec4 arrow_color : source_color = vec4(0.153, 0.741, 0.384, 0.55);
uniform float bob_phase = 0.0;

void vertex() {
    VERTEX.y += sin(bob_phase) * 0.14;
}

void fragment() {
    ALBEDO = arrow_color.rgb;
    ALPHA = arrow_color.a;
    ROUGHNESS = 0.55;
    EMISSION = arrow_color.rgb * 0.25;
}
