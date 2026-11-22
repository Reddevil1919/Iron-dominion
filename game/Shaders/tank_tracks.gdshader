shader_type spatial;
render_mode cull_disabled;

uniform vec2 travel;

void fragment() {
    float phase = mix(travel.x, travel.y, COLOR.r);
    float section = fract(UV.x - phase);
    float groove = smoothstep(0.04, 0.14, section) * (1.0 - smoothstep(0.82, 0.94, section));
    float shoe = groove * smoothstep(0.055, 0.13, min(UV.y, 1.0 - UV.y));
    float grouser = smoothstep(0.36, 0.44, section) * (1.0 - smoothstep(0.59, 0.65, section));
    float rim = 1.0 - smoothstep(0.04, 0.14, min(UV.y, 1.0 - UV.y));
    ALBEDO = mix(vec3(0.13, 0.12, 0.10), vec3(0.26, 0.25, 0.22), shoe + rim * 0.63)
        + vec3(0.067, 0.062, 0.053) * grouser * shoe;
    ROUGHNESS = 0.92;
}
