shader_type spatial;
render_mode specular_disabled;

varying vec3 world_position;

float hash_cell(vec2 cell) {
    vec3 p = fract(vec3(cell.xyx) * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}

float noise_at(vec2 p) {
    vec2 cell = floor(p);
    vec2 mix_weight = smoothstep(vec2(0.0), vec2(1.0), fract(p));
    return mix(
        mix(hash_cell(cell), hash_cell(cell + vec2(1.0, 0.0)), mix_weight.x),
        mix(hash_cell(cell + vec2(0.0, 1.0)), hash_cell(cell + vec2(1.0)), mix_weight.x),
        mix_weight.y
    );
}

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec2 p = world_position.xz;
    vec2 a = abs(p);
    float macro = noise_at(p * 0.055);
    float grit = noise_at(p * 1.18);
    float fine = noise_at(p * 3.6);
    float soil_mottle = noise_at(p * 0.29);
    float surface_detail = (grit - 0.5) * 0.05 + (fine - 0.5) * 0.022;

    float vertical_road = max(abs(a.x - 32.0) - 3.13, a.y - 31.7);
    float cross_road = max(abs(a.y - 24.0) - 3.13, a.x - 33.2);
    float main_road = max(a.y - 3.95, a.x - 63.0);
    float road = min(min(vertical_road, cross_road), main_road);
    float forecourt = max(abs(a.x - 32.0) - 8.15, abs(a.y - 24.0) - 7.15);
    float central_apron = max(a.x - 7.5, a.y - 9.2);
    float relay_apron = max(abs(p.x + 30.0) - 5.10, abs(p.y + 6.0) - 4.45);
    float service_apron = max(abs(p.x - 15.0) - 6.30, abs(p.y - 22.0) - 5.50);
    float check_apron = max(abs(p.x - 30.0) - 4.0, abs(p.y - 6.6) - 4.05);
    float concrete = min(min(forecourt, central_apron), min(relay_apron, min(service_apron, check_apron)));

    vec3 scrub = mix(vec3(0.29, 0.33, 0.22), vec3(0.48, 0.48, 0.34),
        smoothstep(0.26, 0.82, macro * 0.67 + soil_mottle * 0.33));
    vec3 verge = vec3(0.42, 0.39, 0.31) + vec3((grit - 0.5) * 0.11);
    vec3 ground = mix(scrub, verge, 1.0 - smoothstep(0.08, 1.8, min(road, concrete) + grit * 0.7));
    vec3 aggregate = vec3(0.31, 0.33, 0.32) + vec3((soil_mottle - 0.5) * 0.07);
    float tire_wear = exp(-pow((a.y - 1.33) * 2.0, 2.0)) * smoothstep(0.22, 0.75, macro);
    aggregate -= vec3(tire_wear * 0.049);
    ground = mix(ground, aggregate, 1.0 - smoothstep(-0.18, 0.20, road + (grit - 0.5) * 0.2));

    vec2 slab = abs(fract((p + vec2(2.0, 0.7)) / 3.8) - 0.5);
    float seam = smoothstep(0.465, 0.495, max(slab.x, slab.y));
    float slab_tone = hash_cell(floor((p + vec2(2.0, 0.7)) / 3.8));
    float patchwork = smoothstep(0.33, 0.77, macro * 0.54 + soil_mottle * 0.46);
    vec3 cement = mix(vec3(0.44, 0.43, 0.38), vec3(0.49, 0.49, 0.44), patchwork)
        + vec3(slab_tone * 0.06 - seam * 0.10);
    ground = mix(ground, cement, 1.0 - smoothstep(-0.12, 0.28, concrete));

    float main_dash = 1.0 - smoothstep(0.045, 0.12, a.y);
    main_dash *= 1.0 - smoothstep(0.76, 0.88, abs(mod(p.x + 2.8, 5.6) - 2.8));
    main_dash *= smoothstep(9.0, 11.0, a.x) * (1.0 - smoothstep(56.5, 57.0, a.x));
    main_dash *= smoothstep(3.8, 4.5, abs(a.x - 32.0));
    float apron_stripe = (1.0 - smoothstep(0.048, 0.14, abs(a.x - 7.0))) * (1.0 - smoothstep(8.8, 9.1, a.y));
    float paint_mask = clamp(max(main_dash, apron_stripe) * (0.63 + grit * 0.48), 0.0, 0.9);
    ground = mix(ground, vec3(0.70, 0.64, 0.43), paint_mask);

    ALBEDO = ground + vec3(surface_detail);
    ROUGHNESS = 0.93;
}
