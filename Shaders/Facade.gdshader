shader_type spatial;

// The surface of a building's box (Art.Facade): `style` 0 is painted plaster with now and then a rounded patch of bare
// brick, 1 bare brick, 2 upright boards (a barn or a shed), 3 overlapping horizontal boards and 4 rounded shingles for a
// roof panel. The pattern is laid out in metres in the box's own frame (a BoxMesh of size 1 scaled to the wall), so it
// keeps its size whatever the box is scaled or turned to; shingles run in rows that fall with the world's height.
uniform vec4 wall_color : source_color = vec4(0.96, 0.85, 0.62, 1.0);
uniform int style = 0;

const vec3 BRICK = vec3(0.80, 0.45, 0.33);
const vec3 MORTAR = vec3(0.86, 0.80, 0.70);

varying vec3 local_metres;
varying vec3 local_normal;
varying vec3 world_position;
varying vec2 seed;

global uniform float road_wetness;

vec2 wall_hash(vec2 p) {
    return fract(sin(vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)))) * 43758.5453);
}

float wall_noise(vec2 p) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(wall_hash(cell).x, wall_hash(cell + vec2(1.0, 0.0)).x, u.x),
               mix(wall_hash(cell + vec2(0.0, 1.0)).x, wall_hash(cell + vec2(1.0)).x, u.x), u.y);
}

void vertex() {
    vec3 scale = vec3(length(MODEL_MATRIX[0].xyz), length(MODEL_MATRIX[1].xyz), length(MODEL_MATRIX[2].xyz));
    local_metres = VERTEX * scale;
    local_normal = NORMAL;
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    // Every building gets its own patches and tints.
    seed = fract(MODEL_MATRIX[3].xz * 0.0137) * 97.0;
}

// Rounded bricks in running bond; rgb the colour, a how much of it is mortar.
vec4 brickwork(vec2 q, float footprint) {
    vec2 size = vec2(0.26, 0.12);
    float row = floor(q.y / size.y);
    vec2 b = vec2(q.x / size.x + 0.5 * mod(row, 2.0), q.y / size.y);
    vec2 cell = floor(b);
    vec2 f = fract(b) - 0.5;
    vec2 d = abs(f * size) - (size * 0.5 - vec2(0.022));
    float corner = length(max(d + 0.012, 0.0)) - 0.012;
    float aa = max(footprint, 0.003);
    float mortar = smoothstep(-aa, aa, corner);
    float pick = wall_hash(cell + 3.0).x;
    vec3 brick = BRICK * (0.86 + pick * 0.22);
    brick = mix(brick, vec3(0.86, 0.58, 0.42), step(0.85, pick) * 0.5);
    brick *= 0.94 + (0.5 - f.y) * 0.1;
    return vec4(mix(brick, MORTAR, mortar), mortar);
}

// Painted boards `width` wide running along q.y (upright boards) with a dark gap between them, grain and a knot here and
// there. With `lapped` the boards run along q.x and overlap: each is lighter at the top and shaded at the bottom.
vec3 boards(vec2 q, vec3 paint, float footprint, bool lapped) {
    vec2 b = lapped ? vec2(q.y, q.x) : q;
    float width = lapped ? 0.2 : 0.24;
    float index = floor(b.x / width);
    float across = fract(b.x / width);
    float pick = wall_hash(vec2(index, 5.0) + seed).x;
    // Boards are pieced together end to end now and then.
    float piece = floor(b.y / 2.4 + pick * 3.0);
    float tint = 0.9 + wall_hash(vec2(index, piece) + seed).y * 0.16;
    vec3 color = paint * tint;
    float grain = wall_noise(vec2(b.x * 40.0, b.y * 2.2)) * 0.07;
    color *= 0.97 + grain - 0.035;
    float aa = max(footprint / width, 0.01);
    float gap = 1.0 - smoothstep(0.035, 0.035 + aa * 2.0, min(across, 1.0 - across));
    if (lapped) {
        // The lower edge of each board throws a soft shadow; its top catches the light.
        color *= 0.86 + smoothstep(0.0, 0.85, across) * 0.2;
        gap = 1.0 - smoothstep(0.0, aa * 2.0 + 0.03, across);
    }
    float joint = 1.0 - smoothstep(0.0, aa * 3.0 + 0.004, abs(fract(b.y / 2.4 + pick * 3.0) - 0.5) - 0.497);
    vec2 knot_cell = floor(vec2(index, b.y / 0.9));
    vec2 knot_random = wall_hash(knot_cell + seed + 9.0);
    vec2 knot_center = vec2((index + 0.3 + knot_random.x * 0.4) * width, (knot_cell.y + knot_random.y) * 0.9);
    float knot = (1.0 - smoothstep(0.018, 0.03, length((b - knot_center) * vec2(1.0, 0.7)))) * step(0.82, knot_random.y);
    color = mix(color, paint * 0.62, knot * 0.7);
    return mix(color, paint * 0.45, max(gap, joint) * 0.85);
}

