shader_type spatial;

uniform vec3 canvas_shade = vec3(0.78, 0.78, 0.78);
uniform bool casualty_stains = false;

float hash_cell(vec2 cell) {
    vec3 p = fract(vec3(cell.xyx) * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}

float motif(vec2 coordinate) {
    vec2 cell = floor(coordinate);
    vec2 weight = smoothstep(vec2(0.0), vec2(1.0), fract(coordinate));
    return mix(
        mix(hash_cell(cell), hash_cell(cell + vec2(1.0, 0.0)), weight.x),
        mix(hash_cell(cell + vec2(0.0, 1.0)), hash_cell(cell + vec2(1.0)), weight.x),
        weight.y
    );
}

void fragment() {
    float material = COLOR.a;
    float cloth = 1.0 - step(0.29, material);
    float equipment = step(0.29, material) - step(0.64, material);
    float metal = step(0.64, material) - step(0.85, material);
    float wear = motif(UV * vec2(28.0, 36.0));
    float pattern = smoothstep(0.46, 0.56, motif(UV * vec2(11.0, 14.0)) * 0.8 + wear * 0.2);
    vec3 textile = mix(vec3(1.18), canvas_shade, pattern);
    ALBEDO = COLOR.rgb * mix(vec3(1.0), textile, cloth)
        * (1.0 + (wear - 0.5) * (cloth * 0.10 + equipment * 0.18 + metal * 0.16));
    ROUGHNESS = 0.91 - 0.17 * equipment - 0.36 * metal;
    METALLIC = 0.52 * metal;
    if (casualty_stains) {
        float torso = length((UV - vec2(0.06, 1.31)) / vec2(0.20, 0.15));
        float sleeve = length((UV - vec2(-0.27, 1.13)) / vec2(0.10, 0.13));
        float thigh = length((UV - vec2(0.14, 0.77)) / vec2(0.10, 0.17));
        float patch_distance = min(torso, min(sleeve, thigh));
        float stain = (1.0 - smoothstep(0.58, 1.03, patch_distance + (wear - 0.50) * 0.45))
            * (cloth + equipment) * 0.91;
        ALBEDO = mix(ALBEDO, vec3(0.30, 0.033, 0.025), stain);
        ROUGHNESS = mix(ROUGHNESS, 0.58, stain);
    }
}
