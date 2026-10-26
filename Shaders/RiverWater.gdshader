shader_type spatial;
render_mode cull_disabled;

// Keep the surface opaque: no screen/depth texture dependency in Compatibility.
uniform vec4 deep_color : source_color = vec4(0.025, 0.43, 0.64, 1.0);
uniform vec4 shallow_color : source_color = vec4(0.12, 0.68, 0.66, 1.0);
uniform vec4 foam_color : source_color = vec4(0.84, 0.97, 0.92, 1.0);
uniform float flow_speed : hint_range(0.0, 2.0) = 0.45;
uniform float wave_height : hint_range(0.0, 0.1) = 0.035;

varying vec3 world_position;
varying float water_depth;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(cell), hash(cell + vec2(1.0, 0.0)), u.x),
               mix(hash(cell + vec2(0.0, 1.0)), hash(cell + vec2(1.0, 1.0)), u.x), u.y);
}

float swell(vec2 p, float t) {
    return sin(p.x * 0.85 + p.y * 0.55 - t * 1.1) * 0.55
         + sin(p.x * 1.7 - p.y * 0.8 + t * 0.85) * 0.25
         + (noise(p * 1.1 + vec2(0.0, -t * 0.4)) - 0.5) * 0.4;
}

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    water_depth = COLOR.r * 3.0;
    float shore_fade = smoothstep(0.0, 0.45, water_depth);
    VERTEX.y += swell(world_position.xz, TIME) * wave_height * shore_fade;
}

void fragment() {
    vec2 p = world_position.xz;
    vec2 flow = vec2(0.12, -1.0) * TIME * flow_speed;
    float broad = noise(p * 0.21 + flow * 0.18);
    float detail = noise(p * vec2(1.1, 0.65) + flow);
    float eddy = noise(p * 0.65 - flow * 0.35 + vec2(broad * 2.0));
    float depth_mix = smoothstep(0.05, 1.15, water_depth);
    vec3 color = mix(shallow_color.rgb, deep_color.rgb, depth_mix);
    color *= 0.93 + broad * 0.13 + eddy * 0.06;

    // Perturbed surface normals create small moving sun/sky highlights.
    float h = swell(p, TIME);
    vec2 gradient = vec2(swell(p + vec2(0.15, 0.0), TIME) - h,
                         swell(p + vec2(0.0, 0.15), TIME) - h) / 0.15;
    vec3 normal_world = normalize(vec3(-gradient.x * 0.16, 1.0, -gradient.y * 0.16));
    NORMAL = normalize((VIEW_MATRIX * vec4(normal_world, 0.0)).xyz);
    float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
    color = mix(color, vec3(0.35, 0.65, 0.85), fresnel * 0.28);

    // Broken, soft glints instead of continuous stripes across the channel.
    float glints = smoothstep(0.74, 0.9, detail) * smoothstep(0.52, 0.76, eddy);
    float foam = (1.0 - smoothstep(0.04, 0.38, water_depth))
               * smoothstep(0.24, 0.68, detail + broad * 0.15) * 0.38;
    color = mix(color, foam_color.rgb, clamp(foam + glints * 0.28, 0.0, 0.8));
    ALBEDO = color;
    ROUGHNESS = mix(0.24, 0.46, foam);
    SPECULAR = 0.5;
    EMISSION = color * 0.035;
}