vec3 shingles(vec2 q, vec3 paint, float down) {
    const float width = 0.5;
    const float row_height = 0.36;
    float row = floor(down / row_height);
    float in_row = down - row * row_height;
    float radius = width * 0.5;
    float above_u = q.x / width + 0.5 * mod(row - 1.0, 2.0);
    float above_x = (fract(above_u) - 0.5) * width;
    float above_distance = length(vec2(above_x, in_row));
    vec2 tile;
    float across;
    float shadow = 0.0;
    if (above_distance < radius) {
        tile = vec2(floor(above_u), row - 1.0);
        across = above_x / radius;
    } else {
        float u = q.x / width + 0.5 * mod(row, 2.0);
        tile = vec2(floor(u), row);
        across = (fract(u) - 0.5) * 2.0;
        shadow = 1.0 - smoothstep(0.0, 0.08, above_distance - radius);
    }
    float pick = wall_hash(tile + seed).x;
    vec3 color = paint * (0.86 + pick * 0.22);
    color *= 0.86 + (1.0 - across * across) * 0.18;
    return color * (1.0 - shadow * 0.32);
}

void fragment() {
    vec3 n = abs(local_normal);
    // Coordinates in the plane of this face, in metres: along it and up it (or along the ridge and down a roof).
    vec2 q;
    if (n.x >= n.y && n.x >= n.z) {
        q = local_metres.zy;
    } else if (n.z >= n.y) {
        q = local_metres.xy;
    } else {
        q = local_metres.xz;
    }
    q += seed;
    float footprint = max(length(dFdx(q)), length(dFdy(q)));
    vec3 paint = wall_color.rgb;
    vec3 color;
    if (style == 2 || style == 3) {
        color = boards(q, paint, footprint, style == 3);
    } else if (style == 4) {
        // Along the ridge, which is the box's long horizontal axis, and down the slope by the world's height.
        float along = local_metres.z + seed.x;
        color = shingles(vec2(along, 0.0), paint, -world_position.y / 0.42);
    } else {
        vec3 plaster = paint;
        float mottle = wall_noise(q * 1.1) * 0.07 + wall_noise(q * 4.3) * 0.035;
        float strokes = wall_noise(vec2(q.x * 0.8, q.y * 7.0)) * 0.03;
        plaster *= 0.95 + mottle + strokes - 0.03;
        // Now and then a rounded patch of bare brick, with a pale chipped rim.
        vec2 patch_cell = floor(q / vec2(1.7, 1.3));
        vec2 patch_random = wall_hash(patch_cell + 11.0);
        vec2 patch_center = (patch_cell + vec2(0.3) + patch_random * 0.4) * vec2(1.7, 1.3);
        vec2 patch_offset = (q - patch_center) / vec2(0.46, 0.28);
        float wobble = (wall_noise(q * 6.0) - 0.5) * 0.35;
        float patch_distance = length(patch_offset) + wobble;
        float bare = style == 1 ? 1.0 : 0.0;
        float has_patch = step(0.72, patch_random.y) * (1.0 - bare);
        float patch_aa = max(footprint * 4.0, 0.02);
        float show = max(bare, has_patch * (1.0 - smoothstep(1.0 - patch_aa, 1.0 + patch_aa, patch_distance)));
        float chipped = has_patch * (1.0 - smoothstep(1.0, 1.14, patch_distance)) * step(1.0, patch_distance);
        color = mix(plaster, brickwork(q, footprint).rgb, show);
        color = mix(color, plaster * 1.08, chipped * 0.8);
    }
    ALBEDO = color * mix(1.0, 0.82, road_wetness);
    ROUGHNESS = style == 4 ? mix(0.8, 0.5, road_wetness) : 0.9;
    SPECULAR = 0.2;
}
