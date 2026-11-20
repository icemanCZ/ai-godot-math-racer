shader_type spatial;
render_mode cull_disabled;

// A gabled roof of rounded, fish-scale tiles (Art.RoofTiles). The roof mesh has its ridge along the local z axis at x 0,
// so the tiles are laid out in the roof's own frame: along the ridge and down the slope. Every tile has a shade of its
// own, a lighter middle and a soft shadow under the rounded edge of the row above. The gable ends take the colour of
// the walls.
uniform vec4 tile_color : source_color = vec4(0.92, 0.39, 0.29, 1.0);
// The gable ends are part of the wall below them and take its colour.
uniform vec4 gable_color : source_color = vec4(0.96, 0.85, 0.62, 1.0);

const float TILE_WIDTH = 0.56;
const float ROW_HEIGHT = 0.40;
// The slope rises about 0.675 for every metre across, so a metre across is 1.2 m down the tiles.
const float SLOPE = 1.2;

varying vec3 local_position;

global uniform float road_wetness;

float tile_hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
    local_position = VERTEX;
}

void fragment() {
    vec3 base = tile_color.rgb;
    float wet = road_wetness;
    // The face's own normal: every corner of the roof is shared with a gable end, so the smoothed normals blend them.
    vec3 face = normalize(cross(dFdx(local_position), dFdy(local_position)));
    if (abs(face.z) > 0.6) {
        float mottle = tile_hash(floor(local_position.xy * 3.0)) * 0.03;
        ALBEDO = gable_color.rgb * (0.94 + mottle) * mix(1.0, 0.82, wet);
        ROUGHNESS = 0.9;
    } else {
        float along = local_position.z;
        float down = abs(local_position.x) * SLOPE;
        float row = floor(down / ROW_HEIGHT);
        float in_row = down - row * ROW_HEIGHT;
        float radius = TILE_WIDTH * 0.5;
        // Does the rounded foot of a tile of the row above reach over this point?
        float above_parity = mod(row - 1.0, 2.0);
        float above_u = along / TILE_WIDTH + 0.5 * above_parity;
        float above_x = (fract(above_u) - 0.5) * TILE_WIDTH;
        float above_distance = length(vec2(above_x, in_row));
        vec2 tile;
        float across;
        float shadow = 0.0;
        float rim;
        if (above_distance < radius) {
            tile = vec2(floor(above_u), row - 1.0);
            across = above_x / radius;
            rim = radius - above_distance;
        } else {
            float parity = mod(row, 2.0);
            float u = along / TILE_WIDTH + 0.5 * parity;
            tile = vec2(floor(u), row);
            across = (fract(u) - 0.5) * 2.0;
            // The row above throws a soft shadow just under its rounded edge.
            shadow = 1.0 - smoothstep(0.0, 0.09, above_distance - radius);
            rim = 1.0;
        }
        float pick = tile_hash(tile);
        vec3 color = base * (0.88 + pick * 0.2);
        // Now and then a tile from another batch, a touch lighter or more orange.
        color = mix(color, base * vec3(1.08, 1.12, 1.05), step(0.86, pick) * 0.6);
        // A rounded tile: lighter down its middle, darker at its sides.
        color *= 0.86 + (1.0 - across * across) * 0.18;
        // A light lip right at the rounded foot, then the shadow under it on the tile below.
        color *= 1.0 + (1.0 - smoothstep(0.0, 0.04, rim)) * 0.12;
        color *= 1.0 - shadow * 0.32;
        ALBEDO = color * mix(1.0, 0.78, wet);
        ROUGHNESS = mix(0.72, 0.45, wet);
    }
    SPECULAR = 0.3;
}
