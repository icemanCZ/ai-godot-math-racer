shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec4 leaf_color : source_color = vec4(0.3, 0.6, 0.3, 1.0);
varying vec3 leaf_position;

void vertex() {
    leaf_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    // Quiet breeze, with a different phase across the valley.
    float breeze = sin(TIME * 1.15 + leaf_position.x * 0.35 + leaf_position.z * 0.24);
    VERTEX.x += breeze * 0.035;
    VERTEX.z += cos(TIME * 0.85 + leaf_position.x * 0.2) * 0.02;
}

void fragment() {
    vec3 p = leaf_position;
    // Broad painted patches and small leaf-shaped flecks, with no external texture.
    float patch = sin(p.x * 5.5 + sin(p.y * 4.0)) * sin(p.z * 5.0 - p.y * 3.0);
    float fleck = smoothstep(0.66, 0.94, sin(p.x * 23.0 + p.z * 9.0) * sin(p.y * 25.0 - p.z * 17.0));
    ALBEDO = leaf_color.rgb * COLOR.rgb * (0.96 + patch * 0.075 + fleck * 0.14);
    ROUGHNESS = 0.91;
    // Soft transmission makes backlit leaves warm instead of metallic.
    BACKLIGHT = leaf_color.rgb * 0.18;
}
